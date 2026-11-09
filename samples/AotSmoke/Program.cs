// Copyright (c) ResulTus. All rights reserved.

// The AOT smoke check: publishes trimmed and Native AOT, runs natively, and
// proves the source-generated System.Text.Json round trip of Result<int>
// end to end. Exits non-zero when any assertion fails so CI fails with the
// process. Every serializer call goes through the AOT-safe surface from
// api-completion S7: ResultJsonConverter<T> with a JsonTypeInfo<T> from the
// app's source-generated context, never reflection-based Deserialize<T>.

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ResulTus;
using ResulTus.Serialization;

var successResult = Result<int>.Success(41);
var failureResult = Result<int>.Failure(Error.Failure("E1", "boom"));

if (successResult.GetValueOrDefault() != 41)
{
    Fail("GetValueOrDefault on success");
}

if (failureResult.GetValueOrDefault(99) != 99)
{
    Fail("GetValueOrDefault on failure");
}

var converter = new ResultJsonConverter<int>(SmokeJsonContext.Default.Int32);

var successJson = RoundTrip(converter, successResult);
const string ExpectedSuccess = """{"isSuccess":true,"value":41}""";
if (!string.Equals(successJson, ExpectedSuccess, StringComparison.Ordinal))
{
    Fail($"success round trip wrote '{successJson}'");
}

var failureJson = RoundTrip(converter, failureResult);
const string ExpectedFailure = """{"isSuccess":false,"error":{"code":"E1","message":"boom","type":"Failure"}}""";
if (!string.Equals(failureJson, ExpectedFailure, StringComparison.Ordinal))
{
    Fail($"failure round trip wrote '{failureJson}'");
}

var parsedFailure = ReadResult(converter, """{"isSuccess":false,"error":{"code":"E9","type":"NotFound"}}""");
if (parsedFailure.TryGetValue(out _, out var error)
    || error.Code != "E9"
    || error.Type != ErrorType.NotFound)
{
    Fail("a failure was not read as its wire representation");
}

if (successResult.Match(value => value * 2, _ => -1) != 82)
{
    Fail("Match did not transform the success payload");
}

Console.WriteLine("aot-smoke: all assertions passed");
return 0;

// Sends the value through the converter's full write/read cycle and returns
// exactly what the write produced.
static string RoundTrip(ResultJsonConverter<int> converter, Result<int> value)
{
    using var stream = new MemoryStream();
    using (var writer = new Utf8JsonWriter(stream))
    {
        converter.Write(writer, value, new JsonSerializerOptions());
    }

    var json = Encoding.UTF8.GetString(stream.ToArray());
    var parsed = ReadResult(converter, json);
    if (parsed != value)
    {
        Fail($"round trip changed the payload: '{json}'");
    }

    return json;
}

static Result<int> ReadResult(ResultJsonConverter<int> converter, string json)
{
    var bytes = Encoding.UTF8.GetBytes(json);
    var reader = new Utf8JsonReader(bytes);
    if (!reader.Read())
    {
        Fail("empty JSON payload");
    }

    return converter.Read(ref reader, typeof(Result<int>), new JsonSerializerOptions());
}

static void Fail(string reason)
{
    Console.Error.WriteLine($"aot-smoke: {reason}");
    Environment.Exit(1);
}

[JsonSerializable(typeof(int))]
internal sealed partial class SmokeJsonContext : JsonSerializerContext;
