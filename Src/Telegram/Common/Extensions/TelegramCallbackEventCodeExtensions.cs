using System.Collections.Concurrent;
using System.Reflection;
using Telegram.Common.Attributes;
using Telegram.Enums;

namespace Telegram.Common.Extensions;

public static class TelegramCallbackEventCodeExtensions
{
    private static readonly ConcurrentDictionary<TelegramCallbackEvent, string> _codeByEvent = new();
    private static readonly Lazy<IReadOnlyDictionary<string, TelegramCallbackEvent>> _eventByCode =
        new(BuildEventByCode, isThreadSafe: true);

    public static string GetCode(this TelegramCallbackEvent callbackEvent)
    {
        return _codeByEvent.GetOrAdd(callbackEvent, static e =>
        {
            var member = typeof(TelegramCallbackEvent)
                .GetMember(e.ToString(), BindingFlags.Public | BindingFlags.Static)
                .Single();

            var attribute = member.GetCustomAttribute<CallbackCodeAttribute>();
            if (attribute is null)
            {
                throw new InvalidOperationException(
                    $"Не удалось найти сокращение для {nameof(TelegramCallbackEvent)}.{e}");
            }

            return attribute.Code;
        });
    }

    public static bool TryFromCode(string code, out TelegramCallbackEvent callbackEvent)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            callbackEvent = default;
            return false;
        }

        return _eventByCode.Value.TryGetValue(code, out callbackEvent);
    }

    private static IReadOnlyDictionary<string, TelegramCallbackEvent> BuildEventByCode()
    {
        var dict = new Dictionary<string, TelegramCallbackEvent>(StringComparer.Ordinal);

        var fields = typeof(TelegramCallbackEvent).GetFields(BindingFlags.Public | BindingFlags.Static);
        foreach (var field in fields)
        {
            var attribute = field.GetCustomAttribute<CallbackCodeAttribute>();
            if (attribute is null)
            {
                continue;
            }

            var value = (TelegramCallbackEvent)field.GetValue(null)!;

            if (!dict.TryAdd(attribute.Code, value))
            {
                throw new InvalidOperationException(
                    $"Дубликат обратного вызова: '{attribute.Code}' из {nameof(TelegramCallbackEvent)}. " +
                    $"Проверьте значение атрибутов.");
            }
        }

        return dict;
    }
}