using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.Configurations;

public sealed class InviteConfiguration : IEntityTypeConfiguration<Invite>
{
	public void Configure(EntityTypeBuilder<Invite> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.Token)
			.HasMaxLength(64)
			.IsRequired();

		builder.HasIndex(x => x.Token)
			.IsUnique();

		builder.Property(x => x.CreatedByTelegramId)
			.IsRequired();

		builder.Property(x => x.CreatedAtUtc)
			.IsRequired();

		builder.Property(x => x.ExpiresAtUtc)
			.IsRequired();
	}
}
