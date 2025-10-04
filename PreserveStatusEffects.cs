using System.Collections.Generic;
using HarmonyLib;

namespace Lunarbin.Valheim.CrossServerPortals;

internal static class PreserveStatusEffects
{
    private static List<SEData> StatusEffects = new List<SEData>();

    /// <summary>
    /// Apply saved status effects to the player ONLY if the config is enabled
    /// </summary>
    /// <param name="player"></param>
    public static void ApplyStatusEffects(Player player)
    {
        if (CSPConfig.preserveStatusEffects.Value && StatusEffects.Count > 0)
        {
            foreach (var se in StatusEffects)
            {
                player.GetSEMan().AddStatusEffect(se.Name);
                StatusEffect theSE = player.GetSEMan().GetStatusEffect(se.Name);
                if (theSE != null)
                {
                    theSE.m_ttl = se.Time;
                }
            }
        }

        // Clear status effects
        StatusEffects.Clear();
    }

    /// <summary>
    /// Save the given player's current status effects.
    /// </summary>
    /// <param name="player"></param>
    public static void SaveStatusEffects(Player player)
    {
        // Clear the status effects before saving, just in case.
        StatusEffects.Clear();
        
        foreach (var se in player.GetSEMan().GetStatusEffects())
        {
            StatusEffects.Add(new SEData(se.NameHash(), se.m_ttl, 
                    // StatusEffect.m_time is protected, so instead I just set the ttl to the remaining time.
                se.m_ttl > 0 ? se.GetRemaningTime() : 0));
        }
    }


    /// <summary>
    /// Patch Player.Load to apply saved status effects on load.
    /// </summary>
    [HarmonyPatch(typeof(Player), "Load")]
    private static class PatchPlayerLoad
    {
        private static void Postfix(Player __instance)
        {
            ApplyStatusEffects(__instance);
        }
    }
    
    /// <summary>
    /// Struct for holding status effect data
    /// </summary>
    private readonly struct SEData
    {
        public int Name { get; }
        public float Ttl { get; }
        public float Time { get; }

        public SEData(int name, float ttl, float time)
        {
            Name = name;
            Ttl = ttl;
            Time = time;
        }
    }
}