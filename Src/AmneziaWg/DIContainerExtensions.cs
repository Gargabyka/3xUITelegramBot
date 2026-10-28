using System.Net;
using AmneziaWg.Models;
using AmneziaWg.Services;
using Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AmneziaWg;

public static class DiContainerExtensions
{
	/// <summary>
	/// Регистрирует amnezia-wg-easy, если он включён настройкой AmneziaWg:Enabled
	/// </summary>
	public static void AddAmneziaWgConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		var section = configuration.GetSection("AmneziaWg");
		services.AddOptions<AmneziaWgOptions>().Bind(section);

		if (!section.GetValue<bool>("Enabled"))
		{
			return;
		}

		services.AddHttpClient<AmneziaWgApiService>((sp, client) =>
			{
				var options = sp.GetRequiredService<IOptions<AmneziaWgOptions>>().Value;
				client.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/");
			})
			.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
			{
				// Сессия amnezia-wg-easy хранится в cookie
				CookieContainer = new CookieContainer(),
				UseCookies = true,
				AutomaticDecompression = DecompressionMethods.All,
			});

		services.AddTransient<IVpnBackend>(sp => sp.GetRequiredService<AmneziaWgApiService>());
	}
}
