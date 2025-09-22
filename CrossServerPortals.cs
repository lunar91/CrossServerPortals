using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnityEngine;
using ServerSync;
using UnityEngine.SceneManagement;

namespace Lunarbin.Valheim.CrossServerPortals
{
    [BepInPlugin("lunarbin.games.valheim", "Valheim Cross Server Portals", "1.0.2")]
    public class CrossServerPortals : BaseUnityPlugin
    {
        // Regex sourceTag|server:port|targetTag
        // port and targetTag are optional
        private static string PortalTagRegex =
            @"^(?<SourceTag>[A-z0-9]+)\|(?<Server>(world:)?[A-z0-9\.]+):?(?<Port>[0-9]+)?\|?(?<TargetTag>[A-z0-9]+)?$";

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
            CurrentVersion = "1.0.2",
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

        enum PortalType
        {
            Stone,
            Wood,
            Other
        }

        private static TeleportWorld _portal_stone;
        private static TeleportWorld _portal_wood;
        private static TeleportWorld _portal;

        private static TeleportWorld[] _modified;

        private static bool IsModified(TeleportWorld portal)
        {
            if (portal == null || _modified == null || _modified.Length == 0) return false;
            for (int i = 0; i < _modified.Length; i++)
                if (ReferenceEquals(_modified[i], portal) || _modified[i] == portal)
                    return true;
            return false;
        }

        private static void Modify(TeleportWorld portal)
        {
            if (portal == null) return;
            if (IsModified(portal)) return;

            if (_modified == null || _modified.Length == 0)
            {
                _modified = new[] { portal };
                return;
            }

            var old = _modified;
            var len = old.Length;
            var next = new TeleportWorld[len + 1];
            for (int i = 0; i < len; i++) next[i] = old[i];
            next[len] = portal;
            _modified = next;
        }

        private static void Unmodify(TeleportWorld portal)
        {
            if (portal == null || _modified == null || _modified.Length == 0) return;

            int index = -1;
            for (int i = 0; i < _modified.Length; i++)
            {
                if (ReferenceEquals(_modified[i], portal) || _modified[i] == portal)
                {
                    index = i;
                    break;
                }
            }

            if (index == -1) return; // not found

            int newLen = _modified.Length - 1;
            if (newLen == 0)
            {
                _modified = null;
                return;
            }

            var next = new TeleportWorld[newLen];
            for (int i = 0, j = 0; i < _modified.Length; i++)
            {
                if (i == index) continue;
                next[j++] = _modified[i];
            }

            _modified = next;
        }

        // Finds any UnityEngine.Object by name, including disabled/prefabs/assets (editor + runtime for loaded assets)
        public static T FindAnyObjectByName<T>(string name) where T : Object
        {
            foreach (var obj in Resources.FindObjectsOfTypeAll<T>())
                if (obj != null && obj.name == name)
                    return obj;
            return null;
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

        private static TeleportWorld GetDefaultPortal(TeleportWorld portal)
        {
            switch (GetPortalType(portal))
            {
                case PortalType.Stone:
                    if (!_portal_stone)
                    {
                        _portal_stone = FindAnyObjectByName<TeleportWorld>("portal_stone");
                    }

                    return _portal_stone;
                case PortalType.Wood:
                    if (!_portal_wood)
                    {
                        _portal_wood = FindAnyObjectByName<TeleportWorld>("portal_wood");
                    }

                    return _portal_wood;
                default:
                    if (!_portal)
                    {
                        _portal = FindAnyObjectByName<TeleportWorld>("portal");
                    }

                    return _portal;
            }
        }

        // Reset a portal's colors to the default portal colors.
        private static void ResetPortalColors(TeleportWorld portal)
        {
            if (!IsModified(portal))
            {
                return;
            }

            Unmodify(portal);
            var type = GetPortalType(portal);
            var defaultPortal = GetDefaultPortal(portal);
            if (!defaultPortal)
            {
                Debug.Log($"Could not find defualt portal for {portal.name}");
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
        private static void SetPortalColors(TeleportWorld portal, bool useDefaultColors)
        {
            var target_found_red = portal.transform.Find("_target_found_red");
            if (!target_found_red)
                return;

            Modify(portal);
            var particle_system = target_found_red.transform.Find("Particle System");

            var blue_flames = particle_system.transform.Find("blue flames").GetComponent<ParticleSystem>();
            var black_suck = particle_system.transform.Find("Black_suck").GetComponent<ParticleSystem>();
            var suck_particles = particle_system.transform.Find("suck particles").GetComponent<ParticleSystem>();

            var point_light = target_found_red.transform.Find("Point light").GetComponent<Light>();


            if (portal.gameObject.name == "portal_stone(Clone)")
            {
                portal.m_colorTargetfound = customPortalGlyphColor.Value * 12f;
            }
            else
            {
                portal.m_colorTargetfound = customPortalGlyphColor.Value * 4f;
            }


            blue_flames.startColor = customPortalEffectColor.Value;
            blue_flames.customData.SetColor(ParticleSystemCustomData.Custom1, customPortalEffectColor.Value);
            blue_flames.customData.SetColor(ParticleSystemCustomData.Custom2, customPortalEffectColor.Value);
            Color customColorWithAlpha = customPortalEffectColor.Value;
            customColorWithAlpha = customColorWithAlpha * 0.1f;
            customColorWithAlpha.a = 0.1f;
            black_suck.startColor = customColorWithAlpha;
            suck_particles.startColor = customPortalEffectColor.Value;

            point_light.color = customPortalEffectColor.Value;
        }

        // Patch Player.Load to apply saved status effects on load, AFTER load has finished.
        [HarmonyPatch(typeof(Player), "Load")]
        private static class PatchPlayerLoad
        {
            private static void Postfix(Player __instance)
            {
                if (preserveStatusEffects.Value && StatusEffects.Count > 0)
                {
                    foreach (var se in StatusEffects)
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

        ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true) =>
            config(group, name, value, new ConfigDescription(description), synchronizedSetting);


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


            requireAdminToRename = config<bool>("General",
                "RequireAdminToRename",
                false,
                "Require admin permissions to rename cross server portals.");

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
                foreach (var se in Player.m_localPlayer.GetSEMan().GetStatusEffects())
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
                    ResetPortalColors(__instance);
                    return true;
                }

                SetPortalColors(__instance, false);

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
                    ResetPortalColors(__instance);
                    return true;
                }

                SetPortalColors(__instance, false);

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


                string portalName = __instance.GetText();
                if (portalName.Contains("|"))
                {
                    if (requireAdminToRename.Value)
                    {
                        if (ZNet.instance.LocalPlayerIsAdminOrHost())
                        {
                            // nothing
                        }
                        else
                        {
                            human.Message(MessageHud.MessageType.Center, "$piece_noaccess");

                            return false;
                        }
                    }
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
                        //ServerJoinDataDedicated joinDataDedicated = new ServerJoinDataDedicated(ServerToJoin?.Address, (ushort)(ServerToJoin?.Port));
                        ServerJoinData joinData =
                            new ServerJoinData(new ServerJoinDataDedicated(ServerToJoin?.Address,
                                (ushort)(ServerToJoin?.Port)));

                        FejdStartup.instance.SetServerToJoin(joinData);
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