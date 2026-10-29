using System;
using System.Collections.Generic;
using System.Linq;

namespace Coop.Library.Records
{
    public static class RecordTokens
    {
        private static readonly HashSet<string> StopWords = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "an", "and", "any", "are", "as", "at", "be", "but", "by", "can", "do", "does", "for", "from",
            "had", "has", "have", "if", "in", "into", "is", "it", "its", "not", "of", "on", "one", "or", "our",
            "out", "per", "so", "than", "that", "the", "then", "this", "to", "use", "used", "using", "via",
            "was", "we", "were", "when", "with", "you", "your"
        };

        public static List<string> Of(string name, string description, string category)
        {
            string text = (name + " " + description + " " + category).ToLowerInvariant();
            char[] separators = text
                .Where(character => char.IsLetterOrDigit(character) == false)
                .Distinct()
                .ToArray();
            return text
                .Split(separators, StringSplitOptions.RemoveEmptyEntries)
                .Where(token => token.Length >= 2)
                .Where(token => StopWords.Contains(token) == false)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToList();
        }
    }
}
