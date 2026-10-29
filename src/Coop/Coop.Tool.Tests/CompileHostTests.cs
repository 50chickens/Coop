using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Coop.Services.Surface;

namespace Coop.Tool.Tests
{
    [TestFixture]
    public sealed class CompileHostTests
    {
        [Test]
        public void Test_That_A_Frame_Round_Trips_A_Request_And_A_Response()
        {
            string pipeName = NewPipeName();
            Task server = Task.Run(() => AnswerOnce(pipeName));
            WaitForPipe(pipeName);
            string response = CompileClient.Request(pipeName, "request");
            server.Wait();
            Assert.That(response, Is.EqualTo("request-response"));
        }

        [Test]
        public void Test_That_A_Second_Request_Is_Answered_After_The_First()
        {
            string pipeName = NewPipeName();
            CompileHost host = new CompileHost(Configuration(), pipeName);
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = CompileHost.Open(pipeName))
                {
                    host.ServeOnce(stream);
                    host.ServeOnce(stream);
                }
            });
            WaitForPipe(pipeName);
            string first = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
            string second = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
            server.Wait();
            Assert.Multiple(() =>
            {
                Assert.That(JsonNode.Parse(first)["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(JsonNode.Parse(second)["status"].GetValue<string>(), Is.EqualTo("completed"));
            });
        }

        [Test]
        public void Test_That_A_Bad_Argument_Is_Answered_With_A_Refusal_Envelope()
        {
            string pipeName = NewPipeName();
            CompileHost host = new CompileHost(Configuration(), pipeName);
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = CompileHost.Open(pipeName))
                {
                    host.ServeOnce(stream);
                    host.ServeOnce(stream);
                }
            });
            WaitForPipe(pipeName);
            JsonNode refused = JsonNode.Parse(CompileClient.Request(pipeName, "{}"));
            string next = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
            server.Wait();
            Assert.Multiple(() =>
            {
                Assert.That(refused["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(refused["field"].GetValue<string>(), Is.EqualTo("source"));
                Assert.That(JsonNode.Parse(next)["status"].GetValue<string>(), Is.EqualTo("completed"));
            });
        }

        [Test]
        public void Test_That_A_Host_That_Does_Not_Answer_Produces_A_Refusal_Envelope()
        {
            string pipeName = NewPipeName();
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1))
                {
                    stream.WaitForConnection();
                    Frames.Read(stream);
                }
            });
            WaitForPipe(pipeName);
            string text = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
            server.Wait();
            JsonNode document = JsonNode.Parse(text);
            Assert.Multiple(() =>
            {
                Assert.That(document["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(document["reason"].GetValue<string>(), Does.Contain(pipeName));
                Assert.That(Outcome.ExitCodeOfText(text), Is.EqualTo(Program.NotCompletedExitCode));
            });
        }

        [Test]
        public void Test_That_We_Can_Complete_Two_Concurrent_Requests()
        {
            string pipeName = NewPipeName();
            string folder = NewFolder();
            string startedPath = Path.Combine(folder, "started.txt");
            Process host = StartHost(folder, pipeName);
            try
            {
                Task<string> slow = Task.Run(() => CompileClient.Request(pipeName, RequestOf(Signalled(startedPath, 5000))));
                AwaitFile(startedPath);
                Stopwatch clock = Stopwatch.StartNew();
                string quick = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
                long quickMilliseconds = (long)clock.Elapsed.TotalMilliseconds;
                string answer = slow.Result;
                Assert.Multiple(() =>
                {
                    Assert.That(JsonNode.Parse(quick)["status"].GetValue<string>(), Is.EqualTo("completed"));
                    Assert.That(JsonNode.Parse(answer)["status"].GetValue<string>(), Is.EqualTo("completed"));
                    Assert.That(quick, Does.Not.Contain("no host answered"));
                    Assert.That(quickMilliseconds, Is.GreaterThan(1000));
                });
            }
            finally
            {
                StopHost(host);
            }
        }

        [Test]
        public void Test_That_We_Can_Run_One_Submission_At_A_Time_Under_Concurrency()
        {
            string pipeName = NewPipeName();
            string folder = NewFolder();
            string tracePath = Path.Combine(folder, "trace.txt");
            Process host = StartHost(folder, pipeName);
            try
            {
                Task<string> first = Task.Run(() => CompileClient.Request(pipeName, RequestOf(Traced(tracePath, "one"))));
                Task<string> second = Task.Run(() => CompileClient.Request(pipeName, RequestOf(Traced(tracePath, "two"))));
                Task.WaitAll(first, second);
                Assert.Multiple(() =>
                {
                    Assert.That(JsonNode.Parse(first.Result)["status"].GetValue<string>(), Is.EqualTo("completed"));
                    Assert.That(JsonNode.Parse(second.Result)["status"].GetValue<string>(), Is.EqualTo("completed"));
                    Assert.That(File.ReadAllLines(tracePath), Is.EqualTo(new string[] { "start", "end", "start", "end" }));
                });
            }
            finally
            {
                StopHost(host);
            }
        }

        [Test]
        public void Test_That_Every_Response_Carries_The_Serving_Version_At_The_Top_Level()
        {
            string pipeName = NewPipeName();
            CompileHost host = new CompileHost(Configuration(), pipeName);
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = CompileHost.Open(pipeName))
                {
                    host.ServeOnce(stream);
                    host.ServeOnce(stream);
                    host.ServeOnce(stream);
                }
            });
            WaitForPipe(pipeName);
            string completed = CompileClient.Request(pipeName, RequestOf(Printed(NewMarker())));
            string failed = CompileClient.Request(pipeName, RequestOf("this is not a program"));
            string refused = CompileClient.Request(pipeName, "{}");
            server.Wait();
            Assert.Multiple(() =>
            {
                Assert.That(JsonNode.Parse(completed)["status"].GetValue<string>(), Is.EqualTo("completed"));
                Assert.That(JsonNode.Parse(completed)["runnerVersion"].GetValue<string>(), Is.EqualTo("test"));
                Assert.That(JsonNode.Parse(failed)["status"].GetValue<string>(), Is.EqualTo("failed"));
                Assert.That(JsonNode.Parse(failed)["reused"], Is.Null);
                Assert.That(JsonNode.Parse(failed)["runnerVersion"].GetValue<string>(), Is.EqualTo("test"));
                Assert.That(JsonNode.Parse(refused)["status"].GetValue<string>(), Is.EqualTo("refused"));
                Assert.That(JsonNode.Parse(refused)["runnerVersion"].GetValue<string>(), Is.Not.Empty);
            });
        }

        [Test]
        public void Test_That_The_Probe_Matches_A_Host_That_Serves_The_Same_Version()
        {
            string pipeName = NewPipeName();
            CompileHost host = new CompileHost(Configuration(), pipeName);
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = CompileHost.Open(pipeName))
                {
                    host.ServeOnce(stream);
                }
            });
            WaitForPipe(pipeName);
            int state = HostProbe.Run(pipeName, SurfaceConfiguration.VersionOf());
            server.Wait();
            Assert.That(state, Is.EqualTo(HostProbe.Matching));
        }

        [Test]
        public void Test_That_The_Probe_Rejects_A_Host_That_Serves_Another_Version()
        {
            string pipeName = NewPipeName();
            CompileHost host = new CompileHost(Configuration(), pipeName);
            Task server = Task.Run(() =>
            {
                using (NamedPipeServerStream stream = CompileHost.Open(pipeName))
                {
                    host.ServeOnce(stream);
                }
            });
            WaitForPipe(pipeName);
            int state = HostProbe.Run(pipeName, "1.0.4");
            server.Wait();
            Assert.That(state, Is.EqualTo(HostProbe.Stale));
        }

        [Test]
        public void Test_That_The_Probe_Reports_No_Answer_When_No_Host_Holds_The_Pipe()
        {
            Assert.That(HostProbe.Run(NewPipeName(), SurfaceConfiguration.VersionOf()), Is.EqualTo(HostProbe.Absent));
        }

        [Test]
        public void Test_That_A_Host_Stops_When_It_Is_Asked_To()
        {
            string pipeName = NewPipeName();
            string folder = NewFolder();
            Process host = StartHost(folder, pipeName);
            bool stopped = HostStop.Send(pipeName);
            bool exited = host.WaitForExit(30000);
            if (exited == false)
            {
                StopHost(host);
            }
            host.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(stopped, Is.True);
                Assert.That(exited, Is.True);
            });
        }

        private static Process StartHost(string dataRoot, string pipeName)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
            startInfo.ArgumentList.Add(typeof(CompileHost).Assembly.Location);
            startInfo.ArgumentList.Add(Program.HostCommand);
            startInfo.UseShellExecute = false;
            startInfo.Environment[CompileHost.PipeNameVariable] = pipeName;
            startInfo.Environment[SurfaceConfiguration.DataRootVariable] = dataRoot;
            Process process = Process.Start(startInfo);
            WaitForPipe(pipeName);
            return process;
        }

        private static void StopHost(Process host)
        {
            host.Kill(true);
            host.WaitForExit();
            host.Dispose();
        }

        private static void AwaitFile(string path)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(60);
            while (File.Exists(path) == false)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail("the submission did not begin within 60 seconds");
                }
                Thread.Sleep(20);
            }
        }

        private static string Signalled(string path, int milliseconds)
        {
            return "System.IO.File.WriteAllText(@\"" + path + "\", \"started\");\nSystem.Threading.Thread.Sleep(" + milliseconds + ");\nSystem.Console.WriteLine(\"slow\");";
        }

        private static string Traced(string path, string marker)
        {
            string start = "System.IO.File.AppendAllText(@\"" + path + "\", \"start\" + System.Environment.NewLine);";
            string end = "System.IO.File.AppendAllText(@\"" + path + "\", \"end\" + System.Environment.NewLine);";
            return "System.Console.WriteLine(\"" + marker + "\");\n" + start + "\nSystem.Threading.Thread.Sleep(1000);\n" + end;
        }

        private static string NewFolder()
        {
            string path = Path.Combine(TestContext.CurrentContext.WorkDirectory, "host-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void AnswerOnce(string pipeName)
        {
            using (NamedPipeServerStream stream = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1))
            {
                stream.WaitForConnection();
                Frames.Write(stream, Frames.Read(stream) + "-response");
            }
        }

        private static void WaitForPipe(string pipeName)
        {
            string path = @"\\.\pipe\" + pipeName;
            DateTime deadline = DateTime.UtcNow.AddSeconds(60);
            while (Directory.GetFiles(@"\\.\pipe\").Contains(path) == false)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Assert.Fail("the host did not open " + path);
                }
                Thread.Sleep(20);
            }
        }

        private static string NewPipeName()
        {
            return "coop-test-" + Guid.NewGuid().ToString("N");
        }

        private static string NewMarker()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static string Printed(string marker)
        {
            return "System.Console.WriteLine(\"" + marker + "\");";
        }

        private static string RequestOf(string source)
        {
            JsonObject arguments = new JsonObject();
            arguments["source"] = source;
            return arguments.ToJsonString();
        }

        private static SurfaceConfiguration Configuration()
        {
            string dataRoot = Path.Combine(TestContext.CurrentContext.WorkDirectory, "host-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataRoot);
            SurfaceConfiguration configuration = new SurfaceConfiguration();
            configuration.Version = "test";
            configuration.DataRoot = dataRoot;
            configuration.ArtefactStoreRoot = dataRoot;
            configuration.RecordStoreRoot = Path.Combine(dataRoot, "records");
            configuration.LockPath = Path.Combine(dataRoot, "coop.lock");
            return configuration;
        }
    }
}
