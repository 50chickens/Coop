using System.IO;
using System.Text.Json;
using Coop.Library.Common;

namespace Coop.Library.Common.Tests
{
    [TestFixture]
    public class CoopJsonTests
    {
        [Test]
        public void Test_That_We_Can_WriteCamelCaseWithoutNulls()
        {
            CoopJsonSample sample = new CoopJsonSample();
            sample.SomeName = "value";
            sample.AbsentValue = null;
            string json = JsonSerializer.Serialize(sample, CoopJson.Options);
            Assert.That(json, Is.EqualTo("{\"someName\":\"value\"}"));
        }

        [Test]
        public void Test_That_We_Can_ReadPropertyNamesCaseInsensitively()
        {
            CoopJsonSample sample = JsonSerializer.Deserialize<CoopJsonSample>("{\"SOMENAME\":\"other\"}", CoopJson.Options);
            Assert.That(sample.SomeName, Is.EqualTo("other"));
        }

        public sealed class CoopJsonSample
        {
            public string SomeName { get; set; }

            public string AbsentValue { get; set; }
        }
    }
}
