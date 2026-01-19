using ModAPI.Core;

namespace FourPersonExpeditions
{
    /// <summary>
    /// Centralized logging helper that safely handles early initialization scenarios.
    /// Falls back to MMLog when the plugin context is not yet available.
    /// </summary>
    internal static class FPELog
    {
        private static IPluginContext Context => MyPlugin.Context;
        
        public static void Info(string msg)
        {
            var ctx = Context;
            if (ctx != null)
            {
                ctx.Log?.Info(msg);
            }
            else
            {
                // Fallback for early initialization before plugin context is ready
                MMLog.WriteInfo(msg, MMLog.LogCategory.General);
            }
        }
        
        public static void Warn(string msg)
        {
            var ctx = Context;
            if (ctx != null)
            {
                ctx.Log?.Warn(msg);
            }
            else
            {
                // Fallback for early initialization before plugin context is ready
                MMLog.WriteWarning(msg, MMLog.LogCategory.General);
            }
        }
    }

    internal static class FpeDebug
    {
        // Debug tracing disabled for release. Set to true for development/debugging.
        public static bool Enabled => false;
    }
}
