namespace DiscriminatedUnionConverter.JsonNet
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Newtonsoft.Json.Serialization;

    public class DiscriminatedUnionJsonConverter<TUnion, TCase> : JsonConverter
        where TCase : notnull
    {
        readonly Func<JObject, TCase> _discriminator;
        readonly Func<TCase, Func<JsonReader, JsonSerializer, TUnion>?> _deserializerSelector;

        public DiscriminatedUnionJsonConverter(string discriminatorName,
                                               params (TCase Case, Func<JsonReader, JsonSerializer, TUnion> Deserializer)[] cases) :
            this(obj => obj.TryGetValue(discriminatorName, out var token)
                        && token.ToObject<TCase>() is { } @case ? @case : throw new JsonSerializationException($"Property '{discriminatorName}' is not defined."),
                 cases) { }

        public DiscriminatedUnionJsonConverter(Func<JObject, TCase> discriminator,
                                               params (TCase Case, Func<JsonReader, JsonSerializer, TUnion> Deserializer)[] cases) :
            this(discriminator, from c in cases select KeyValuePair.Create(c.Case, c.Deserializer)) { }

        public DiscriminatedUnionJsonConverter(Func<JObject, TCase> discriminator,
                                               IEnumerable<KeyValuePair<TCase, Func<JsonReader, JsonSerializer, TUnion>>> cases) :
            this(discriminator, CreateDeserializerSelector(cases ?? throw new ArgumentNullException(nameof(cases)))) { }

        static Func<TCase, Func<JsonReader, JsonSerializer, TUnion>?>
            CreateDeserializerSelector(IEnumerable<KeyValuePair<TCase, Func<JsonReader, JsonSerializer, TUnion>>> cases)
        {
            var map = cases.ToDictionary(e => e.Key, e => e.Value);
            return @case => map.TryGetValue(@case, out var deserializer) ? deserializer : null;
        }

        public DiscriminatedUnionJsonConverter(Func<JObject, TCase> discriminator,
                                               Func<TCase, Func<JsonReader, JsonSerializer, TUnion>?> deserializerSelector)
        {
            _deserializerSelector = deserializerSelector ?? throw new ArgumentNullException(nameof(deserializerSelector));
            _discriminator = discriminator ?? throw new ArgumentNullException(nameof(discriminator));
        }

        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType) =>
            typeof(TUnion).IsAssignableFrom(objectType);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
        {
            if (reader is null) throw new ArgumentNullException(nameof(reader));
            if (objectType is null) throw new ArgumentNullException(nameof(objectType));
            if (serializer is null) throw new ArgumentNullException(nameof(serializer));

            var oldContractResolver = serializer.ContractResolver;
            try
            {
                // To deserialize a subtype we need to switch out the converter temporarily.
                // Not doing this would result in an infinite recursion.
                serializer.ContractResolver = CachedContractResolver;
                return JToken.ReadFrom(reader) is JObject obj && _discriminator(obj) is var @case
                     ? _deserializerSelector(@case) is { } deserializer
                       ? deserializer(obj.CreateReader(), serializer)
                       : throw new JsonSerializationException($"Cannot deserialize {typeof(TUnion)} from JSON object due to unhandled case: {@case}.")
                     : throw new JsonSerializationException($"Cannot deserialize {typeof(TUnion)} from any other value (\"{reader.TokenType}\") than a JSON object.");
            }
            finally
            {
                serializer.ContractResolver = oldContractResolver;
            }
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) =>
            throw new NotImplementedException();

        [ThreadStatic] static IContractResolver? _cachedContractResolver;
        static IContractResolver CachedContractResolver => _cachedContractResolver ??= new ContractResolver();

        sealed class ContractResolver : DefaultContractResolver
        {
            protected override JsonConverter? ResolveContractConverter(Type objectType) =>
                typeof(TUnion).IsAssignableFrom(objectType) ? null : base.ResolveContractConverter(objectType);
        }

        public class Case<TUnionCase>
            where TUnionCase : TUnion
        {
            public static (TCase Case, Func<JsonReader, JsonSerializer, TUnion> Deserializer)
                When(TCase @case) => (@case, Deserialize<TUnionCase>());
        }

        public static Func<JsonReader, JsonSerializer, TUnion> Deserialize<TUnionCase>() where TUnionCase : TUnion =>
            (r, s) => s.Deserialize<TUnionCase>(r)!;
    }
}
