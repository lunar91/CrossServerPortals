using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using HarmonyLib;

namespace Lunarbin.Valheim.CrossServerPortals;

/**
 * This class manages portal colors.
 */
/** Heirarchy of the game object:
                 TELEPORT
                 _target_Found_red
                    SFX
                    Point Light
                    Particle System
                        Black_suck
                        blue flames
                        suck particles
                            Portal Plane
                New
                    small_portal
                PlayerBase
                Mesh collider
                Proximity
                GuidePoint


                _target_found_red/Particle System/blue flames
                    The red flames that surround the portal.
                _target_found_red/Particle System/Black_suck
                    The black portal effect sucking in.
                _target_found_red/Particle System/suck particles
                    The square glowing particles that float around the portal, sucking in
                 */

public static class ModifyPortalColors
{
    private static List<TeleportWorld> modifiedPortals = new();

    private static Dictionary<string, TeleportWorld> defaultPortals = new();
    
    public enum PortalType
    {
        Stone,
        Wood,
        Other
    }

    public static void GarbageCollect()
    {
        for (int i = modifiedPortals.Count - 1; i >= 0; i--)
        {
            if (modifiedPortals[i] == null) modifiedPortals.RemoveAt(i);
        }
    }

    // Returns true if the portal is listed as modified in the modifiedPortals list
    private static bool IsModified(TeleportWorld portal)
    {
        if (portal == null || modifiedPortals == null || modifiedPortals.Count == 0) return false;
        for (int i = 0; i < modifiedPortals.Count; i++)
            if (ReferenceEquals(modifiedPortals[i], portal) || modifiedPortals[i] == portal)
                return true;
        return false;
    }

    // Adds the portal to a list of modified portals to indicate that its colors
    // have been changed.
    private static void SetModified(TeleportWorld portal)
    {
        
        
        if (portal == null) return;
        if (modifiedPortals == null) modifiedPortals = new List<TeleportWorld>();
        if (IsModified(portal)) return;
        modifiedPortals.Add(portal);
    }

    // Remove the portal from the list of Modified Portals
    private static void SetNotModified(TeleportWorld portal)
    {
            
        if (portal == null || modifiedPortals == null || modifiedPortals.Count == 0) return;
        // Remove by reference first, fall back to Equals
        for (int i = 0; i < modifiedPortals.Count; i++)
        {
            if (ReferenceEquals(modifiedPortals[i], portal) || modifiedPortals[i] == portal)
            {
                modifiedPortals.RemoveAt(i);
                if (modifiedPortals.Count == 0) modifiedPortals = null;
                return;
            }
        }
    }
    
    private static PortalType GetPortalType(TeleportWorld portal)
    {
        // If this is a portal_stone, return portaltype.stone
        if (portal.gameObject.name.Contains("portal_stone"))
        {
            return PortalType.Stone;
        }

        // If this is a portal_wood, return portaltype.wood
        if (portal.gameObject.name.Contains("portal_wood"))
        {
            return PortalType.Wood;
        }

        // Unknown Portal Type
        return PortalType.Other;
    }

    private static string GetPortalBaseName(TeleportWorld portal)
    {
        if (portal.gameObject.name.Contains("portal_stone"))
            return "portal_stone";
        if (portal.gameObject.name.Contains("portal_wood"))
            return "portal_wood";
        return "portal";
    }

    // Update the colors for a TeleportWorld
    public static void ResetPortalColors(TeleportWorld portal)
        {
            if (!IsModified(portal))
            {
                return;
            }
            Debug.Log($"Resetting colors for {portal.GetText()}");

            SetNotModified(portal);
            var type = GetPortalType(portal);
            var defaultPortal = GetDefaultPortal(portal);
            if (!defaultPortal)
            {
                Debug.Log($"Could not find default portal for {portal.GetText()}");
                return;
            }

            switch (type)
            {
                case PortalType.Stone:
                case PortalType.Wood:

                    var target_found_red = portal.transform.Find("_target_found_red");
                    var def_target_found_red = defaultPortal.transform.Find("_target_found_red");
                    
                    // Reset the glyph color
                    portal.m_colorTargetfound = defaultPortal.m_colorTargetfound;

                    // The primary particle system color
                    var particleSystem = target_found_red.transform.Find("Particle System");
                    var defParticleSystem = def_target_found_red.transform.Find("Particle System");

                    // The Flames particle system color.
                    var blueFlames = particleSystem.transform.Find("blue flames").GetComponent<ParticleSystem>();
                    var defBlueFlames = defParticleSystem.transform.Find("blue flames").GetComponent<ParticleSystem>();
                    blueFlames.startColor = defBlueFlames.startColor;
                    blueFlames.customData.SetColor(ParticleSystemCustomData.Custom1, defBlueFlames.customData.GetColor(ParticleSystemCustomData.Custom1));
                    blueFlames.customData.SetColor(ParticleSystemCustomData.Custom2, defBlueFlames.customData.GetColor(ParticleSystemCustomData.Custom2));
                    

                    // The black portal sucking particle system
                    var blackSuck = particleSystem.transform.Find("Black_suck").GetComponent<ParticleSystem>();
                    var defBlackSuck = defParticleSystem.transform.Find("Black_suck").GetComponent<ParticleSystem>();
                    blackSuck.startColor = defBlackSuck.startColor;
                    
                    // The Sucking Particles
                    var suckParticles = particleSystem.transform.Find("suck particles").GetComponent<ParticleSystem>();
                    var defSuckParticles = defParticleSystem.transform.Find("suck particles").GetComponent<ParticleSystem>();
                    suckParticles.startColor = defSuckParticles.startColor;

                    // The light color
                    var pointLight = target_found_red.transform.Find("Point light").GetComponent<Light>();
                    var defPointLight = def_target_found_red.transform.Find("Point light").GetComponent<Light>();
                    pointLight.color = defPointLight.color;
                    return;
                default:
                    // Nothing to do for other portals...
                    return;
            }
        }

    public static void SetPortalColors(TeleportWorld portal)
    {
        // Do nothing if this portal is already modified.
        if (IsModified(portal)) return;
        
        // If the recolor options are both off, do nothing.
        if (!CSPConfig.recolorPortalEffects.Value && !CSPConfig.recolorPortalGlyphs.Value) return;
        
        var target_found_red = portal.transform.Find("_target_found_red");
        if (!target_found_red)
            return;
        
        SetModified(portal);
        var particle_system = target_found_red.transform.Find("Particle System");

        var blue_flames = particle_system.transform.Find("blue flames").GetComponent<ParticleSystem>();
        var black_suck = particle_system.transform.Find("Black_suck").GetComponent<ParticleSystem>();
        var suck_particles = particle_system.transform.Find("suck particles").GetComponent<ParticleSystem>();

        var point_light = target_found_red.transform.Find("Point light").GetComponent<Light>();

        if (CSPConfig.recolorPortalGlyphs.Value)
        {
            if (portal.gameObject.name == "portal_stone(Clone)")
            {
                portal.m_colorTargetfound = CSPConfig.customPortalGlyphColor.Value * 12f;
            }
            else
            {
                portal.m_colorTargetfound = CSPConfig.customPortalGlyphColor.Value * 4f;
            }
        }

        if (CSPConfig.recolorPortalEffects.Value)
        {
            blue_flames.startColor = CSPConfig.customPortalEffectColor.Value;
            blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, CSPConfig.customPortalEffectColor.Value);
            blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, CSPConfig.customPortalEffectColor.Value);
            Color customColorWithAlpha = CSPConfig.customPortalEffectColor.Value;
            customColorWithAlpha = customColorWithAlpha * 0.1f;
            customColorWithAlpha.a = 0.1f;
            black_suck.startColor = customColorWithAlpha;
            suck_particles.startColor = CSPConfig.customPortalEffectColor.Value;
            point_light.color = CSPConfig.customPortalEffectColor.Value;
        }
    }
    
    // Finds any UnityEngine.Object by name, including disabled/prefabs/assets (editor + runtime for loaded assets)
    private static T FindAnyObjectByName<T>(string name) where T : Object
    {
        foreach (var obj in Resources.FindObjectsOfTypeAll<T>())
            if (obj != null && obj.name == name)
                return obj;
        return null;
    }

    // When the config changes, re-do the portal colors.
    public static void OnColorSettingsChanged()
    {
        foreach (var portal in new List<TeleportWorld>(modifiedPortals))
        {
            ResetPortalColors(portal);
            SetPortalColors(portal);
        }
    }

        

    // Get the default portal for a given portal.
    // portal_wood
    // portal_stone
    // portal
    private static TeleportWorld GetDefaultPortal(TeleportWorld portal)
    {
        TeleportWorld defaultPortal;
        // Portal type as string of the base object name.
        var baseName = GetPortalBaseName(portal);
        // Try to get the value 
        if (!defaultPortals.TryGetValue(baseName, out defaultPortal))
        {
            defaultPortal = FindAnyObjectByName<TeleportWorld>(baseName);
            defaultPortals.Add(baseName, defaultPortal);
        }

        return defaultPortal;
    }
    
    /// <summary>
    /// Patch TeleportWorld.HaveTarget.
    /// If the portalTag counts as ServerInfo then treat it as active
    /// </summary>
    [HarmonyPatch(typeof(TeleportWorld), "HaveTarget")]
    internal class PatchTeleportWorldHaveTarget
    {
        private static bool Prefix(ref bool __result, TeleportWorld __instance)
        {
            string portalTag = __instance.GetText();
            if (!Lunarbin.Valheim.CrossServerPortals.TeleportInfo.PortalTagIsTeleportInfo(portalTag))
            {
                ModifyPortalColors.ResetPortalColors(__instance);
                return true;
            }

            ModifyPortalColors.SetPortalColors(__instance);

            __result = true;
            return false;
        }
    }

    /// <summary>
    /// Patch TeleportWorld.TargetFound.
    /// If the portalTag counts as ServerInfo then treat it as active
    /// </summary>
    [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
    internal class PatchTeleportWorldTargetFound
    {
        private static bool Prefix(ref bool __result, TeleportWorld __instance)
        {
            string portalTag = __instance.GetText();
            if (!TeleportInfo.PortalTagIsTeleportInfo(portalTag))
            {
                ResetPortalColors(__instance);
                return true;
            }

            SetPortalColors(__instance);

            __result = true;
            return false;
        }
    }
    
}