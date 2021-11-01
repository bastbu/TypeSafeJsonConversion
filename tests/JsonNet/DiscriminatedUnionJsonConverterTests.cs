namespace DiscriminatedUnionConverterTests.JsonNet
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using DiscriminatedUnionConverter.JsonNet;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Xunit;

    public sealed class DiscriminatedUnionJsonConverterTests
    {
        public class Serialization
        {
            [Fact]
            public void Is_Per_Default()
            {
                var success = new Success(1.0);
                var error = new Error(1, "Some error occurred");

                var results = new Result[] { success, error };

                var json = JsonConvert.SerializeObject(results);

                var actuals = JToken.Parse(json).Values<JObject>().ToArray();
                Assert.Equal(2, actuals.Length);

                var actual1 = actuals[0] ?? throw new Exception("Value must not be null");
                Assert.NotNull(actual1);
                Assert.Equal(2, actual1.Count);
                Assert.Equal(0, actual1.Value<int?>("ResultCode"));
                Assert.Equal(1.0, actual1.Value<int>("Value"));

                var actual2 = actuals[1] ?? throw new Exception("Value must not be null");
                Assert.Equal(2, actual2.Count);
                Assert.Equal(1, actual2.Value<int?>("ResultCode"));
                Assert.Equal("Some error occurred", actual2.Value<string>("ErrorDetail"));
            }
        }

        public class Deserialization
        {
            [Fact]
            public void Can_Discriminate_Between_Cases()
            {
                const string json = @"
                [
                    { ResultCode: 0, Value: 1.0 },
                    { ResultCode: 1, ErrorDetail: 'Calculation failed' }
                ]";

                var results = JsonConvert.DeserializeObject<Result[]>(json) ?? throw new Exception("Deserialized object must not be null");

                Assert.Equal(2, results.Length);
                var successResult = Assert.IsType<Success>(results[0]);
                Assert.Equal(0, successResult.ResultCode);
                Assert.Equal(1.0, successResult.Value);
                var errorResult = Assert.IsType<Error>(results[1]);
                Assert.Equal(1, errorResult.ResultCode);
                Assert.Equal("Calculation failed", errorResult.ErrorDetail);
            }

            [Theory]
            [InlineData("{ ResultCode: 2 }", "Cannot deserialize DiscriminatedUnionConverterTests.JsonNet.DiscriminatedUnionJsonConverterTests+Result from JSON object due to unhandled case: 2.")]
            [InlineData("{ Detail: 'Calculation failed' }", "Property 'ResultCode' is not defined.")]
            [InlineData("true", "Cannot deserialize DiscriminatedUnionConverterTests.JsonNet.DiscriminatedUnionJsonConverterTests+Result from any other value (\"Boolean\") than a JSON object.")]
            public void Unhandled_Case_Throws_JsonSerializationException(string json, string expectedMessage)
            {
                void Act() => JsonConvert.DeserializeObject<Result>(json, new ResultJsonConverter());

                var exception = Assert.Throws<JsonSerializationException>(Act);
                Assert.Equal(expectedMessage, exception.Message);
            }
        }

        public class Constructor
        {
            public static object[] Null_Arguments_Data() => new object[]
            {
                 new Action[] { () => new DiscriminatedUnionJsonConverter<int, int>((JObject obj) => 0, (IEnumerable<KeyValuePair<int, Func<JsonReader, JsonSerializer, int>>>)null!) },
                 new Action[] { () => new DiscriminatedUnionJsonConverter<int, int>((JObject obj) => 0, (Func<int, Func<JsonReader, JsonSerializer, int>?>)null!) },
                 new Action[] { () => new DiscriminatedUnionJsonConverter<int, int>(null!, (int _) => (JsonReader __, JsonSerializer ___) => 0) }
            };

            [Theory]
            [MemberData(nameof(Null_Arguments_Data))]
            public void Null_Arguments(Action action)
            {
                Assert.Throws<ArgumentNullException>(action);
            }
        }

        public class Capabilities
        {
            readonly DiscriminatedUnionJsonConverter<int, int> _converter = new DiscriminatedUnionJsonConverter<int, int>(string.Empty);

            [Fact]
            public void Cannot_Write()
            {
                Assert.False(_converter.CanWrite);
            }

            [Fact]
            public void Writing_Throws()
            {
                using var writer = new JsonTextWriter(new StringWriter());

                void Act() => _converter.WriteJson(writer, new Error(1, "Some error detail"), JsonSerializer.CreateDefault());

                Assert.Throws<NotImplementedException>(Act);
            }

            [Fact]
            public void Can_Read()
            {
                var converter = new DiscriminatedUnionJsonConverter<int, int>(string.Empty);
                Assert.True(converter.CanRead);
            }

            [Theory]
            [InlineData(typeof(int), true)]
            [InlineData(typeof(string), false)]
            public void Can_Convert(Type type, bool expected)
            {
                var converter = new DiscriminatedUnionJsonConverter<int, int>(string.Empty);
                Assert.Equal(expected, converter.CanConvert(type));
            }
        }

        [JsonConverter(typeof(ResultJsonConverter))]
        abstract class Result
        {
            public abstract int ResultCode { get; }
        }

        sealed class Error : Result
        {
            public Error(int resultCode, string errorDetail)
            {
                ResultCode = resultCode;
                ErrorDetail = errorDetail;
            }

            public override int ResultCode { get; }
            public string ErrorDetail { get; }
        }

        sealed class Success : Result
        {
            public Success(double value)
            {
                Value = value;
            }

            public override int ResultCode => 0;
            public double Value { get; }
        }

        sealed class ResultJsonConverter : DiscriminatedUnionJsonConverter<Result, int>
        {
            public ResultJsonConverter() :
                base("ResultCode",
                     Case<Success>.When(0),
                     Case<Error>.When(1)) { }
        }
    }
}
