using System;
using System.IO;

namespace Coop.Library.Locking
{
    public sealed class LockHandle : IDisposable
    {
        private readonly FileStream stream;

        internal LockHandle(FileStream stream, string path)
        {
            this.stream = stream;
            Path = path;
        }

        public string Path { get; }

        public void Dispose()
        {
            stream.Dispose();
        }
    }
}
