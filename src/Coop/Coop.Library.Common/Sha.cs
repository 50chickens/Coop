using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Coop.Library.Common
{
    public static class Sha
    {
        public static string HexOfUtf8(string text)
        {
            return HexOfBytes(Encoding.UTF8.GetBytes(text));
        }

        public static string HexOfBytes(byte[] bytes)
        {
            return Convert.ToHexStringLower(SHA256.HashData(bytes));
        }

        public static string HexOfStream(Stream stream)
        {
            return Convert.ToHexStringLower(SHA256.HashData(stream));
        }

        public static string HexOfFile(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                return HexOfStream(stream);
            }
        }
    }
}
