// Tests for raw JSON deserialization

#nullable enable

using System.Text.Json;
using NUnit.Framework;
using TilesData.Json;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using DataUri = TilesData.DataUri;
using System;

namespace TilesDataTest;

/// <summary>
/// Tests for deserialising individual classes
/// </summary>
[TestFixture]
public class JsonTests
{
    [Test]
    [TestCaseSource(nameof(BoundingVolumeJson))]
    public void BoundingVolumeShouldParse(string json)
    {
        var result = JsonSerializer.Deserialize<BoundingVolume>(json, Tileset.JsonSerializerOptions);
        Assert.IsNotNull(result);
        Assert.IsTrue(result!.Box is not null || result.Region is not null || result.Sphere is not null);
    }

    public static IEnumerable BoundingVolumeJson()
    {
        string[] tests = {
                """{ "sphere": [0.1, 0.4, 0.2, 0.5] }""",
                """{ "region": [2.5, 2.504, -1.4, -1.398, 20, 300] }""",
                """{ "box": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12] }"""
            };
        return from test in tests select new TestCaseData(test);
    }

    [Test]
    [TestCaseSource(nameof(Tilesets))]
    public void TilesetShouldParse(string fname, byte[] json)
    {
        var result = Tileset.FromJson(json);
        Assert.IsNotNull(result);
    }

    public static IEnumerable<TestCaseData> Tilesets()
    {
        return TestHelpers.Tilesets();
    }
}

[TestFixture]
public class DataUriTests
{
    public abstract record class Expected(string? MediaType = null) { };
    sealed record class DataString(string String, string? MediaType = null) : Expected(MediaType);
    sealed record class DataBytes(byte[] Bytes, string? MediaType = null) : Expected(MediaType)
    {
        public DataBytes(ReadOnlySpan<byte> bytes, string? MediaType = null) : this(bytes.ToArray(), MediaType) { }
    }

    [Test]
    [TestCaseSource(nameof(TestCases))]
    public void Test(string input, Expected expected)
    {
        var actual = DataUri.Create(input);
        Assert.NotNull(actual);

        if (expected.MediaType is not null)
            Assert.AreEqual(actual!.MediaType, expected.MediaType);

        if (expected is DataString str)
            Assert.AreEqual(str.String, actual!.String);

        else if (expected is DataBytes bys)
            Assert.AreEqual(bys.Bytes, actual!.Bytes);
    }

    public static IEnumerable<TestCaseData> TestCases()
    {
        yield return new TestCaseData("data:,Hello%20World", new DataString("Hello World"));
        yield return new TestCaseData("data:;base64,SGVsbG8sIFdvcmxkIQ==", new DataBytes("Hello, World!"u8));
        yield return new TestCaseData("data:application/json;base64,eyAiZm9vIjogMTAgfQ==", new DataBytes("""{ "foo": 10 }"""u8, "application/json"));
    }
}
