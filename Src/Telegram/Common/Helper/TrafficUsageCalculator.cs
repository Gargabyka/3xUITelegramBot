using Domain.Entities;

namespace Telegram.Common.Helper;

/// <summary>
/// Считает расход трафика за период по снимкам счётчиков, корректно обрабатывая
/// сброс счётчика (например, после перезапуска xray или сброса трафика) —
/// в этом случае новое значение меньше предыдущего, и оно засчитывается целиком с нуля,
/// а не как отрицательная разница.
/// </summary>
public static class TrafficUsageCalculator
{
	public static (long Rx, long Tx) ComputeUsage(IReadOnlyList<VpnTrafficSnapshot> orderedSnapshots)
	{
		long rx = 0;
		long tx = 0;

		for (var i = 1; i < orderedSnapshots.Count; i++)
		{
			var prev = orderedSnapshots[i - 1];
			var cur = orderedSnapshots[i];

			rx += cur.TransferRx >= prev.TransferRx ? cur.TransferRx - prev.TransferRx : cur.TransferRx;
			tx += cur.TransferTx >= prev.TransferTx ? cur.TransferTx - prev.TransferTx : cur.TransferTx;
		}

		return (rx, tx);
	}
}
