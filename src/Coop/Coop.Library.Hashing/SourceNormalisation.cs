using System;

namespace Coop.Library.Hashing
{
    public static class SourceNormalisation
    {
        private const char ByteOrderMark = '\uFEFF';

        public static string Apply(string source)
        {
            string text = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
            if (text.Length > 0 && text[0] == ByteOrderMark)
            {
                return text.Substring(1);
            }
            return text;
        }
    }
}
