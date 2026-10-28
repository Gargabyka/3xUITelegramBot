using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AmneziaWg.Models;
using Domain.Enums;
using Domain.Interfaces;
using Domain.Models;
using Microsoft.Extensions.Options;

namespace AmneziaWg.Services;

/// <summary>
/// Клиент API amnezia-wg-easy (форк wg-easy): авторизация паролем и cookie-сессия
/// </summary>
public sealed class AmneziaWgApiService : IVpnBackend
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	private readonly HttpClient _httpClient;
	private readonly AmneziaWgOptions _options;

	public AmneziaWgApiService(HttpClient httpClient, IOptions<AmneziaWgOptions> options)
	{
		_httpClient = httpClient;
		_options = options.Value;
	}

	public VpnProfile Profile { get; } = new(VpnProfileType.AmneziaWg);

	public async Task<VpnServerClient[]> GetClients(CancellationToken cancellationToken)
	{
		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Get, "api/wireguard/client"),
			cancellationToken);

		var json = await response.Content.ReadAsStringAsync(cancellationToken);
		var clients = JsonSerializer.Deserialize<AmneziaWgClient[]>(json, JsonOptions) ?? [];

		return clients
			.Select(x => new VpnServerClient(
				Id: x.Id,
				Profile: VpnProfileType.AmneziaWg,
				Name: x.Name,
				Enabled: x.Enabled,
				Address: x.Address,
				PublicKey: x.PublicKey,
				CreatedAt: x.CreatedAt,
				LatestHandshakeAt: x.LatestHandshakeAt,
				TransferRx: x.TransferRx,
				TransferTx: x.TransferTx))
			.ToArray();
	}

	public async Task<byte[]> DownloadConfigAsync(Guid id, CancellationToken cancellationToken)
	{
		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Get, $"api/wireguard/client/{id}/configuration"),
			cancellationToken);

		return await response.Content.ReadAsByteArrayAsync(cancellationToken);
	}

	public async Task DeleteClientAsync(Guid id, CancellationToken cancellationToken)
	{
		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Delete, $"api/wireguard/client/{id}"),
			cancellationToken);
	}

	public async Task AddClientAsync(string name, CancellationToken cancellationToken)
	{
		var body = JsonSerializer.Serialize(new { name });

		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Post, "api/wireguard/client")
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			},
			cancellationToken);
	}

	public async Task EnableClientAsync(Guid id, CancellationToken cancellationToken)
	{
		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Post, $"api/wireguard/client/{id}/enable"),
			cancellationToken);
	}

	public async Task DisableClientAsync(Guid id, CancellationToken cancellationToken)
	{
		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Post, $"api/wireguard/client/{id}/disable"),
			cancellationToken);
	}

	public async Task RenameClientAsync(Guid id, string newName, CancellationToken cancellationToken)
	{
		var body = JsonSerializer.Serialize(new { name = newName });

		using var response = await SendWithAuthRetryAsync(
			() => new HttpRequestMessage(HttpMethod.Put, $"api/wireguard/client/{id}/name")
			{
				Content = new StringContent(body, Encoding.UTF8, "application/json"),
			},
			cancellationToken);
	}

	/// <summary>
	/// Как EnsureSuccessStatusCode, но в ошибке — какой запрос упал и что ответил сервер:
	/// по одному коду (например, 500) причину не понять, а текст ответа amnezia-wg-easy её называет
	/// </summary>
	private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}

		const int maxBodyLength = 300;

		var body = (await response.Content.ReadAsStringAsync(cancellationToken))
			.ReplaceLineEndings(" ")
			.Trim();
		if (body.Length > maxBodyLength)
		{
			body = body[..maxBodyLength] + "…";
		}

		var request = response.RequestMessage;
		var message = $"amnezia-wg-easy: {request?.Method} /{request?.RequestUri?.AbsolutePath.TrimStart('/')} → {(int)response.StatusCode} {response.ReasonPhrase}"
		              + (body.Length > 0 ? $": {body}" : string.Empty);

		var statusCode = response.StatusCode;
		response.Dispose();

		throw new HttpRequestException(message, null, statusCode);
	}

	/// <summary>
	/// Отправляет запрос; если сессия истекла (401/403) — авторизуется и повторяет один раз
	/// </summary>
	private async Task<HttpResponseMessage> SendWithAuthRetryAsync(
		Func<HttpRequestMessage> requestFactory,
		CancellationToken cancellationToken)
	{
		var response = await _httpClient.SendAsync(requestFactory(), cancellationToken);

		if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
		{
			response.Dispose();
			await AuthenticateAsync(cancellationToken);
			response = await _httpClient.SendAsync(requestFactory(), cancellationToken);
		}

		await EnsureSuccessAsync(response, cancellationToken);
		return response;
	}

	private async Task AuthenticateAsync(CancellationToken cancellationToken)
	{
		using var request = new HttpRequestMessage(HttpMethod.Post, "api/session");
		request.Content = new StringContent(
			JsonSerializer.Serialize(new { password = _options.ApiPassword }),
			Encoding.UTF8,
			"application/json");
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

		var response = await _httpClient.SendAsync(request, cancellationToken);
		await EnsureSuccessAsync(response, cancellationToken);
		response.Dispose();
	}
}