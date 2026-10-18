using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.Configurations;

public sealed class VpnUsersConfiguration : IEntityTypeConfiguration<VpnUser>
{
	public void Configure(EntityTypeBuilder<VpnUser> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.TelegramUserId)
			.IsRequired();

		builder.Property(x => x.PeerId)
			.IsRequired();

		builder.HasOne(x => x.Telegram)
			.WithMany()
			.HasForeignKey(x => x.TelegramUserId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasOne(x => x.Peer)
			.WithMany()
			.HasForeignKey(x => x.PeerId)
			.OnDelete(DeleteBehavior.Cascade);

		builder.HasIndex(x => new { x.TelegramUserId, x.PeerId })
			.IsUnique();
	}
}