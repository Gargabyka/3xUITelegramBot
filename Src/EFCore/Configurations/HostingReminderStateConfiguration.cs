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

		builder.Property(x => x.Enabled)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.PeriodMonths)
			.IsRequired()
			.HasDefaultValue(2);

		builder.Property(x => x.MessageText)
			.HasMaxLength(4096);
	}
}
