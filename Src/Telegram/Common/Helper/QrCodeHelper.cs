using QRCoder;

namespace Telegram.Common.Helper;

/// <summary>
/// QR-коды для ссылок и конфигураций
/// </summary>
public static class QrCodeHelper
{
	/// <summary>
	/// PNG с QR-кодом
	/// </summary>
	/// <returns>PNG или null, если текст слишком длинный для QR-кода</returns>
	public static byte[]? TryBuildPng(string payload)
	{
		try
		{
			using var gen = new QRCodeGenerator();
			using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
			using var qr = new PngByteQRCode(data);
			return qr.GetGraphic(pixelsPerModule: 8);
		}
		catch (QRCoder.Exceptions.DataTooLongException)
		{
			return null;
		}
	}
}
