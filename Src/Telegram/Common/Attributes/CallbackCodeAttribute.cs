namespace Telegram.Common.Attributes;

/// <summary>
/// Атрибут для кодирования данных обратного вызова в Telegram
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class CallbackCodeAttribute : Attribute
{
    public CallbackCodeAttribute(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Code must be non-empty", nameof(code));
        }

        Code = code;
    }

    public string Code { get; }
}