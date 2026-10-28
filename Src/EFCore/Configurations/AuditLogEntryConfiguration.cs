using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.Configurations;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
	public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.ActorName)
			.HasMaxLength(256);

		builder.Property(x => x.Details)
			.HasMaxLength(1024)
			.IsRequired();

		builder.Property(x => x.CreatedAtUtc)
			.IsRequired();

		builder.HasIndex(x => x.CreatedAtUtc);
	}
}
