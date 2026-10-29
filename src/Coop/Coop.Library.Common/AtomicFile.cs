using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Coop.Library.Common
{
    public static class AtomicFile
    {
        public const int AttemptLimit = 6;

        public const int RetryDelayMilliseconds = 50;

        private const int AccessDenied = unchecked((int)0x80070005);

        public static void Write(string path, string content)
        {
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            Replace(temporaryPath, path);
        }

        public static void Replace(string sourcePath, string destinationPath)
        {
            RetryOnAccessDenied(() => File.Move(sourcePath, destinationPath, true));
        }

        public static void MoveDirectory(string sourcePath, string destinationPath)
        {
            RetryOnAccessDenied(() => Directory.Move(sourcePath, destinationPath));
        }

        public static void RetryOnAccessDenied(Action attempt)
        {
            int attempts = 1;
            while (true)
            {
                try
                {
                    attempt();
                    return;
                }
                catch (Exception exception) when (IsAccessDenied(exception))
                {
                    if (attempts == AttemptLimit)
                    {
                        throw;
                    }
                    attempts = attempts + 1;
                    Thread.Sleep(RetryDelayMilliseconds);
                }
            }
        }

        private static bool IsAccessDenied(Exception exception)
        {
            if (exception.HResult != AccessDenied)
            {
                return false;
            }
            return exception is IOException || exception is UnauthorizedAccessException;
        }
    }
}
