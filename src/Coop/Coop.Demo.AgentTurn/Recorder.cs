using System;
using System.Collections.Generic;
using System.Globalization;

namespace Coop.Demo.AgentTurn
{
    public sealed class RecordedStep
    {
        public long OffsetMilliseconds { get; set; }

        public string Kind { get; set; }

        public string Title { get; set; }

        public string Detail { get; set; }
    }

    public sealed class Recorder
    {
        private readonly DateTime _started = DateTime.Now;

        private readonly List<RecordedStep> _steps = new List<RecordedStep>();

        public IReadOnlyList<RecordedStep> Steps
        {
            get
            {
                return _steps;
            }
        }

        public void Add(string kind, string title, string detail)
        {
            RecordedStep step = new RecordedStep();
            step.OffsetMilliseconds = (long)(DateTime.Now - _started).TotalMilliseconds;
            step.Kind = kind;
            step.Title = title;
            step.Detail = detail;
            _steps.Add(step);
        }
    }
}
