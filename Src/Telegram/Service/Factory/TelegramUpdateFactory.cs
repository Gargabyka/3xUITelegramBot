using Microsoft.Extensions.DependencyInjection;
using Telegram.Service.Interface;
using Telegram.Service.Updates;

namespace Telegram.Service.Factory;

/// <summary>
/// Класс создания фабрики по работе с обновлениями
/// </summary>
public class TelegramUpdateFactory
{
    private readonly IServiceProvider _serviceProvider;

    public TelegramUpdateFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITelegramUpdateHandler CreateHandler(string command)
    {
        return command switch
        {
            "/start" => _serviceProvider.GetRequiredService<StartTelegramUpdateHandler>(),
            "/status" => _serviceProvider.GetRequiredService<StatusTelegramUpdateHandler>(),
            _ => throw new ArgumentException()
        };
    }
}