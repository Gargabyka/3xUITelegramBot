using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XUi.Models.Options;
using XUi.Services;
using XUi.Services.Interface;

namespace XUi;

public static class DiContainerExtensions
{
	public static void AddXUiConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddOptions<XUiOptions>()
			.Bind(configuration.GetSection("XUi"))
			.ValidateOnStart();

		services.AddHttpClient<IXUiApiService, XUiApiService>((sp, client) =>
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
	}
}
