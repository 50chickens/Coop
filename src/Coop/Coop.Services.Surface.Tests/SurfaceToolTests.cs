using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Coop.Services.Surface.Tests
{
    [TestFixture]
    public sealed class SurfaceToolTests
    {
        private SurfaceClient client;

        private JsonNode handshake;

        [OneTimeSetUp]
        public void StartServer()
        {
            string dataRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "surface-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataRoot);
            client = new SurfaceClient(dataRoot);
            handshake = client.Initialize();
        }

        [OneTimeTearDown]
        public void StopServer()
        {
            client.Dispose();
        }

        [Test]
        public void Test_That_We_Can_Complete_An_Initialize_Handshake()
        {
            JsonNode result = handshake["result"];
            Assert.Multiple(() =>
            {
                Assert.That(result["protocolVersion"].GetValue<string>(), Is.Not.Empty);
                Assert.That(result["serverInfo"]["name"].GetValue<string>(), Is.EqualTo("coop-surface"));
                Assert.That(result["serverInfo"]["version"].GetValue<string>(), Is.Not.Empty);
                Assert.That(result["capabilities"]["tools"], Is.Not.Null);
            });
        }

        [Test]
        public void Test_That_We_Can_Announce_Startup_Lines_On_Standard_Error()
        {
            List<string> lines = client.ErrorLines();
            Assert.Multiple(() =>
            {
                Assert.That(lines.Any(line => line.StartsWith("coop-surface ", StringComparison.Ordinal)), Is.True);
                Assert.That(lines.Any(line => line == "data root: " + client.DataRoot), Is.True);
                Assert.That(lines.Any(line => line == "record store root: " + Path.Combine(client.DataRoot, "records")), Is.True);
                Assert.That(lines.Any(line => line == "tools (4): execute, template, get, search"), Is.True);
                Assert.That(lines.Last(), Is.EqualTo("ready"));
            });
        }

        [Test]
        public void Test_That_We_Can_List_Four_Tools()
        {
            JsonNode result = client.Call("tools/list", new JsonObject())["result"];
            List<string> names = result["tools"].AsArray().Select(tool => tool["name"].GetValue<string>()).ToList();
            JsonNode execute = result["tools"].AsArray().First(tool => tool["name"].GetValue<string>() == "execute");
            Assert.Multiple(() =>
            {
                Assert.That(names, Is.EqualTo(new List<string> { "execute", "template", "get", "search" }));
                Assert.That(execute["inputSchema"]["required"].AsArray().Select(item => item.GetValue<string>()), Is.EqualTo(new List<string> { "source" }));
                Assert.That(execute["inputSchema"]["properties"]["timeoutSeconds"]["type"].GetValue<string>(), Is.EqualTo("integer"));
                Assert.That(execute["annotations"]["readOnlyHint"].GetValue<bool>(), Is.False);
                Assert.That(execute["annotations"]["openWorldHint"].GetValue<bool>(), Is.False);
            });
        }

        [Test]
        public void Test_That_We_Can_Execute_A_Hello_World_Program()
        {
            string marker = Guid.NewGuid().ToString("N");
            JsonNode document = Execute(HelloWorld(marker), null);
            Assert.Multiple(() =>
            {
                Assert.That(document["executionId"].GetValue<string>().Length, Is.EqualTo(32));
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(document["reused"], Is.Null);
                Assert.That(document["run"]["standardOutput"].GetValue<string>().Trim(), Is.EqualTo("hello " + marker));
                Assert.That(document["run"]["exitStatus"].GetValue<int>(), Is.EqualTo(0));
                Assert.That(document["run"]["deadlineExpired"].GetValue<bool>(), Is.False);
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.True);
                Assert.That(document["cache"]["artefactReused"].GetValue<bool>(), Is.False);
                Assert.That(document["cache"]["resultReused"].GetValue<bool>(), Is.False);
            });
        }

        [Test]
        public void Test_That_We_Can_Reuse_The_Second_Identical_Execution()
        {
            string marker = Guid.NewGuid().ToString("N");
            string source = HelloWorld(marker);
            JsonNode first = Execute(source, null);
            JsonNode second = Execute(source, null);
            Assert.Multiple(() =>
            {
                Assert.That(second["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(second["executionId"].GetValue<string>(), Is.Not.EqualTo(first["executionId"].GetValue<string>()));
                Assert.That(second["reused"], Is.Not.Null);
                Assert.That(second["reused"]["executionId"].GetValue<string>(), Is.EqualTo(first["executionId"].GetValue<string>()));
                Assert.That(second["cache"]["resultReused"].GetValue<bool>(), Is.True);
                Assert.That(second["cache"]["artefactReused"].GetValue<bool>(), Is.True);
                Assert.That(second["cache"]["resultKey"].GetValue<string>(), Is.EqualTo(first["cache"]["resultKey"].GetValue<string>()));
                Assert.That(second["run"]["elapsedMilliseconds"].GetValue<long>(), Is.EqualTo(first["run"]["elapsedMilliseconds"].GetValue<long>()));
                Assert.That(second["run"]["standardOutput"].GetValue<string>(), Is.EqualTo(first["run"]["standardOutput"].GetValue<string>()));
            });
        }

        [Test]
        public void Test_That_We_Can_Report_A_Compile_Failure_With_Diagnostics()
        {
            string source = "public static class Program { public static int Main(string[] args) { int value = ; return 0; } }";
            JsonNode document = Execute(source, null);
            JsonNode diagnostics = document["compile"]["diagnostics"];
            Assert.Multiple(() =>
            {
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("failed"));
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.False);
                Assert.That(document["run"], Is.Null);
                Assert.That(diagnostics.AsArray().Count, Is.GreaterThan(0));
                Assert.That(diagnostics[0]["id"].GetValue<string>(), Is.Not.Empty);
                Assert.That(diagnostics[0]["message"].GetValue<string>(), Is.Not.Empty);
                Assert.That(diagnostics[0]["line"].GetValue<int>(), Is.GreaterThan(0));
                Assert.That(document["compile"]["diagnosticCount"].GetValue<int>(), Is.EqualTo(diagnostics.AsArray().Count));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Get_A_Record_By_A_Known_Identifier()
        {
            string marker = Guid.NewGuid().ToString("N");
            string source = HelloWorld(marker);
            JsonNode executed = Execute(source, null);
            string executionId = executed["executionId"].GetValue<string>();
            JsonObject arguments = new JsonObject();
            arguments["executionId"] = executionId;
            JsonNode record = DocumentOf(CallTool("get", arguments));
            Assert.Multiple(() =>
            {
                Assert.That(record["executionId"].GetValue<string>(), Is.EqualTo(executionId));
                Assert.That(record["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(record["name"].GetValue<string>(), Is.EqualTo("execute"));
                Assert.That(record["category"].GetValue<string>(), Is.EqualTo("agent"));
                Assert.That(record["submission"]["sourceText"].GetValue<string>(), Is.EqualTo(source));
                Assert.That(record["response"]["standardOutput"].GetValue<string>().Trim(), Is.EqualTo("hello " + marker));
                Assert.That(record["timestamp"].GetValue<string>(), Is.Not.Empty);
            });
        }

        [Test]
        public void Test_That_We_Can_Refuse_An_Unknown_Identifier_With_Code_Six()
        {
            JsonObject arguments = new JsonObject();
            arguments["executionId"] = "doesnotexist";
            JsonNode response = CallTool("get", arguments);
            JsonNode refusal = DocumentOf(response);
            Assert.Multiple(() =>
            {
                Assert.That(response["result"]["isError"].GetValue<bool>(), Is.True);
                Assert.That(refusal["code"].GetValue<int>(), Is.EqualTo(6));
                Assert.That(refusal["executionId"].GetValue<string>(), Is.EqualTo("doesnotexist"));
                Assert.That(refusal["reason"].GetValue<string>(), Does.Contain("doesnotexist"));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Find_The_Record_Just_Written_By_Search()
        {
            string marker = Guid.NewGuid().ToString("N");
            JsonNode executed = ExecuteWithName(HelloWorld(marker), marker);
            string executionId = executed["executionId"].GetValue<string>();
            JsonObject arguments = new JsonObject();
            arguments["name"] = marker;
            arguments["limit"] = 5;
            JsonNode answer = DocumentOf(CallTool("search", arguments));
            List<string> found = answer["records"].AsArray().Select(record => record["executionId"].GetValue<string>()).ToList();
            Assert.Multiple(() =>
            {
                Assert.That(found, Does.Contain(executionId));
                Assert.That(answer["stale"].GetValue<bool>(), Is.False);
            });
        }

        [Test]
        public void Test_That_We_Can_Refuse_A_Package_Request_By_Name()
        {
            JsonObject package = new JsonObject();
            package["id"] = "Newtonsoft.Json";
            package["version"] = "13.0.3";
            JsonArray packages = new JsonArray();
            packages.Add(package);
            JsonObject arguments = new JsonObject();
            arguments["source"] = HelloWorld(Guid.NewGuid().ToString("N"));
            arguments["packages"] = packages;
            JsonNode response = CallTool("execute", arguments);
            JsonNode refusal = DocumentOf(response);
            Assert.Multiple(() =>
            {
                Assert.That(response["result"]["isError"].GetValue<bool>(), Is.True);
                Assert.That(refusal["field"].GetValue<string>(), Is.EqualTo("packages"));
                Assert.That(refusal["reason"].GetValue<string>(), Does.Contain("package support is not available"));
                Assert.That(refusal["reason"].GetValue<string>(), Does.Contain("Newtonsoft.Json"));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Refuse_A_Missing_Reference_By_Name()
        {
            string missing = Path.Combine(TestContext.CurrentContext.WorkDirectory, Guid.NewGuid().ToString("N"), "missing.dll");
            JsonArray references = new JsonArray();
            references.Add(missing);
            JsonObject arguments = new JsonObject();
            arguments["source"] = HelloWorld(Guid.NewGuid().ToString("N"));
            arguments["references"] = references;
            JsonNode response = CallTool("execute", arguments);
            JsonNode refusal = DocumentOf(response);
            Assert.Multiple(() =>
            {
                Assert.That(response["result"]["isError"].GetValue<bool>(), Is.True);
                Assert.That(refusal["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(refusal["field"].GetValue<string>(), Is.EqualTo("references"));
                Assert.That(refusal["path"].GetValue<string>(), Is.EqualTo(missing));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Execute_A_Submission_That_Uses_An_Extra_Reference()
        {
            string reference = typeof(Coop.Library.Common.Sha).Assembly.Location;
            JsonArray references = new JsonArray();
            references.Add(reference);
            JsonObject arguments = new JsonObject();
            arguments["source"] = "public static class Program { public static int Main(string[] args) { System.Console.WriteLine(Coop.Library.Common.Sha.HexOfUtf8(\"abc\")); return 0; } }";
            arguments["references"] = references;
            JsonNode document = DocumentOf(CallTool("execute", arguments));
            Assert.Multiple(() =>
            {
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(document["run"]["standardOutput"].GetValue<string>().Trim(), Is.EqualTo(Coop.Library.Common.Sha.HexOfUtf8("abc")));
            });
        }

        [Test]
        public void Test_That_We_Can_Key_A_Reference_Together_With_The_Dependency_Beside_It()
        {
            string reference = typeof(Coop.Library.Execution.SubmissionCompiler).Assembly.Location;
            string dependency = typeof(Coop.Library.Common.Sha).Assembly.Location;
            JsonObject supplied = new JsonObject();
            supplied["source"] = ShaProgram();
            supplied["references"] = References(reference);
            JsonNode first = DocumentOf(CallTool("execute", supplied));
            JsonObject listed = new JsonObject();
            listed["source"] = ShaProgram();
            listed["references"] = References(reference, dependency);
            JsonNode second = DocumentOf(CallTool("execute", listed));
            Assert.Multiple(() =>
            {
                Assert.That(first["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(first["run"]["standardOutput"].GetValue<string>().Trim(), Is.EqualTo(Coop.Library.Common.Sha.HexOfUtf8("abc")));
                Assert.That(second["cache"]["artefactKey"].GetValue<string>(), Is.EqualTo(first["cache"]["artefactKey"].GetValue<string>()));
                Assert.That(second["cache"]["artefactReused"].GetValue<bool>(), Is.True);
            });
        }

        [Test]
        public void Test_That_We_Can_Key_Two_References_In_Either_Order()
        {
            string reference = typeof(Coop.Library.Execution.SubmissionCompiler).Assembly.Location;
            string dependency = typeof(Coop.Library.Common.Sha).Assembly.Location;
            JsonObject forward = new JsonObject();
            forward["source"] = ShaProgram();
            forward["references"] = References(reference, dependency);
            JsonNode first = DocumentOf(CallTool("execute", forward));
            JsonObject backward = new JsonObject();
            backward["source"] = ShaProgram();
            backward["references"] = References(dependency, reference);
            JsonNode second = DocumentOf(CallTool("execute", backward));
            Assert.Multiple(() =>
            {
                Assert.That(first["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(second["cache"]["artefactKey"].GetValue<string>(), Is.EqualTo(first["cache"]["artefactKey"].GetValue<string>()));
                Assert.That(second["cache"]["artefactReused"].GetValue<bool>(), Is.True);
            });
        }

        [Test]
        public void Test_That_We_Can_Refuse_A_Request_Without_Source()
        {
            JsonNode response = CallTool("execute", new JsonObject());
            JsonNode refusal = DocumentOf(response);
            Assert.Multiple(() =>
            {
                Assert.That(response["result"]["isError"].GetValue<bool>(), Is.True);
                Assert.That(refusal["field"].GetValue<string>(), Is.EqualTo("source"));
                Assert.That(refusal["reason"].GetValue<string>(), Does.Contain("required by the execution template"));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Report_A_Deadline_Expiry_As_A_Failed_Outcome()
        {
            JsonObject arguments = new JsonObject();
            arguments["source"] = SleepingProgram();
            arguments["timeoutSeconds"] = 1;
            Stopwatch stopwatch = Stopwatch.StartNew();
            JsonNode document = DocumentOf(CallTool("execute", arguments));
            stopwatch.Stop();
            Assert.Multiple(() =>
            {
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("failed"));
                Assert.That(document["run"]["deadlineExpired"].GetValue<bool>(), Is.True);
                Assert.That(document["run"]["exitStatus"], Is.Null);
                Assert.That(document["run"]["elapsedMilliseconds"].GetValue<long>(), Is.LessThan(15000));
                Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(30)));
            });
            AssertServing();
        }

        [Test]
        public void Test_That_We_Can_Report_The_Serving_Version_On_Every_Response()
        {
            JsonNode completed = Execute(HelloWorld(Guid.NewGuid().ToString("N")), null);
            JsonNode failed = Execute("public static class Program { public static int Main(string[] args) { int value = ; return 0; } }", null);
            JsonNode refusal = DocumentOf(CallTool("execute", new JsonObject()));
            string expected = SurfaceConfiguration.VersionOf();
            Assert.Multiple(() =>
            {
                Assert.That(completed["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(completed["runnerVersion"].GetValue<string>(), Is.EqualTo(expected));
                Assert.That(failed["status"].GetValue<string>(), Is.EqualTo("failed"));
                Assert.That(failed["runnerVersion"].GetValue<string>(), Is.EqualTo(expected));
                Assert.That(refusal["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(refusal["runnerVersion"].GetValue<string>(), Is.EqualTo(expected));
            });
        }

        [Test]
        public void Test_That_A_Missing_Type_Names_The_Sibling_Absent_Beside_The_Reference()
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "surface-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string reference = Path.Combine(folder, "Coop.Library.Execution.dll");
            File.Copy(typeof(Coop.Library.Execution.SubmissionCompiler).Assembly.Location, reference);
            JsonObject arguments = new JsonObject();
            arguments["source"] = ShaProgram();
            arguments["references"] = References(reference);
            JsonNode document = DocumentOf(CallTool("execute", arguments));
            JsonNode missing = document["compile"]["missingSiblings"];
            Assert.Multiple(() =>
            {
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.False);
                Assert.That(missing.AsArray().Select(entry => entry["name"].GetValue<string>()), Does.Contain("Coop.Library.Common"));
                Assert.That(missing.AsArray().Single(entry => entry["name"].GetValue<string>() == "Coop.Library.Common")["folder"].GetValue<string>(), Is.EqualTo(folder));
                Assert.That(document["compile"]["missingSiblingsDetail"].GetValue<string>(), Does.Contain("whole closure"));
            });
        }

        [Test]
        public void Test_That_A_Compile_Failure_Without_A_Missing_Type_Names_No_Sibling()
        {
            JsonNode document = Execute("public static class Program { public static int Main(string[] args) { int value = ; return 0; } }", null);
            Assert.Multiple(() =>
            {
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.False);
                Assert.That(document["compile"]["missingSiblings"].AsArray().Count, Is.EqualTo(0));
                Assert.That(document["compile"]["missingSiblingsDetail"], Is.Null);
            });
        }

        [Test]
        public void Test_That_A_Missing_Type_Names_A_Sibling_Present_That_The_Reference_Does_Not_Declare()
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "surface-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string reference = Path.Combine(folder, "Coop.Library.Execution.dll");
            File.Copy(typeof(Coop.Library.Execution.SubmissionCompiler).Assembly.Location, reference);
            File.Copy(typeof(Coop.Library.Common.Sha).Assembly.Location, Path.Combine(folder, "An.Unrelated.Assembly.dll"));
            JsonObject arguments = new JsonObject();
            arguments["source"] = ShaProgram();
            arguments["references"] = References(reference);
            JsonNode document = DocumentOf(CallTool("execute", arguments));
            JsonNode undeclared = document["compile"]["undeclaredSiblings"];
            Assert.Multiple(() =>
            {
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.False);
                Assert.That(undeclared.AsArray().Select(entry => entry["name"].GetValue<string>()), Does.Contain("An.Unrelated.Assembly"));
                Assert.That(undeclared.AsArray().Single(entry => entry["name"].GetValue<string>() == "An.Unrelated.Assembly")["folder"].GetValue<string>(), Is.EqualTo(folder));
                Assert.That(document["compile"]["undeclaredSiblingsDetail"].GetValue<string>(), Does.Contain("does not declare them"));
            });
        }

        [Test]
        public void Test_That_A_Successful_Compile_With_A_Reference_Reports_No_Undeclared_Sibling()
        {
            string folder = Path.Combine(TestContext.CurrentContext.WorkDirectory, "surface-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string reference = Path.Combine(folder, "Coop.Library.Execution.dll");
            File.Copy(typeof(Coop.Library.Execution.SubmissionCompiler).Assembly.Location, reference);
            string dependency = Path.Combine(folder, "Coop.Library.Common.dll");
            File.Copy(typeof(Coop.Library.Common.Sha).Assembly.Location, dependency);
            JsonObject arguments = new JsonObject();
            arguments["source"] = ShaProgram();
            arguments["references"] = References(reference, dependency);
            JsonNode document = DocumentOf(CallTool("execute", arguments));
            Assert.Multiple(() =>
            {
                Assert.That(document["compile"]["success"].GetValue<bool>(), Is.True);
                Assert.That(document["compile"]["missingSiblings"].AsArray().Count, Is.EqualTo(0));
                Assert.That(document["compile"]["undeclaredSiblings"].AsArray().Count, Is.EqualTo(0));
                Assert.That(document["compile"]["undeclaredSiblingsDetail"], Is.Null);
            });
        }

        [Test]
        public void Test_That_We_Can_Return_The_Template_Document()
        {
            JsonNode response = CallTool("template", new JsonObject());
            string text = response["result"]["content"][0]["text"].GetValue<string>();
            JsonNode template = JsonNode.Parse(text);
            Assert.Multiple(() =>
            {
                Assert.That(template["templateVersion"].GetValue<int>(), Is.EqualTo(1));
                Assert.That(template["toolVersion"].GetValue<string>(), Is.Not.Empty);
                Assert.That(template["language"]["targetFramework"].GetValue<string>(), Is.EqualTo("net10.0"));
                Assert.That(template["request"]["fields"].AsArray().Count, Is.GreaterThan(0));
                Assert.That(template["timeout"]["ceilingSeconds"].GetValue<int>(), Is.EqualTo(600));
                Assert.That(template["packages"], Is.Not.Null);
            });
        }

        private static string HelloWorld(string marker)
        {
            return "public static class Program { public static int Main(string[] args) { System.Console.WriteLine(\"hello " + marker + "\"); return 0; } }";
        }

        private static string SleepingProgram()
        {
            return "public static class Program { public static int Main(string[] args) { System.Threading.Thread.Sleep(20000); return 0; } }";
        }

        private static string ShaProgram()
        {
            return "public static class Program { public static int Main(string[] args) { System.Console.WriteLine(Coop.Library.Common.Sha.HexOfUtf8(\"abc\")); return 0; } }";
        }

        private static JsonArray References(params string[] paths)
        {
            JsonArray references = new JsonArray();
            paths.ToList().ForEach(path => references.Add(path));
            return references;
        }

        private JsonNode Execute(string source, int? timeoutSeconds)
        {
            JsonObject arguments = new JsonObject();
            arguments["source"] = source;
            if (timeoutSeconds != null)
            {
                arguments["timeoutSeconds"] = timeoutSeconds.Value;
            }
            return DocumentOf(CallTool("execute", arguments));
        }

        private JsonNode ExecuteWithName(string source, string name)
        {
            JsonObject arguments = new JsonObject();
            arguments["source"] = source;
            arguments["name"] = name;
            return DocumentOf(CallTool("execute", arguments));
        }

        private JsonNode CallTool(string name, JsonObject arguments)
        {
            JsonObject parameters = new JsonObject();
            parameters["name"] = name;
            parameters["arguments"] = arguments;
            return client.Call("tools/call", parameters);
        }

        private JsonNode DocumentOf(JsonNode response)
        {
            JsonNode content = response["result"]["content"];
            Assert.That(content, Is.Not.Null, "the tool answered without content: " + response.ToJsonString());
            string text = content[0]["text"].GetValue<string>();
            return JsonNode.Parse(text);
        }

        private void AssertServing()
        {
            JsonNode document = Execute(HelloWorld(Guid.NewGuid().ToString("N")), null);
            Assert.That(document["status"].GetValue<string>(), Is.EqualTo("completed"), "the server stopped serving after a refusal");
        }
    }
}
