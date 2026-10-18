using System.Buffers.Binary;

namespace Telegram.Common.Helper;

/// <summary>
/// Утилита для упаковки long в Guid (детерминированно), чтобы передавать long через TelegramRequest.Id.
/// </summary>
public static class LongGuidCodec
{
	public static Guid Encode(long value)
	{
		Span<byte> bytes = stackalloc byte[16];
		BinaryPrimitives.WriteInt64LittleEndian(bytes[..8], value);

		// Остальные 8 байт = 0 (детерминированно)
		return new Guid(bytes);
	}

	public static long Decode(Guid guid)
	{
		Span<byte> bytes = stackalloc byte[16];
		guid.TryWriteBytes(bytes);
		return BinaryPrimitives.ReadInt64LittleEndian(bytes[..8]);
	}
}