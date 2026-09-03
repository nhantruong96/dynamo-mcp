using System;
using System.IO;

namespace DynamoMcp
{
    /// <summary>Minimal append-only file log at %LOCALAPPDATA%\DynamoMCP\extension.log.</summary>
    internal static class Log
    {
        private static readonly object Gate = new object();
        public static readonly string Path = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DynamoMCP", "extension.log");

        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                    File.AppendAllText(Path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never break the host.
            }
        }
    }
}
