**Raid Tracker** tracks explosions (C4, Satchel Charges, and Rockets) in the event that an admin needs to investigate a raid for suspicious activity. Detailed reports include an on-screen drawing of arrows from where the explosive was thrown to the entity the explosive detonated on and the attacker's name.

Each explosion is colour coded to represent the attacker's name in chat and on screen for ease of reading. If the number of explosions exceeds the configured amount then X's will be drawn in place of the attacker's name to reduce clutter.

A config is available for server owners to setup the plugin to their liking. Includes support for Discord, Discord Messages, Popup Notifications, and Slack plugins.

## Admin Chat Commands

- `/x [radius]` -- Show explosions within X radius
- `/x del [player id]` -- Remove explosion data for specified player ID
- `/x delm` -- Remove explosion data within configured radius
- `/x delbefore [date]` -- Remove explosion data before specified date
- `/x delafter [date]` -- Remove explosion data after specified date
- `/x help` -- Show available help information for this command
- `/x wipe` -- Wipe explosion data

**Note:** Auth level of 1  is required to use the `/x` command.

## Player Chat Commands

- `/px` -- Show all explosions in range *(default 50m)*
- `/px [distance]` -- Show explosions from a further distance
- `/px help` -- Show available help information for this command

**Note:** The `/px` command is only available if the `Show Explosions` or `Allow DDRAW` are set to `true`.

## Permissions

- `raidtracker.see` -- Players can see explosion broadcasts or popups if enabled
- `raidtracker.use` -- Players with the permission cannot see their own explosions. Command: `/px`

**Note:** The `raidtracker.use` permission is only available if the `Show Explosions` or `Allow DDRAW` settings are set to `true`.

## Configuration

```
Additional Tracking:
    Track Beancan Grenades -> false
    Track F1 Grenades -> false
    Track Explosive Ammo -> false
    Track Explosive Ammo Time -> 30
    Track Explosive Deaths Only -> true
    Track Supply Signals -> false
    Track Eco Raiding -> false (track all raiding done to building parts. Such as pickaxe raiding a foundation.)

Discord:
    Send Notifications -> false -> Send explosion messages to configured channel in the Discord plugin

DiscordMessages:
    Message - Embed Color (DECIMAL): 3329330
    Message - Webhook URL -> https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks

Players:
    Allow DDRAW -> false -> Facepunch removed access to DDRAW for players. This setting temporarily gives the player admin access to use DDRAW long enough to draw on their screen all explosions affecting the player by using the /px command.
    Command Name -> px -> The command players must use to access the plugin. This limits the amount of information sent to the player based on entities which they own, instead of all entities.
    Limit Command Once Every X Seconds -> 60 seconds -> The number in seconds a player must wait before using the command for DDRAW.
    Permission Name -> raidtracker.use -> Players must have this permission to use the /px command. This permission will not exist unless Allow DDRAW or Show Explosions is true.
    Show Explosions -> false -> Enables or disables players from using this plugin.
    Show Explosions Within X Meters -> 50.0

PopupNotifications:
    Duration -> 0.0 -> The duration in which popup's are shown. Setting this value to 0.0 will use the default value of 8 seconds from the PopupNotifications plugin.
    Use Popups -> false

Settings:
    Allow Manual Deletions and Wipe of Explosion Data -> true -> Allow command line access to alter or wipe the database.
    Apply Retroactive Deletion Dates When Days Before Delete Is Changed -> true
    Apply Retroactive Deletion Dates When Days To Delete Is Changed" -> true
    Apply Retroactive Deletion Dates When No Date Is Set -> true
    Auth Level -> 1 -> The required auth level for admins to use the plugin
    Authorized List": [] -> Only steam64 ID's in this list will be able to use this plugin if configured
    Automatically Delete Each Explosion X Days After Being Logged -> 7
    Color By Weapon Type Instead Of By Player Name -> false
    Delete Radius -> 50
    Draw Arrows -> true
    Explosions Command -> x -> Admin command to access plugin
    Max Explosion Messages To Player -> 10
    Max Names To Draw -> 25 -> The amount of names to draw on screen. Configuring a limit helps prevent names from overlapping one another.
    Output Detailed Explosion Messages To Client Console -> true -> Print additional information to the client's console
    Print Explosions To Server Console -> true - Show explosions in server console
    Print Milliseconds Taken To Track Explosives In Server Console -> false -> Show the amount of time taken to track each explosion
    Show Explosion Messages To Player -> true -> Allow players to see detailed explosion messages when using the explosions command if Players -> Show Explosions is true
    Show Explosions Within X Meters -> 50
    Time In Seconds To Draw Explosions -> 60.0
    Wipe Data When Server Is Wiped (Recommended) -> true -> Wipe database when server is wiped

Slack:
    Channel -> general
    Message Style (FancyMessage, SimpleMessage, Message, TicketMessage) -> FancyMessage
    Send Notifications -> false -> Use Slack plugin
```

## Localization

## Credits
Original plugin (v1): nivex

Rewrite of plugin (v2): Clearshot
