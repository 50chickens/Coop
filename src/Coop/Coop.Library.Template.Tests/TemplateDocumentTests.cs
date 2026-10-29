using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using Coop.Library.Execution;

namespace Coop.Library.Template.Tests
{
    [TestFixture]
    public sealed class TemplateDocumentTests
    {
        [Test]
        public void Test_That_We_Can_Serialise_The_Template()
        {
            string json = CoopTemplate.Serialise("1.2.3+build");
            JsonObject document = JsonNode.Parse(json).AsObject();
            Assert.That(document["templateVersion"].GetValue<int>(), Is.EqualTo(CoopTemplate.CurrentTemplateVersion));
            Assert.That(document["toolVersion"].GetValue<string>(), Is.EqualTo("1.2.3+build"));
            string[] sections = { "language", "request", "submission", "parameters", "references", "packages", "timeout", "response", "exampleRequest" };
            Assert.That(sections.Where(name => !document.ContainsKey(name)).ToList(), Is.Empty);
            Assert.That(sections.Where(name => document[name] == null).ToList(), Is.Empty);
            Assert.That(document["request"]["fields"].AsArray().Count, Is.GreaterThan(0));
            Assert.That(document["response"]["fields"].AsArray().Count, Is.GreaterThan(0));
        }

        [Test]
        public void Test_That_We_Can_Substitute_Only_The_Tool_Version()
        {
            string token = CoopTemplate.Serialise(CoopTemplate.ToolVersionToken);
            string served = CoopTemplate.Serialise("9.9.9+sha");
            Assert.That(served, Is.Not.EqualTo(token));
            Assert.That(served.Replace("9.9.9+sha", CoopTemplate.ToolVersionToken), Is.EqualTo(token));
        }

        [Test]
        public void Test_That_We_Can_Give_Every_Request_Field_A_Default_Or_A_Required_Mark()
        {
            List<RequestField> fields = new List<RequestField>();
            fields.AddRange(CoopTemplate.Create().Request.Fields);
            fields.AddRange(CoopTemplate.Create().Submission.Fields);
            Assert.That(fields, Is.Not.Empty);
            Assert.That(fields.Where(field => !field.Required && field.Default == null).Select(field => field.Name).ToList(), Is.Empty);
        }

        [Test]
        public void Test_That_We_Can_Publish_The_Timeout_Field_With_Default_And_Ceiling()
        {
            RequestField timeoutField = CoopTemplate.Create().Request.Fields.Single(field => field.Name == "timeoutSeconds");
            Assert.That(timeoutField.Required, Is.False);
            Assert.That(timeoutField.Default, Is.EqualTo(ExecutionDeadline.DefaultSeconds.ToString(CultureInfo.InvariantCulture)));
            Assert.That(timeoutField.Description, Does.Contain("600"));
            TimeoutContract timeout = CoopTemplate.Create().Timeout;
            Assert.That(timeout.DefaultSeconds, Is.EqualTo(ExecutionDeadline.DefaultSeconds));
            Assert.That(timeout.CeilingSeconds, Is.EqualTo(ExecutionDeadline.CeilingSeconds));
            Assert.That(timeout.Clamped, Is.True);
            Assert.That(timeout.Detail, Does.Contain("timeoutSeconds"));
            Assert.That(timeout.Detail, Does.Contain("60"));
            Assert.That(timeout.Expiry, Does.Contain("deadlineExpired"));
            Assert.That(timeout.Guidance, Does.Contain("60 seconds"));
        }

        [Test]
        public void Test_That_We_Can_Describe_Unmeasured_Numbers_As_Null()
        {
            ResponseContract response = CoopTemplate.Create().Response;
            Assert.That(response.UnmeasuredValue, Is.Null);
            Assert.That(response.UnmeasuredDetail, Does.Contain("null"));
            List<string> described = response.Fields.Where(field => field.WhenUnmeasured != null).Select(field => field.WhenUnmeasured).ToList();
            Assert.That(described, Is.Not.Empty);
            Assert.That(described, Does.Not.Contain("unavailable"));
            Assert.That(described.Where(value => value != "null").ToList(), Is.Empty);
            JsonObject published = JsonNode.Parse(CoopTemplate.Serialise("1.0.0"))["response"].AsObject();
            Assert.That(published.ContainsKey("unmeasuredValue"), Is.True);
            Assert.That(published["unmeasuredValue"], Is.Null);
        }

        [Test]
        public void Test_That_We_Can_Describe_The_Reuse_Block()
        {
            ResponseContract response = CoopTemplate.Create().Response;
            Assert.That(response.Reuse, Is.Not.Null);
            Assert.That(response.Reuse.Detail, Does.Contain("timings belong to that execution"));
            Assert.That(response.Reuse.Fields.Select(field => field.Name).ToList(), Is.EquivalentTo(new[] { "executionId", "recordedAt", "runnerVersion", "hint" }));
            JsonNode published = JsonNode.Parse(CoopTemplate.Serialise("1.0.0"))["response"]["reuse"]["fields"];
            Assert.That(published.AsArray().Count, Is.EqualTo(4));
        }

        [Test]
        public void Test_That_We_Can_Omit_A_Field_Coop_Does_Not_Honour()
        {
            Assert.That(CoopTemplate.Create().Request.Fields.Select(field => field.Name).ToList(), Does.Not.Contain("shouldRunAsync"));
        }

        [Test]
        public void Test_That_We_Can_Describe_Every_Reference_Source()
        {
            ReferencesContract references = CoopTemplate.Create().References;
            Assert.That(references.Platform, Is.EqualTo("Microsoft.NETCore.App"));
            Assert.That(references.AvailableByDefault, Is.Not.Empty);
            Assert.That(references.SuppliedByCaller, Is.Not.Empty);
            Assert.That(references.Followed, Does.Contain("followed into"));
            Assert.That(references.FolderRule, Does.Contain("whole closure"));
            Assert.That(references.FolderRule, Does.Contain("no other folder is searched"));
            Assert.That(references.SuppliedByPackages, Is.Not.Empty);
            Assert.That(references.NothingElse, Does.Contain("compile error"));
        }

        [Test]
        public void Test_That_We_Can_Describe_Parameters_Packages_And_An_Executable_Example()
        {
            ParametersContract parameters = CoopTemplate.Create().Parameters;
            Assert.That(parameters.Arrival, Does.Contain("command-line arguments"));
            Assert.That(parameters.Order, Does.Contain("ordinal"));
            Assert.That(parameters.Values, Does.Contain("first ="));
            Assert.That(parameters.Empty, Is.Not.Empty);
            PackagesContract packages = CoopTemplate.Create().Packages;
            Assert.That(packages.Feed, Does.Contain("nuget.org"));
            Assert.That(packages.Refusals.Count, Is.EqualTo(10));
            Assert.That(packages.Refusals.Where(refusal => refusal.MessageShape == null).ToList(), Is.Empty);
            ExampleRequest example = CoopTemplate.Create().ExampleRequest;
            Assert.That(example.Submission.SourceText, Does.Contain("static int Main"));
            Assert.That(example.Response, Is.Null);
            Assert.That(example.Packages, Is.Empty);
        }
    }
}
