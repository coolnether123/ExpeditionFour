using HarmonyLib;
using ModAPI.Core;

namespace FourPersonExpeditions
{
    // Applies Harmony patches and initializes four-member party state.
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
            var harmony = new Harmony("Coolnether123.FourPersonExpeditions");

            try
            {
                harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
            }
            catch (System.Exception ex)
            {
                // PatchAll may have applied some patches before reporting a
                // later failure. Roll those back so a partially active mod
                // cannot corrupt expedition state while still claiming to be
                // loaded.
                try
                {
                    harmony.UnpatchAll("Coolnether123.FourPersonExpeditions");
                }
                catch (System.Exception rollbackException)
                {
                    ctx.Log?.Error($"Four Person Expeditions Harmony rollback failed: {rollbackException}");
                }

                ctx.Log?.Error($"Four Person Expeditions failed to load: {ex}");
                return;
            }

            ctx.Log?.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
        }
    }
}
