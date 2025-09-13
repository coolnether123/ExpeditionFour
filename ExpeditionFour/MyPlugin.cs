using HarmonyLib;
using UnityEngine;

// Plugin entry. Applies Harmony patches and seeds config.
public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
        // Configure tracing from settings so we can turn noisy logs on/off
        try
        {
            bool trace = ctx?.Settings != null ? ctx.Settings.GetBool("trace", false) : false;
            FpeDebug.Enabled = trace;
        }
        catch { FpeDebug.Enabled = false; }
    }

    public void Start(IPluginContext ctx)
    {
        // Read configurable party size (default 4, clamped >= 2)
        int configured = 4;
        try { configured = ctx?.Settings != null ? ctx.Settings.GetInt("maxPartySize", 4) : 4; } catch { configured = 4; }
        FourPersonConfig.MaxPartySize = Mathf.Max(2, configured);

        // Apply patches
        var harmony = new Harmony("com.coolnether123.fourpersonexpeditions");

        try { harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly()); }
        catch (System.Exception ex) { FPELog.Warn($"Harmony PatchAll failed: {ex.ToString()}"); }



        // Attach our logic to the Expedition panel if it already exists; otherwise, UI patches will attach on OnShow.
        try
        {
            if (FpeDebug.Enabled) ctx.Log?.Info("Attempting AddComponentToPanel: UI Root/ExpeditionPanel/ExpeditionMainPanelNew");
            var logic = ctx.AddComponentToPanel<FourPersonPartyLogic>("UI Root/ExpeditionPanel/ExpeditionMainPanelNew");
            if (logic != null)
            {
                logic.MaxPartySize = FourPersonConfig.MaxPartySize;
                if (FpeDebug.Enabled) ctx.Log?.Info("AddComponentToPanel succeeded; logic attached.");
            }
            else
            {
                if (FpeDebug.Enabled) ctx.Log?.Info("FindPanel failed; will attach on OnShow.");
            }
        }
        catch (System.Exception ex)
        {
            if (FpeDebug.Enabled) ctx.Log?.Warn("AddComponentToPanel failed: " + ex.Message);
        }

        ctx.Log?.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
    }
}
