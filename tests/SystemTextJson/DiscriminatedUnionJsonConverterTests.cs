namespace DiscriminatedUnionConverterTests.SystemTextJson
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using DiscriminatedUnionConverter.SystemTextJson;
    using Xunit;

    public sealed class DiscriminatedUnionJsonConverterTests
    {
        public class Serialization
        {
            [Fact]
            public void Is_Per_Default()
            {
                var success = new Success { Value = 1.0 };
                var error = new Error { ErrorDetail = "Some error occurred" };

                var results = new Result[] { success, error };

                var json = JsonSerializer.Serialize(results);

                var rootElement = JsonDocument.Parse(json).RootElement;
                Assert.Equal(2, rootElement.GetArrayLength());

                var actual1 = rootElement[0];
                Assert.Equal(0, actual1.GetProperty("ResultCode").GetInt32());
                Assert.Equal(1.0, actual1.GetProperty("Value").GetDouble());

                var actual2 = rootElement[1];
                Assert.Equal(1, actual2.GetProperty("ResultCode").GetInt32());
                Assert.Equal("Some error occurred", actual2.GetProperty("ErrorDetail").GetString());
            }

            [Fact]
            public void Applies_Serializer_Options()
            {
                var result = JsonSerializer.Serialize(new Success { Value = 1.0 },
                                                      new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                Assert.Equal("{\"resultCode\":0,\"value\":1}", result);
            }

            [Theory]
            [InlineData(false, "{\"Result\":null}")]
            [InlineData(true, "{}")]
            public void Serializes_Null(bool ignoreNullValues, string expected)
            {
                var serializerOptions = new JsonSerializerOptions { IgnoreNullValues = ignoreNullValues };

                var result = JsonSerializer.Serialize(new Wrapper { Result = null }, serializerOptions);

                Assert.Equal(expected, result);
            }

            class Wrapper
            {
                public Result? Result { get; set; }
            }
        }

        public class Deserialization
        {
            [Fact]
            public void Can_Discriminate_Between_Cases()
            {
                const string json = @"
                [
                    { ""ResultCode"": 0, ""Value"": 1.0 },
                    { ""ResultCode"": 1, ""ErrorDetail"": ""Calculation failed"" }
                ]";

                var results = JsonSerializer.Deserialize<Result[]>(json) ?? throw new Exception("Deserialized object must not be null");

                Assert.Equal(2, results.Length);
                var successResult = Assert.IsType<Success>(results[0]);
                Assert.Equal(ResultCode.Success, successResult.ResultCode);
                Assert.Equal(1.0, successResult.Value);
                var errorResult = Assert.IsType<Error>(results[1]);
                Assert.Equal(ResultCode.Failure, errorResult.ResultCode);
                Assert.Equal("Calculation failed", errorResult.ErrorDetail);
            }

            [Fact]
            public void Deserialize_Is_Agnostic_To_ResultCode_Position()
            {
                var result = JsonSerializer.Deserialize<Result>("{ \"Value\": 1.0, \"ResultCode\": 0 }")
                    ?? throw new Exception("Deserialized object must not be null");

                Assert.NotNull(result);
            }

            [Fact]
            public void Deserialize_Null()
            {
                var result = JsonSerializer.Deserialize<Result?>("null");

                Assert.Null(result);
            }

            [Theory]
            [InlineData("{ \"ResultCode\": 2 }")]
            [InlineData("{ \"ResultCode\": \"4\" }")]
            [InlineData("{ \"Detail\": \"Calculation failed\" }")]
            [InlineData("\"Detail\": \"Calculation failed\" }")]
            [InlineData("{}")]
            [InlineData("{ \"4\" }")]
            public void Unhandled_Case_Throws_JsonSerializationException(string json)
            {
                void Act() => JsonSerializer.Deserialize<Result>(json);

                Assert.Throws<JsonException>(Act);
            }

            [Fact]
            public void Applies_Serializer_Options()
            {
                var result = JsonSerializer.Deserialize<Result>("{ \"vAlue\": 1.0, \"rEsultcode\": 0 }",
                                                                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new Exception("Deserialized object must not be null");

                var success = Assert.IsType<Success>(result);
                Assert.Equal(1.0, success.Value);
            }
        }

        public class Constructor
        {
            public static object[] Null_Arguments_Data() => new object[]
            {
                new Action[] { () => new DiscriminatedUnionJsonConverter<int, int>(null!, (int _) => null) },
                new Action[] { () => new DiscriminatedUnionJsonConverter<int, int>((JsonElement _, JsonSerializerOptions __) => 0, (Func<int, DiscriminatedUnionJsonConverter<int, int>.Deserializer?>)null!) }
            };

            [Theory]
            [MemberData(nameof(Null_Arguments_Data))]
            public void Null_Arguments(Action action)
            {
                Assert.Throws<ArgumentNullException>(action);
            }
        }

        [JsonConverter(typeof(ResultConverter))]
        abstract class Result
        {
            public abstract ResultCode ResultCode { get; }
        }

        sealed class Error : Result
        {
            public override ResultCode ResultCode => ResultCode.Failure;
            public string ErrorDetail { get; set; } = string.Empty;
        }

        sealed class Success : Result
        {
            public override ResultCode ResultCode => ResultCode.Success;
            public double Value { get; set; }
        }

        enum ResultCode
        {
            Success,
            Failure
        }

        sealed class ResultConverter : DiscriminatedUnionJsonConverter<Result, ResultCode>
        {
            public ResultConverter() :
                base("ResultCode",
                     Case<Success>.When(ResultCode.Success),
                     Case<Error>.When(ResultCode.Failure)) { }
        }
    }
}
