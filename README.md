**Raid Tracker** tracks raids by explosions, weapons, ammo and fire in the event that an admin needs to investigate a raid for suspicious activity. Detailed reports include an on-screen drawing of arrows from where the attacker was standing to the entity that was damaged or destroyed with the attacker's name, team and weapon.

Basic filtering is available to limit the number of events shown on-screen when there is a large amount of raid events near by. Each raid event is color coded by player, team or weapon depending on the type of filter used.

## Upgrading from v1 to v2

Raid Tracker v2 has been completely redesigned from scratch and is **not** compatible with any configuration or data files from Raid Tracker v1.

**Before installing Raid Tracker v2 backup or delete all Raid Tracker v1 files and folders from `/oxide/config`, `/oxide/data`, `/oxide/lang` and `/oxide/plugins`.**

- `/oxide/config/RaidTracker.json`
- `/oxide/data/RaidTracker.json`
- `/oxide/data/RaidTracker/`
- `/oxide/logs/RaidTracker/`
- `/oxide/lang/en/RaidTracker.json`
- `/oxide/plugins/RaidTracker.cs`

## Permissions

- All commands require auth level 1 permission
- `raidtracker.wipe` -- Allow admins to wipe raid events
- `raidtracker.px` -- Allow players to view raid events

## Discord

Discord webhook alerts are supported without any additional plugins. Simply set `discord.webhookURL` to your discord webhook url and enable `notifyDiscord` for the trackers you would like to recieve alerts for. Reload the plugin for the config changes to take effect.

There are 2 different types of discord webhook messages available to use. The default is a detailed embed style message and the other is a simple text message style that can be enabled with the `discord.simpleMessage.enabled` config option.

#### Embed message
![discord webhook embed](https://i.imgur.com/apTMXFH.png "Discord Webhook Embed")

#### Simple message
```
Clearshot[7656119] is raiding Clearshot[7656119] ~ C4 -> attached to Sheet Metal Door (door.hinged.metal) @ M23 (teleportpos -286.3,10.4,-1373.9)
```

#### Webhook message keys

- "attackerName"
- "attackerSteamID"
- "attackerTeamName"
- "victimName"
- "victimSteamID"
- "victimTeamName"
- "weaponName"
- "weaponShortname"
- "weaponItemShortname"
- "entityItemName"
- "entityShortname"
- "entityItemShortname"
- "raidEventIndex"
- "raidTrackerCategory"
- "raidEventType"
- "gridPos"
- "teleportPos"

## Raid Events

All raids logged are called a "Raid Event" which contains detailed info about the type of raid, where a raid happened and who raided.

**There are 5 event types that can cause a raid event to be logged:**

1. Destroyed (entity_death_weapon, entity_death_ammo)
2. Burnt (entity_death_fire)
3. Attached To Entity (entity_collision)
4. Hit Entity (entity_collision)
5. Always Logged Tracker (entity_collision)

**Raid Event**

```json
{
  "attackerName": "Attacker Name",
  "attackerSteamID": 7656119123,
  "attackerTeamID": 0,
  "victimSteamID": 7656119456,
  "weapon": "explosive.satchel.deployed[entity_collision]",
  "hitEntity": "EVENT.ATTACHED door.hinged.metal",
  "startPos": "(-336.8, 61.2, 245.9)",
  "endPos": "(-335.5, 61.8, 240.7)",
  "timestamp": "2022-08-04T22:53:26.05157Z"
}
```

## Admin Chat Commands

**All commands require auth level 1 permission**

- `/x help` -- raid tracker command help
- `/x <radius>` -- show all raid events within *\<radius\>* (default 50m)
- `/x extra` -- toggle extra info mode
- `/x wipe <radius>` -- wipe all raid events within *\<radius\>* ( perm: `raidtracker.wipe` )
- `/x last` -- Re-run last command
- `/x <filterType> <filter> <radius>` -- basic filtering, args depend on type of filter used
  - `/x time <hrs> <radius>` -- show all raid events near by over the past *\<hrs\>* within *\<radius\>*
  - `/x weapon <partial weapon shortname or item name> <radius>` -- show all raid events within *\<radius\>* filtered by weapon
  - `/x entity <partial entity shortname> <radius>` -- show all raid events within *\<radius\>* filtered by entity
  - `/x team <team id> <radius>` -- show all raid events within *\<radius\>* filtered by team
  - `/x player <steam id or partial name> <radius>` -- show all raid events within *\<radius\>* filtered by player
- `/re <event id>` -- print info about a raid event, each event has an ID

## Player Chat Commands

**All player commands require `raidtracker.px` permission**

- `/px <radius>` -- show all raid events within *\<radius\>* (default 50m)

## Configuration

- `debug` -- debug mode
- `chatIconID` -- steamid to use for chat icon
- `deleteDataOnWipe` -- delete all raid event logs on wipe
- `daysBeforeDelete` -- number of days to save a raid event before it is deleted
- `searchRadius` -- default raid event search radius
- `drawDuration` -- duration to display on-screen visuals in seconds
- `ignoreBuildingGrades` -- ignore raids based on building grade (TopTier = HQM)
- `ignoreSameOwner` -- ignore raids by the same owner
- `ignoreTeamMember` -- ignore raids of team members
- `ignoreClanMemberOrAlly` -- ignore raids of team members or allies
- `enableNewTrackers` -- automatically enable new weapon config entries added to the tracker config list
- `printToClientConsole` -- print chat messages to client F1 console
- `playerViewExplosionsCommand` -- player view explosion command config options
  - `drawAttackerName` -- draw attackers name
  - `ignoreRaidEventsLessThanMinutes` -- ignore recent raid events to prevent abuse (players can abuse /px command to see where attacking player is during raid)
- `notificationCooldown` -- cooldown for all notification types if same attacking player
  - `enabled` -- enable global notification cooldown
  - `cooldown` -- notification cooldown per attacking player in seconds
- `discord` -- discord webhook config, trackers must have `notifyDiscord` enabled
  - `webhookURL` -- discord webhook url
  - `simpleMessage` - discord simple message options
  - `embed` -- discord embed message options
- `trackers` -- list of tracker weapon configs
- `eventTypes` -- list of event type translations (used with discord and server console messages)

```json
{
  "debug": false,
  "chatIconID": 76561199278762587,
  "deleteDataOnWipe": true,
  "daysBeforeDelete": 7.0,
  "searchRadius": 50.0,
  "drawDuration": 30.0,
  "ignoreBuildingGrades": {
    "Twigs": true,
    "Wood": false,
    "Stone": false,
    "Metal": false,
    "TopTier": false
  },
  "ignoreSameOwner": true,
  "ignoreTeamMember": true,
  "ignoreClanMemberOrAlly": true,
  "enableNewTrackers": true,
  "printToClientConsole": true,
  "playerViewExplosionsCommand": {
    "drawAttackerName": false,
    "ignoreRaidEventsLessThanMinutes": 30.0
  },
  "notificationCooldown": {
    "enabled": false,
    "cooldown": 300.0
  },
  "discord": {
    "webhookURL": "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
    "simpleMessage": {
      "enabled": false,
      "message": "{attackerName}[{attackerSteamID}] is raiding {victimName}[{victimSteamID}] ~ {weaponName} -> {raidEventType} {entityItemName} ({entityShortname}) @ {gridPos} (teleportpos {teleportPos})"
    },
    "embed": {
      "title": "{attackerName} is raiding {victimName} @ {gridPos}",
      "thumbnail": {
        "url": "https://www.rustedit.io/images/imagelibrary/{weaponItemShortname}.png"
      },
      "fields": [
        {
          "name": "Weapon",
          "value": "{weaponName} ({raidTrackerCategory} / {weaponShortname})",
          "inline": false
        },
        {
          "name": "Entity",
          "value": "{raidEventType} {entityItemName} ({entityShortname})",
          "inline": false
        },
        {
          "name": "Attacker",
          "value": "{attackerName} \n[Steam Profile](https://steamcommunity.com/profiles/{attackerSteamID}) ({attackerSteamID})\n[SteamID.uk](https://steamid.uk/profile/{attackerSteamID})\n\n**Attacker Team**\n{attackerTeamName}",
          "inline": true
        },
        {
          "name": "Victim",
          "value": "{victimName} \n[Steam Profile](https://steamcommunity.com/profiles/{victimSteamID}) ({victimSteamID})\n[SteamID.uk](https://steamid.uk/profile/{victimSteamID})\n\n**Victim Team**\n{victimTeamName}",
          "inline": true
        },
        {
          "name": "Location",
          "value": "{gridPos} - teleportpos {teleportPos}",
          "inline": false
        }
      ],
      "footer": {
        "text": "Raid Tracker {0} by Clearshot",
        "icon_url": "https://i.imgur.com/DluJ5X5.png"
      }
    }
  },
  "trackers": {
    "_global": {
      "*": {
        "enabled": false,
        "name": "Enable all trackers in every category",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      }
    },
    "entity_collision": {
      "*": {
        "enabled": false,
        "name": "Enable all 'entity_collision' trackers",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      },
      "rocket_basic": {
        "enabled": true,
        "name": "Rocket",
        "hexColor": "#8800FF",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      }
    },
    "entity_death_ammo": {
      "*": {
        "enabled": false,
        "name": "Enable all 'entity_death_ammo' trackers",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      },
      "ammo.rifle.explosive": {
        "enabled": true,
        "name": "Explosive 5.56 Rifle Ammo",
        "hexColor": "#808080",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      }
    },
    "entity_death_fire": {
      "*": {
        "enabled": false,
        "name": "Enable all 'entity_death_fire' trackers",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      },
      "fireball_small_arrow": {
        "enabled": true,
        "name": "Small Arrow Fireball",
        "hexColor": "#FF8C24",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      }
    },
    "entity_death_weapon": {
      "*": {
        "enabled": false,
        "name": "Enable all 'entity_death_weapon' trackers",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      },
      "rifle.ak": {
        "enabled": true,
        "name": "Assault Rifle",
        "hexColor": "#C5D52D",
        "notifyConsole": false,
        "notifyAdmin": false,
        "notifyDiscord": false,
        "logToFile": false
      }
    }
  },
  "eventTypes": {
    "EVENT.ATTACHED": "attached to",
    "EVENT.BURNT": "burnt",
    "EVENT.DESTROYED": "destroyed",
    "EVENT.HIT": "hit",
    "EVENT.NO_HIT": "no hit"
  }
}
```
### Trackers

There are 4 tracker categories to log different types of raid events. Some weapons can appear in multiple categories and can be enabled across multiple categories but will result in a larger data file. For example, rockets are seen in the `entity_collision` and `entity_death_weapon` category. If both trackers are enabled, with `entity_collision` you will see where the attacking player fired the rocket from and where the rocket hit. With `entity_death_weapon` you will see every single entity the rocket destroyed.

Certain ammo types (ammo.rocket, ammo.grenadelauncher) will appear in the `entity_death_weapon` category instead of `entity_death_ammo` due to how rust handles these types of projectiles. **The config is automatically generated with the correct items in each category, do not move them around thinking there is an error in the config.**

**1. entity_collision**

* Logs all deployed explosives that collide or attach to player owned entities. (Rocket, MLRS, C4, Satchel)

**2. entity_death_ammo**

* Logs player entities that have been destroyed by certain ammo types. (Explosive ammo, Fire arrow, Incendiary ammo)

**3. entity_death_fire**

* Logs player entities that have been destroyed by fire or fireballs. (Fire arrow fireball, Shotgun fireball)

**4. entity_death_weapon**

* Logs player entities that have been destroyed by certain weapons. (Guns, Melee weapons)

### Weapon Config

- `key` -- weapon shortname
- `enabled` -- enable tracker
- `name` -- weapon name displayed on-screen
- `hexColor` -- weapon hex color displayed on-screen
- `notifyConsole` -- notify server console of raid event
- `notifyAdmin` -- notify online admins of raid event
- `notifyDiscord` -- notify discord webhook of raid event
- `logToFile` -- log raid event to `/oxide/logs/RaidTracker/raidtracker_raid_events-DATE.txt`

#### Optional Properties

- `alwaysLog` -- always log explosion, even if no entity is hit (`entity_collision` category only)
- `shortArrow` -- draw short arrows to reduce clutter
- `discordIcon` -- override icon url for discord webhook alerts

```json
"ammo.rifle.explosive": {
  "enabled": true,
  "name": "Explosive 5.56 Rifle Ammo",
  "hexColor": "#808080",
  "notifyConsole": false,
  "notifyAdmin": false,
  "notifyDiscord": false,
  "logToFile": false
}
```

### Global Weapon Config

Trackers and notifications can be enabled globally with the global wildcard item `*` under the `_global` tracker category or under a specific tracker category.

**Note: If the global wildcard tracker `*` is enabled individual trackers that are enabled take priority over global settings. When the plugin is first installed a default config is generated with certain trackers already enabled, if you would like to use the global config option first disable all trackers in the default config to avoid them overriding the global config settings!**

**Globally enable all trackers and notifications in every category**
```json
"_global": {
  "*": {
    "enabled": true,
    "name": "Enable all trackers in every category",
    "notifyConsole": true,
    "notifyAdmin": true,
    "notifyDiscord": true,
    "logToFile": true
  }
}
```

**Globally enable trackers and notifications in a specific category**
```json
"entity_collision": {
  "*": {
    "enabled": true,
    "name": "Enable all 'entity_collision' trackers",
    "notifyConsole": true,
    "notifyAdmin": true,
    "notifyDiscord": true,
    "logToFile": true
  }
}
```

## Notification Cooldown

All notification types from the same attacking player can be put on cooldown with the `notificationCooldown.enabled` config option. When enabled, the notification cooldown per attacking player is 5 minutes by default. The cooldown duration can be changed with the `notificationCooldown.cooldownSeconds` config option.

**Note: Trackers with `logToFile` enabled will always be written to the log file regardless of notification cooldown settings.**

```json
"notificationCooldown": {
  "enabled": true,
  "cooldownSeconds": 300
}
```

## Ignoring Entities

You can ignore entities from being logged or ignore them from being sent to discord. After ignoring any entities the plugin will need to be reloaded for the changes to take effect. 

A list of entities is automatically generated and saved to `/oxide/data/RaidTracker/DecayEntityIgnoreList.json` when the plugin is first installed. 

- `name` -- english item name (item shortname)
- `ignore` -- ignore entity logs and notifications when destroyed
- `ignoreDiscord` -- continues to log entity but ignore from being sent to discord if `discord.webhookURL` and tracker `notifyDiscord` are enabled

```json
"assets/prefabs/misc/summer_dlc/abovegroundpool/abovegroundpool.deployed.prefab": {
  "name": "Above Ground Pool (abovegroundpool)",
  "ignore": false,
  "ignoreDiscord": false
}
```

## Localization

```json
{
  "ChatPrefix": "<color=#00a7fe>[Raid Tracker]</color>",
  "RaidEvent.Message": "(RE: {raidEventIndex}) {attackerName}[{attackerSteamID}] is raiding {victimName}[{victimSteamID}] ~ {weaponName} -> {hitEntity} @ {gridPos} (teleportpos {teleportPos})",
  "RaidEvent.PrettyMessage": "<color=#f5646c>{attackerName}[{attackerSteamID}]</color> is raiding <color=#52bf6f>{victimName}[{victimSteamID}]</color> ~ <color={weaponColor}>{weaponName}</color> -> {raidEventType} {entityItemName} ({entityShortname}) @ {gridPos}",
  "ViewEventsCommand.HelpHeader": "<size=16><color=#00a7fe>Raid Tracker</color> Help</size>\n",
  "ViewEventsCommand.HelpDefault": "<size=12><color=#00a7fe>/x <radius></color> - Show all raid events within X radius (default 50m)</size>",
  "ViewEventsCommand.HelpExtraMode": "<size=12><color=#00a7fe>/x extra</color> - Toggle extra info mode</size>",
  "ViewEventsCommand.HelpWipe": "<size=12><color=#00a7fe>/x wipe <radius></color> - Wipe all raid events within <radius></size>",
  "ViewEventsCommand.HelpLast": "<size=12><color=#00a7fe>/x last</color> - Re-run last command</size>",
  "ViewEventsCommand.HelpFilter": "<size=12><color=#00a7fe>/x <filterType> <filter> <radius></color></size>",
  "ViewEventsCommand.HelpFilterTime": "<size=12><color=#00a7fe>/x time <hrs> <radius></color> - Show all raid events near by over the past <hrs> within <radius></size>",
  "ViewEventsCommand.HelpFilterWeapon": "<size=12><color=#00a7fe>/x weapon <partial name or item name> <radius></color> - Show all raid events within <radius> filtered by weapon</size>",
  "ViewEventsCommand.HelpFilterEntity": "<size=12><color=#00a7fe>/x entity <partial entity shortname> <radius></color> - Show all raid events within <radius> filtered by entity</size>",
  "ViewEventsCommand.HelpFilterTeam": "<size=12><color=#00a7fe>/x team <team id> <radius></color> - Show all raid events within <radius> filtered by team</size>",
  "ViewEventsCommand.HelpFilterPlayer": "<size=12><color=#00a7fe>/x player <steam id or partial name> <radius></color> - Show all raid events within <radius> filtered by player</size>",
  "ViewEventsCommand.HelpPrintRaidEvent": "<size=12><color=#00a7fe>/re <event id></color> - Print info about a raid event by event id</size>",
  "ViewEventsCommand.ExtraModeEnabled": "<color=#52bf6f>Extra info mode enabled</color>",
  "ViewEventsCommand.ExtraModeDisabled": "<color=#f5646c>Extra info mode disabled</color>",
  "ViewEventsCommand.WipePermission": "You do not have permission to wipe raid events!",
  "ViewEventsCommand.NotFoundRadius": "No raid events found within <color=#00a7fe>{0}m</color>!",
  "ViewEventsCommand.WipedRaidEventsRadius": "Wiped <color=#00a7fe>{0}</color> raid events within <color=#00a7fe>{1}m</color> at <color=#00a7fe>{2}</color>",
  "ViewEventsCommand.Header": "<size=16><color=#00a7fe>Raid Tracker</color> ~ {0} raid event(s) within {1}m</size>\n",
  "ViewEventsCommand.Filter": "<color=#00a7fe>filter:</color> [{0}, {1}]\n",
  "ViewEventsCommand.Team": "<color={0}> T:{1}</color>",
  "ViewEventsCommand.StartTextExtra": "<size=12>{0}[{1}]{2}</size>",
  "ViewEventsCommand.EndTextExtra": "<size=12><color={0}>X</color> (RE:{1}) {2} <color={3}>-></color> {4}</size>",
  "ViewEventsCommand.StartText": "<size=12>{0}{1}</size>",
  "ViewEventsCommand.EndText": "<size=12><color={0}>X</color> (RE:{1}) {2}</size>",
  "ViewEventsCommand.GroupingCount": "<color={0}>{1} raid event(s) [{2}, {3}]</color>",
  "ViewEventsCommand.WeaponCount": "<color={0}>• {1}x {2} <size=10>({3})</size></color>",
  "ViewEventsCommand.NotFound": "no raid events found!"
}
```

## Logs

**Weapon config log**
- `/oxide/logs/RaidTracker/raidtracker_weapon_config_log.txt` -- Logs when a new weapon config is added to the tracker config list

**DecayEntity ignore list log**
- `/oxide/logs/RaidTracker/raidtracker_decay_entity_log.txt` -- Logs when a new DecayEntity is added to the DecayEntityIgnoreList

**px command error log**
- `/oxide/logs/RaidTracker/raidtracker_px_error_log.txt` -- Error log if the `/px` command throws an exception

**Wipe log**
- `/oxide/logs/RaidTracker/raidtracker_wiped_raid_events-DATE.txt` -- Logs when an admin wipes raid events with the `/x wipe` command

## Debug mode

While debug mode is enabled detailed information will be printed to the server console along with additional on-screen visuals for admins. Debug mode can be enabled by setting config option `debug` to `true` and reloading the plugin or running command `rt.debug` from the server console.

**All commands require auth level 1 permission**

- `rt.debug` -- enable debug mode (server only)
- `/rt.weapon_colors <tracker category>` -- print weapon colors from tracker config to chat

## Developer mode

While dev mode is enabled the following settings are forced:

- debug mode -- forced: true
- `ignoreBuildingGrades` -- forced: false
- `ignoreSameOwner` -- forced: false
- `ignoreTeamMember` -- forced: false
- `ignoreClanMemberOrAlly` -- forced: false
- `DecayEntityIgnoreList` -- nothing ignored, all prefabs enabled
- if discord webhook is enabled all events will be sent to discord regardless of tracker config setting

## Credits

Original plugin (v1): nivex

Rewrite of plugin (v2): Clearshot

Thanks to [nivex](https://umod.org/user/nivex) and [WhiteThunder](https://umod.org/user/WhiteThunder) for help with Rust and Unity-related code
