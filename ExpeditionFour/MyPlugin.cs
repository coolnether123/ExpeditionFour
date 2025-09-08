using HarmonyLib;
using UnityEngine;

// Plugin entry. Applies Harmony patches and seeds config.
public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        // no-op for now
    }

    public void Start(IPluginContext ctx)
    {
        // Read configurable party size (default 4)
        FourPersonConfig.MaxPartySize = Mathf.Max(2, ctx.Settings.GetInt("maxPartySize", 4));

        // Apply patches
        var harmony = new Harmony("com.coolnether123.fourpersonexpeditions");
        harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

        // Optional: if the API provides AddComponentToPanel, use it to prime the behaviour instance
        // Otherwise, our OnShow patch attaches this component on-demand.
        // Try to call ctx.AddComponentToPanel<FourPersonPartyLogic>(path) if available
        try
        {
            var method = ctx.GetType().GetMethod("AddComponentToPanel");
            if (method != null)
            {
                ctx.Log.Info("Attempting AddComponentToPanel: UI Root/ExpeditionPanel/ExpeditionMainPanelNew");
                var generic = method.MakeGenericMethod(typeof(FourPersonPartyLogic));
                var logicObj = generic.Invoke(ctx, new object[] { "UI Root/ExpeditionPanel/ExpeditionMainPanelNew" });
                var logic = logicObj as FourPersonPartyLogic;
                if (logic != null)
                {
                    logic.MaxPartySize = FourPersonConfig.MaxPartySize;
                    ctx.Log.Info("AddComponentToPanel succeeded; logic attached.");
                }
                else
                {
                    ctx.Log.Info("AddComponentToPanel returned null; will attach on OnShow.");
                }
            }
        }
        catch (System.Exception ex) { ctx.Log.Info($"AddComponentToPanel failed: {ex.Message}"); }

        ctx.Log.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
    }
}
