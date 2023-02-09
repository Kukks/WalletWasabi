using System.ComponentModel;
using System.Globalization;
using System.Linq;
using NBitcoin;
using Newtonsoft.Json;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Models;

public class DefaultAffiliationFlagAttribute : DefaultValueAttribute
{
	public DefaultAffiliationFlagAttribute() : base(AffiliationFlag.Default)
	{
	}
}

public record ReadyToSignRequestRequest(uint256 RoundId, Guid AliceId,
	[property: JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate), DefaultAffiliationFlag()]
	[property: JsonConverter(typeof(AffiliationFlagJsonConverter))]
	AffiliationFlag AffiliationFlag);

public class AffiliationFlagJsonConverter : JsonConverter<AffiliationFlag>
{
	public override AffiliationFlag? ReadJson(JsonReader reader, Type objectType, AffiliationFlag? existingValue,
		bool hasExistingValue, JsonSerializer serializer)
	{
		if (reader.Value is string serialized)
		{
			return new AffiliationFlag(serialized);
		}

		throw new JsonSerializationException("Cannot deserialize object.");
	}

	public override void WriteJson(JsonWriter writer, AffiliationFlag? value, JsonSerializer serializer)
	{
		Guard.NotNull(nameof(value), value);
		writer.WriteValue(value.Name);
	}
}

public class AffiliationFlagConverter : TypeConverter
{
	public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
	{
		if (sourceType == typeof(string))
		{
			return true;
		}

		return base.CanConvertFrom(context, sourceType);
	}

	public override object ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
	{
		if (value is string)
		{
			return new AffiliationFlag((string) value);
		}

		throw new NotSupportedException();
	}

	public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value,
		Type destinationType)
	{
		if (destinationType == typeof(string))
		{
			if (value is AffiliationFlag)
			{
				return ((AffiliationFlag) value).Name;
			}
		}

		return base.ConvertTo(context, culture, value, destinationType);
	}
}

[TypeConverter(typeof(AffiliationFlagConverter))]
public record AffiliationFlag
{
	public static readonly AffiliationFlag Default = new("btcpayserver");

	private const int MinimumNameLength = 1;
	private const int MaximumNameLength = 20;

	public AffiliationFlag(string name)
	{
		if (!IsValidName(name))
		{
			throw new ArgumentException("The name is too long, too short or contains non-alphanumeric characters.",
				nameof(name));
		}

		Name = name;
	}

	public string Name { get; }

	public override string ToString()
	{
		return Name;
	}

	private static bool IsAlphanumeric(string text)
	{
		return text.All(x => char.IsAscii(x) && char.IsLetterOrDigit(x));
	}

	private static bool IsValidName(string name)
	{
		if (!IsAlphanumeric(name))
		{
			return false;
		}

		if (name.Length < MinimumNameLength || name.Length > MaximumNameLength)
		{
			return false;
		}

		return true;
	}
}
