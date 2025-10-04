using System;
using System.Net;
using System.Text.RegularExpressions;

namespace Lunarbin.Valheim.CrossServerPortals;

public struct TeleportInfo
{
    public string Address;
    public ushort Port;
    public PortalType Type = PortalType.Server;
    public string SourceTag;
    public string TargetTag;

    public enum PortalType
    {
        World,
        Server
    }

    private static string PortalTagRegex =
        @"^(?<SourceTag>[A-z0-9]+)\|(?<Server>(world:)?[A-z0-9\.]+):?(?<Port>[0-9]+)?\|?(?<TargetTag>[A-z0-9]+)?$";

    public TeleportInfo(string sourceTag, string address, PortalType type, ushort port = 0, string targetTag = "")
    {
        Address = address;
        Port = port;
        Type = type;
        SourceTag = sourceTag;
        TargetTag = targetTag;
    }
    
    public TeleportInfo(string sourceTag, string address, PortalType type, string targetTag = "")
    {
        Address = address;
        Port = 0;
        Type = type;
        TargetTag = targetTag;
        SourceTag = sourceTag;
    }

    public static bool IsAddressValid(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        if (IPAddress.TryParse(address, out var ipAddr))
        {
            return true;
        }
        
        return true;
    }

    public static bool PortalTagIsTeleportInfo(string portalTag)
    {
        TeleportInfo? serverInfo = ParsePortalTag(portalTag);
        if (serverInfo == null) return false;

        // If this is to a world, it's valid if it's not an empty string.
        if (serverInfo?.Type == PortalType.World && !string.IsNullOrWhiteSpace(serverInfo?.Address))
        {
            return true;
        }

        // If it's to a server, it's valid if the address is valid.
        return IsAddressValid(serverInfo?.Address);
    }

    /// <summary>
    /// Takes a portalTag and tries to convert it to ServerInfo. Returns null if tag format does not match.
    /// </summary>
    /// <param name="portalTag"></param>
    /// <returns></returns>
    public static TeleportInfo? ParsePortalTag(string portalTag)
    {
        var match = Regex.Match(portalTag, PortalTagRegex);
        if (match.Success)
        {
            string addr = match.Groups["Server"].Value;
            PortalType type = PortalType.Server;
            
            if (addr.ToLower().StartsWith("world:") || addr.ToLower() == "world:")
            {
                type = PortalType.World;
                addr = addr.Substring("world:".Length);
            }
                        
            return new TeleportInfo(
                match.Groups["SourceTag"].Value,
                addr,
                type,
                ushort.Parse("0" + match.Groups["Port"].Value),
                match.Groups["TargetTag"].Value
            );
        }

        return null;
    }
}