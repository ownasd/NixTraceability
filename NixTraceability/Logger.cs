using System;
using System.IO;

namespace NixTraceability
{
    public static class Logger
    {
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "NixTraceability", "logs");

        public static void LogError(string context, Exception ex)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string logFile = Path.Combine(LogDir, $"app_{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ERROR in {context}:{Environment.NewLine}  {ex.GetType().Name}: {ex.Message}{Environment.NewLine}  StackTrace: {ex.StackTrace}{Environment.NewLine}";
                File.AppendAllText(logFile, entry);
            }
            catch
            {
                // Logging failed silently — avoid infinite loops
            }
        }

        public static void LogInfo(string context, string message)
        {
            try
            {
                Directory.CreateDirectory(LogDir);
                string logFile = Path.Combine(LogDir, $"app_{DateTime.Now:yyyy-MM-dd}.log");
                string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO [{context}]: {message}{Environment.NewLine}";
                File.AppendAllText(logFile, entry);
            }
            catch { }
        }
    }
}
