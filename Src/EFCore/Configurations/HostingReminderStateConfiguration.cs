using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EFCore.Configurations;

public sealed class HostingReminderStateConfiguration : IEntityTypeConfiguration<HostingReminderState>
{
	public void Configure(EntityTypeBuilder<HostingReminderState> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.RemindersSentCount)
			.IsRequired()
			.HasDefaultValue(0);

		builder.Property(x => x.LastSentAtUtc);
	}
}
