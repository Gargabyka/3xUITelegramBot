namespace Telegram.Common.Helper;

/// <summary>
/// Форматирование объёма трафика для отображения пользователю
/// </summary>
public static class TrafficFormatter
{
	private static readonly string[] Units = ["Б", "КБ", "МБ", "ГБ", "ТБ"];

	public static string FormatBytes(long bytes)
	{
		double value = bytes;
		var unitIndex = 0;

		while (value >= 1024 && unitIndex < Units.Length - 1)
		{
			value /= 1024;
			unitIndex++;
		}

		return $"{value:0.##} {Units[unitIndex]}";
	}
}
