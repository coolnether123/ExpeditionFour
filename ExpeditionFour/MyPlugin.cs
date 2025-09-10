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

        try
        {
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }
        catch (System.Exception ex)
        {
            FPELog.Warn($"Harmony PatchAll failed: {ex.ToString()}");
        }



        // Optional: if the API provides AddComponentToPanel, use it to prime the behaviour instance
        // Otherwise, our OnShow patch attaches this component on-demand.
        // Try to call ctx.AddComponentToPanel<FourPersonPartyLogic>(path) if available
        try
        {
            var method = ctx.GetType().GetMethod("AddComponentToPanel");
            if (method != null)
            {
                FPELog.Info("Attempting AddComponentToPanel: UI Root/ExpeditionPanel/ExpeditionMainPanelNew");
                var generic = method.MakeGenericMethod(typeof(FourPersonPartyLogic));
                var logicObj = generic.Invoke(ctx, new object[] { "UI Root/ExpeditionPanel/ExpeditionMainPanelNew" });
                var logic = logicObj as FourPersonPartyLogic;
                if (logic != null)
                {
                    logic.MaxPartySize = FourPersonConfig.MaxPartySize;
                    FPELog.Info("AddComponentToPanel succeeded; logic attached.");
                }
                else
                {
                    FPELog.Info("AddComponentToPanel returned null; will attach on OnShow.");
                }
            }
        }
        catch (System.Exception ex) { FPELog.Info($"AddComponentToPanel failed: {ex.Message}"); }

        FPELog.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
    }
}
