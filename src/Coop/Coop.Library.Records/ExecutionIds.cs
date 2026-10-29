using System;
using System.IO;
using System.Linq;

namespace Coop.Library.Records
{
    public static class ExecutionIds
    {
        public const string IndexFileName = "index.json";

        public static bool Usable(string executionId)
        {
            if (string.IsNullOrWhiteSpace(executionId))
            {
                return false;
            }
            if (executionId.Contains("..", StringComparison.Ordinal))
            {
                return false;
            }
            char[] forbidden = Path.GetInvalidFileNameChars();
            if (executionId.Any(character => Array.IndexOf(forbidden, character) >= 0))
            {
                return false;
            }
            return string.Equals(executionId, Path.GetFileNameWithoutExtension(IndexFileName), StringComparison.OrdinalIgnoreCase) == false;
        }
    }
}
