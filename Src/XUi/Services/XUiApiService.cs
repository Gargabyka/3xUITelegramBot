using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using XUi.Models;
using XUi.Models.Options;
using XUi.Services.Interface;

namespace XUi.Services;

/// <summary>
/// Клиент API панели 3x-ui (v3): клиенты VLESS + Reality, привязанные к одному inbound.
/// Идентификатор клиента — UUID VLESS, имя — email клиента в 3x-ui.
/// </summary>
public class XUiApiService : IXUiApiService
{
    private readonly HttpClient _httpClient;
    private readonly XUiOptions _options;

    public XUiApiService(HttpClient httpClient, IOptions<XUiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiToken);
    }

    public async Task<XUiClient[]?> GetClients(CancellationToken cancellationToken)
    {
        var list = await SendAsync(HttpMethod.Get, "panel/api/clients/list", null, cancellationToken);
        var lastOnline = await TryGetLastOnlineAsync(cancellationToken);

        var result = new List<XUiClient>();
        foreach (var item in list as JsonArray ?? [])
        {
            if (item is not JsonObject client || !IsOnInbound(client))
            {
                continue;
            }

            var email = client["email"]?.GetValue<string>();
            if (string.IsNullOrEmpty(email) || !Guid.TryParse(client["uuid"]?.ToString(), out var uuid))
            {
                continue;
            }

            var traffic = client["traffic"] as JsonObject;
            DateTimeOffset? seen = lastOnline.TryGetValue(email, out var ts) && ts > 0
                ? DateTimeOffset.FromUnixTimeSeconds(ts)
                : null;

            result.Add(new XUiClient(
                Id: uuid,
                Name: email,
                Enabled: client["enable"]?.GetValue<bool>() ?? true,
                Address: string.Empty,
                PublicKey: string.Empty,
                CreatedAt: ReadUnixMillis(client, "createdAt") ?? DateTimeOffset.UtcNow,
                LatestHandshakeAt: seen,
                TransferRx: ReadLong(traffic, "up"),
                TransferTx: ReadLong(traffic, "down")));
        }

        return result.ToArray();
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
            : Encoding.UTF8.GetBytes(link);
    }

    public async Task DeleteClientAsync(Guid id, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        await SendAsync(HttpMethod.Post, $"panel/api/clients/del/{Uri.EscapeDataString(email)}?keepTraffic=0", null, cancellationToken);
    }

    public async Task AddClientAsync(string name, CancellationToken cancellationToken)
    {
        var body = new JsonObject
        {
            ["client"] = new JsonObject
            {
                ["email"] = name,
                ["id"] = Guid.NewGuid().ToString(),
                ["subId"] = Guid.NewGuid().ToString("N")[..16],
                ["flow"] = _options.Flow,
                ["enable"] = true,
                ["totalGB"] = 0,
                ["expiryTime"] = 0,
                ["limitIp"] = 0,
            },
            ["inboundIds"] = new JsonArray(_options.InboundId),
        };

        await SendAsync(HttpMethod.Post, "panel/api/clients/add", body, cancellationToken);
    }

    public Task EnableClientAsync(Guid id, CancellationToken cancellationToken)
        => SetEnabledAsync(id, "bulkEnable", cancellationToken);

    public Task DisableClientAsync(Guid id, CancellationToken cancellationToken)
        => SetEnabledAsync(id, "bulkDisable", cancellationToken);

    private async Task SetEnabledAsync(Guid id, string action, CancellationToken cancellationToken)
    {
        var email = await GetEmailAsync(id, cancellationToken);
        var body = new JsonObject { ["emails"] = new JsonArray(email) };

        var result = await SendAsync(HttpMethod.Post, $"panel/api/clients/{action}", body, cancellationToken);

        if (result is JsonObject obj && obj["skipped"] is JsonArray { Count: > 0 } skipped)
        {
            throw new InvalidOperationException($"3x-ui не изменил клиента {email}: {skipped[0]?["reason"]}");
        }
    }

    private async Task<string> GetEmailAsync(Guid id, CancellationToken cancellationToken)
    {
        var clients = await GetClients(cancellationToken) ?? [];
        return clients.FirstOrDefault(x => x.Id == id)?.Name
               ?? throw new InvalidOperationException($"Клиент {id} не найден в 3x-ui");
    }

    private bool IsOnInbound(JsonObject client)
        => client["inboundIds"] is JsonArray ids && ids.Any(x => x?.GetValue<int>() == _options.InboundId);

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

    private static long ReadLong(JsonObject? obj, string key) => obj?[key]?.GetValue<long>() ?? 0;

    private static DateTimeOffset? ReadUnixMillis(JsonObject obj, string key)
        => obj[key]?.GetValue<long>() is > 0 and var ms ? DateTimeOffset.FromUnixTimeMilliseconds(ms) : null;
}
