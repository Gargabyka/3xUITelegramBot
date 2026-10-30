using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Options;
using XUi.Models;
using XUi.Models.Options;

namespace XUi.Services;

/// <summary>
/// Клиент API панели 3x-ui (v3): клиенты VLESS + Reality, привязанные к одному inbound.
/// Идентификатор клиента — UUID VLESS, имя — email клиента в 3x-ui.
/// </summary>
public class XUiApiService : IVpnBackend
{
    private static readonly Regex VlessAuthorityRegex = new(@"^(vless://[^@/?#]+@)(\[[^\]]+\]|[^:/?#]+):\d+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly HttpClient _httpClient;
    private readonly XUiOptions _options;
    private readonly XUiInboundOptions _inbound;
    private readonly XUiClientListCache _cache;

    public XUiApiService(HttpClient httpClient, IOptions<XUiOptions> options, XUiInboundOptions inbound, XUiClientListCache cache)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _inbound = inbound;
        _cache = cache;
        Profile = new VpnProfile(inbound.Profile);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
    }

    public VpnProfile Profile { get; }

    public bool SupportsSubscription => true;

    public async Task<VpnServerClient[]> GetClients(CancellationToken cancellationToken)
    {
        var snapshot = await _cache.GetAsync(FetchSnapshotAsync, cancellationToken);

        return snapshot.Clients
            .Where(x => x.InboundIds.Contains(_inbound.Id))
            .Select(x => new VpnServerClient(
                Id: x.Id,
                Profile: _inbound.Profile,
                Name: x.Email,
                Enabled: x.Enabled,
                Address: string.Empty,
                PublicKey: string.Empty,
                CreatedAt: x.CreatedAt ?? DateTimeOffset.UtcNow,
                LatestHandshakeAt: snapshot.LastOnline.TryGetValue(x.Email, out var ts) && ts > 0
                    ? DateTimeOffset.FromUnixTimeMilliseconds(ts)
                    : null,
                TransferRx: x.Up,
                TransferTx: x.Down,
                SubscriptionId: x.SubId))
            .ToArray();
    }

    /// <summary>
    /// Возвращает vless://-ссылку клиента (в 3x-ui нет файлов конфигурации)
    /// </summary>
    public async Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        var links = await SendAsync(HttpMethod.Get, $"panel/api/clients/links/{Uri.EscapeDataString(email)}", null, cancellationToken);

        var link = (links as JsonArray ?? [])
            .Select(x => x?.GetValue<string>())
            .FirstOrDefault(x => !string.IsNullOrEmpty(x));

        return link is null
            ? throw new InvalidOperationException($"3x-ui не вернул ссылку для клиента {email}")
            : Encoding.UTF8.GetBytes(ApplyPort(link, _inbound.Port));
    }

    public async Task DeleteClientAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        try
        {
            await SendAsync(HttpMethod.Post, $"panel/api/clients/del/{Uri.EscapeDataString(email)}?keepTraffic=0", null, cancellationToken);
        }
        finally
        {
            _cache.Invalidate();
        }
    }

    public async Task AddClientAsync(string name, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["client"] = new JsonObject
            {
                ["email"] = name,
                ["id"] = Guid.NewGuid().ToString(),
                // Своя случайная ссылка-подписка у каждого соединения
                ["subId"] = SubscriptionIds.New(),
                ["flow"] = _inbound.Flow ?? _options.Flow,
                ["enable"] = true,
                ["totalGB"] = 0,
                ["expiryTime"] = 0,
                ["limitIp"] = 0,
            },
            ["inboundIds"] = new JsonArray(_inbound.Id),
        };

        try
        {
            await SendAsync(HttpMethod.Post, "panel/api/clients/add", body, cancellationToken);
        }
        finally
        {
            _cache.Invalidate();
        }
    }

    public Task EnableClientAsync(Guid id, CancellationToken cancellationToken)
        => SetEnabledAsync(id, "bulkEnable", cancellationToken);

    public Task DisableClientAsync(Guid id, CancellationToken cancellationToken)
        => SetEnabledAsync(id, "bulkDisable", cancellationToken);

    /// <summary>
    /// Меняет email клиента; UUID и подключение не меняются
    /// </summary>
    public Task RenameClientAsync(Guid id, string newName, CancellationToken cancellationToken)
        => UpdateClientRecordAsync(
            id,
            record =>
            {
                record["email"] = newName;
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Меняет subId клиента: сервер подписок 3x-ui отдаёт по ссылке всех клиентов с этим subId
    /// </summary>
    public Task SetSubscriptionIdAsync(Guid id, string subscriptionId, CancellationToken cancellationToken)
        => UpdateClientRecordAsync(
            id,
            record =>
            {
                if (record["subId"] is JsonValue current && current.TryGetValue<string>(out var currentId) && currentId == subscriptionId)
                {
                    return false;
                }

                record["subId"] = subscriptionId;
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Подставляет в vless://-ссылку заданный порт (панель отдаёт порт inbound'а,
    /// а наружу, например за CDN, клиент подключается на другой)
    /// </summary>
    private static string ApplyPort(string link, int? port)
    {
	    if (port is null)
	    {
		    return link;
	    }

	    // vless://uuid@host:port?params#remark; host может быть IPv6 в квадратных скобках
	    return VlessAuthorityRegex.Replace(link, m => $"{m.Groups[1].Value}{m.Groups[2].Value}:{port}", count: 1);
    }

    private static long ReadLong(JsonObject? obj, string key) => obj?[key]?.GetValue<long>() ?? 0;

    private static DateTimeOffset? ReadUnixMillis(JsonObject obj, string key)
	    => obj[key]?.GetValue<long>() is > 0 and var ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;

    private async Task SetEnabledAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        var body = new JsonObject { ["emails"] = new JsonArray(email) };

        JsonNode? result;
        try
        {
            result = await SendAsync(HttpMethod.Post, $"panel/api/clients/{action}", body, cancellationToken);
        }
        finally
        {
            _cache.Invalidate();
        }

        if (result is JsonObject obj && obj["skipped"] is JsonArray { Count: > 0 } skipped)
        {
            throw new InvalidOperationException($"3x-ui не изменил клиента {email}: {skipped[0]?["reason"]}");
        }
    }

    /// <summary>
    /// Изменяет запись клиента. update/{email} заменяет запись целиком, поэтому берём полную текущую запись,
    /// меняем нужные поля и отправляем её обратно
    /// </summary>
    /// <param name="id">UUID клиента</param>
    /// <param name="modify">Изменяет запись; false — менять нечего, запрос не отправляется</param>
    /// <param name="cancellationToken">Токен отмены</param>
    private async Task UpdateClientRecordAsync(Guid id, Func<JsonObject, bool> modify, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        var path = Uri.EscapeDataString(email);

        var node = await SendAsync(HttpMethod.Get, $"panel/api/clients/get/{path}", null, cancellationToken);
        var record = (node as JsonObject)?["client"] as JsonObject
                     ?? node as JsonObject
                     ?? throw new InvalidOperationException($"3x-ui не вернул данные клиента {email}");

        var body = (JsonObject)record.DeepClone();

        // В записи панели id — числовой ключ строки, а UUID VLESS лежит в uuid; в теле клиента id — это UUID
        if (body["uuid"] is JsonValue uuid && uuid.TryGetValue<string>(out var uuidText))
        {
            body["id"] = uuidText;
            body.Remove("uuid");
        }

        if (!modify(body))
        {
            return;
        }

        try
        {
            await SendAsync(HttpMethod.Post, $"panel/api/clients/update/{path}", body, cancellationToken);
        }
        finally
        {
            _cache.Invalidate();
        }
    }

    private async Task<string> GetEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        var clients = await GetClients(cancellationToken);
        return clients.FirstOrDefault(x => x.Id == id)?.Name
               ?? throw new InvalidOperationException($"Клиент {id} не найден в 3x-ui");
    }

    /// <summary>
    /// Загружает клиентов всех inbound'ов панели (кэшируется в <see cref="XUiClientListCache"/>)
    /// </summary>
    private async Task<XUiPanelSnapshot> FetchSnapshotAsync(CancellationToken cancellationToken)
    {
        var list = await SendAsync(HttpMethod.Get, "panel/api/clients/list", null, cancellationToken);
        var lastOnline = await TryGetLastOnlineAsync(cancellationToken);

        var clients = new List<XUiPanelClient>();
        foreach (var item in list as JsonArray ?? [])
        {
            if (item is not JsonObject client)
            {
                continue;
            }

            var email = client["email"]?.GetValue<string>();
            if (string.IsNullOrEmpty(email) || !Guid.TryParse(client["uuid"]?.ToString(), out var uuid))
            {
                continue;
            }

            var inboundIds = (client["inboundIds"] as JsonArray ?? [])
                .Where(x => x is not null)
                .Select(x => x!.GetValue<int>())
                .ToArray();

            var traffic = client["traffic"] as JsonObject;

            clients.Add(new XUiPanelClient(
                Id: uuid,
                Email: email,
                Enabled: client["enable"]?.GetValue<bool>() ?? true,
                InboundIds: inboundIds,
                Up: ReadLong(traffic, "up"),
                Down: ReadLong(traffic, "down"),
                CreatedAt: ReadUnixMillis(client, "createdAt"),
                SubId: client["subId"]?.GetValue<string>()));
        }

        return new XUiPanelSnapshot(clients, lastOnline, DateTime.UtcNow);
    }

    private async Task<Dictionary<string, long>> TryGetLastOnlineAsync(CancellationToken cancellationToken)
    {
        try
        {
            var node = await SendAsync(HttpMethod.Post, "panel/api/clients/lastOnline", null, cancellationToken);
            return (node as JsonObject)?.ToDictionary(x => x.Key, x => x.Value?.GetValue<long>() ?? 0) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException)
        {
            // Время последнего подключения не критично — не роняем весь список
            return [];
        }
    }

    /// <summary>
    /// Выполняет запрос и возвращает поле obj ответа вида {success,msg,obj}
    /// </summary>
    private async Task<JsonNode?> SendAsync(HttpMethod method, string path, JsonNode? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        JsonObject? envelope;
        try
        {
            envelope = JsonNode.Parse(text) as JsonObject;
        }
        catch (JsonException)
        {
            envelope = null;
        }

        if (envelope is null)
        {
            throw new InvalidOperationException("Неожиданный ответ 3x-ui (не JSON). Проверьте ApiUrl и webBasePath.");
        }

        if (envelope["success"]?.GetValue<bool>() != true)
        {
            throw new InvalidOperationException($"3x-ui: {envelope["msg"]}");
        }

        return envelope["obj"];
    }
}