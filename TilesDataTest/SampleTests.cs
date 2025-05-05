// Only compile these tests on windows, where Rhino is available
#if WINDOWS || MACOS

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Rhino.Testing.Fixtures;
using TilesData;

namespace TilesDataTest;

/// <summary>
/// Tests for entire tilesets
/// </summary>
[RhinoTestFixture]
public class SampleTests
{
    [Test]
    [TestCaseSource(nameof(Tilesets))]
    public void TilesetShouldParse(string fname, byte[] json)
    {
        var result = Tileset.Deserialize(json, new Uri("http://localhost"), null);
        Assert.IsNotNull(result);
    }

    public static IEnumerable<TestCaseData> Tilesets()
    {
        return TestHelpers.Tilesets();
    }
}

#endif
