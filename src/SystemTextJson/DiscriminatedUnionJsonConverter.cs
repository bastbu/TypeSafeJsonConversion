namespace DiscriminatedUnionConverter.SystemTextJson
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class DiscriminatedUnionJsonConverter<TUnion, TCase> : JsonConverter<TUnion>
    {
        readonly Func<JsonElement, JsonSerializerOptions, TCase> _discriminator;
        readonly Func<TCase, Deserializer?> _deserializerSelector;

        public delegate TUnion Deserializer(ref Utf8JsonReader utf8JsonReader, JsonSerializerOptions options);

        public DiscriminatedUnionJsonConverter(string discriminatorName,
                                               params (TCase Case, Deserializer Deserializer)[] cases)
            : this((el, opt) => el.TryGetProperty(discriminatorName, opt.PropertyNameCaseInsensitive, out var value)
                                ? JsonSerializer.Deserialize<TCase>(value.GetRawText(), opt)
                                : throw new JsonException($"Property '{discriminatorName}' is not defined."),
                   cases) { }

        public DiscriminatedUnionJsonConverter(Func<JsonElement, JsonSerializerOptions, TCase> discriminator,
                                               params (TCase Case, Deserializer Deserializer)[] cases)
            : this(discriminator, CreateCases(cases)) { }

        static Func<TCase, Deserializer?> CreateCases(IEnumerable<(TCase Case, Deserializer Deserializer)> cases)
        {
            var map = cases.ToDictionary(e => e.Case, e => e.Deserializer);
            return @case => map.TryGetValue(@case, out var deserializer) ? deserializer : null;
        }

        public DiscriminatedUnionJsonConverter(Func<JsonElement, JsonSerializerOptions, TCase> discriminator,
                                               Func<TCase, Deserializer?> deserializerSelector)
        {
            _discriminator = discriminator ?? throw new ArgumentNullException(nameof(discriminator));
            _deserializerSelector = deserializerSelector ?? throw new ArgumentNullException(nameof(deserializerSelector));
        }

        public override bool CanConvert(Type objectType) =>
            typeof(TUnion).IsAssignableFrom(objectType);

        public override TUnion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert is null) throw new ArgumentNullException(nameof(typeToConvert));
            if (options is null) throw new ArgumentNullException(nameof(options));

            return GetDeserializer(reader)(ref reader, options);
            
            Deserializer GetDeserializer(Utf8JsonReader reader)
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
                var @case = _discriminator(jsonElement, options);
                return _deserializerSelector(@case) ?? throw new JsonException($"Cannot deserialize {typeof(TUnion)} from JSON object due to unhandled case: {@case}.");
            }
        }

        public override void Write(Utf8JsonWriter writer, TUnion value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (object?)value, options);

        public class Case<TUnionCase>
            where TUnionCase : TUnion
        {
            public static (TCase Case, Deserializer Deserializer)
                When(TCase @case) => (@case, Deserialize<TUnionCase>());
        }

        public static Deserializer Deserialize<TUnionCase>() where TUnionCase : TUnion =>
            new Deserializer((ref Utf8JsonReader reader, JsonSerializerOptions options) => JsonSerializer.Deserialize<TUnionCase>(ref reader, options));
    }

    internal static class Extensions
    {
        public static bool TryGetProperty(this JsonElement jsonElement,
                                          string propertyName,
                                          bool propertyNameCaseInsensitive,
                                          out JsonElement value)
        {
            foreach (var e in jsonElement.EnumerateObject())
            {
                if (!propertyNameCaseInsensitive)
                {
                    if (!e.NameEquals(propertyName))
                        continue;
                }
                else
                {
                    if (!e.NameEquals(propertyName) && !propertyName.Equals(e.Name, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                value = e.Value;
                return true;
            }

            value = default;
            return false;
        }
    }
}
