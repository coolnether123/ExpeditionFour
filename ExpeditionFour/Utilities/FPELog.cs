using ModAPI.Core;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Centralized logging helper for the mod.
    /// Demonstrates best practice for using the IModLogger provided by the ModAPI.
    /// </summary>
    internal static class FPELog
    {
        private static IPluginContext Context => MyPlugin.Context;
        private static IModLogger Log => Context?.Log;

        public static void Info(string msg)
        {
            if (Log != null) Log.Info(msg);
            else MMLog.WriteInfo(msg, MMLog.LogCategory.General);
        }

        public static void Warn(string msg)
        {
            if (Log != null) Log.Warn(msg);
            else MMLog.WriteWarning(msg, MMLog.LogCategory.General);
        }

        public static void Debug(string msg)
        {
            if (FpeDebug.Enabled)
            {
                if (Log != null) Log.Info(msg);
                else MMLog.WriteDebug(msg, MMLog.LogCategory.General);
            }
        }
    }

    internal static class FpeDebug
    {
        /// <summary>
        /// Set to true during development to enable detailed diagnostic tracing.
        /// Should be false for release builds.
        /// </summary>
        public static bool Enabled => false;
    }
}
