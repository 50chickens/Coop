using System;
using Coop.Library.Common;

namespace Coop.Library.Common.Tests
{
    [TestFixture]
    public class ContentKeyBuilderTests
    {
        [Test]
        public void Test_That_We_Can_DistinguishConcatenationAmbiguousFields()
        {
            ContentKeyBuilder split = new ContentKeyBuilder();
            split.Add("first", "ab");
            split.Add("second", "c");
            ContentKeyBuilder shifted = new ContentKeyBuilder();
            shifted.Add("first", "a");
            shifted.Add("second", "bc");
            Assert.That(split.Finish(), Is.Not.EqualTo(shifted.Finish()));
        }

        [Test]
        public void Test_That_We_Can_GetTheSameKeyForFieldsAddedInAnyOrder()
        {
            ContentKeyBuilder forward = new ContentKeyBuilder();
            forward.Add("alpha", "one");
            forward.Add("beta", "two");
            forward.Add("gamma", new string[] { "x", "y" });
            ContentKeyBuilder reverse = new ContentKeyBuilder();
            reverse.Add("gamma", new string[] { "x", "y" });
            reverse.Add("beta", "two");
            reverse.Add("alpha", "one");
            Assert.That(forward.Finish(), Is.EqualTo(reverse.Finish()));
        }

        [Test]
        public void Test_That_We_Can_GetAStableKeyFromRepeatedFinishCalls()
        {
            ContentKeyBuilder builder = new ContentKeyBuilder();
            builder.Add("alpha", "one");
            builder.Add("beta", new string[] { "x", "y" });
            string first = builder.Finish();
            string second = builder.Finish();
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Test_That_We_Can_RefuseADuplicateField()
        {
            ContentKeyBuilder builder = new ContentKeyBuilder();
            builder.Add("alpha", "one");
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => builder.Add("alpha", "two"));
            Assert.That(exception.Message, Does.Contain("alpha"));
        }

        [Test]
        public void Test_That_We_Can_DistinguishASingleValueFromAListOfOne()
        {
            ContentKeyBuilder single = new ContentKeyBuilder();
            single.Add("alpha", "one");
            ContentKeyBuilder list = new ContentKeyBuilder();
            list.Add("alpha", new string[] { "one" });
            Assert.That(single.Finish(), Is.Not.EqualTo(list.Finish()));
        }
    }
}
