using System;
using System.Collections.Generic;

namespace Coop.Library.Execution
{
    public static class ExecutionDeadline
    {
        public const int DefaultSeconds = 60;

        public const int CeilingSeconds = 600;

        public static TimeSpan FromSeconds(int? seconds)
        {
            if (seconds == null)
            {
                return TimeSpan.FromSeconds(DefaultSeconds);
            }
            if (seconds.Value > CeilingSeconds)
            {
                return TimeSpan.FromSeconds(CeilingSeconds);
            }
            return TimeSpan.FromSeconds(seconds.Value);
        }
    }
}
