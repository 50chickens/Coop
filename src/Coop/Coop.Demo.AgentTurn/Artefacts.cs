using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Coop.Demo.AgentTurn
{
    public static class Artefacts
    {
        public static void Write(string outputRoot, string workingRoot, Recorder recorder, ScriptedModel model, ChatResponse response)
        {
            ExecutionFacts facts = ExecutionFacts.Read(model.ToolResult);

            File.WriteAllText(Path.Combine(outputRoot, "submitted.cs"), model.SubmittedSource);
            string compiledAssembly = CopyCompiledAssembly(outputRoot, workingRoot);

            File.WriteAllText(Path.Combine(outputRoot, "transcript.md"), Transcript(recorder, model, response, facts, compiledAssembly));
            File.WriteAllText(Path.Combine(outputRoot, "trace.json"), Trace(recorder, facts));
            File.WriteAllText(Path.Combine(outputRoot, "execution.log"), ExecutionLog(recorder, facts, compiledAssembly));
            File.WriteAllText(Path.Combine(outputRoot, "timeline.svg"), Timeline(recorder));
            File.WriteAllText(Path.Combine(outputRoot, "index.html"), Viewer(recorder, model, response, facts, compiledAssembly));
        }

        private static string CopyCompiledAssembly(string outputRoot, string workingRoot)
        {
            string[] directories = Directory.GetDirectories(workingRoot);
            if (directories.Length != 1)
            {
                return "";
            }

            string assembly = Path.Combine(directories[0], "submission.dll");
            if (File.Exists(assembly) == false)
            {
                return "";
            }

            string destination = Path.Combine(outputRoot, "submission.dll");
            File.Copy(assembly, destination, true);
            return destination;
        }

        private static string Transcript(Recorder recorder, ScriptedModel model, ChatResponse response, ExecutionFacts facts, string compiledAssembly)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("# Agent turn transcript");
            builder.AppendLine();
            builder.AppendLine("A scripted model stands in for the provider. Everything below the model is real: the framework's tool loop, the Coop tool, Roslyn compilation and the child process.");
            builder.AppendLine();
            builder.AppendLine("## The C# that ran");
            builder.AppendLine();
            builder.AppendLine("This is the exact text the model submitted, byte for byte, which is also what the artefact key hashed.");
            builder.AppendLine();
            builder.AppendLine("```csharp");
            builder.AppendLine(model.SubmittedSource.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
            if (compiledAssembly.Length > 0)
            {
                builder.AppendLine($"Compiled to `{Path.GetFileName(compiledAssembly)}`, {new FileInfo(compiledAssembly).Length} bytes, copied into this folder.");
                builder.AppendLine();
            }
            builder.AppendLine("## Conversation");
            builder.AppendLine();
            builder.AppendLine("| # | Step | Detail |");
            builder.AppendLine("|---|---|---|");
            recorder.Steps.ToList().ForEach(step => builder.AppendLine($"| {step.OffsetMilliseconds} ms | {step.Kind} - {step.Title} | {step.Detail} |"));
            builder.AppendLine();
            builder.AppendLine("## The answer");
            builder.AppendLine();
            builder.AppendLine("```");
            builder.AppendLine(response.Text);
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine("## The execution behind it");
            builder.AppendLine();
            builder.AppendLine("| Field | Value |");
            builder.AppendLine("|---|---|");
            builder.AppendLine($"| executionId | `{facts.ExecutionId}` |");
            builder.AppendLine($"| status | {facts.Status} |");
            builder.AppendLine($"| compile success | {facts.CompileSuccess} |");
            builder.AppendLine($"| diagnostics | {facts.DiagnosticCount} |");
            builder.AppendLine($"| exit status | {facts.ExitStatus} |");
            builder.AppendLine($"| elapsed | {facts.ElapsedMilliseconds} ms |");
            builder.AppendLine($"| standard output | `{facts.StandardOutput.Trim()}` |");
            builder.AppendLine();
            builder.AppendLine("## The tool result the model received");
            builder.AppendLine();
            builder.AppendLine("```json");
            builder.AppendLine(model.ToolResult);
            builder.AppendLine("```");
            return builder.ToString();
        }

        private static string Trace(Recorder recorder, ExecutionFacts facts)
        {
            List<object> events = new List<object>();
            recorder.Steps.ToList().ForEach(step => events.Add(new
            {
                offsetMilliseconds = step.OffsetMilliseconds,
                layer = step.Kind,
                name = step.Title,
                detail = step.Detail
            }));
            events.Add(new
            {
                offsetMilliseconds = recorder.Steps.Last().OffsetMilliseconds,
                layer = "execution",
                name = "execution " + facts.ExecutionId,
                detail = $"status={facts.Status} exit={facts.ExitStatus} elapsedMs={facts.ElapsedMilliseconds}"
            });

            return JsonSerializer.Serialize(new { events }, new JsonSerializerOptions { WriteIndented = true });
        }

        private static string ExecutionLog(Recorder recorder, ExecutionFacts facts, string compiledAssembly)
        {
            StringBuilder builder = new StringBuilder();
            recorder.Steps.ToList().ForEach(step => builder.AppendLine($"[{step.OffsetMilliseconds,5} ms] [{step.Kind,-8}] {step.Title}: {step.Detail}"));
            builder.AppendLine($"[execution] executionId   {facts.ExecutionId}");
            builder.AppendLine($"[execution] status        {facts.Status}");
            builder.AppendLine($"[execution] compile       success={facts.CompileSuccess} diagnostics={facts.DiagnosticCount}");
            builder.AppendLine($"[execution] run           exit={facts.ExitStatus} elapsedMs={facts.ElapsedMilliseconds} deadlineExpired={facts.DeadlineExpired}");
            builder.AppendLine($"[execution] stdout        {facts.StandardOutput.Trim()}");
            builder.AppendLine($"[execution] source        submitted.cs");
            if (compiledAssembly.Length > 0)
            {
                builder.AppendLine($"[execution] assembly      {Path.GetFileName(compiledAssembly)} {new FileInfo(compiledAssembly).Length} bytes");
            }
            return builder.ToString();
        }

        private static string Timeline(Recorder recorder)
        {
            IReadOnlyList<RecordedStep> steps = recorder.Steps;
            int width = 900;
            int rowHeight = 42;
            int height = 50 + (steps.Count * rowHeight);
            long last = steps.Count == 0 ? 1 : Math.Max(1, steps.Last().OffsetMilliseconds);

            string colours = "fill=\"#2563eb\"";
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\">");
            builder.AppendLine("<rect width=\"100%\" height=\"100%\" fill=\"#0f172a\"/>");
            builder.AppendLine("<text x=\"20\" y=\"30\" fill=\"#e2e8f0\" font-family=\"Segoe UI, sans-serif\" font-size=\"16\">Coop agent turn - one scripted conversation, one real execution</text>");

            int index = 0;
            foreach (RecordedStep step in steps)
            {
                int y = 50 + (index * rowHeight);
                double fraction = (double)step.OffsetMilliseconds / last;
                int barWidth = 30 + (int)(fraction * 700);
                builder.AppendLine($"<text x=\"20\" y=\"{y + 14}\" fill=\"#94a3b8\" font-family=\"Consolas, monospace\" font-size=\"12\">{step.Kind}</text>");
                builder.AppendLine($"<rect x=\"110\" y=\"{y}\" width=\"{barWidth}\" height=\"22\" rx=\"4\" {colours}/>");
                builder.AppendLine($"<text x=\"{110 + barWidth + 8}\" y=\"{y + 15}\" fill=\"#e2e8f0\" font-family=\"Segoe UI, sans-serif\" font-size=\"12\">{Escape(step.Title)} - {step.OffsetMilliseconds} ms</text>");
                index++;
            }

            builder.AppendLine("</svg>");
            return builder.ToString();
        }

        private static string Viewer(Recorder recorder, ScriptedModel model, ChatResponse response, ExecutionFacts facts, string compiledAssembly)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("<!DOCTYPE html>");
            builder.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"/><title>Coop agent turn</title>");
            builder.AppendLine("<style>body{background:#0f172a;color:#e2e8f0;font-family:Segoe UI,sans-serif;margin:24px}code,pre{font-family:Consolas,monospace;font-size:13px}pre{background:#111c33;padding:12px;border-radius:6px;overflow:auto}table{border-collapse:collapse;margin:12px 0}td,th{border:1px solid #1e293b;padding:6px 10px;text-align:left;font-size:13px}</style>");
            builder.AppendLine("</head><body>");
            builder.AppendLine("<h1>Coop agent turn</h1>");
            builder.AppendLine($"<p>Scripted model, real tool loop, real compilation, real child process. {recorder.Steps.Count} recorded steps.</p>");
            builder.AppendLine(Timeline(recorder));
            builder.AppendLine("<h2>The C# that ran</h2>");
            builder.AppendLine($"<pre>{Escape(model.SubmittedSource)}</pre>");
            if (compiledAssembly.Length > 0)
            {
                builder.AppendLine($"<p>Compiled to <code>{Escape(Path.GetFileName(compiledAssembly))}</code>, {new FileInfo(compiledAssembly).Length} bytes.</p>");
            }
            builder.AppendLine("<h2>Answer</h2>");
            builder.AppendLine($"<pre>{Escape(response.Text)}</pre>");
            builder.AppendLine("<h2>Execution</h2><table>");
            builder.AppendLine($"<tr><th>executionId</th><td><code>{Escape(facts.ExecutionId)}</code></td></tr>");
            builder.AppendLine($"<tr><th>status</th><td>{Escape(facts.Status)}</td></tr>");
            builder.AppendLine($"<tr><th>compile</th><td>success={facts.CompileSuccess} diagnostics={facts.DiagnosticCount}</td></tr>");
            builder.AppendLine($"<tr><th>run</th><td>exit={facts.ExitStatus} elapsed={facts.ElapsedMilliseconds} ms</td></tr>");
            builder.AppendLine($"<tr><th>stdout</th><td><code>{Escape(facts.StandardOutput.Trim())}</code></td></tr>");
            builder.AppendLine("</table>");
            builder.AppendLine("<h2>Tool result the model received</h2>");
            builder.AppendLine($"<pre>{Escape(model.ToolResult)}</pre>");
            builder.AppendLine("</body></html>");
            return builder.ToString();
        }

        private static string Escape(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        }
    }
}
