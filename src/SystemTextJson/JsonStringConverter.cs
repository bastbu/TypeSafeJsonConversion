namespace DiscriminatedUnionConverter.SystemTextJson
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class JsonStringConverter<T, TParser> : JsonConverter<T>
        where TParser : IJsonStringParser<T>, new()
    {
        private static readonly TParser Parser = new TParser();

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            reader.TokenType switch
            {
                JsonTokenType.String => Parser.Parse(reader.GetString()),
                _ => throw new JsonException()
            };

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value is null) return;

            writer.WriteStringValue(value.ToString());
        }
    }

    public interface IJsonStringParser<out T>
    {
        T Parse(string input);
    }
}
