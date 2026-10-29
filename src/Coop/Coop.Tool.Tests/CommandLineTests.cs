using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using Coop.Services.Surface;
using ModelContextProtocol.Protocol;

namespace Coop.Tool.Tests
{
    [TestFixture]
    public sealed class CommandLineTests
    {
        [Test]
        public void Test_That_Repeated_Parameter_Arguments_Become_Two_Entries()
        {
            string source = SourceFile("System.Console.WriteLine(1);");
            JsonObject arguments = CommandLine.Parse(new[] { "exec", "--file", source, "--param", "a=1", "--param", "b=2" }).Arguments;
            List<string> parameters = arguments["parameters"].AsObject().Select(entry => entry.Key + "=" + entry.Value.GetValue<string>()).ToList();
            Assert.That(parameters, Is.EqualTo(new List<string> { "a=1", "b=2" }));
        }

        [Test]
        public void Test_That_Repeated_Reference_Arguments_Become_Two_Entries()
        {
            string source = SourceFile("System.Console.WriteLine(1);");
            string first = ReferenceFile("first.dll");
            string second = ReferenceFile("second.dll");
            JsonObject arguments = CommandLine.Parse(new[] { "exec", "--file", source, "--reference", first, "--reference", second }).Arguments;
            List<string> references = arguments["references"].AsArray().Select(reference => reference.GetValue<string>()).ToList();
            Assert.That(references, Is.EqualTo(new List<string> { first, second }));
        }

        [Test]
        public void Test_That_A_Missing_File_Argument_Produces_A_Refusal()
        {
            ParsedCommandLine parsed = CommandLine.Parse(new[] { "exec", "--param", "a=1" });
            Assert.Multiple(() =>
            {
                Assert.That(parsed.Arguments, Is.Null);
                Assert.That(parsed.Refusal["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(parsed.Refusal["field"].GetValue<string>(), Is.EqualTo("--file"));
                Assert.That(parsed.Refusal["reason"].GetValue<string>(), Does.Contain("--file"));
            });
        }

        [Test]
        public void Test_That_A_Missing_Source_Path_Produces_A_Refusal_Carrying_The_Path()
        {
            string missing = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "missing.cs");
            ParsedCommandLine parsed = CommandLine.Parse(new[] { "exec", "--file", missing });
            Assert.Multiple(() =>
            {
                Assert.That(parsed.Arguments, Is.Null);
                Assert.That(parsed.Refusal["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(parsed.Refusal["field"].GetValue<string>(), Is.EqualTo("--file"));
                Assert.That(parsed.Refusal["path"].GetValue<string>(), Is.EqualTo(missing));
                Assert.That(parsed.Refusal["reason"].GetValue<string>(), Does.Contain("does not exist"));
            });
        }

        [Test]
        public void Test_That_We_Can_Read_The_Source_From_Standard_Input()
        {
            TextReader original = Console.In;
            Console.SetIn(new StringReader("System.Console.WriteLine(1);"));
            ParsedCommandLine parsed = CommandLine.Parse(new[] { "exec", "--file", "-" });
            Console.SetIn(original);
            Assert.That(parsed.Arguments["source"].GetValue<string>(), Is.EqualTo("System.Console.WriteLine(1);"));
        }

        [Test]
        public void Test_That_A_Missing_Reference_Writes_A_Refusal_Envelope_And_Exits_Non_Zero()
        {
            UseDataRoot();
            string source = SourceFile("System.Console.WriteLine(1);");
            string missing = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "missing.dll");
            PrintedCommand printed = PrintedCommandOf(new[] { "exec", "--file", source, "--reference", missing });
            JsonNode document = JsonNode.Parse(printed.Text);
            Assert.Multiple(() =>
            {
                Assert.That(printed.ExitCode, Is.EqualTo(Program.NotCompletedExitCode));
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(document["field"].GetValue<string>(), Is.EqualTo("references"));
                Assert.That(document["path"].GetValue<string>(), Is.EqualTo(missing));
            });
        }

        [Test]
        public void Test_That_A_Missing_File_Path_Writes_A_Refusal_Envelope_And_Exits_Non_Zero()
        {
            UseDataRoot();
            string missing = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "missing.cs");
            PrintedCommand printed = PrintedCommandOf(new[] { "exec", "--file", missing });
            JsonNode document = JsonNode.Parse(printed.Text);
            Assert.Multiple(() =>
            {
                Assert.That(printed.ExitCode, Is.EqualTo(Program.NotCompletedExitCode));
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(document["field"].GetValue<string>(), Is.EqualTo("--file"));
                Assert.That(document["path"].GetValue<string>(), Is.EqualTo(missing));
            });
        }

        [Test]
        public void Test_That_A_Compile_Error_Exits_Non_Zero()
        {
            Assert.That(Outcome.ExitCodeOf(Execute("this is not a program")), Is.Not.Zero);
        }

        [Test]
        public void Test_That_A_Completed_Run_Exits_Zero()
        {
            Assert.That(Outcome.ExitCodeOf(Execute("System.Console.WriteLine(\"hello\");")), Is.Zero);
        }

        [Test]
        public void Test_That_The_Template_Command_Prints_The_Served_Document()
        {
            string first = Printed(new[] { "template" });
            string second = Printed(new[] { "template" });
            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Empty);
                Assert.That(first, Is.EqualTo(second));
                Assert.That(JsonNode.Parse(first)["toolVersion"].GetValue<string>(), Is.EqualTo(SurfaceConfiguration.Resolve().Version));
            });
        }

        private static string Printed(string[] args)
        {
            PrintedCommand printed = PrintedCommandOf(args);
            Assert.That(printed.ExitCode, Is.Zero);
            return printed.Text;
        }

        private static PrintedCommand PrintedCommandOf(string[] args)
        {
            UseDataRoot();
            StringWriter writer = new StringWriter();
            TextWriter original = Console.Out;
            Console.SetOut(writer);
            int exitCode = Program.Main(args);
            Console.SetOut(original);
            PrintedCommand printed = new PrintedCommand();
            printed.ExitCode = exitCode;
            printed.Text = writer.ToString().Trim();
            return printed;
        }

        private static CallToolResult Execute(string source)
        {
            UseDataRoot();
            ExecuteTool tool = new ExecuteTool(SurfaceConfiguration.Resolve());
            JsonObject arguments = CommandLine.Parse(new[] { "exec", "--file", SourceFile(source) }).Arguments;
            return tool.Execute(arguments);
        }

        private static string ReferenceFile(string name)
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tool-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, name);
            return path;
        }

        private sealed class PrintedCommand
        {
            public int ExitCode { get; set; }

            public string Text { get; set; }
        }

        private static void UseDataRoot()
        {
            string dataRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tool-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataRoot);
            Environment.SetEnvironmentVariable(SurfaceConfiguration.DataRootVariable, dataRoot);
        }

        private static string SourceFile(string source)
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "tool-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "submission.cs");
            File.WriteAllText(path, source);
            return path;
        }
    }
}
