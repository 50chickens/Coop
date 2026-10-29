using System;
using System.Collections.Generic;
using System.IO;
using Coop.Library.Execution;

namespace Coop.Library.Records.Tests
{
    internal static class TestRecords
    {
        internal static string NewRoot()
        {
            return Path.Combine(TestContext.CurrentContext.WorkDirectory, "records-" + Guid.NewGuid().ToString("N"));
        }

        internal static ExecutionRecord Record(string executionId, string name, string category, string timestamp, string status)
        {
            ExecutionRecord record = new ExecutionRecord();
            record.ExecutionId = executionId;
            record.Name = name;
            record.Description = "a submission that prints the result of " + name;
            record.Category = category;
            record.Timestamp = timestamp;
            record.Status = status;
            record.Submission = new Submission();
            record.Submission.SourceText = "class Program { static int Main(string[] args) { return 0; } }";
            record.Submission.Parameters = new Dictionary<string, string>();
            record.Submission.Parameters.Add("count", "3");
            record.Packages = new List<RecordPackage>();
            record.Packages.Add(new RecordPackage { Id = "Newtonsoft.Json", Version = "13.0.3" });
            return record;
        }

        internal static ExecutionRecord Completed(string executionId, string name, string category, string timestamp)
        {
            ExecutionRecord record = Record(executionId, name, category, timestamp, RecordStatuses.Completed);
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = executionId;
            outcome.Status = ExecutionStatuses.Completed;
            outcome.ExitStatus = 0;
            outcome.StandardOutput = "hello";
            outcome.StandardError = "";
            outcome.Diagnostics = new List<CompileDiagnostic>();
            record.Response = outcome;
            return record;
        }
    }
}
