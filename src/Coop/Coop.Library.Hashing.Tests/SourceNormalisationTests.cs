using Coop.Library.Hashing;

namespace Coop.Library.Hashing.Tests
{
    [TestFixture]
    public class SourceNormalisationTests
    {
        [Test]
        public void Test_That_We_Can_Turn_Windows_Line_Endings_Into_Lf()
        {
            Assert.That(SourceNormalisation.Apply("a\r\nb\r\n"), Is.EqualTo("a\nb\n"));
        }

        [Test]
        public void Test_That_We_Can_Turn_A_Lone_Carriage_Return_Into_Lf()
        {
            Assert.That(SourceNormalisation.Apply("a\rb"), Is.EqualTo("a\nb"));
        }

        [Test]
        public void Test_That_We_Can_Remove_One_Leading_Byte_Order_Mark()
        {
            Assert.That(SourceNormalisation.Apply("\uFEFFsource"), Is.EqualTo("source"));
            Assert.That(SourceNormalisation.Apply("\uFEFF\uFEFFsource"), Is.EqualTo("\uFEFFsource"));
        }

        [Test]
        public void Test_That_We_Can_Leave_Tabs_Trailing_Whitespace_And_Literals_Alone()
        {
            Assert.That(SourceNormalisation.Apply("\tvar s = \" a \";  \n"), Is.EqualTo("\tvar s = \" a \";  \n"));
        }

        [Test]
        public void Test_That_We_Can_Leave_A_Byte_Order_Mark_That_Is_Not_Leading_Alone()
        {
            Assert.That(SourceNormalisation.Apply("a\uFEFFb"), Is.EqualTo("a\uFEFFb"));
        }
    }
}
