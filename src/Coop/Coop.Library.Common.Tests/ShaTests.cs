using System.IO;
using Coop.Library.Common;

namespace Coop.Library.Common.Tests
{
    [TestFixture]
    public class ShaTests
    {
        [Test]
        public void Test_That_We_Can_HashUtf8Text()
        {
            Assert.That(Sha.HexOfUtf8("abc"), Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
        }

        [Test]
        public void Test_That_We_Can_HashBytes()
        {
            Assert.That(Sha.HexOfBytes(new byte[0]), Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
        }

        [Test]
        public void Test_That_We_Can_HashAStream()
        {
            using (MemoryStream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("abc")))
            {
                Assert.That(Sha.HexOfStream(stream), Is.EqualTo(Sha.HexOfUtf8("abc")));
            }
        }
    }
}
