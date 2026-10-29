using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Coop.Services.Surface.Tests
{
    public sealed class SurfaceClient : IDisposable
    {
        public const string ServerPathKey = "CoopSurfaceServerPath";

        private readonly Process process;

        private readonly BlockingCollection<string> answers = new BlockingCollection<string>();

        private readonly ConcurrentQueue<string> errors = new ConcurrentQueue<string>();

        private int nextId;

        public SurfaceClient(string dataRoot)
        {
            DataRoot = dataRoot;
            ProcessStartInfo startInfo = new ProcessStartInfo("dotnet");
            startInfo.ArgumentList.Add(ServerPath());
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.Environment["COOP_DATA_ROOT"] = dataRoot;
            process = Process.Start(startInfo);
            StartAnswers();
            StartErrors();
            AwaitReady();
        }

        public string DataRoot { get; private set; }

        public static string ServerPath()
        {
            AssemblyMetadataAttribute metadata = typeof(SurfaceClient)
                .Assembly
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attribute => attribute.Key == ServerPathKey);
            if (metadata == null)
            {
                throw new InvalidOperationException("the test assembly carries no " + ServerPathKey + " metadata");
            }
            return metadata.Value;
        }

        public JsonNode Initialize()
        {
            JsonObject parameters = new JsonObject();
            parameters["protocolVersion"] = "2025-06-18";
            parameters["capabilities"] = new JsonObject();
            JsonObject clientInfo = new JsonObject();
            clientInfo["name"] = "coop-surface-tests";
            clientInfo["version"] = "1.0.0";
            parameters["clientInfo"] = clientInfo;
            JsonNode response = Call("initialize", parameters);
            Notify("notifications/initialized");
            return response;
        }

        public JsonNode Call(string method, JsonObject parameters)
        {
            int id = Interlocked.Increment(ref nextId);
            JsonObject request = new JsonObject();
            request["jsonrpc"] = "2.0";
            request["id"] = id;
            request["method"] = method;
            request["params"] = parameters;
            process.StandardInput.WriteLine(request.ToJsonString());
            process.StandardInput.Flush();
            return Await(id);
        }

        public void Notify(string method)
        {
            JsonObject notification = new JsonObject();
            notification["jsonrpc"] = "2.0";
            notification["method"] = method;
            process.StandardInput.WriteLine(notification.ToJsonString());
            process.StandardInput.Flush();
        }

        public List<string> ErrorLines()
        {
            return errors.ToList();
        }

        public void Dispose()
        {
            if (process.HasExited == false)
            {
                process.Kill(true);
            }
            process.WaitForExit();
            process.Dispose();
            answers.Dispose();
        }

        private JsonNode Await(int id)
        {
            while (true)
            {
                string line;
                if (answers.TryTake(out line, TimeSpan.FromSeconds(180)) == false)
                {
                    throw new InvalidOperationException("the server did not answer request " + id);
                }
                JsonNode answer = JsonNode.Parse(line);
                if (answer == null)
                {
                    continue;
                }
                JsonNode answered = answer["id"];
                if (answered == null)
                {
                    continue;
                }
                if (answered.GetValue<int>() == id)
                {
                    return answer;
                }
            }
        }

        private void AwaitReady()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(60))
            {
                if (errors.Any(line => line == "ready"))
                {
                    return;
                }
                if (process.HasExited)
                {
                    throw new InvalidOperationException("the server exited during startup");
                }
                Thread.Sleep(20);
            }
            throw new InvalidOperationException("the server did not announce readiness");
        }

        private void StartAnswers()
        {
            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardOutput.ReadLine()) != null)
                {
                    answers.Add(line);
                }
                answers.CompleteAdding();
            });
        }

        private void StartErrors()
        {
            Task.Run(() =>
            {
                string line;
                while ((line = process.StandardError.ReadLine()) != null)
                {
                    errors.Enqueue(line);
                }
            });
        }
    }
}
