# TypeSafeJsonConversion

When handling JSON at your system boundaries, you can use several strategies that help improve type safety after parsing the JSON. With the sample code of this repository, you can improve on two specific problems that come with JSON conversion: primitive obsession and discriminated unions (type hierarchies).

In the first section we discuss how we can avoid primitive obsession by introducing serializable custom primitive wrapper types. In the second section we talk about how we can support polymorphic deserialization when (re-)storing data, modeled as type hierarchies, from Azure Cosmos DB.

Note that we were using C# 8, some notes explain how we might want to handle it differently when using C#9 or C# 10.

## Reducing primitive obsession

> Note: If you are on C# 9, consider using [StronglyTypedId](https://github.com/andrewlock/StronglyTypedId), which relies on source generators to generate types for your identifiers (primitives). From C# 10 onwards, you can consider using `record struct` with a custom serializer if you do not want to rely on a library.

Primitive obsession is a problem that is well-known and which has been thoroughly discussed already. By introducing thin wrapper types as your primitives, you can solve some of the issues that primitive obsession brings, such as accidentally changing the parameter order or a decreased expressiveness of the code.

Throughout this sample code we are going to use a "calculation" that needs to be executed to illustrate how we can use the sample code to improve on type safety. A calculation represents a unit of work that is very CPU intensive. Each calculation has an identifier, among other properties, such as its input formula. We will subsequently call the identifier of the calculation `CalculationId`. This identifier might be based on a free-form `string` or a `Guid`, but in our case we choose an `int` to be its underlying type. Instead of relying on one of the built-in types directly, we create a thin wrapper-type for the `CalculationId`.

```csharp
partial struct CalculationId
{
    public CalculationId(int value) => Value = value;
    public int Value { get; }
}
```

We should use the type for the `CalculationId` consistently within the boundaries of our system, and only if it leaves the application boundaries do we access the `CalculationId.Value`. While this works well for our use case, it comes with a catch: when serializing data containing a `CalculationId`, JSON.NET and `System.Text.Json` cannot handle these types out of the box as we want them to. Deserializing will, per default, nest the `Value` property of the `CalculationId`.

This is why we create a `JsonStringConverter` that helps us (de-)serializing these wrapper types without an additional level of nesting. Proper deserialization of the `CalculationId` then becomes a matter of defining a `Parser`, including its inverse, `ToString`, for the `CalculationId`. Supporting JSON conversion for our `CalculationId` means writing the following code:

```csharp
[JsonConverter(typeof(JsonStringConverter<CalculationId, Parser>))]
partial struct CalculationId
{
    public static CalculationId Parse(string value) => new CalculationId(int.Parse(value));
    public override string? ToString() => Value.ToString();

    class Parser : IJsonStringParser<CalculationId>
    {
        public CalculationId Parse(string input) => CalculationId.Parse(input);
    }
}
```

While the `IJsonStringParser<CalculationId>` is used for deserialization to parse the `CalculationId` from its `string` representation, the `ToString` is picked up by the `JsonStringConverter` when serializing to JSON.

## Polymorphic deserialization using JSON.NET

Given that our calculation is CPU-intensive, for our example we now want to store the outcome of the calculation in Azure Cosmos DB. The result of the calculation could either be a success or a failure. Depending on the outcome, the properties of the result are going to change. A successful calculation contains the value of the result, while a failure contains information about why the calculation failed. When modeling this as a type hierarchy and trying to persist the JSON using the Cosmos DB SDK (v3), it becomes apparent that the SDK can not handle the hierarchy out of the box. Since the Cosmos DB SDK internally uses JSON.NET for serialization, solving the problem for the Cosmos DB SDK means solving the problem for the JSON.NET serializer.

Even though the problem was already [described and solved by Thomas Levesque](https://thomaslevesque.com/2019/10/14/handling-type-hierarchies-in-cosmos-db-part1/), we use our sample code to improve some shortcomings that do not work well for our scenario. When using `TypeNameHandling`, the type of the class is always included in the serialized JSON, which prevents renaming the classes and implies brittle serialization logic and introduces potential security issues. When using the custom `JsonConverter`, we find that implementing the polymorphic deserialization for different type hierarchies involves a lot of code duplication. We want the flexibility to define polymorphic deserialization behavior for type hierarchies based on an arbitrary discriminator, which does not necessarily need to be the name of the type, in a succinct way. Hence, we decide to create our own `JsonConverter`.

Our starting point is the success/failure type hierarchy described above, which we can model as follows: 

```csharp
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
```

With the `DiscriminatedUnionJsonConverter` sample code found in this repository, supporting polymorphic deserialization boils down to creating a converter and annotating the `Result` to use the custom converter:

```csharp
sealed class ResultJsonConverter : DiscriminatedUnionJsonConverter<Result, int>
{
    public ResultJsonConverter() :
    	base("ResultCode",
             Case<Success>.Value(0),
             Case<Error>.Value(1))
        { }
}
```

When annotating the `Result` correctly, we get polymorphic deserialization for items in a Cosmos container:

```csharp
[JsonConverter(typeof(ResultJsonConverter))]
abstract class Result
{
    public abstract int ResultCode { get; }
}
```

Alternatively, if you want to get the compiler support for all the possible cases of the discriminator, you can decide to use a different overload of the `DiscriminatedUnionJsonConverter` constructor.

```csharp
sealed class ResultJsonConverter : DiscriminatedUnionJsonConverter<Result, int>
{
    public ResultJsonConverter() :
        base(obj => obj.TryGetValue("ResultCode", out var tok)
                    && tok.ToObject<int>() is { } n ? n : throw new JsonSerializationException($"Property 'ResultCode' is not defined."),
             c => c switch
             {
             	0 => Deserialize<Success>(),
                1 => Deserialize<Error>(),
                _ => throw new JsonSerializationException($"Cannot deserialize DiscriminatedUnionConverterTests.Result from JSON object due to unhandled case: {c}")
             })
        { }
}
```

## Using System.Text.Json

If you decide to use `System.Text.Json` instead of Json.NET, you will find an equivalent set of classes attached in the repository. Refer to the test cases to see how to use the classes. Be aware that the `System.Text.Json`-compatible implementation loads the whole JSON document into memory to expose the `JsonElement` as the object on which to extract the discriminator. This means that if the JSON document is very large, you should not use the polymorphic deserialization logic of this repository.

**Important**: Always register the converter using the `[JsonConverter]` attribute. If you register it on `JsonSerializerOptions.Converters` instead, you will get an infinite loop on serialization as per the docs in [Migrate from Newtonsoft.Json to System.Text.Json - .NET | Microsoft Docs](https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-migrate-from-newtonsoft-how-to?pivots=dotnet-core-3-1#required-properties)

