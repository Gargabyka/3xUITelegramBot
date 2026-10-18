using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Domain.Extensions;

public static class EnumExtensions
{
	public static string GetDescription(this Enum value)
	{
		var type = value.GetType();
		var name = Enum.GetName(type, value);
		if (string.IsNullOrWhiteSpace(name))
		{
			return value.ToString();
		}

		var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static);
		if (field is null)
		{
			return name;
		}

		// DisplayAttribute (DataAnnotations)
		var display = field.GetCustomAttribute<DisplayAttribute>();
		if (display is not null)
		{
			if (!string.IsNullOrWhiteSpace(display.Description))
			{
				return display.Description;
			}

			if (!string.IsNullOrWhiteSpace(display.Name))
			{
				return display.Name;
			}
		}

		// DescriptionAttribute (ComponentModel)
		var description = field.GetCustomAttribute<DescriptionAttribute>();
		if (description is not null && !string.IsNullOrWhiteSpace(description.Description))
		{
			return description.Description;
		}

		return name;
	}
}