using Telegram.Enums;
using Telegram.Models;
using Telegram.Service.Interface;

namespace Telegram.Service.Factory;

public class TelegramCallbackFactory
{
    private readonly IReadOnlyDictionary<TelegramCallbackEvent, ITelegramCallbackHandler> _handlers;

    public TelegramCallbackFactory(IEnumerable<ITelegramCallbackHandler> handlers)
    {
        var dict = new Dictionary<TelegramCallbackEvent, ITelegramCallbackHandler>();

        foreach (var handler in handlers)
        {
            if (!dict.TryAdd(handler.CallbackEvent, handler))
            {
                throw new InvalidOperationException(
                    $"Duplicate ITelegramCallbackHandler for event: {handler.CallbackEvent}. " +
                    "Проверьте DI-регистрации: на одно событие должен быть ровно один handler.");
            }
        }

        _handlers = dict;
    }

    public ITelegramCallbackHandler CreateHandler(TelegramRequest request)
    {
        if (_handlers.TryGetValue(request.CallbackEvent, out var handler))
        {
            return handler;
        }

        throw new ArgumentException($"Нет зарегистрированного обработчика для события: {request.CallbackEvent}");
    }
}