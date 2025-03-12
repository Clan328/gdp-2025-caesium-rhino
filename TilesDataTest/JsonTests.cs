// Tests for raw JSON deserialization

using System.Text.Json;
using NUnit.Framework;
using TilesData.Json;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
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
        Assert.IsTrue(result.Box is not null || result.Region is not null || result.Sphere is not null);
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
