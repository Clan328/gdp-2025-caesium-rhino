namespace Test
{
    using System.Text.Json;
    using NUnit.Framework;
    using Rhino.Testing.Fixtures;
    using Rhino.Geometry;
    using TilesData;
    using System.Collections;

    [RhinoTestFixture]
    public class JsonDeser
    {
        [Test]
        [TestCaseSource(nameof(BoundingVolumeJson))]
        public void BoundingVolumeShouldParse(string json, BoundingVolume expected)
        {
            var result = JsonSerializer.Deserialize<BoundingVolume>(json, Tileset.JsonSerializerOptions);
            Assert.IsNotNull(result);
            if (expected is not null) Assert.AreEqual(expected, result);
        }

        public static IEnumerable BoundingVolumeJson()
        {
            yield return new TestCaseData(
                """{ "sphere": [0.1, 0.4, 0.2, 0.5] }""",
                BoundingVolume.From(new BoundingSphere(new Point3d(0.1, 0.4, 0.2), 0.5))
            );
            yield return new TestCaseData(
                """{ "region": [2.5, 2.504, -1.4, -1.398, 20, 300] }""",
                BoundingVolume.From(new BoundingRegion(2.5, 2.504, -1.4, -1.398, 20, 300))
            );
        }
    }
}
