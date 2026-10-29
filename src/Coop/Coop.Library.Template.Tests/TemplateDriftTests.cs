using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Coop.Library.Common;
using Coop.Library.Execution;

namespace Coop.Library.Template.Tests
{
    [TestFixture]
    public sealed class TemplateDriftTests
    {
        [Test]
        public void Test_That_We_Can_Match_The_Response_Shape_To_The_Outcome_Type()
        {
            ResponseContract response = CoopTemplate.Create().Response;
            Assert.That(Names(response.Fields), Is.EquivalentTo(NamesOf(typeof(ExecutionOutcome))));
            Assert.That(Names(response.Diagnostic.Fields), Is.EquivalentTo(NamesOf(typeof(CompileDiagnostic))));
            Assert.That(Names(response.Reuse.Fields), Is.EquivalentTo(NamesOf(typeof(ReuseMarker))));
            Assert.That(Names(response.Refusal.Fields), Is.EquivalentTo(NamesOf(typeof(ExecutionRefusal))));
        }

        [Test]
        public void Test_That_We_Can_Match_The_Response_Shape_To_The_Serialised_Outcome()
        {
            string json = JsonSerializer.Serialize(Outcome(), CoopJson.Options);
            List<string> written = JsonNode.Parse(json).AsObject().Select(property => property.Key).ToList();
            Assert.That(written, Has.Count.EqualTo(12));
            Assert.That(Names(CoopTemplate.Create().Response.Fields), Is.EquivalentTo(written));
        }

        private static string[] Names(List<ResponseField> fields)
        {
            return fields.Select(field => field.Name).ToArray();
        }

        private static string[] NamesOf(Type type)
        {
            return type.GetProperties().Select(property => JsonNamingPolicy.CamelCase.ConvertName(property.Name)).ToArray();
        }

        private static ExecutionOutcome Outcome()
        {
            ExecutionOutcome outcome = new ExecutionOutcome();
            outcome.ExecutionId = "execution";
            outcome.Status = ExecutionStatuses.Completed;
            outcome.ExitStatus = 0;
            outcome.StandardOutput = "";
            outcome.StandardError = "";
            outcome.DeadlineExpired = false;
            outcome.ElapsedMilliseconds = 1;
            outcome.ProcessorMilliseconds = 1;
            outcome.PeakWorkingSetBytes = 1;
            outcome.Diagnostics = new List<CompileDiagnostic>();
            outcome.Reused = new ReuseMarker();
            outcome.Refusal = new ExecutionRefusal();
            return outcome;
        }
    }
}
