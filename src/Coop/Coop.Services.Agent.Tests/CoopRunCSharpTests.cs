using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.AI;

namespace Coop.Services.Agent.Tests
{
    public sealed class CoopRunCSharpTests
    {
        [Test]
        public void Test_That_We_Can_Create_The_Run_CSharp_Function_With_The_Expected_Name_And_Description()
        {
            AIFunction function = CoopRunCSharp.CreateFunction(TestOptions.Create());

            Assert.That(function.Name, Is.EqualTo("run_csharp"));
            Assert.That(function.Description, Does.Contain("C#"));
            Assert.That(function.Description, Does.Contain("contained child process"));
            Assert.That(function.Description, Does.Contain("deadline"));
            Assert.That(function.Description.ToLowerInvariant(), Does.Contain("cached"));
        }

        [Test]
        public void Test_That_We_Can_Advertise_Source_As_The_Only_Required_Argument()
        {
            AIFunction function = CoopRunCSharp.CreateFunction(TestOptions.Create());
            JsonElement required = function.JsonSchema.GetProperty("required");

            List<string> names = required.EnumerateArray().Select(name => name.GetString()).ToList();

            Assert.That(names, Is.EquivalentTo(new string[] { "source" }));
        }

        [Test]
        public void Test_That_We_Can_Run_Real_CSharp_Through_The_Function_And_Read_Its_Standard_Output()
        {
            AIFunction function = CoopRunCSharp.CreateFunction(TestOptions.Create());
            Dictionary<string, object> arguments = new Dictionary<string, object>();
            arguments["source"] = "public static class Program { public static int Main(string[] args) { System.Console.WriteLine(\"hello from coop\"); return 0; } }";

            object result = function.InvokeAsync(new AIFunctionArguments(arguments), CancellationToken.None).AsTask().GetAwaiter().GetResult();

            JsonElement outcome = JsonDocument.Parse(result.ToString()).RootElement;
            Assert.That(outcome.GetProperty("status").GetString(), Is.EqualTo("completed"));
            Assert.That(outcome.GetProperty("compile").GetProperty("success").GetBoolean(), Is.True);
            Assert.That(outcome.GetProperty("run").GetProperty("standardOutput").GetString(), Does.Contain("hello from coop"));
            Assert.That(outcome.GetProperty("run").GetProperty("exitStatus").GetInt32(), Is.EqualTo(0));
        }

        [Test]
        public void Test_That_We_Can_Pass_Parameters_To_The_Submission()
        {
            CoopExecutionOptions options = TestOptions.Create();
            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters["greeting"] = "hello world";
            string source = "public static class Program { public static int Main(string[] args) { System.Console.WriteLine(args[0]); return 0; } }";

            string json = CoopRunCSharp.Execute(options, source, parameters, null);

            JsonElement outcome = JsonDocument.Parse(json).RootElement;
            Assert.That(outcome.GetProperty("run").GetProperty("standardOutput").GetString(), Does.Contain("greeting=hello world"));
        }

        [Test]
        public void Test_That_We_Can_Read_The_Fixed_Top_Level_Fields_Of_The_Outcome()
        {
            CoopExecutionOptions options = TestOptions.Create();
            string source = "public static class Program { public static int Main(string[] args) { return 0; } }";

            string json = CoopRunCSharp.Execute(options, source, null, null);

            List<string> names = JsonDocument.Parse(json).RootElement.EnumerateObject().Select(property => property.Name).ToList();
            Assert.That(names, Is.EqualTo(new string[] { "executionId", "status", "reused", "cache", "compile", "run" }));
        }

        [Test]
        public void Test_That_We_Can_Kill_A_Submission_That_Exceeds_Its_Deadline()
        {
            CoopExecutionOptions options = TestOptions.Create();
            string source = "public static class Program { public static int Main(string[] args) { System.Threading.Thread.Sleep(30000); return 0; } }";

            string json = CoopRunCSharp.Execute(options, source, null, 1);

            JsonElement outcome = JsonDocument.Parse(json).RootElement;
            Assert.That(outcome.GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(outcome.GetProperty("run").GetProperty("deadlineExpired").GetBoolean(), Is.True);
            Assert.That(outcome.GetProperty("run").GetProperty("exitStatus").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }

        [Test]
        public void Test_That_We_Can_Read_Compile_Diagnostics_Rather_Than_An_Exception()
        {
            CoopExecutionOptions options = TestOptions.Create();
            string source = "public static class Program { public static int Main(string[] args) { this does not compile } }";

            string json = CoopRunCSharp.Execute(options, source, null, null);

            JsonElement outcome = JsonDocument.Parse(json).RootElement;
            Assert.That(outcome.GetProperty("status").GetString(), Is.EqualTo("failed"));
            Assert.That(outcome.GetProperty("compile").GetProperty("success").GetBoolean(), Is.False);
            Assert.That(outcome.GetProperty("compile").GetProperty("diagnosticCount").GetInt32(), Is.GreaterThan(0));
            Assert.That(outcome.GetProperty("compile").GetProperty("diagnostics")[0].GetProperty("id").GetString(), Is.Not.Empty);
            Assert.That(outcome.GetProperty("run").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
    }
}
