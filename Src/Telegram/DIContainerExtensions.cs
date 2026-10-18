using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Telegram.Bot;
using Telegram.Models;
using Telegram.Service;
using Telegram.Service.Factory;
using Telegram.Service.Interface;
using Telegram.Service.TextFlows;
using Telegram.Service.Updates;
using Telegram.Store;
using Telegram.Store.Interfaces;

namespace Telegram;

public static class DiContainerExtensions
{
    public static void AddTelegramConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TelegramOptions>()
            .Bind(configuration.GetSection("Telegram"))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Token), "Токен бота обязателен для заполнения")
            .Validate(o => o.AdminIds.Length > 0, "Список админов обязателен для заполнения")
            .ValidateOnStart();

        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelegramOptions>>().Value;
            return new TelegramBotClient(opts.Token);
        });

        services.AddOptions<HostingReminderOptions>()
            .Bind(configuration.GetSection("HostingReminder"))
            .ValidateOnStart();

        services.AddSingleton<ITelegramHandler, TelegramHandler>();
        services.AddHostedService<TelegramLongPollingService>();
        services.AddHostedService<HostingReminderBackgroundService>();
        services.AddHostedService<VpnTrafficSnapshotBackgroundService>();

        AddTelegramUpdate(services);
        AddTelegramCallback(services);
        AddTelegramTextFlows(services);
        AddTelegramStores(services);
    }

    private static void AddTelegramUpdate(IServiceCollection services)
    {
        services.AddSingleton<TelegramUpdateFactory>();
        services.AddTransient<StartTelegramUpdateHandler>();
        services.AddTransient<StatusTelegramUpdateHandler>();
    }

    private static void AddTelegramCallback(IServiceCollection services)
    {
        services.AddSingleton<TelegramCallbackFactory>();
        services.AddAllImplementationsAsTransient<ITelegramCallbackHandler>(typeof(DiContainerExtensions).Assembly);
    }

    private static void AddTelegramTextFlows(IServiceCollection services)
    {
	    services.AddSingleton<ITelegramTextFlowHandler, AddClientNameTextFlowHandler>();
	    services.AddSingleton<ITelegramTextFlowHandler, AddUserByContactTextFlowHandler>();
    }

    private static void AddTelegramStores(IServiceCollection services)
    {
	    services.AddSingleton<IAddClientStateStore, InMemoryAddClientStateStore>();
	    services.AddSingleton<IAddUserStateStore, InMemoryAddUserStateStore>();
    }

    private static void AddAllImplementationsAsTransient<TService>(
	    this IServiceCollection services,
	    Assembly assembly)
	    where TService : class
    {
	    var serviceType = typeof(TService);

	    var implementationTypes = assembly
		    .GetTypes()
		    .Where(t => t is { IsAbstract: false, IsInterface: false } && serviceType.IsAssignableFrom(t));

	    foreach (var implType in implementationTypes)
	    {
		    services.TryAddEnumerable(ServiceDescriptor.Transient(serviceType, implType));
	    }
    }
}