using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Coop.Library.Execution.Tests
{
    [TestFixture]
    public class ReferenceClosureTests
    {
        private List<string> createdDirectories;

        [SetUp]
        public void SetUp()
        {
            createdDirectories = new List<string>();
        }

        [TearDown]
        public void TearDown()
        {
            createdDirectories
                .Where(path => Directory.Exists(path))
                .ToList()
                .ForEach(path => Directory.Delete(path, true));
        }

        [Test]
        public void Test_That_We_Can_Resolve_No_References_To_No_References()
        {
            Assert.That(ReferenceClosure.Resolve(new string[0]), Is.Empty);
        }

        [Test]
        public void Test_That_We_Can_Resolve_A_Dependency_That_Sits_Beside_The_Reference()
        {
            string reference = typeof(SubmissionCompiler).Assembly.Location;
            string dependency = Path.Combine(Path.GetDirectoryName(reference), "Coop.Library.Common.dll");
            List<string> resolved = ReferenceClosure.Resolve(new string[] { reference });
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Does.Contain(reference));
                Assert.That(resolved, Does.Contain(dependency));
                Assert.That(resolved.Distinct(StringComparer.OrdinalIgnoreCase).Count(), Is.EqualTo(resolved.Count));
            });
        }

        [Test]
        public void Test_That_We_Can_Leave_A_Declared_Dependency_That_Is_Absent_From_The_Folder()
        {
            string folder = NewDirectory();
            string reference = CopyInto(folder, typeof(SubmissionCompiler).Assembly.Location);
            List<string> resolved = ReferenceClosure.Resolve(new string[] { reference });
            Assert.That(resolved, Is.EqualTo(new string[] { reference }));
        }

        [Test]
        public void Test_That_We_Can_Skip_A_Platform_Assembly_That_Sits_Beside_The_Reference()
        {
            string folder = NewDirectory();
            string reference = CopyInto(folder, typeof(SubmissionCompiler).Assembly.Location);
            string platform = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.dll");
            CopyInto(folder, platform);
            List<string> resolved = ReferenceClosure.Resolve(new string[] { reference });
            Assert.That(resolved, Is.EqualTo(new string[] { reference }));
        }

        [Test]
        public void Test_That_We_Can_Leave_A_File_The_Reference_Does_Not_Declare()
        {
            string folder = NewDirectory();
            string reference = CopyInto(folder, typeof(SubmissionCompiler).Assembly.Location);
            string dependency = CopyInto(folder, typeof(Coop.Library.Common.Sha).Assembly.Location);
            string undeclared = CopyInto(folder, typeof(Coop.Library.Common.Sha).Assembly.Location, "An.Unrelated.Assembly.dll");
            List<string> resolved = ReferenceClosure.Resolve(new string[] { reference });
            Assert.Multiple(() =>
            {
                Assert.That(resolved, Does.Contain(dependency));
                Assert.That(resolved, Does.Not.Contain(undeclared));
            });
        }

        [Test]
        public void Test_That_We_Can_Name_A_File_Present_Beside_The_Reference_That_It_Does_Not_Declare()
        {
            string folder = NewDirectory();
            string reference = CopyInto(folder, typeof(SubmissionCompiler).Assembly.Location);
            CopyInto(folder, typeof(Coop.Library.Common.Sha).Assembly.Location);
            CopyInto(folder, typeof(Coop.Library.Common.Sha).Assembly.Location, "An.Unrelated.Assembly.dll");
            List<UndeclaredSibling> undeclared = ReferenceClosure.Undeclared(new string[] { reference });
            Assert.Multiple(() =>
            {
                Assert.That(undeclared.Select(entry => entry.Name), Is.EqualTo(new string[] { "An.Unrelated.Assembly" }));
                Assert.That(undeclared.Single().Folder, Is.EqualTo(folder));
            });
        }

        [Test]
        public void Test_That_We_Can_Leave_A_Declared_File_Present_Beside_The_Reference_Out_Of_The_Undeclared_List()
        {
            string folder = NewDirectory();
            string reference = CopyInto(folder, typeof(SubmissionCompiler).Assembly.Location);
            CopyInto(folder, typeof(Coop.Library.Common.Sha).Assembly.Location);
            List<UndeclaredSibling> undeclared = ReferenceClosure.Undeclared(new string[] { reference });
            Assert.That(undeclared, Is.Empty);
        }

        [Test]
        public void Test_That_We_Can_Resolve_The_Same_Set_Whatever_Order_The_References_Arrive_In()
        {
            string reference = typeof(SubmissionCompiler).Assembly.Location;
            string dependency = Path.Combine(Path.GetDirectoryName(reference), "Coop.Library.Common.dll");
            List<string> forward = ReferenceClosure.Resolve(new string[] { reference, dependency });
            List<string> backward = ReferenceClosure.Resolve(new string[] { dependency, reference });
            Assert.Multiple(() =>
            {
                Assert.That(Sorted(backward), Is.EqualTo(Sorted(forward)));
                Assert.That(forward.Count(path => path == dependency), Is.EqualTo(1));
            });
        }

        private static List<string> Sorted(List<string> paths)
        {
            return paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string CopyInto(string folder, string sourcePath)
        {
            return CopyInto(folder, sourcePath, Path.GetFileName(sourcePath));
        }

        private static string CopyInto(string folder, string sourcePath, string fileName)
        {
            string destination = Path.Combine(folder, fileName);
            File.Copy(sourcePath, destination);
            return destination;
        }

        private string NewDirectory()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "closure-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            createdDirectories.Add(path);
            return path;
        }
    }
}
