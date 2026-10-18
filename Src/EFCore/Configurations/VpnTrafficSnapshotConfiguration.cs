using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.Configurations;

public sealed class VpnTrafficSnapshotConfiguration : IEntityTypeConfiguration<VpnTrafficSnapshot>
{
	public void Configure(EntityTypeBuilder<VpnTrafficSnapshot> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.ClientId)
			.IsRequired();

		builder.Property(x => x.CapturedAtUtc)
			.IsRequired();

		builder.Property(x => x.TransferRx)
			.IsRequired();

		builder.Property(x => x.TransferTx)
			.IsRequired();

		builder.HasIndex(x => new { x.ClientId, x.CapturedAtUtc });
	}
}
