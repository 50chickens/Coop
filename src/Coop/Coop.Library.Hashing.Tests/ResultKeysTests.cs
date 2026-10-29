using System.Collections.Generic;
using Coop.Library.Hashing;

namespace Coop.Library.Hashing.Tests
{
    [TestFixture]
    public class ResultKeysTests
    {
        private const string ArtefactKey = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90";

        private const string BuildVersion = "0.1.0+2f4c1a9";

        [Test]
        public void Test_That_We_Can_Key_The_Same_Result_From_Two_Separately_Constructed_Requests()
        {
            string first = KeyOf(Request());
            string second = KeyOf(Request());
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Test_That_We_Can_Key_The_Same_Result_Whatever_Order_Parameters_Arrive_In()
        {
            ResultKeyRequest reversed = Request();
            reversed.Parameters = new Dictionary<string, string>();
            reversed.Parameters.Add("name", "sample");
            reversed.Parameters.Add("count", "3");
            Assert.That(KeyOf(reversed), Is.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_Key_The_Same_Result_Whatever_Order_Packages_Arrive_In()
        {
            ResultKeyRequest reversed = Request();
            reversed.Packages.Reverse();
            Assert.That(KeyOf(reversed), Is.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Parameter_Value()
        {
            ResultKeyRequest changed = Request();
            changed.Parameters["count"] = "4";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Parameter_Name()
        {
            ResultKeyRequest changed = Request();
            changed.Parameters.Remove("count");
            changed.Parameters.Add("total", "3");
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_Tell_Parameter_Sets_Apart_Whose_Concatenation_Is_Identical()
        {
            ResultKeyRequest joinedFirst = Request();
            joinedFirst.Parameters = new Dictionary<string, string>();
            joinedFirst.Parameters.Add("ab", "c");
            ResultKeyRequest joinedSecond = Request();
            joinedSecond.Parameters = new Dictionary<string, string>();
            joinedSecond.Parameters.Add("a", "bc");
            Assert.That(KeyOf(joinedFirst), Is.Not.EqualTo(KeyOf(joinedSecond)));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Package_Version()
        {
            ResultKeyRequest changed = Request();
            changed.Packages[0].Version = "13.0.4";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Artefact_Key()
        {
            ResultKeyRequest changed = Request();
            changed.ArtefactKey = "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f91";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Can_See_A_Changed_Build_Version()
        {
            ResultKeyRequest changed = Request();
            changed.BuildVersion = "0.1.0+3a5d2b0";
            Assert.That(KeyOf(changed), Is.Not.EqualTo(KeyOf(Request())));
        }

        [Test]
        public void Test_That_We_Refuse_An_Unpinned_Package_By_Name()
        {
            ResultKeyRequest refused = Request();
            refused.Packages.Add(new ResolvedPackage { Id = "Unpinned.Package", Version = null });
            KeyOutcome outcome = ResultKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.UnpinnedPackage));
            Assert.That(outcome.Refusal.Message, Does.Contain("Unpinned.Package"));
        }

        [Test]
        public void Test_That_We_Refuse_An_Absent_Build_Version_By_Name()
        {
            ResultKeyRequest refused = Request();
            refused.BuildVersion = null;
            KeyOutcome outcome = ResultKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.MissingBuildVersion));
        }

        [Test]
        public void Test_That_We_Refuse_An_Absent_Artefact_Key_By_Name()
        {
            ResultKeyRequest refused = Request();
            refused.ArtefactKey = null;
            KeyOutcome outcome = ResultKeys.Of(refused);
            Assert.That(outcome.Key, Is.Null);
            Assert.That(outcome.Refusal.Name, Is.EqualTo(HashingRefusalNames.MissingArtefactKey));
        }

        [Test]
        public void Test_That_We_Can_Produce_A_Lowercase_Hex_Result_Key()
        {
            string key = KeyOf(Request());
            Assert.That(key.Length, Is.EqualTo(64));
            Assert.That(key, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void Test_That_We_Can_Key_A_Known_Result_Across_Processes_And_Machines()
        {
            Assert.That(KeyOf(Request()), Is.EqualTo("b931b143699ac043ee553b45fe7a2dcbfa2770c290963a8df3855a3d6bc54ddc"));
        }

        private static string KeyOf(ResultKeyRequest request)
        {
            KeyOutcome outcome = ResultKeys.Of(request);
            Assert.That(outcome.Refusal, Is.Null);
            return outcome.Key;
        }

        private static ResultKeyRequest Request()
        {
            ResultKeyRequest request = new ResultKeyRequest();
            request.ArtefactKey = ArtefactKey;
            request.BuildVersion = BuildVersion;
            request.Parameters = new Dictionary<string, string>();
            request.Parameters.Add("count", "3");
            request.Parameters.Add("name", "sample");
            request.Packages = new List<ResolvedPackage>
            {
                new ResolvedPackage { Id = "Humanizer.Core", Version = "2.14.1" },
                new ResolvedPackage { Id = "Newtonsoft.Json", Version = "13.0.3" }
            };
            return request;
        }
    }
}
