using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Lunarbin.Valheim.CrossServerPortals
{
    [BepInPlugin("lunarbin.games.valheim", "Valheim Cross Server Portals", "0.1.0.0")]
    public class CrossServerPortals : BaseUnityPlugin
    {
        // Regex sourceTag|server:port|targetTag
        // port and targetTag are optional
        private static string PortalTagRegex = @"^(?<SourceTag>[A-z0-9]+)\|(?<Server>[A-z0-9\.]+):?(?<Port>[0-9]+)?\|?(?<TargetTag>[A-z0-9]+)?$";
        public static ServerInfo? ServerToJoin = null;
        public static bool TeleportingToServer = false;
        public static bool HasJoinedServer = false;
        public static float PortalExitDistance = 0.0f;

        public static string portalPrefab = "portal_wood";
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

        private readonly Harmony harmony = new Harmony("lunarbin.games.valheim");

        private void Awake()
        {
            harmony.PatchAll();
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
            private static Color defaultColor = Color.white;
            private static bool Prefix(ref bool __result, TeleportWorld __instance)
            {
                if (defaultColor == Color.white && __instance.m_colorTargetfound != Color.green)
                    defaultColor = __instance.m_colorTargetfound;

                string text = __instance.GetText();
                if (!PortalTagIsToServer(text))
                {

                    __instance.m_colorTargetfound = defaultColor;
                    return true;
                }

                __instance.m_colorTargetfound = Color.green;
                __result = true;
                return false;
            }
        }

        // Patch TeleportWorld.TargetFound
        // This makes it so the portal is usable and glows.
        [HarmonyPatch(typeof(TeleportWorld), "TargetFound")]
        internal class PatchTeleportWorldTargetFound
        {
            private static Color defaultColor = Color.white;
            private static bool Prefix(ref bool __result, TeleportWorld __instance)
            {
                if (defaultColor == Color.white && __instance.m_colorTargetfound != Color.green)
                    defaultColor = __instance.m_colorTargetfound;

                string text = __instance.GetText();
                if (!PortalTagIsToServer(text))
                {

                    __instance.m_colorTargetfound = defaultColor;
                    return true;
                }

                __instance.m_colorTargetfound = Color.green;
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
        internal class PatchFejdStartupStart
        {
            private static void Postfix()
            {
                if (ServerToJoin != null && TeleportingToServer && !HasJoinedServer)
                {
                    //Player.m_localPlayer.Message(MessageHud.MessageType.Center, $"Connecting to {ServerToJoin?.Address}:{ServerToJoin?.Port}");
                    ZSteamMatchmaking.instance.QueueServerJoin($"{ServerToJoin?.Address}:{ServerToJoin?.Port}");
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
                    List<ZDO> zdos = new();
                    int index = 0;
                    ZDOMan.instance.GetAllZDOsWithPrefabIterative(portalPrefab, zdos, ref index);
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
                    List<ZDO> portals = new();

                    int index = 0;
                    while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(portalPrefab, portals, ref index))
                    {
                        yield return null;
                    }

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
                    ushort.Parse(match.Groups["Port"].Value ?? "0"),
                    match.Groups["TargetTag"].Value
                );
            }
            return null;
        }

    }
}
