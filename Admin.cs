using HarmonyLib;

namespace Lunarbin.Valheim.CrossServerPortals;

public static class Admin
{
    public static bool IsAdmin { get; private set; }= false;
    private static bool IsChecking = false;

    /// <summary>
    /// Check if we have admin permissions.
    /// </summary>
    public static void Check()
    {
        if (IsChecking) return;
        
        if ((bool)ZNet.instance)
        {
            IsChecking = true;
            if (ZNet.instance.IsServer())
            {
                IsAdmin = true;
                IsChecking = false;
            }
            else
            {
                ZNet.instance.Unban("admincheck");
            }
            
        }
    }

    /// <summary>
    /// Reset the state of admin permissions.
    /// </summary>
    public static void Reset()
    {
        IsAdmin = false;
        IsChecking = false;
    }
    
    /// <summary>
    /// Verify if the user is admin by parsing the console command response text.
    /// Behavior mimicked from ServerDevCommands mod.
    /// If text is "Unbanning user admincheck" then they are an admin.
    /// If text is "You are not admin." then they are not admin.
    /// </summary>
    /// <param name="text"></param>
    public static void Verify(string text)
    {
        if (text == "Unbanning user admincheck")
        {
            IsAdmin = true;
        }

        if (text == "You are not admin")
        {
            IsAdmin = false;
        }

        IsChecking = false;
    }

    /// <summary>
    /// Patch the ZNet.RPC_RemotePrint to pass the text back to Admin.Verify
    /// </summary>
    [HarmonyPatch(typeof(ZNet), "RPC_RemotePrint")]
    internal class ZNet_RPC_RemotePrint
    {
        private static bool Prefix(ZNet __instance, string text)
        {
            // If we aren't currently checking admin permission, do nothing.
            if (!Admin.IsChecking)
            {
                return true;
            }
            // Otherwise, verify the RPC print text
            Admin.Verify(text);
            return false;
        }
    }

    /// <summary>
    /// When the player spawns, check if they are admin.
    /// </summary>
    [HarmonyPatch(typeof(Player), "OnSpawned")]
    internal class Player_OnSpawned
    {
        private static void Postfix()
        {
            Admin.Check();
        }
    }

    [HarmonyPatch(typeof(Game), "Awake")]
    internal class GameAwake
    {
        private static void Postfix()
        {
            Admin.Reset();
        }
        
    }
}