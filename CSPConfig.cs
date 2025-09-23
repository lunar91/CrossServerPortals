using BepInEx.Configuration;
using ServerSync;
using UnityEngine;

namespace Lunarbin.Valheim.CrossServerPortals;

public static class CSPConfig
{
    public static ConfigEntry<bool> preserveStatusEffects;
    public static ConfigEntry<bool> recolorPortalGlyphs;
    public static ConfigEntry<bool> recolorPortalEffects;
    public static ConfigEntry<Color> customPortalGlyphColor;
    public static ConfigEntry<Color> customPortalEffectColor;
    public static ConfigEntry<bool> requireAdminToRename;
    public static ConfigFile Config;
    private static ConfigEntry<bool> lockAdminConfig;
    
    // Synchronize Server Config
    private static ServerSync.ConfigSync configSync = new ServerSync.ConfigSync("lunarbin.games.valheim")
    {
        DisplayName = "Cross Server Portals",
        CurrentVersion = BuildInfo.Version,
        MinimumRequiredVersion = "1.1.0"
    };

    public static void Init(ConfigFile config)
    {
        Config = config;
        
        // Config for preserving status effects while switching servers.
        preserveStatusEffects = config<bool>("General",
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
            true,
            "Require admin permissions to rename cross server portals.");

        lockAdminConfig = config<bool>("Server", "LockAdminConfig", true, "Prevent this config from being changed.");
        
        configSync.AddLockingConfigEntry(lockAdminConfig);
    }   
    
    static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    static ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true) =>
        config(group, name, value, new ConfigDescription(description), synchronizedSetting);


}