using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Coop.Library.Common;

namespace Coop.Library.Artefacts.Tests
{
    [TestFixture]
    public class ArtefactStoreTests
    {
        private List<string> createdRoots;

        [SetUp]
        public void SetUp()
        {
            createdRoots = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            createdRoots
                .Where(path => Directory.Exists(path))
                .ToList()
                .ForEach(path => Directory.Delete(path, true));
        }

        [Test]
        public void Test_That_We_Can_StoreAnArtefactAndReadItBack()
        {
            ArtefactStore store = new ArtefactStore(NewRoot());
            string key = Key("artefact one");
            string staging = StagedArtefact(store, "one");

            ArtefactLookup claimed = store.Claim(key, staging);

            Assert.That(claimed.Found, Is.True);
            Assert.That(claimed.ArtefactKey, Is.EqualTo(key));
            Assert.That(claimed.DirectoryPath, Is.EqualTo(store.DirectoryPathOf(key)));
            Assert.That(File.ReadAllText(claimed.AssemblyPath), Is.EqualTo("assembly one"));
            Assert.That(File.Exists(Path.Combine(claimed.DirectoryPath, ArtefactStore.RuntimeConfigurationFileName)), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(claimed.DirectoryPath, "reference.dll")), Is.EqualTo("reference one"));
            Assert.That(Directory.Exists(staging), Is.False);

            ArtefactLookup read = store.Read(key);
            Assert.That(read.Found, Is.True);
            Assert.That(read.AssemblyPath, Is.EqualTo(claimed.AssemblyPath));
        }

        [Test]
        public void Test_That_We_Can_MissOnAnArtefactKeyThatIsNotStored()
        {
            ArtefactStore store = new ArtefactStore(NewRoot());
            string key = Key("never stored");

            ArtefactLookup lookup = store.Read(key);

            Assert.That(lookup.Found, Is.False);
            Assert.That(lookup.AssemblyPath, Is.Null);
            Assert.That(lookup.DirectoryPath, Is.Null);
            Assert.That(Directory.Exists(store.DirectoryPathOf(key)), Is.False);
        }

        [Test]
        public void Test_That_We_Can_TreatAnArtefactWithoutItsRuntimeConfigurationAsAMiss()
        {
            ArtefactStore store = new ArtefactStore(NewRoot());
            string key = Key("partial");
            string directory = store.DirectoryPathOf(key);
            Directory.CreateDirectory(directory);
            string assemblyPath = Path.Combine(directory, ArtefactStore.AssemblyFileName);
            File.WriteAllText(assemblyPath, "assembly only");

            ArtefactLookup lookup = store.Read(key);

            Assert.That(File.Exists(assemblyPath), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, ArtefactStore.RuntimeConfigurationFileName)), Is.False);
            Assert.That(lookup.Found, Is.False);
        }

        [Test]
        public void Test_That_We_Can_KeepTheFirstArtefactWhenTheSameKeyIsClaimedTwice()
        {
            ArtefactStore store = new ArtefactStore(NewRoot());
            string key = Key("claimed once");
            ArtefactLookup first = store.Claim(key, StagedArtefact(store, "first"));

            ArtefactLookup second = store.Claim(key, StagedArtefact(store, "second"));

            Assert.That(second.Found, Is.True);
            Assert.That(second.AssemblyPath, Is.EqualTo(first.AssemblyPath));
            Assert.That(File.ReadAllText(second.AssemblyPath), Is.EqualTo("assembly first"));
            Assert.That(Directory.GetDirectories(Path.Combine(store.Root, "staging")).Length, Is.EqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_LeaveOneReadableArtefactWhenTwoWritersRaceOnOneKey()
        {
            ArtefactStore store = new ArtefactStore(NewRoot());
            string key = Key("raced");

            Task first = Task.Run(() => store.Claim(key, StagedArtefact(store, "first")));
            Task second = Task.Run(() => store.Claim(key, StagedArtefact(store, "second")));
            Task.WaitAll(first, second);

            ArtefactLookup lookup = store.Read(key);
            Assert.That(lookup.Found, Is.True);
            Assert.That(File.ReadAllText(lookup.AssemblyPath), Is.AnyOf("assembly first", "assembly second"));
            Assert.That(File.Exists(Path.Combine(lookup.DirectoryPath, ArtefactStore.RuntimeConfigurationFileName)), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(lookup.DirectoryPath, "reference.dll")), Is.AnyOf("reference first", "reference second"));
            Assert.That(Directory.GetDirectories(Path.Combine(store.Root, "staging")).Length, Is.EqualTo(0));
        }

        private static string Key(string material)
        {
            return Sha.HexOfUtf8(material);
        }

        private static string StagedArtefact(ArtefactStore store, string marker)
        {
            string staging = store.NewStagingDirectory();
            File.WriteAllText(Path.Combine(staging, ArtefactStore.AssemblyFileName), "assembly " + marker);
            File.WriteAllText(Path.Combine(staging, ArtefactStore.RuntimeConfigurationFileName), "{\"runtimeOptions\":{}}");
            File.WriteAllText(Path.Combine(staging, "reference.dll"), "reference " + marker);
            return staging;
        }

        private string NewRoot()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "artefact-store-" + Guid.NewGuid().ToString("N"));
            createdRoots.Add(path);
            return path;
        }
    }
}
