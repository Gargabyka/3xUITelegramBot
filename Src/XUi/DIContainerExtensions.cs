using Domain.Enums;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XUi.Models.Options;
using XUi.Services;

namespace XUi;

public static class DiContainerExtensions
{
	private const string HttpClientName = "xui";

	public static void AddXUiConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var section = configuration.GetSection("XUi");
		services.AddOptions<XUiOptions>()
			.Bind(section)
			.ValidateOnStart();

		services.AddHttpClient(HttpClientName, (sp, client) =>
			{
				var options = sp.GetRequiredService<IOptions<XUiOptions>>().Value;

				// Слэш в конце обязателен, иначе webBasePath отбрасывается при склейке относительных путей
				client.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/");
			})
			.ConfigurePrimaryHttpMessageHandler(sp =>
			{
				var options = sp.GetRequiredService<IOptions<XUiOptions>>().Value;
				var handler = new HttpClientHandler();
				if (options.AllowInvalidCertificate)
				{
					handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
				}

				return handler;
			});

		var inbounds = section.GetSection("Inbounds").Get<List<XUiInboundOptions>>() ?? [];
		if (inbounds.Count == 0)
		{
			throw new InvalidOperationException("Не задан ни один профиль VLESS: заполните XUi:Inbounds (Id, Profile).");
		}

		if (inbounds.Any(x => x.Profile == VpnProfileType.AmneziaWg) || inbounds.GroupBy(x => x.Profile).Any(g => g.Count() > 1))
		{
			throw new InvalidOperationException("XUi:Inbounds: профиль должен быть VlessRaw, VlessXhttp или VlessXhttpCdn и не повторяться.");
		}

		services.AddSingleton<XUiClientListCache>();

		// По одному серверу на каждый inbound: они отличаются только inbound'ом и портом в ссылке
		foreach (var inbound in inbounds)
		{
			services.AddTransient<IVpnBackend>(sp => new XUiApiService(
				sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientName),
				sp.GetRequiredService<IOptions<XUiOptions>>(),
				inbound,
				sp.GetRequiredService<XUiClientListCache>()));
		}
	}
}