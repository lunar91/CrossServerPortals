# Cross Server Portals
Connect your servers together with Cross-Server Portals.

## Installation Guide
Add the CrossServerPortals.dll to your `BepInEx/plugins` folder on both client and server.

## Usage
Update any portal's tag to the following format: `SourceTag|Server:Port|TargetTag`

 - **SourceTag**: The Tag used to identify this portal.
 - **TargetTag**: The portal tag to search for when teleporting to the other server. If this tag is found either as a SourceTag or the full tag on a portal, you will come out through that portal. *This feature only works if the mod is also installed on the server.*
 - **Server:Port**: The Address/Hostname of the server and the port.

 Examples:
 `homebase|127.0.0.1:2456|worldspawn`
 `bestbase|127.0.0.1:2460`
 `stonehenge|127.0.0.1`

 After updating the portal's tag, the portal will change color to indicate it is a cross-server portal. Going through the portal currently *ignores* all teleportation restrictions.

## Configuration
There is currently no configuration.

## Planned Features
 - Prompt users before switching servers.
 - Validate Address:Port before making portal active.
 - Support for SinglePlayer worlds

## Changelog
 - **0.1.1** - Updated to work with Valheim v0.217.24
 - **0.1.0.0** - Initial Release


