namespace DiscriminatedUnionConverter.JsonNet
{
    using System;
    using Newtonsoft.Json;

    public class JsonStringConverter<T, TParser> : JsonConverter
        where TParser : IJsonStringParser<T>, new()
    {
        private static readonly TParser Parser = new TParser();

        public override bool CanConvert(Type objectType) =>
            objectType == typeof(T);

        public override object? ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer) =>
            reader switch
            {
                null => throw new ArgumentNullException(nameof(reader)),
                { TokenType: JsonToken.Null } => null,
                { TokenType: JsonToken.String, Value: string s } => Parser.Parse(s),
                { TokenType: var token, Value: var value } => throw new JsonSerializationException($"Unexpected token or value when parsing version. Token: {token}, Value: {value}")
            };

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (writer is null) throw new ArgumentNullException(nameof(writer));

            writer.WriteValue(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public interface IJsonStringParser<out T>
    {
        T Parse(string input);
    }
}
