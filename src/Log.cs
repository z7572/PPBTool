using System;
using BepInEx.Logging;

namespace PPBTool
{
    internal static class Log
    {
        private static ManualLogSource source;

        public static void Init(ManualLogSource logSource)
        {
            source = logSource;
        }

        public static void Info(string message)
        {
            source?.LogInfo("[PPBTool] " + message);
        }

        public static void Warn(string message)
        {
            source?.LogWarning("[PPBTool] " + message);
        }

        public static void Error(string message)
        {
            source?.LogError("[PPBTool] " + message);
        }

        public static void Error(string message, Exception ex)
        {
            source?.LogError("[PPBTool] " + message + Environment.NewLine + ex);
        }
    }
}
