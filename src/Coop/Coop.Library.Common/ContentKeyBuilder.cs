using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Coop.Library.Common
{
    public sealed class ContentKeyBuilder
    {
        private readonly SortedDictionary<string, ContentKeyField> fields = new SortedDictionary<string, ContentKeyField>(StringComparer.Ordinal);
        private string finished;

        public void Add(string field, string value)
        {
            ContentKeyField stored = new ContentKeyField();
            stored.Values = new string[] { value };
            stored.Single = true;
            Store(field, stored);
        }

        public void Add(string field, IReadOnlyList<string> values)
        {
            ContentKeyField stored = new ContentKeyField();
            stored.Values = values.ToArray();
            stored.Single = false;
            Store(field, stored);
        }

        public string Finish()
        {
            if (finished == null)
            {
                finished = Sha.HexOfBytes(Encode());
            }
            return finished;
        }

        private void Store(string field, ContentKeyField stored)
        {
            if (fields.ContainsKey(field))
            {
                throw new InvalidOperationException("Field already added: " + field);
            }
            fields.Add(field, stored);
        }

        private byte[] Encode()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                fields.ToList().ForEach(entry => WriteField(stream, entry.Key, entry.Value));
                return stream.ToArray();
            }
        }

        private static void WriteField(MemoryStream stream, string field, ContentKeyField stored)
        {
            WriteFramed(stream, Encoding.UTF8.GetBytes(field));
            if (stored.Single)
            {
                WriteFramed(stream, Encoding.UTF8.GetBytes(stored.Values[0]));
                return;
            }
            WriteInt32(stream, stored.Values.Length);
            stored.Values.ToList().ForEach(value => WriteFramed(stream, Encoding.UTF8.GetBytes(value)));
        }

        private static void WriteFramed(MemoryStream stream, byte[] bytes)
        {
            WriteInt32(stream, bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static void WriteInt32(MemoryStream stream, int value)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
            stream.Write(buffer, 0, buffer.Length);
        }

        private sealed class ContentKeyField
        {
            public string[] Values { get; set; }

            public bool Single { get; set; }
        }
    }
}
