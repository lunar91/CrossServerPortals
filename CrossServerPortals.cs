using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using ServerSync;

using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.SceneManagement;

namespace Lunarbin.Valheim.CrossServerPortals
{
    [BepInPlugin("lunarbin.games.valheim", "Valheim Cross Server Portals", "1.0.0")]
    public class CrossServerPortals : BaseUnityPlugin
    {
        // Regex sourceTag|server:port|targetTag
        // port and targetTag are optional
        private static string PortalTagRegex = @"^(?<SourceTag>[A-z0-9]+)\|(?<Server>(world:)?[A-z0-9\.]+):?(?<Port>[0-9]+)?\|?(?<TargetTag>[A-z0-9]+)?$";
        public static ServerInfo? ServerToJoin = null;
        public static bool TeleportingToServer = false;
        public static bool HasJoinedServer = false;
        public static float PortalExitDistance = 0.0f;

        public static ConfigEntry<bool> preserveStatusEffects;
        public static ConfigEntry<bool> recolorPortalGlyphs;
        public static ConfigEntry<bool> recolorPortalEffects;
        public static ConfigEntry<Color> customPortalGlyphColor;
        public static ConfigEntry<Color> customPortalEffectColor;
        public static ConfigEntry<bool> requireAdminToRename;

        // Synchronize Server Config
        private static ServerSync.ConfigSync configSync = new ServerSync.ConfigSync("lunarbin.games.valheim")
        {
            DisplayName = "Cross Server Portals", 
            CurrentVersion = "1.0.0", 
            MinimumRequiredVersion = "1.0.0"
        };
        
        private static List<SEData> StatusEffects = new List<SEData>();

        public static List<ZDO> knownPortals = new();

        public static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("CrossServerPortals");

        public struct ServerInfo
        {
            public string Address;
            public ushort Port;
            public string SourceTag;
            public string TargetTag;

            public ServerInfo()
            {

            }

            public ServerInfo(string sourceTag, string address, ushort port = 0, string targetTag = "")
            {
                Address = address;
                Port = port;
                SourceTag = sourceTag;
                TargetTag = targetTag;
            }

            public ServerInfo(string sourceTag, string address, string targetTag = "")
            {
                Address = address;
                Port = 0;
                TargetTag = targetTag;
                SourceTag = sourceTag;
            }

        }


        // Portal Colors
        private static Color defaultColor = Color.white;
        private static UnityEngine.ParticleSystem.MinMaxGradient defaultFlameColor1 = new UnityEngine.ParticleSystem.MinMaxGradient();
        private static UnityEngine.ParticleSystem.MinMaxGradient defaultFlameColor2 = new UnityEngine.ParticleSystem.MinMaxGradient();
        private static Color defaultParticleColor = Color.white;
        private static Color defaultPortalSuckColor = Color.white;
        private static Color defaultLightColor = Color.white;
        private static bool defaultsSet = false;


        // region preserveStatusEffects

        private readonly struct SEData // Status Effect Data
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

        private static void SetPortalDefaults(TeleportWorld portal)
        {
            var target_found_red = portal.transform.Find("_target_found_red");
            if (!target_found_red) return;
            var particle_system = target_found_red.transform.Find("Particle System");

            var blue_flames = particle_system.transform.Find("blue flames").GetComponent<ParticleSystem>();
            var black_suck = particle_system.transform.Find("Black_suck").GetComponent<ParticleSystem>();
            var suck_particles = particle_system.transform.Find("suck particles").GetComponent<ParticleSystem>();

            var point_light = target_found_red.transform.Find("Point light").GetComponent<Light>();

            if (defaultColor == Color.white && portal.m_colorTargetfound != customPortalGlyphColor.Value)
            {
                defaultColor = portal.m_colorTargetfound;
            }

           
            defaultColor = portal.m_colorTargetfound;

            defaultFlameColor1 = blue_flames.customData.GetColor(ParticleSystemCustomData.Custom1);
            defaultFlameColor2 = blue_flames.customData.GetColor(ParticleSystemCustomData.Custom2);


            defaultPortalSuckColor = black_suck.startColor;

            defaultParticleColor = suck_particles.startColor;

            defaultLightColor = point_light.color;

            defaultsSet = true;
            
        }

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
        private static void SetPortalColors(TeleportWorld portal, bool defaultColors)
        {
            var target_found_red = portal.transform.Find("_target_found_red");
            if (!target_found_red)
                return;
            var particle_system = target_found_red.transform.Find("Particle System");

            var blue_flames = particle_system.transform.Find("blue flames").GetComponent<ParticleSystem>();
            var black_suck = particle_system.transform.Find("Black_suck").GetComponent<ParticleSystem>();
            var suck_particles = particle_system.transform.Find("suck particles").GetComponent<ParticleSystem>();

            var point_light = target_found_red.transform.Find("Point light").GetComponent<Light>();

            if (!defaultsSet)
            {
                defaultColor = portal.m_colorTargetfound;                 

                defaultFlameColor1 = blue_flames.customData.GetColor(ParticleSystemCustomData.Custom1);
                defaultFlameColor2 = blue_flames.customData.GetColor(ParticleSystemCustomData.Custom2);

                defaultPortalSuckColor = black_suck.startColor;

                defaultParticleColor = suck_particles.startColor;

                defaultLightColor = point_light.color;

                defaultsSet = true;
            }

            if (recolorPortalGlyphs.Value && !defaultColors)
            {
                if (portal.gameObject.name == "portal_stone(Clone)")
                {
                    portal.m_colorTargetfound = customPortalGlyphColor.Value * 12f;
                }
                else
                {
                    portal.m_colorTargetfound = customPortalGlyphColor.Value * 4f;
                }
                
            } else
            {
                portal.m_colorTargetfound = defaultColor;
            }

            if (recolorPortalEffects.Value && !defaultColors)
            {   
                blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, customPortalEffectColor.Value);
                blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, customPortalEffectColor.Value);
                Color customColorWithAlpha = customPortalEffectColor.Value;
                customColorWithAlpha = customColorWithAlpha * 0.1f;
                customColorWithAlpha.a = 0.1f;
                black_suck.startColor = customColorWithAlpha;
                suck_particles.startColor = customPortalEffectColor.Value;

                point_light.color = customPortalEffectColor.Value;
            } else
            {
                blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, defaultFlameColor1);
                blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, defaultFlameColor2);
                black_suck.startColor = defaultPortalSuckColor;
                suck_particles.startColor = defaultParticleColor;

                point_light.color = defaultLightColor;
            }


            //if (defaultColors)
            //{
            //    // if config says to recolor glyphs
            //    //if (recolorPortalGlyphs.Value)
            //    //{
            //        if (portal.m_colorTargetfound == customPortalGlyphColor.Value)
            //            portal.m_colorTargetfound = defaultColor;   
            //    //}

            //    // if config says to recolor effects
            //    //if (recolorPortalEffects.Value)
            //    //{
            //        blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, defaultFlameColor1);
            //        blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, defaultFlameColor2);
            //        black_suck.startColor = defaultPortalSuckColor;
            //        suck_particles.startColor = defaultParticleColor;

            //        point_light.color = defaultLightColor;
            //    //}
            //} else
            //{
            //    // if config says to recolor glyphs
            //    if (recolorPortalGlyphs.Value)
            //    {
            //        if (portal.gameObject.name == "portal_stone(Clone)")
            //        {
            //            portal.m_colorTargetfound = customPortalGlyphColor.Value * 12f;
            //        }
            //        else
            //        {
            //            portal.m_colorTargetfound = customPortalGlyphColor.Value * 4f;
            //        }
            //    }

            //    // if config says to recolor effects
            //    if (recolorPortalEffects.Value)
            //    {
            //        blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, customPortalEffectColor.Value);
            //        blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, customPortalEffectColor.Value);
            //        Color customColorWithAlpha = customPortalEffectColor.Value;
            //        customColorWithAlpha = customColorWithAlpha * 0.1f;
            //        customColorWithAlpha.a = 0.1f;
            //        black_suck.startColor = customColorWithAlpha;
            //        suck_particles.startColor = customPortalEffectColor.Value;

            //        point_light.color = customPortalEffectColor.Value;
            //    }
            //}

        }

        // Patch Player.Load to apply saved status effects on load, AFTER load has finished.
        [HarmonyPatch(typeof(Player), "Load")]
        private static class PatchPlayerLoad
        {
            private static void Postfix(Player __instance)
            {
                
                if (preserveStatusEffects.Value && StatusEffects.Count > 0)
                {
                    foreach(var se in StatusEffects)
                    {
                        __instance.GetSEMan().AddStatusEffect(se.Name);
                        StatusEffect theSE = __instance.GetSEMan().GetStatusEffect(se.Name);
                        if (theSE != null)
                        {
                            theSE.m_ttl = se.Time;
                        }
                    }
                }
                // Clear the saved Status Effects now that we have applied them.
                StatusEffects.Clear();
            }
        }
        
        // endregion preserveStatuseffects

        private readonly Harmony harmony = new Harmony("lunarbin.games.valheim");

        
        
        private void Awake()
        {
            harmony.PatchAll();
            

            // Config for preserving status effects while switching servers.
            preserveStatusEffects = Config.Bind("General",
                                                "PreserveStatusEffects",
                                                true,
                                                "Preserve Status Effects while switching servers (such as rested, wet, etc.)");

            recolorPortalGlyphs = Config.Bind("General", 
                                              "RecolorPortalGlyphs", 
                                              true, 
                                              "Set to true to change the color of the glyphs for cross-server portals.");
            customPortalGlyphColor = Config.Bind<Color>("General",
                                            "CustomPortalGlyphColor",
                                            new Color(0f, 1f, 0f, 1f),
                                            "Custom color for portal glyphs. (defaults to Green) *Note: Stone portal glyph colors behave strangely and favor red.");

            recolorPortalEffects = Config.Bind("General",
                                               "RecolorPortalEffects", 
                                               true, 
                                               "Set to true to change the portal effects colors for cross-server portals.");

            customPortalEffectColor = Config.Bind<Color>("General",
                                            "CustomPortalEffectColor",
                                            new Color(0f, 1f, 0f, 0.5f),
                                            "Custom color for portal effects. (defaults to Green)");
            
            requireAdminToRename = Config.Bind<bool>("General", "RequireAdminToRename", false, "Require admin permissions to rename cross server portals.");
            
            configSync.AddLockingConfigEntry(requireAdminToRename);
        }

        // Patch TeleportWorld.Teleport.
        // If the portal tag matches the PortalTagRegex, use the custom Teleport method instead.
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        internal static class PatchTeleport
        {
            private static bool Prefix(TeleportWorld __instance, ref Player player)
            {
                string portalTag = __instance.GetText();

                if (PortalTagIsToServer(portalTag))
                {
                    player.Message(MessageHud.MessageType.Center, $"Teleporting to {portalTag}");
                    TeleportToServer(portalTag, __instance);
                    return false;
                }
                return true;
            }
        }

        // TeleportToServer simply logs the player out.
        // The connection will be handled on the main menu.
        public static void TeleportToServer(string tag, TeleportWorld instance)
        {
            ServerToJoin = PortalTagToServerInfo(tag);
            if (ServerToJoin != null)
            {
                // Preserve Status Effects across worlds
                // if (preserveStatusEffects.Value)
                // {
                    foreach(var se in Player.m_localPlayer.GetSEMan().GetStatusEffects())
                    {
                        StatusEffects.Add(new SEData(se.NameHash(), se.m_ttl, 
                             // StatusEffect.m_time is protected, so instead I just set the ttl to the remaining time.
                                se.m_ttl > 0 ? se.GetRemaningTime() : 0));
                    }
                // }

                MovePlayerToPortalExit(ref Player.m_localPlayer, ref instance);

                Game.instance.IncrementPlayerStat(PlayerStatType.PortalsUsed);

                TeleportingToServer = true;
                HasJoinedServer = false;

                Game.instance.Logout();
                return;
            }
            Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Invalid portal tag: {tag}");
        }

        // Move the player to the portal exit position.
        // This is called prior to logging the player out to prevent
        // the player from logging back in inside the portal.
        public static void MovePlayerToPortalExit(ref Player player, ref TeleportWorld portal)
        {
            var position = portal.transform.position;
            var rotation = portal.transform.rotation;
            var offsetDir = rotation * Vector3.forward;
            var newPos = position + offsetDir * portal.m_exitDistance + Vector3.up;
            player.transform.position = newPos;
            player.transform.rotation = rotation;
            // Set the portal exit distance to double the normal
            PortalExitDistance = portal.m_exitDistance * 2;
        }

        // Patch TeleportWorld.HaveTarget
        // This makes it so the portal is usable and glows.
        [HarmonyPatch(typeof(TeleportWorld), "HaveTarget")]
        internal class PatchTeleportWorldHaveTarget
        {
            private static bool Prefix(ref bool __result, TeleportWorld __instance)
            {
            
                string text = __instance.GetText();
                if (!PortalTagIsToServer(text))
                {
                    
                    if (!defaultsSet)
                    {
                        SetPortalDefaults(__instance);
                    }

                    if (defaultsSet)
                    {
                        SetPortalColors(__instance, true);
                    }
                    
                    return true;
                }

                
                if (defaultsSet)
                {
                    SetPortalColors(__instance, false);
                }
                
                __result = true;
                return false;
            }
        }

        // Patch TeleportWorld.TargetFound
        // This makes it so the portal is usable and glows.
        [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
        internal class PatchTeleportWorldTargetFound
        {
            
            private static bool Prefix(ref bool __result, TeleportWorld __instance)
            {
                string text = __instance.GetText();
                if (!PortalTagIsToServer(text))
                {

                    
                    if (!defaultsSet)
                    {
                        SetPortalDefaults(__instance);
                    }

                    if (defaultsSet)
                    {
                        SetPortalColors(__instance, true);
                    }
                    

                    return true;
                }

                
                if (defaultsSet)
                {
                    SetPortalColors(__instance, false);
                }
                


                __result = true;
                return false;
            }
        }


        // Patch TeleportWorld.Interact
        // This is to increase character limit for portal tags.
        [HarmonyPatch(typeof(TeleportWorld), "Interact")]
        internal class PatchTeleportWorldInteract
        {
            private static bool Prefix(ref Humanoid human, ref bool hold, ref bool __result, TeleportWorld __instance)
            {
                if (hold)
                {
                    __result = false;
                    return false;
                }
                if (!PrivateArea.CheckAccess(__instance.transform.position))
                {
                    human.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                    __result = true;
                    return false;
                }
                TextInput.instance.RequestText(__instance, "$piece_portal_tag", 50);
                __result = true;
                return false;
            }
        }


        // Patch FejdStartup.Start
        // This hooks in to connect to a server if the player has used
        // a cross-server portal to logout from a server.
        [HarmonyPatch(typeof(FejdStartup), "Start")]
        internal static class PatchFejdStartupStart
        {
            private static void Postfix()
            {
                MessageHud.print("Test");
                if (ServerToJoin != null && TeleportingToServer && !HasJoinedServer)
                {
                    //Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Connecting to {ServerToJoin?.Address}:{ServerToJoin?.Port}");
                    var Address = ServerToJoin?.Address;
                    if (Address.StartsWith("world:"))
                    {
                        var world = Address.Substring(6);
                        if (world != "")
                        {
                            List<World> worlds = SaveSystem.GetWorldList();
                            foreach (var w in worlds)
                            {
                                if (w.m_name == world)
                                {
                                    PlayerPrefs.SetString("world", world);

                                    FejdStartup.instance.OnStartGame();
                                    FejdStartup.instance.OnWorldStart();
                                    return;
                                }
                            }
                            MessageHud.print($"World does not exist {world}");
                            //Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"World does not exist {world}");

                        }

                    }
                    else
                    {
                        ServerStatus serverStatus = new ServerStatus(new ServerJoinDataDedicated($"{ServerToJoin?.Address}:{ServerToJoin?.Port}"));
                        FejdStartup.instance.SetServerToJoin(serverStatus);
                        FejdStartup.instance.JoinServer();
                    }
                    //ZSteamMatchmaking.instance.QueueServerJoin($"{ServerToJoin?.Address}:{ServerToJoin?.Port}");
                }
            }
        }


        // Patch FejdStartup.ShowCharacterSelection
        // This confirms the Character Selector with the current character
        // if the player has used a cross-server portal to logout from a server.
        [HarmonyPatch(typeof(FejdStartup), "ShowCharacterSelection")]
        internal class PatchFejdStartupShowCharacterSelection
        {
            private static void Postfix()
            {
                if (ServerToJoin != null && TeleportingToServer && !HasJoinedServer)
                {
                    HasJoinedServer = true;
                    FejdStartup.instance.OnCharacterStart();
                }
            }
        }

        // Patch Game.SpawnPlayer
        // This teleports the player to the selected portal tag when they login
        // instead of their logged-out location.
        [HarmonyPatch(typeof(Game), "SpawnPlayer")]
        internal class PatchGameSpawnPlayer
        {
            private static void Prefix(ref Vector3 spawnPoint)
            {
                if (TeleportingToServer && ServerToJoin != null && ServerToJoin?.TargetTag != "")
                {
                    List<ZDO> zdos = ZDOMan.instance.GetPortals();
                    foreach (var portal in zdos)
                    {
                        if (portal == null) continue;
                        string tag = portal.GetString(ZDOVars.s_tag);
                        // If the tag is a direct match OR has a SourceTag that is
                        // then change the SpawnPoint to the portal exit location.
                        if (tag == ServerToJoin?.TargetTag
                            || PortalTagToServerInfo(tag)?.SourceTag == ServerToJoin?.TargetTag)
                        {
                            var position = portal.GetPosition();
                            var rotation = portal.GetRotation();
                            var offsetDir = rotation * Vector3.forward;
                            spawnPoint = position + offsetDir * PortalExitDistance + Vector3.up;
                            break;

                        }
                    }
                }
                // Null the ServerToJoin since we only just connected.
                TeleportingToServer = false;
                ServerToJoin = null;
            }
        }

        // Patch Game.Start to track portals when the game starts.
        [HarmonyPatch(typeof(Game), "Start")]
        internal class PatchGameStart
        {

            private static void Postfix()
            {
                knownPortals.Clear();
                Game.instance.StartCoroutine(FetchPortals());
            }

            // Every 10 seconds, refresh the list of known portals.
            private static IEnumerator FetchPortals()
            {
                while (true)
                {
                    List<ZDO> portals = ZDOMan.instance.GetPortals();

                    // Send the portals to the connected client.
                    if (ZNet.instance.IsServer())
                    {
                        foreach (ZDO zdo in portals.Except(knownPortals))
                        {
                            ZDOMan.instance.ForceSendZDO(zdo.m_uid);
                        }
                    }

                    knownPortals = portals;

                    yield return new WaitForSeconds(10f);
                }
            }
        }

        // When a player connects to the server, send them the list of all portals.
        [HarmonyPatch(typeof(ZDOMan), "AddPeer")]
        internal class PatchZDOManAddPeer
        {
            private static void Postfix(ZDOMan __instance, ZNetPeer netPeer)
            {
                if (ZNet.instance.IsServer())
                {
                    foreach (ZDO zdo in knownPortals)
                    {
                        __instance.ForceSendZDO(netPeer.m_uid, zdo.m_uid);
                    }
                }
            }
        }

        // Check if the tag matches the regex for a server.
        public static bool PortalTagIsToServer(string tag)
        {
            return Regex.IsMatch(tag, PortalTagRegex);
        }

        // Convert a portal tag to a ServerInfo struct.
        public static ServerInfo? PortalTagToServerInfo(string tag)
        {

            var match = Regex.Match(tag, PortalTagRegex);
            if (match.Success)
            {
                return new ServerInfo(
                    match.Groups["SourceTag"].Value,
                    match.Groups["Server"].Value,
                    ushort.Parse("0" + match.Groups["Port"].Value),
                    match.Groups["TargetTag"].Value
                );
            }
            return null;
        }

    }
}
