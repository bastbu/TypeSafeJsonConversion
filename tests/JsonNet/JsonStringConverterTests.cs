namespace DiscriminatedUnionConverterTests.JsonNet
{
    using System;
    using DiscriminatedUnionConverter.JsonNet;
    using Newtonsoft.Json;
    using Xunit;

    public class JsonStringConverterTests
    {
        [Theory]
        [InlineData("\"5\"", 5)]
        [InlineData("null", null)]
        public void Deserialize_SuccessCases(string input, int? actual)
        {
            var result = JsonConvert.DeserializeObject<TestId>(input);
                
            Assert.Equal(actual is { } a ? new TestId(a) : null, result);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("{}")]
        [InlineData("[]")]
        public void Deserialize_FailureCases(string input)
        {
            void Act() => JsonConvert.DeserializeObject<TestId>(input);

            Assert.Throws<JsonSerializationException>(Act);
        }

        [Fact]
        public void Serialize_Success()
        {
            var id = new TestId(5);

            var result = JsonConvert.SerializeObject(id);

            Assert.Equal("\"5\"", result);
        }

        [Theory]
        [InlineData(typeof(TestId), true)]
        [InlineData(typeof(string), false)]
        public void CanConvert(Type type, bool expected)
        {
            var converter = new JsonStringConverter<TestId, TestId.Parser>();
            var result = converter.CanConvert(type);

            Assert.Equal(expected, result);
        }
    }

    [JsonConverter(typeof(JsonStringConverter<TestId, Parser>))]
    class TestId
    {
        public TestId(int value) => Value = value;

        public int Value { get; }

        public override bool Equals(object? obj) => obj is TestId id && Value == id.Value;

        public override int GetHashCode() => HashCode.Combine(Value);

        public override string? ToString() => $"{Value}";

        public class Parser : IJsonStringParser<TestId>
        {
            public TestId Parse(string input) => new TestId(int.Parse(input));
        }
    }
}
