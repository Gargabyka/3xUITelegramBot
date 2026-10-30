using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EFCore.Configurations;

public sealed class TelegramUsersConfiguration : IEntityTypeConfiguration<TelegramUser>
{
	public void Configure(EntityTypeBuilder<TelegramUser> builder)
	{
		builder.HasKey(x => x.Id);

		builder.Property(x => x.TelegramId)
			.IsRequired();

		builder.HasIndex(x => x.TelegramId)
			.IsUnique();

		builder.Property(x => x.FirstName)
			.HasMaxLength(128);

		builder.Property(x => x.LastName)
			.HasMaxLength(128);

		builder.Property(x => x.UserName)
			.HasMaxLength(64);

		builder.Property(x => x.Role)
			.HasConversion<int>()
			.IsRequired();

		var dateOnlyConverter = new ValueConverter<DateOnly, string>(
			v => v.ToString("yyyy-MM-dd"),
			v => DateOnly.Parse(v));

		builder.Property(x => x.CreatedAt)
			.HasConversion(dateOnlyConverter)
			.IsRequired();

		builder.Property(x => x.ReminderOptOut)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(x => x.MaxConnections);
	}
}