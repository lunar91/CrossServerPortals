# Cross Server Portals
Connect your servers together with Cross-Server Portals.

## Installation Guide
Add the CrossServerPortals.dll to your `BepInEx/plugins` folder on both client and server.

## Usage
Update any portal's tag to the following format: 

`SourceTag|Server:Port|TargetTag` 
or 
`SourceTag|world:WorldName|TargetTag`
or
`SourceTag|Server:Port`

 - **SourceTag**: The Tag used to identify this portal.
 - **TargetTag**: (optional) The portal tag to search for when teleporting to the other server. If this tag is found either as a SourceTag or the full tag on a portal, you will come out through that portal. *This feature only works if the mod is also installed on the server.*
 - **Server:Port**: The Address/Hostname of the server and the port.
 - **world:WorldName**: world: followed by the name of the locally saved world.

 Examples:
 -  `homebase|127.0.0.1:2456|worldspawn`
 -  `bestbase|127.0.0.1:2460`
 - `stonehenge|127.0.0.1`
 - `worldspawn|world:HubWorld|riverbase`

 After updating the portal's tag, the portal will change color to indicate it is a cross-server portal. Going through the portal currently *ignores* all teleportation restrictions.

## Configuration

 - **PreserveStatusEffects** - Defaults to true. Preserve status effects (such as wet, rested, etc.) when teleporting between servers. If the game is closed, the effects are lost. You must use a cross-server portal to preserve the status effect.
 - **RecolorPortalGlyphs** - Toggles whether portal glyphs are recolored
 - **CustomPortalGlyphColor** - The RGBA hex color code to use for portal glyphs
 - **RecolorPortalEffects** - Toggles whether portal effects are recolored
 - **CustomPortalEffectColor** - The RGBA hex color code to use for portal effects.
 - **PromptBeforeTeleport** - When true, the user will be prompted before any cross-server teleport.
 - **RequireAdminToRename** - Require Admin access to rename a Cross Server Portal. *Note that this will require admin to rename any portal with "|" in the tag* This configuration is synchronized with the server via ServerSync.

## Planned Features
 - Validate Address:Port before making portal active.

## Credits
- [Wacky-Mole](https://github.com/Wacky-Mole) - Code for Requiring Admin to rename portals, and for the push toward ServerSync.
- [ServerSync](https://github.com/blaxxun-boop/ServerSync) - Library used to provide Server-Authoritative settings.

## Changelog
 - **1.2.0** - Added optional prompt before teleporting between servers (disabled by default). Added teleporting HUD animation when doing cross-server teleports.
 - **1.1.3** - Fixed Packaging regression in 1.1.2
 - **1.1.2** - Rewrote Admin Permission Checks. Fixed reconnect loop on failure to connect.
 - **1.1.1** - Fixed a version mishap.
 - **1.1.0** - Fixed Admin Permission Synchronization. Fixed visual bug introduced in Valheim v0.221.4.
 - **1.0.0** - Added RequireAdminToRename config (thank you, [Wacky-Mole](https://github.com/Wacky-Mole)) Integrated ServerSync to synchronize this config with server.
 - **0.3.0** - Improved support for Ashlands. Added configurable colors. Added support for single player worlds.
 - **0.2.1** - Updated packaged DLL
 - **0.2.0** - Added option (on by default) to preserve status effects across worlds.
 - **0.1.1** - Updated to work with Valheim v0.217.24
 - **0.1.0.0** - Initial Release


