using Telegram.Common.Extensions;
using Telegram.Models;

namespace Telegram.Common.Helper;

/// <summary>
/// Кодирование и декодирование данных обратного вызова в Telegram
/// </summary>
/// <remarks>
/// У API Telegram лимит body - 64 символа. <br/>
/// Передавать Json нельзя, слишком большой объем. <br/>
/// Передаем кодированную строку и декодируем ее с нашей стороны
/// </remarks>
public static class TelegramCallbackDataCodec
{
    private const char Sep = '|';

    /// <summary>
    /// Кодирование запроса для передачи в telegram
    /// </summary>
    /// <param name="request">Запрос</param>
    /// <remarks>
    /// Кодируется в вид: <br/>
    /// {eventCode|id|sourceCode|page} <br/>
    /// {callback событие|идентификатор|источник события|страница}
    /// </remarks>
    /// <returns>Возвращает кодированную строку</returns>
    public static string Encode(TelegramRequest request)
    {
        string? sourceCode = null;

        var eventCode = request.CallbackEvent.GetCode();

        if (request.SourceEvent.HasValue)
        {
            sourceCode = request.SourceEvent.Value.GetCode();
        }

        // Кодируем в вид {eventCode|id|sourceCode|page} - {callback событие|идентификатор|источник события|страница}
        var id = request.Id.ToString("N");
        return $"{eventCode}{Sep}{id}{Sep}{sourceCode}{Sep}{request.Page}";
    }

    /// <summary>
    /// Попытка декодирования строки
    /// </summary>
    /// <param name="data">Кодированная строка</param>
    /// <param name="request">Декодированный ответ</param>
    /// <returns>
    /// Если декодирование удалось - true и TelegramRequest <br/>
    /// Если нет - false
    /// </returns>
    public static bool TryDecode(string data, out TelegramRequest request)
    {
        request = null!;

        if (string.IsNullOrWhiteSpace(data))
        {
            return false;
        }

        var parts = data.Split(Sep, 4, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not(3 or 4))
        {
            return false;
        }

        if (!TelegramCallbackEventCodeExtensions.TryFromCode(parts[0], out var callbackEvent))
        {
            return false;
        }

        if (!Guid.TryParseExact(parts[1], "N", out var id))
        {
            return false;
        }

        if (!TelegramCallbackEventCodeExtensions.TryFromCode(parts[2], out var sourceEvent))
        {
            return false;
        }

        var page = 0;
        if (parts.Length == 4 && !int.TryParse(parts[3], out page))
        {
            return false;
        }

        request = new TelegramRequest(id, callbackEvent, sourceEvent, page);
        return true;
    }
}