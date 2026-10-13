using System.Text.Json;
using Tomlyn;
using Tomlyn.Serialization;

namespace ClipMate.Core.Helpers;

/// <summary>
/// Shared Tomlyn serializer settings for ClipMate configuration files.
/// </summary>
public static class TomlSerialization
{
    /// <summary>
    /// Options matching the on-disk format written by earlier versions: snake_case keys, enums as strings
    /// and local date-times with millisecond precision. Dictionary keys (database IDs, application names) are left untouched.
    /// </summary>
    public static TomlSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = [new StringEnumConverterFactory(), new LocalDateTimeConverter()],
    };

    /// <summary>
    /// Writes <see cref="DateTime" /> values as TOML local date-times with millisecond precision, regardless of
    /// <see cref="DateTime.Kind" />. Tomlyn's default would drop the milliseconds and append an offset for Local/Utc kinds.
    /// </summary>
    private sealed class LocalDateTimeConverter : TomlConverter<DateTime>
    {
        public override DateTime Read(TomlReader reader) => reader.GetTomlDateTime().DateTime.DateTime;

        public override void Write(TomlWriter writer, DateTime value) =>
            writer.WriteDateTimeValue(new TomlDateTime(new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Unspecified), TimeSpan.Zero), 3, TomlDateTimeKind.LocalDateTime));
    }

    /// <summary>
    /// Writes enums by name and reads them from either a name or an integer
    /// (older config files may have log_level = 3 instead of log_level = "Information").
    /// </summary>
    private sealed class StringEnumConverterFactory : TomlConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override TomlConverter CreateConverter(Type typeToConvert, TomlSerializerOptions options) =>
            (TomlConverter)Activator.CreateInstance(typeof(StringEnumConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class StringEnumConverter<TEnum> : TomlConverter<TEnum> where TEnum : struct, Enum
    {
        public override TEnum Read(TomlReader reader)
        {
            return reader.TokenType switch
            {
                TomlTokenType.Integer => (TEnum)Enum.ToObject(typeof(TEnum), reader.GetInt64()),
                TomlTokenType.String when Enum.TryParse<TEnum>(reader.GetString(), true, out var value) => value,
                _ => throw reader.CreateException($"Invalid value for enum '{typeof(TEnum).Name}'."),
            };
        }

        public override void Write(TomlWriter writer, TEnum value) => writer.WriteStringValue(value.ToString());
    }
}
