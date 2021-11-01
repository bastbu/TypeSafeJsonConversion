namespace DiscriminatedUnionConverterTests.SystemTextJson
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using DiscriminatedUnionConverter.SystemTextJson;
    using Xunit;

    public class JsonStringConverterTests
    {
        [Theory]
        [InlineData("\"5\"", 5)]
        [InlineData("null", null)]
        public void Deserialize_SuccessCases(string input, int? actual)
        {
            var result = JsonSerializer.Deserialize<TestId>(input);
                
            Assert.Equal(actual is { } a ? new TestId(a) : null, result);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("{}")]
        [InlineData("[]")]
        public void Deserialize_FailureCases(string input)
        {
            void Act() => JsonSerializer.Deserialize<TestId>(input);

            Assert.Throws<JsonException>(Act);
        }

        public static object[] Serialize_Success_Data() => new object[]
        {
            new object[] { new TestId(5), "\"5\"" },
            new object?[] { null, "null" }
        };

        [Theory]
        [MemberData(nameof(Serialize_Success_Data))]
        public void Serialize_Success(TestId? testId, string expected)
        {
            var result = JsonSerializer.Serialize(testId);

            Assert.Equal(expected, result);
        }
    }

    [JsonConverter(typeof(JsonStringConverter<TestId, Parser>))]
    public class TestId
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
