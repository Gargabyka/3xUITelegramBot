using Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore;

public static class DiContainerExtensions
{
	public static void AddEfCoreConfiguration(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		services.AddDbContext<AppDbContext>(options =>
			options.UseSqlite(configuration.GetConnectionString("Sqlite")));

		services.AddScoped<IUnitOfWork, UnitOfWork>();
	}
}