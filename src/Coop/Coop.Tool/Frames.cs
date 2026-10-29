using System;
using System.IO;
using System.Text;

namespace Coop.Tool
{
    public static class Frames
    {
        public const int HeaderBytes = 4;

        public static string Read(Stream stream)
        {
            byte[] header = new byte[HeaderBytes];
            stream.ReadExactly(header, 0, HeaderBytes);
            byte[] payload = new byte[BitConverter.ToInt32(header, 0)];
            stream.ReadExactly(payload, 0, payload.Length);
            return Encoding.UTF8.GetString(payload);
        }

        public static void Write(Stream stream, string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            stream.Write(BitConverter.GetBytes(payload.Length), 0, HeaderBytes);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }
    }
}
