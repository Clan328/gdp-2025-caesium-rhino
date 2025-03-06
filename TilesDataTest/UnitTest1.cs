namespace Test
{
    using System.Text.Json;
    using NUnit.Framework;
    using Rhino.Testing.Fixtures;
    using TilesData;

    [RhinoTestFixture]
    public class JsonDeser
    {
        [Test]
        public void BoundingVolumeShouldParse()
        {
            string[] tests = { """{ "sphere": [0.1, 0.4, 0.2, 0.5] }""" };

            foreach (string test in tests)
            {
                var result = JsonSerializer.Deserialize<BoundingVolume>(test);
                Assert.IsNotNull(result);
            }
        }
    }
}
