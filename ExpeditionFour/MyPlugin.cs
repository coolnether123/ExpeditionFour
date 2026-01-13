using HarmonyLib;
using ModAPI.Core;

namespace FourPersonExpeditions
{
    // Plugin entry. Applies Harmony patches and seeds expanded party capacity.
    public class MyPlugin : IModPlugin
    {
        private static IPluginContext _ctx;
        public static IPluginContext Context => _ctx;

        public void Initialize(IPluginContext ctx)
        {
            _ctx = ctx;
        }

        public void Start(IPluginContext ctx)
        {
            // Apply patches
            var harmony = new Harmony("com.coolnether123.fourpersonexpeditions");

            try 
            { 
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly()); 
            }
            catch (System.Exception ex) 
            { 
                ctx.Log.Warn($"Harmony PatchAll failed: {ex}"); 
            }

            ctx.Log?.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
        }
    }
}
