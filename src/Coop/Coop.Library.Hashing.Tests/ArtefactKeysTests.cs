using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Coop.Library.Common;
using Coop.Library.Hashing;

namespace Coop.Library.Hashing.Tests
{
    [TestFixture]
    public class ArtefactKeysTests
    {
        private const string Source = "System.Console.WriteLine(\"hello\");\n";

        [Test]
        public void Test_That_We_Can_Key_The_Same_Artefact_From_Two_Separately_Constructed_Requests()
        {
            string first = KeyOf(Request());
            string second = KeyOf(Request());
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Test_That_We_Can_Key_The_Same_Artefact_Whatever_Order_Packages_And_References_Arrive_In()
        {
            ArtefactKeyRequest reversed = Request();
            reversed.Packages.Reverse();
            reversed.References.Reverse();
            Assert.That(KeyOf(reversed), Is.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Single_Character_Change_To_The_Source()
        {
            ArtefactKeyRequest changed = Request();
            changed.SourceText = "System.Console.WriteLine(\"hallo\");\n";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Runtime_Identifier()
        {
            ArtefactKeyRequest changed = Request();
            changed.RuntimeIdentifier = "win-x64";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_Tell_An_Empty_Runtime_Identifier_From_A_Present_One()
        {
            ArtefactKeyRequest empty = Request();
            empty.RuntimeIdentifier = "";
            Assert.That(KeyOf(empty), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Target_Framework()
        {
            ArtefactKeyRequest changed = Request();
            changed.TargetFramework = "net9.0";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Compiler_Version()
        {
            ArtefactKeyRequest changed = Request();
            changed.CompilerVersion = "5.10.0";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Language_Version()
        {
            ArtefactKeyRequest changed = Request();
            changed.LanguageVersion = "12";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Output_Kind()
        {
            ArtefactKeyRequest changed = Request();
            changed.OutputKind = "DynamicallyLinkedLibrary";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Optimization_Level()
        {
            ArtefactKeyRequest changed = Request();
            changed.OptimizationLevel = "Release";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Reference_Runtime_Version()
        {
            ArtefactKeyRequest changed = Request();
            changed.ReferenceRuntimeVersion = "10.0.1";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Package_Version()
        {
            ArtefactKeyRequest changed = Request();
            changed.Packages[0].Version = "13.0.4";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_Key_Line_Endings_And_A_Byte_Order_Mark_As_The_Same_Source()
        {
            ArtefactKeyRequest windows = Request();
            windows.SourceText = "\uFEFFSystem.Console.WriteLine(\"hello\");\r\n";
            ArtefactKeyRequest oldMacintosh = Request();
            oldMacintosh.SourceText = "System.Console.WriteLine(\"hello\");\r";
            Assert.That(KeyOf(windows), Is.EqualTo(KeyOf(Request())));
            Assert.That(KeyOf(oldMacintosh), Is.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_Whitespace_Inside_A_Literal_As_A_Different_Source()
        {
            ArtefactKeyRequest changed = Request();
            changed.SourceText = "System.Console.WriteLine(\"hello \");\n";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_Trailing_Whitespace_Outside_A_Literal_As_A_Different_Source()
        {
            ArtefactKeyRequest changed = Request();
            changed.SourceText = Source + " ";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Reference_Whose_Bytes_Changed()
        {
            ArtefactKeyRequest first = Request();
            first.References = new List<KeyReference> { Reference("C:/refs/Sample.dll", Encoding.UTF8.GetBytes("sample")) };
            ArtefactKeyRequest rebuilt = Request();
            rebuilt.References = new List<KeyReference> { Reference("C:/refs/Sample.dll", Encoding.UTF8.GetBytes("samplf")) };
            Assert.That(KeyOf(rebuilt), Is.Not.EqualTo(KeyOf(first)));
        }

        [Test]
        public void Test_That_We_Can_See_A_Reference_Moved_To_Another_Path()
        {
            ArtefactKeyRequest first = Request();
            first.References = new List<KeyReference> { Reference("C:/refs/Sample.dll", Encoding.UTF8.GetBytes("sample")) };
            ArtefactKeyRequest elsewhere = Request();
            elsewhere.References = new List<KeyReference> { Reference("C:/other/Sample.dll", Encoding.UTF8.GetBytes("sample")) };
            Assert.That(KeyOf(elsewhere), Is.Not.EqualTo(KeyOf(first)));
        }

        [Test]
        public void Test_That_We_Can_Key_A_Reference_Less_Submission_As_It_Was_Keyed_Before()
        {
            ArtefactKeyRequest request = Request();
            request.References = new List<KeyReference>();
            request.Packages = new List<ResolvedPackage>();
            Assert.That(KeyOf(request), Is.EqualTo("496776b2d58b204a1acedd192175132680fba5ba903a06206affbfef442d5ebe"));
        }

        [Test]
        public void Test_That_We_Can_See_An_Added_Reference()
        {
            ArtefactKeyRequest changed = Request();
            changed.References.Add(Reference("C:/refs/Extra.dll", Encoding.UTF8.GetBytes("extra")));
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Refuse_An_Absent_Runtime_Identifier_By_Name()
        {
            ArtefactKeyRequest refused = Request();
            refused.RuntimeIdentifier = null;
            KeyOutcome outcome = ArtefactKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.MissingRuntimeIdentifier));
            Assert.That(outcome.Refusal.Message, Is.Not.Empty);
        }

        [Test]
        public void Test_That_We_Refuse_An_Unpinned_Package_By_Name()
        {
            ArtefactKeyRequest refused = Request();
            refused.Packages.Add(new ResolvedPackage { Id = "Unpinned.Package", Version = null });
            KeyOutcome outcome = ArtefactKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.UnpinnedPackage));
            Assert.That(outcome.Refusal.Message, Does.Contain("Unpinned.Package"));
        }

        [Test]
        public void Test_That_We_Refuse_An_Absent_Source_By_Name()
        {
            ArtefactKeyRequest refused = Request();
            refused.SourceText = null;
            KeyOutcome outcome = ArtefactKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.MissingSource));
        }

        [Test]
        public void Test_That_We_Can_Produce_A_Lowercase_Hex_Key()
        {
            string key = KeyOf(Request());
            Assert.That(key.Length, Is.EqualTo(64));
            Assert.That(key, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void Test_That_We_Can_Key_A_Known_Artefact_Across_Processes_And_Machines()
        {
            Assert.That(KeyOf(Request()), Is.EqualTo("dae4f14409d09191758d434cf4e0280029e58a1234d3a7986887b42c219ace98"));
        }

        [Test]
        public void Test_That_We_Can_Key_Identically_Under_Another_Culture()
        {
            string invariant = KeyOf(Request());
            CultureInfo original = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.That(KeyOf(Request()), Is.EqualTo(invariant));
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        private static string KeyOf(ArtefactKeyRequest request)
        {
            KeyOutcome outcome = ArtefactKeys.Of(request);
            Assert.That(outcome.Refusal, Is.Null);
            return outcome.Key;
        }

        private static ArtefactKeyRequest Request()
        {
            ArtefactKeyRequest request = new ArtefactKeyRequest();
            request.SourceText = Source;
            request.TargetFramework = "net10.0";
            request.RuntimeIdentifier = "linux-x64";
            request.CompilerVersion = "5.9.0";
            request.LanguageVersion = "Latest";
            request.OutputKind = "ConsoleApplication";
            request.OptimizationLevel = "Debug";
            request.ReferenceRuntimeVersion = "10.0.0";
            request.Packages = new List<ResolvedPackage>
            {
                new ResolvedPackage { Id = "Humanizer.Core", Version = "2.14.1" },
                new ResolvedPackage { Id = "Newtonsoft.Json", Version = "13.0.3" }
            };
            request.References = new List<KeyReference>
            {
                Reference("C:/refs/Other.dll", Encoding.UTF8.GetBytes("other")),
                Reference("C:/refs/Sample.dll", Encoding.UTF8.GetBytes("sample"))
            };
            return request;
        }

        private static KeyReference Reference(string path, byte[] bytes)
        {
            KeyReference reference = new KeyReference();
            reference.Path = path;
            reference.FileName = System.IO.Path.GetFileName(path);
            reference.ContentHash = Sha.HexOfBytes(bytes);
            return reference;
        }
    }
}
