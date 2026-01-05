using HarmonyLib;
using UnityEngine;
using ModAPI.Core;

// Plugin entry. Applies Harmony patches and seeds config.
public class MyPlugin : IModPlugin
{
    public void Initialize(IPluginContext ctx)
    {
    }

    public void Start(IPluginContext ctx)
    {
        // Hard cap until UI support expands beyond 4.

        // Apply patches
        var harmony = new Harmony("com.coolnether123.fourpersonexpeditions");

        try { harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly()); }
        catch (System.Exception ex) { FPELog.Warn($"Harmony PatchAll failed: {ex.ToString()}"); }



        

        ctx.Log?.Info($"Four Person Expeditions loaded. MaxPartySize={FourPersonConfig.MaxPartySize}");
    }
}
