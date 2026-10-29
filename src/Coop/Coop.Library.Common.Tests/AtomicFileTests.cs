using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Coop.Library.Common;

namespace Coop.Library.Common.Tests
{
    [TestFixture]
    public class AtomicFileTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "atomic-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        [Test]
        public void Test_That_We_Can_WriteAndOverwriteAFile()
        {
            string path = Path.Combine(folder, "content.txt");
            AtomicFile.Write(path, "first value");
            Assert.That(File.ReadAllText(path), Is.EqualTo("first value"));
            AtomicFile.Write(path, "second value");
            Assert.That(File.ReadAllText(path), Is.EqualTo("second value"));
            Assert.That(Directory.GetFiles(folder).Length, Is.EqualTo(1));
        }

        [Test]
        public void Test_That_We_Can_ReplaceAFile()
        {
            string source = Path.Combine(folder, "source.txt");
            string destination = Path.Combine(folder, "destination.txt");
            File.WriteAllText(source, "after");
            File.WriteAllText(destination, "before");
            AtomicFile.Replace(source, destination);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("after"));
            Assert.That(File.Exists(source), Is.False);
        }

        [Test]
        public void Test_That_We_Can_RetryAReplaceAfterATransientDenial()
        {
            string source = Path.Combine(folder, "source.txt");
            string destination = Path.Combine(folder, "destination.txt");
            File.WriteAllText(source, "after");
            File.WriteAllText(destination, "before");
            File.SetAttributes(destination, FileAttributes.ReadOnly);
            Task replace = Task.Run(() => AtomicFile.Replace(source, destination));
            Thread.Sleep(120);
            bool deniedOnFirstAttempt = !replace.IsCompleted;
            File.SetAttributes(destination, FileAttributes.Normal);
            replace.GetAwaiter().GetResult();
            Assert.That(deniedOnFirstAttempt, Is.True);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("after"));
            Assert.That(File.Exists(source), Is.False);
        }
    }
}
