using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;

/*
TODO: track flamethrowers - https://discord.com/channels/680078838457565193/680079410971672627/782674391820926976

Fixed date time to use server time
Fixed InvalidCastException
*/

namespace Oxide.Plugins
{
    [Info("Raid Tracker", "nivex", "1.2.5")]
    [Description("Add tracking devices to explosives for detailed raid logging.")]
    public class RaidTracker : RustPlugin
    {
        [PluginReference] private Plugin Discord, Slack, DiscordMessages, PopupNotifications, EventManager, Friends, Clans;

        private bool init;
        private bool wipeData;
        private static RaidTracker ins;
        private bool explosionsLogChanged;
        private List<Dictionary<string, string>> dataExplosions = new List<Dictionary<string, string>>();
        private readonly Dictionary<string, Color> attackersColor = new Dictionary<string, Color>();
        private Dictionary<string, Tracker> Raiders = new Dictionary<string, Tracker>();
        private static Dictionary<string, string> _clans { get; set; } = new Dictionary<string, string>();
        private static Dictionary<string, List<string>> _friends { get; set; } = new Dictionary<string, List<string>>();

        private DynamicConfigFile explosionsFile;
        private readonly List<string> limits = new List<string>();

        long TimeStamp() => (DateTime.Now.Ticks - DateTime.Parse("01/01/1970 00:00:00").Ticks) / 10000000;

        public class Tracker
        {
            public Timer timer;
            public Vector3 position;
            public BasePlayer player;

            public Tracker(BasePlayer player, Vector3 position)
            {
                this.player = player;
                this.position = position;
            }

            public Tracker() { }
        }

        public class EntityInfo
        {
            public string ShortPrefabName { get; set; }
            public ulong OwnerID { get; set; }
            public uint NetworkID { get; set; }
            public Vector3 Position { get; set; }
            public float Health { get; set; }
            public BaseEntity Entity { get; set; }

            public EntityInfo() { }
        }

        public class TrackingDevice : MonoBehaviour
        {
            private readonly int layerMask = LayerMask.GetMask("Construction", "Deployed");
            private int entitiesHit;
            private BaseEntity entity;
            private string entityHit;
            private ulong entityOwner;
            private double millisecondsTaken;
            private Vector3 position;
            private Dictionary<Vector3, EntityInfo> prefabs;
            private bool updated;
            private string weapon;
            private BaseEntity _entity;

            public string playerName { get; set; }
            public ulong playerId { get; set; }
            public Vector3 playerPosition { get; set; }

            private void Awake()
            {
                prefabs = new Dictionary<Vector3, EntityInfo>();
                entity = GetComponent<BaseEntity>();
                weapon = entity.ShortPrefabName;
                position = entity.transform.localPosition;
            }

            private void FixedUpdate()
            {
                var newPosition = entity.transform.localPosition;

                if (newPosition == position || Vector3.Distance(newPosition, Vector3.zero) < 5f) // entity hasn't moved or has moved to vector3.zero
                    return;

                var tick = DateTime.Now;
                position = newPosition;

                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(position, 1f, colliders, layerMask, QueryTriggerInteraction.Collide);

                if (colliders.Count > 0)
                {
                    foreach (var collider in colliders)
                    {
                        if (collider == null || collider.transform == null)
                            continue;

                        var e = collider.ToBaseEntity();

                        if (e == null || prefabs.ContainsKey(e.transform.position))
                            continue;
                        
                        prefabs.Add(e.transform.position, new EntityInfo
                        {
                            NetworkID = e.net?.ID ?? 0u,
                            OwnerID = e.OwnerID,
                            Position = e.transform.position,
                            ShortPrefabName = e.ShortPrefabName,
                            Health = e.Health(),
                            Entity = entity,
                        });

                        updated = true;
                    }
                }

                Pool.FreeList(ref colliders);
                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            private void CheckHealth()
            {
                if (Vector3.Distance(position, Vector3.zero) < 5f) // entity moved to vector3.zero
                    return;

                int count = 0;
                var tick = DateTime.Now;
                var colliders = Pool.GetList<Collider>();
                Vis.Colliders(position, 1f, colliders, layerMask, QueryTriggerInteraction.Collide);

                if (colliders.Count > 0)
                {
                    foreach (var collider in colliders)
                    {
                        var e = collider.ToBaseEntity();

                        if (e == null)
                            continue;

                        if (prefabs.ContainsKey(e.transform.position) && prefabs[e.transform.position].Health != e.Health())
                        {
                            prefabs[e.transform.position].Health = e.Health();
                            entitiesHit++;
                        }

                        count++;
                    }
                }

                Pool.FreeList(ref colliders);
                entitiesHit += prefabs.Count - count;
                millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;
            }

            private bool HasNoOwner(ulong targetId) => _ignoreNoOwners && !targetId.IsSteamId();

            private void OnDestroy()
            {
                ins.NextTick(() => 
                {
                    var tick = DateTime.Now;

                    if (prefabs.Count > 0)
                    {
                        CheckHealth();

                        var sorted = prefabs.ToList();
                        sorted.Sort((x, y) => Vector3.Distance(x.Key, playerPosition).CompareTo(Vector3.Distance(y.Key, playerPosition)));

                        //foreach (var kvp in sorted) Debug.Log(string.Format("{0} {1}", kvp.Value, kvp.Key));

                        entityHit = sorted[0].Value.ShortPrefabName;
                        entityHit = ItemManager.FindItemDefinition(entityHit)?.displayName?.english ?? entityHit;
                        entityOwner = sorted[0].Value.OwnerID;
                        _entity = sorted[0].Value.Entity;

                        prefabs.Clear();
                        sorted.Clear();
                    }

                    if (string.IsNullOrEmpty(entityHit) || HasNoOwner(entityOwner) || ins.IsAlly(playerId, entityOwner))
                    {
                        Destroy(this);
                        return;
                    }

                    if (weapon.Contains("timed"))
                        weapon = ins.msg("C4");
                    else if (weapon.Contains("satchel"))
                        weapon = ins.msg("Satchel Charge");
                    else if (weapon.Contains("basic"))
                        weapon = ins.msg("Rocket");
                    else if (weapon.Contains("hv"))
                        weapon = ins.msg("High Velocity Rocket");
                    else if (weapon.Contains("fire"))
                        weapon = ins.msg("Incendiary Rocket");
                    else if (weapon.Contains("beancan"))
                        weapon = ins.msg("Beancan Grenade");
                    else if (weapon.Contains("f1"))
                        weapon = ins.msg("F1 Grenade");

                    if (entityHit.Contains("wall.low"))
                        entityHit = ins.msg("Low Wall");
                    else if (entityHit.Contains("wall.half"))
                        entityHit = ins.msg("Half Wall");
                    else if (entityHit.Contains("wall.doorway"))
                        entityHit = ins.msg("Doorway");
                    else if (entityHit.Contains("wall.frame"))
                        entityHit = ins.msg("Wall Frame");
                    else if (entityHit.Contains("wall.window"))
                        entityHit = ins.msg("Window");
                    else if (entityHit.Equals("wall"))
                        entityHit = ins.msg("Wall");
                    else if (entityHit.Contains("foundation.triangle"))
                        entityHit = ins.msg("Triangle Foundation");
                    else if (entityHit.Contains("foundation.steps"))
                        entityHit = ins.msg("Foundation Steps");
                    else if (entityHit.Contains("foundation"))
                        entityHit = ins.msg("Foundation");
                    else if (entityHit.Contains("floor.triangle"))
                        entityHit = ins.msg("Triangle Floor");
                    else if (entityHit.Contains("floor.frame"))
                        entityHit = ins.msg("Floor Frame");
                    else if (entityHit.Contains("floor"))
                        entityHit = ins.msg("Floor");
                    else if (entityHit.Contains("roof"))
                        entityHit = ins.msg("Roof");
                    else if (entityHit.Contains("pillar"))
                        entityHit = ins.msg("Pillar");
                    else if (entityHit.Contains("block.stair.lshape"))
                        entityHit = ins.msg("L Shape Stairs");
                    else if (entityHit.Contains("block.stair.ushape"))
                        entityHit = ins.msg("U Shape Stairs");

                    Log(playerName, playerId.ToString(), playerPosition.ToString(), position.ToString(), weapon, entitiesHit.ToString(), entityHit, entityOwner.ToString());

                    millisecondsTaken += (DateTime.Now - tick).TotalMilliseconds;

                    if (_showMillisecondsTaken)
                        Debug.Log(string.Format("Took {0}ms for tracking device operations", millisecondsTaken));

                    if (playerId == entityOwner && _hideSelfFeed)
                    {
                        Destroy(this);
                        return;
                    }

                    CreateFeed(position, entityOwner, playerName, playerId.ToString(), playerPosition, weapon, entityHit);
                    Notify(position, entityOwner, playerName, weapon, entityHit);
                    Destroy(this);
                });
            }

            private void Notify(Vector3 position, ulong ownerId, string playerName, string weapon, string entityHit)
            {
                if (!entityOwner.IsSteamId() || _entity == null || _entity.IsDestroyed)
                {
                    return;
                }

                var player = BasePlayer.FindByID(ownerId);
                string endPosStr = PositionToGrid(position);
                string victim = entityOwner > 0 ? ins.covalence.Players.FindPlayerById(entityOwner.ToString())?.Name ?? entityOwner.ToString() : "No owner";
                string message = ins.msg("ExplosionMessage", player.UserIDString).Replace("{AttackerName}", playerName).Replace("{AttackerId}", playerId.ToString()).Replace("{EndPos}", endPosStr).Replace("{Distance}", Vector3.Distance(position, playerPosition).ToString("N2")).Replace("{Weapon}", weapon).Replace("{EntityHit}", entityHit).Replace("{VictimName}", victim).Replace("{OwnerID}", entityOwner.ToString());

                if (_notifyAuthed)
                {
                    var tc = _entity.GetBuildingPrivilege();

                    if (tc == null)
                    {
                        return;
                    }

                    foreach(var pnid in tc.authorizedPlayers)
                    {
                        var target = BasePlayer.FindByID(pnid.userid);

                        if (!target || !target.IsConnected || _notifyOwner && target.userID == entityOwner)
                        {
                            continue;
                        }

                        ins.Player.Message(target, message);
                    }
                }

                if (_notifyOwner)
                {
                    if (!player || !player.IsConnected)
                    {
                        return;
                    }

                    ins.Player.Message(player, message);
                }
            }
        }


        public bool IsInSameClan(string playerId, string targetId)
        {
            if (Clans == null)
            {
                return false;
            }

            if (_clans.ContainsKey(playerId) && _clans.ContainsKey(targetId))
            {
                return _clans[playerId] == _clans[targetId];
            }

            string playerClan = _clans.ContainsKey(playerId) ? _clans[playerId] : Clans?.Call("GetClanOf", playerId) as string;

            if (string.IsNullOrEmpty(playerClan))
            {
                return false;
            }

            string targetClan = _clans.ContainsKey(targetId) ? _clans[targetId] : Clans?.Call("GetClanOf", targetId) as string;

            if (string.IsNullOrEmpty(targetClan))
            {
                return false;
            }

            _clans[playerId] = playerClan;
            _clans[targetId] = targetClan;

            return playerClan == targetClan;
        }

        public bool IsFriends(string playerId, string targetId)
        {
            if (ins.Friends == null || !ins.Friends.IsLoaded)
            {
                return false;
            }

            List<string> targetList;
            if (!_friends.TryGetValue(targetId, out targetList))
            {
                _friends[targetId] = targetList = new List<string>();
            }

            if (targetList.Contains(playerId))
            {
                return true;
            }

            var success = ins.Friends?.Call("AreFriends", playerId, targetId);

            if (success is bool && (bool)success)
            {
                targetList.Add(playerId);
                return true;
            }

            return false;
        }

        private bool IsOnSameTeam(ulong playerId, ulong targetId) // Credits @ctv
        {
            RelationshipManager.PlayerTeam team1;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out team1))
            {
                return false;
            }

            RelationshipManager.PlayerTeam team2;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(targetId, out team2))
            {
                return false;
            }

            return team1.teamID == team2.teamID;
        }

        public bool IsAlly(ulong playerId, ulong targetId)
        {
            if (!_ignoreTeamMates)
            {
                return false;
            }

            return playerId == targetId || IsOnSameTeam(playerId, targetId) || IsInSameClan(playerId.ToString(), targetId.ToString()) || IsFriends(playerId.ToString(), targetId.ToString());
        }

        private void OnClanUpdate(string tag) => UpdateClans(tag);

        private void OnClanDestroy(string tag) => UpdateClans(tag);

        private void OnClanDisbanded(string tag) => UpdateClans(tag);

        private void UpdateClans(string tag)
        {
            var clans = new Dictionary<string, string>();

            foreach (var clan in _clans)
            {
                if (clan.Value != tag)
                {
                    clans[clan.Key] = clan.Value;
                }
            }

            _clans = clans;
        }

        private void OnFriendAdded(string playerId, string targetId)
        {
            UpdateFriends(playerId, targetId, true);
        }

        private void OnFriendRemoved(string playerId, string targetId)
        {
            UpdateFriends(playerId, targetId, false);
        }

        public void UpdateFriends(string playerId, string targetId, bool added)
        {
            List<string> playerList;
            if (_friends.TryGetValue(playerId, out playerList))
            {
                if (added)
                {
                    playerList.Add(targetId);
                }
                else playerList.Remove(targetId);
            }
        }

        private static void CreateFeed(Vector3 position, ulong entityOwner, string playerName, string playerId, Vector3 playerPosition, string weapon, string entityHit)
        {
            string endPosStr = PositionToGrid(position);
            string victim = entityOwner > 0 ? ins.covalence.Players.FindPlayerById(entityOwner.ToString())?.Name ?? entityOwner.ToString() : "No owner";
            string message = ins.msg("ExplosionMessage").Replace("{AttackerName}", playerName).Replace("{AttackerId}", playerId).Replace("{EndPos}", endPosStr).Replace("{Distance}", Vector3.Distance(position, playerPosition).ToString("N2")).Replace("{Weapon}", weapon).Replace("{EntityHit}", entityHit).Replace("{VictimName}", victim).Replace("{OwnerID}", entityOwner.ToString());

            if (_outputExplosionMessages)
                Debug.Log(message);

            if (_sendDiscordNotifications)
                ins.Discord?.Call("SendMessage", message);

            if (_sendSlackNotifications)
            {
                switch (_slackMessageStyle.ToLower())
                {
                    case "message":
                        ins.Slack?.Call(_slackMessageStyle, message, _slackChannel);
                        break;
                    default:
                        ins.Slack?.Call(_slackMessageStyle, message, ins.covalence.Players.FindPlayerById(playerId), _slackChannel);
                        break;
                }
            }

            if (_sendDiscordMessages)
                ins.DiscordMessage(playerName, playerId, message);

            foreach (var target in BasePlayer.activePlayerList)
            {
                if (target != null && ins.permission.UserHasPermission(target.UserIDString, "raidtracker.see"))
                {
                    message = ins.msg("ExplosionMessage", target.UserIDString).Replace("{AttackerName}", playerName).Replace("{AttackerId}", playerId).Replace("{EndPos}", endPosStr).Replace("{Distance}", Vector3.Distance(position, playerPosition).ToString("N2")).Replace("{Weapon}", weapon).Replace("{EntityHit}", entityHit).Replace("{VictimName}", victim).Replace("{OwnerID}", entityOwner.ToString());

                    if (usePopups && ins.PopupNotifications != null)
                        ins.PopupNotifications.Call("CreatePopupNotification", message, target, popupDuration);
                    else
                        ins.Player.Message(target, message);
                }
            }
        }

        private static Dictionary<string, string> Log(string attacker, string attackerId, string startPos, string endPos, string weapon, string hits, string hit, string owner)
        {
            var explosion = new Dictionary<string, string>
            {
                ["Attacker"] = attacker,
                ["AttackerId"] = attackerId,
                ["StartPositionId"] = startPos,
                ["EndPositionId"] = endPos,
                ["Weapon"] = weapon,
                ["EntitiesHit"] = hits,
                ["EntityHit"] = hit,
                ["DeleteDate"] = _daysBeforeDelete > 0 ? DateTime.Now.AddDays(_daysBeforeDelete).ToString() : DateTime.MinValue.ToString(),
                ["LoggedDate"] = DateTime.Now.ToString(),
                ["EntityOwner"] = owner
            };

            ins.dataExplosions.Add(explosion);
            ins.explosionsLogChanged = true;

            return explosion;
        }

        private void OnServerSave()
        {
            SaveExplosionData();
        }

        private void OnNewSave(string filename)
        {
            wipeData = true;
        }

        private void Unload()
        {
            foreach (var entry in Raiders)
            {
                if (entry.Value.timer != null && !entry.Value.timer.Destroyed)
                {
                    entry.Value.timer.Destroy();
                }
            }

            var objects = UnityEngine.Object.FindObjectsOfType(typeof(TrackingDevice));

            if (objects != null)
                foreach (var gameObj in objects)
                    UnityEngine.Object.Destroy(gameObj);

            SaveExplosionData();
            ins = null;
            _clans.Clear();
            _friends.Clear();
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityTakeDamage));
            Unsubscribe(nameof(OnExplosiveThrown));
            Unsubscribe(nameof(OnRocketLaunched));
        }

        private void OnServerInitialized()
        {
            ins = this;

            LoadMessages();
            LoadVariables();

            if (_trackExplosiveDeathsOnly || _trackEcoRaiding)
            {
                Subscribe(nameof(OnEntityDeath));
            }
            else if (_trackExplosiveAmmo)
            {
                Subscribe(nameof(OnEntityDeath));
                Subscribe(nameof(OnEntityTakeDamage));
            }

            Subscribe(nameof(OnExplosiveThrown));
            Subscribe(nameof(OnRocketLaunched));

            explosionsFile = Interface.Oxide.DataFileSystem.GetFile("RaidTracker");

            try
            {
                dataExplosions = explosionsFile.ReadObject<List<Dictionary<string, string>>>();
            }
            catch { }

            if (dataExplosions == null)
            {
                dataExplosions = new List<Dictionary<string, string>>();
                explosionsLogChanged = true;
            }

            if (wipeData && _automateWipes)
            {
                int entries = dataExplosions.Count;
                dataExplosions.Clear();
                explosionsLogChanged = true;
                wipeData = false;
                SaveExplosionData();
                Puts("Wipe detected; wiped {0} entries.", entries);
            }

            if (dataExplosions.Count > 0)
            {
                foreach (var dict in dataExplosions.ToList())
                {
                    if (dict.ContainsKey("DeleteDate")) // apply retroactive changes
                    {
                        if (_applyInactiveChanges)
                        {
                            if (_daysBeforeDelete > 0 && dict["DeleteDate"] == DateTime.MinValue.ToString())
                            {
                                dict["DeleteDate"] = DateTime.Now.AddDays(_daysBeforeDelete).ToString();
                                explosionsLogChanged = true;
                            }
                            if (_daysBeforeDelete == 0 && dict["DeleteDate"] != DateTime.MinValue.ToString())
                            {
                                dict["DeleteDate"] = DateTime.MinValue.ToString();
                                explosionsLogChanged = true;
                            }
                        }

                        if (_applyActiveChanges && _daysBeforeDelete > 0 && dict.ContainsKey("LoggedDate"))
                        {
                            var deleteDate = DateTime.Parse(dict["DeleteDate"]);
                            var loggedDate = DateTime.Parse(dict["LoggedDate"]);
                            int days = deleteDate.Subtract(loggedDate).Days;

                            if (days != _daysBeforeDelete)
                            {
                                int daysLeft = deleteDate.Subtract(DateTime.Now).Days;

                                if (daysLeft > _daysBeforeDelete)
                                {
                                    dict["DeleteDate"] = loggedDate.AddDays(_daysBeforeDelete).ToString();
                                    explosionsLogChanged = true;
                                }
                            }
                        }
                    }
                    else
                    {
                        dict.Add("DeleteDate", _daysBeforeDelete > 0 ? DateTime.Now.AddDays(_daysBeforeDelete).ToString() : DateTime.MinValue.ToString());
                        explosionsLogChanged = true;
                    }
                }
            }

            init = true;
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.net == null)
                return;

            if (EventManager != null && EventManager.IsLoaded && Convert.ToBoolean(EventManager?.Call("isPlaying", player)))
                return;

            if ((entity.ShortPrefabName.Contains("grenade.f1") && _trackF1) ||
                (entity.ShortPrefabName.Contains("grenade.beancan") && _trackBeancan) ||
                (entity.ShortPrefabName.Contains("supply.signal") && _trackSupply) ||
                (entity.ShortPrefabName.Contains("explosive.timed")) ||
                (entity.ShortPrefabName.Contains("explosive.satchel") && _trackSatchel))
                    Track(player, entity.gameObject.AddComponent<TrackingDevice>());
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (!init || player == null || entity?.transform == null)
                return;

            if (EventManager != null && EventManager.IsLoaded && Convert.ToBoolean(EventManager?.Call("isPlaying", player)))
                return;

            Track(player, entity.gameObject.AddComponent<TrackingDevice>());
        }

        void OnEntityDeath(BaseEntity entity, HitInfo hitInfo)
        {
            EntityHandler(entity, hitInfo, true);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            EntityHandler(entity, hitInfo, false);
        }

        void EntityHandler(BaseEntity entity, HitInfo hitInfo, bool death)
        {
            if (!init || entity == null || entity is BasePlayer || !entity.OwnerID.IsSteamId() || hitInfo?.InitiatorPlayer == null)
            {
                return;
            }

            var block = entity as BuildingBlock;

            if (block != null && !_trackTwigDestruction && block.grade == BuildingGrade.Enum.Twigs)
            {
                return;
            }

            var attacker = hitInfo.InitiatorPlayer;

            if (attacker.userID == entity.OwnerID && !attacker.IsAdmin) // reduce number of db entries but allow admins to test
            {
                return;
            }

            if (!IsRaiding(attacker) && !entity.name.Contains("deploy") && !entity.name.Contains("building"))
            {
                return;
            }

            var weapon = attacker.GetHeldEntity();

            if (weapon == null)
            {
                return;
            }

            var projectile = weapon.GetComponent<BaseProjectile>();

            if (!_trackEcoRaiding && projectile == null && !weapon.ShortPrefabName.Contains("flamethrower")) // 1.1.7 fix and 1.2.0 addition
            {
                return;
            }

            if (projectile != null && !_trackEcoRaiding)
            {
                if (!_trackExplosiveAmmo || !projectile.primaryMagazine.ammoType.shortname.Contains("explosive"))
                {
                    return;
                }
            }

            string shortname = projectile == null || !projectile.primaryMagazine.ammoType.shortname.Contains("explosive") ? weapon.GetItem()?.info?.displayName?.english ?? hitInfo.WeaponPrefab?.ShortPrefabName : projectile.primaryMagazine.ammoType.shortname;

            if (string.IsNullOrEmpty(shortname))
            {
                return;
            }

            if (_trackEcoRaiding && projectile == null && !entity.name.Contains("building"))
            {
                return;
            }

            int entitiesHit = 1;
            string uid = attacker.UserIDString;
            string pid = entity.WorldSpaceBounds().ToBounds().center.ToString();
            var explosion = dataExplosions.FirstOrDefault(x => x["EndPositionId"] == pid);
            var position = attacker.transform.position;
            position.y += attacker.GetHeight() - 0.35f;

            if (explosion == null)
            {
                explosion = Log(attacker.displayName, uid, position.ToString(), pid, shortname, entitiesHit.ToString(), entity.ShortPrefabName, entity.OwnerID.ToString());
            }
            else if (int.TryParse(explosion["EntitiesHit"], out entitiesHit))
            {
                entitiesHit++;
                explosion["EntitiesHit"] = entitiesHit.ToString();
            }

            explosionsLogChanged = true;

            Tracker tracker;
            if (Raiders.TryGetValue(uid, out tracker))
            {
                tracker.timer?.Destroy();
                Raiders.Remove(uid);
            }

            Raiders.Add(uid, new Tracker(attacker, position)
            {
                timer = timer.Once(_trackExplosiveAmmoTime, () => Raiders.Remove(uid))
            });

            if (attacker.userID == entity.OwnerID && _hideSelfFeed && !attacker.IsAdmin)
            {
                return;
            }

            if (death)
            {
                if (_ignoreNoOwners && !entity.OwnerID.IsSteamId()) return;
                CreateFeed(position, entity.OwnerID, attacker.displayName, attacker.UserIDString, position, shortname, entity.ShortPrefabName);
            }
        }

        private void Track(BasePlayer player, TrackingDevice device)
        {
            string uid = player.UserIDString;
            var position = player.transform.position;
            position.y += player.GetHeight() - 0.35f;

            device.playerName = player.displayName;
            device.playerId = player.userID;
            device.playerPosition = position;

            Tracker tracker;
            if (Raiders.TryGetValue(uid, out tracker))
            {
                tracker.timer?.Destroy();
                Raiders.Remove(uid);
            }

            Raiders.Add(uid, new Tracker(player, position)
            {
                timer = timer.Once(_trackExplosiveAmmoTime, () => Raiders.Remove(uid))
            });
        }
        
        private bool IsRaiding(BasePlayer player)
        {
            return player != null && Raiders.ContainsKey(player.UserIDString) && Vector3.Distance(player.transform.position, Raiders[player.UserIDString].position) <= 50f;
        }

        private static string PositionToGrid(Vector3 position) // Credit: MagicGridPanel
        {
            var r = new Vector2(position.x + (World.Size / 2f), position.z + (World.Size / 2f));
            int maxGridSize = Mathf.FloorToInt(World.Size / 146.3f) - 1;
            int x = Mathf.FloorToInt(r.x / 146.3f);
            int y = Mathf.FloorToInt(r.y / 146.3f);
            int num1 = Mathf.Clamp(x, 0, maxGridSize);
            int num2 = Mathf.Clamp(maxGridSize - y, 0, maxGridSize);
            string extraA = num1 > 26 ? $"{(char)('A' + (num1 / 26 - 1))}" : string.Empty;
            return $"{extraA}{(char)('A' + num1 % 26)}{num2}";
        }

        private void SaveExplosionData()
        {
            int expired = dataExplosions.RemoveAll(x => x.ContainsKey("DeleteDate") && x["DeleteDate"] != DateTime.MinValue.ToString() && DateTime.Parse(x["DeleteDate"]) < DateTime.Now);

            if (expired > 0)
            {
                explosionsLogChanged = true;
            }

            if (explosionsLogChanged)
            {
                explosionsFile.WriteObject(dataExplosions);
                explosionsLogChanged = false;
            }
        }
        
        public void DiscordMessage(string name, string playerId, string text)
        {
            object fields = new[]
            {
                new {
                    name = msg("Embed_MessagePlayer"), value = $"[{name}](https://steamcommunity.com/profiles/{playerId})", inline = true
                },
                new {
                    name = msg("Embed_MessageMessage"), value = text, inline = false
                }
            };

            string json = Newtonsoft.Json.JsonConvert.SerializeObject(fields);

            DiscordMessages?.Call("API_SendFancyMessage", _webhookUrl, msg("Embed_MessageTitle"), _messageColor, json, null, this);
        }

        private void cmdPX(BasePlayer player, string command, string[] args)
        {
            if (!_allowPlayerExplosionMessages && !_allowPlayerDrawing)
            {
                Player.Message(player, msg("PXHelp", player.UserIDString));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, _playerPerm) && !player.IsAdmin)
            {
                Player.Message(player, msg("Not Allowed", player.UserIDString));
                return;
            }

            if (dataExplosions == null || dataExplosions.Count == 0)
            {
                Player.Message(player, msg("None Logged", player.UserIDString));
                return;
            }

            if (limits.Contains(player.UserIDString))
                return;

            var colors = new List<Color>();
            var explosions = dataExplosions.FindAll(x => x.ContainsKey("EntityOwner") && x["EntityOwner"] == player.UserIDString && x["AttackerId"] != player.UserIDString && Vector3.Distance(player.transform.position, x["EndPositionId"].ToVector3()) <= _playerDistance);

            if (explosions == null || explosions.Count == 0)
            {
                Player.Message(player, msg("None Owned", player.UserIDString));
                return;
            }

            Player.Message(player, msg("Showing Owned", player.UserIDString));

            bool drawX = explosions.Count > _maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();

            if (_showExplosionMessages && _maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x["AttackerId"]))
                        attackers.Add(x["AttackerId"], ParseExplosions(explosions.Where(ex => ex["AttackerId"] == x["AttackerId"]).ToList()));

            if (_allowPlayerDrawing)
            {
                bool isAdmin = player.IsAdmin;

                try
                {
                    if (!isAdmin)
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                        player.SendNetworkUpdateImmediate();
                    }

                    foreach (var x in explosions)
                    {
                        var startPos = x["StartPositionId"].ToVector3();
                        var endPos = x["EndPositionId"].ToVector3();
                        var endPosStr = PositionToGrid(endPos);

                        if (colors.Count == 0)
                            colors = new List<Color>
                            {
                                Color.blue,
                                Color.cyan,
                                Color.gray,
                                Color.green,
                                Color.magenta,
                                Color.red,
                                Color.yellow
                            };

                        var color = _colorByWeaponType ? (x["Weapon"].Contains("Rocket") ? Color.red : x["Weapon"].Equals("C4") ? Color.yellow : x["Weapon"].Contains("explosive") ? Color.magenta : Color.blue) : attackersColor.ContainsKey(x["AttackerId"]) ? attackersColor[x["AttackerId"]] : colors[UnityEngine.Random.Range(0, colors.Count - 1)];

                        attackersColor[x["AttackerId"]] = color;

                        if (colors.Contains(color))
                            colors.Remove(color);

                        if (_showConsoleMessages)
                        {
                            var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(attackersColor[x["AttackerId"]]), x["Attacker"], x["AttackerId"], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x["Weapon"], x["EntitiesHit"], x["EntityHit"]);
                            var victim = x.ContainsKey("EntityOwner") ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x["EntityOwner"])?.Name ?? "Unknown", x["EntityOwner"]) : string.Empty;

                            player.ConsoleMessage(explosion + victim);
                        }

                        if (_drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                        {
                            string weapon = x["Weapon"].Substring(0, 1).Replace("a", "EA");
                            player.SendConsoleCommand("ddraw.arrow", _invokeTime, color, startPos, endPos, 0.2);
                            player.SendConsoleCommand("ddraw.text", _invokeTime, color, startPos, weapon);
                        }

                        player.SendConsoleCommand("ddraw.text", _invokeTime, color, endPos, drawX ? "X" : x["Attacker"]);
                    }
                }
                catch (Exception ex)
                {
                    _allowPlayerExplosionMessages = false;
                    _allowPlayerDrawing = false;
                    Puts("cmdPX Exception: {0} --- {1}", ex.Message, ex.StackTrace);
                    Puts("Player functionality disabled!");
                }

                if (!isAdmin)
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    player.SendNetworkUpdateImmediate();
                }
            }

            if (_allowPlayerExplosionMessages)
            {
                if (attackers.Count > 0)
                    foreach (var kvp in attackers)
                        if (++shownExplosions < _maxMessagesToPlayer)
                            Player.Message(player, string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor.ContainsKey(kvp.Key) ? attackersColor[kvp.Key] : Color.red), kvp.Value));
            }

            Player.Message(player, msg("Explosions Listed", player.UserIDString, explosions.Count, _playerDistance));
            colors.Clear();

            if (player.IsAdmin)
                return;

            string uid = player.UserIDString;

            limits.Add(uid);
            timer.Once(_playerRestrictionTime, () => limits.Remove(uid));
        }

        private void cmdX(BasePlayer player, string command, string[] args)
        {
            if (player.net.connection.authLevel < _authLevel && !_authorized.Contains(player.UserIDString))
            {
                Player.Message(player, msg("Not Allowed", player.UserIDString));
                return;
            }

            if (args.Length > 0)
            {
                if (args.Any(arg => arg.Contains("del") || arg.Contains("wipe")))
                {
                    if (!_allowManualWipe)
                    {
                        Player.Message(player, msg("No Manual Wipe", player.UserIDString));
                        return;
                    }
                }

                switch (args[0].ToLower())
                {
                    case "wipe":
                        {
                            dataExplosions.Clear();
                            explosionsLogChanged = true;
                            SaveExplosionData();
                            Player.Message(player, msg("Wiped", player.UserIDString));
                        }
                        return;
                    case "del":
                        {
                            if (args.Length == 2)
                            {
                                int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("AttackerId") && x["AttackerId"] == args[1] || x.ContainsKey("Attacker") && x["Attacker"] == args[1]);

                                if (deleted > 0)
                                {
                                    Player.Message(player, msg("Removed", player.UserIDString, deleted, args[1]));
                                    explosionsLogChanged = true;
                                    SaveExplosionData();
                                }
                                else
                                    Player.Message(player, msg("None Found", player.UserIDString, _delRadius));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "delm":
                        {
                            int deleted = dataExplosions.RemoveAll(x => Vector3.Distance(player.transform.position, x["EndPositionId"].ToVector3()) < _delRadius);

                            if (deleted > 0)
                            {
                                Player.Message(player, msg("RemovedM", player.UserIDString, deleted, _delRadius));
                                explosionsLogChanged = true;
                                SaveExplosionData();
                            }
                            else
                                Player.Message(player, msg("None In Radius", player.UserIDString, _delRadius));
                        }
                        return;
                    case "delbefore":
                        {
                            if (args.Length == 2)
                            {
                                DateTime deleteDate;
                                if (DateTime.TryParse(args[1], out deleteDate))
                                {
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("LoggedDate") && DateTime.Parse(x["LoggedDate"]) < deleteDate);

                                    if (deleted > 0)
                                    {
                                        Player.Message(player, msg("Removed Before", player.UserIDString, deleted, deleteDate.ToString()));
                                        explosionsLogChanged = true;
                                        SaveExplosionData();
                                    }
                                    else
                                        Player.Message(player, msg("None Dated Before", player.UserIDString, deleteDate.ToString()));
                                }
                                else
                                    Player.Message(player, msg("Invalid Date", player.UserIDString, _chatCommand, DateTime.Now.ToString("yyyy-mm-dd HH:mm:ss")));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "delafter":
                        {
                            if (args.Length == 2)
                            {
                                DateTime deleteDate;
                                if (DateTime.TryParse(args[1], out deleteDate))
                                {
                                    int deleted = dataExplosions.RemoveAll(x => x.ContainsKey("LoggedDate") && DateTime.Parse(x["LoggedDate"]) > deleteDate);

                                    if (deleted > 0)
                                    {
                                        Player.Message(player, msg("Removed After", player.UserIDString, deleted, deleteDate.ToString()));
                                        explosionsLogChanged = true;
                                        SaveExplosionData();
                                    }
                                    else
                                        Player.Message(player, msg("None Dated After", player.UserIDString, deleteDate.ToString()));
                                }
                                else
                                    Player.Message(player, msg("Invalid Date", player.UserIDString, _chatCommand, DateTime.Now.ToString("yyyy-mm-dd HH:mm:ss")));
                            }
                            else
                                SendHelp(player);
                        }
                        return;
                    case "help":
                        {
                            SendHelp(player);
                        }
                        return;
                }
            }

            if (dataExplosions == null || dataExplosions.Count == 0)
            {
                Player.Message(player, msg("None Logged", player.UserIDString));
                return;
            }

            int distance;
            if (args.Length == 0 || !int.TryParse(args[0], out distance))
            {
                distance = _defaultMaxDistance;
            }
            var colors = new List<Color>();
            var explosions = dataExplosions.FindAll(x => Vector3.Distance(player.transform.position, x["EndPositionId"].ToVector3()) <= distance);
            bool drawX = explosions.Count > _maxNamedExplosions;
            int shownExplosions = 0;
            var attackers = new Dictionary<string, string>();

            if (_showExplosionMessages && _maxMessagesToPlayer > 0)
                foreach (var x in explosions)
                    if (!attackers.ContainsKey(x["AttackerId"]))
                        attackers.Add(x["AttackerId"], ParseExplosions(explosions.Where(ex => ex["AttackerId"] == x["AttackerId"]).ToList()));

            foreach (var x in explosions)
            {
                var startPos = x["StartPositionId"].ToVector3();
                var endPos = x["EndPositionId"].ToVector3();
                var endPosStr = PositionToGrid(endPos);

                if (colors.Count == 0)
                {
                    colors = new List<Color>
                    {
                        Color.blue,
                        Color.cyan,
                        Color.gray,
                        Color.green,
                        Color.magenta,
                        Color.red,
                        Color.yellow
                    };
                }

                var color = _colorByWeaponType ? (x["Weapon"].Contains("Rocket") ? Color.red : x["Weapon"].Equals("C4") ? Color.yellow : x["Weapon"].Contains("explosive") ? Color.magenta : Color.blue) : attackersColor.ContainsKey(x["AttackerId"]) ? attackersColor[x["AttackerId"]] : colors[UnityEngine.Random.Range(0, colors.Count - 1)];

                attackersColor[x["AttackerId"]] = color;

                if (colors.Contains(color))
                    colors.Remove(color);

                if (_showConsoleMessages)
                {
                    var explosion = msg("Explosions", player.UserIDString, ColorUtility.ToHtmlStringRGB(color), x["Attacker"], x["AttackerId"], endPosStr, Math.Round(Vector3.Distance(startPos, endPos), 2), x["Weapon"], x["EntitiesHit"], x["EntityHit"]);
                    var victim = x.ContainsKey("EntityOwner") ? string.Format(" - Victim: {0} ({1})", covalence.Players.FindPlayerById(x["EntityOwner"])?.Name ?? "Unknown", x["EntityOwner"]) : string.Empty;

                    player.ConsoleMessage(explosion + victim);
                }

                if (_drawArrows && Vector3.Distance(startPos, endPos) > 1f)
                {
                    string weapon = x["Weapon"].Substring(0, 1).Replace("a", "EA");
                    player.SendConsoleCommand("ddraw.arrow", _invokeTime, color, startPos, endPos, 0.2);
                    player.SendConsoleCommand("ddraw.text", _invokeTime, color, startPos, weapon);
                }

                player.SendConsoleCommand("ddraw.text", _invokeTime, color, endPos, drawX ? "X" : x["Attacker"]);
            }

            if (attackers.Count > 0)
                foreach (var kvp in attackers)
                    if (++shownExplosions < _maxMessagesToPlayer)
                        Player.Message(player, string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(attackersColor[kvp.Key]), kvp.Value));

            if (explosions.Count > 0)
                Player.Message(player, msg("Explosions Listed", player.UserIDString, explosions.Count, distance));
            else
                Player.Message(player, msg("None Found", player.UserIDString, distance));

            colors.Clear();
        }

        private string ParseExplosions(List<Dictionary<string, string>> explosions)
        {
            if (explosions.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            var weapons = explosions.Select(x => x["Weapon"]).Distinct();

            foreach (var weapon in weapons)
            {
                var targets = explosions.Where(x => x["Weapon"] == weapon).Select(x => x["EntityHit"]).Distinct();

                sb.Append(string.Format("{0}: [", weapon));

                foreach (var target in targets)
                {
                    int entitiesHit = explosions.Where(x => x["EntityHit"] == target && x["Weapon"] == weapon).Sum(y => int.Parse(y["EntitiesHit"]));
                    sb.Append(string.Format("{0} ({1}) [{2}], ", target, explosions.Count(x => x["EntityHit"] == target && x["Weapon"] == weapon), entitiesHit));
                }

                sb.Length -= 2;
                sb.Append("], ");
            }

            sb.Length -= 2;
            return string.Format("{0} used {1} explosives: {2}", explosions[0]["Attacker"], explosions.Count, sb.ToString());
        }

        #region Config

        private bool _changed;
        private int _authLevel;
        private int _defaultMaxDistance;
        private int _delRadius;
        private int _maxNamedExplosions;
        private int _invokeTime;
        private bool _showExplosionMessages;
        private int _maxMessagesToPlayer;
        private static bool _outputExplosionMessages;
        private bool _drawArrows;
        private static bool _showMillisecondsTaken;
        private bool _allowManualWipe;
        private bool _automateWipes;
        private bool _colorByWeaponType;
        private bool _applyInactiveChanges;
        private bool _applyActiveChanges;
        private static bool _sendDiscordNotifications;
        private static bool _sendSlackNotifications;
        private static string _slackMessageStyle;
        private static string _slackChannel;
        private string _chatCommand;
        private static List<object> _authorized;
        private static int _daysBeforeDelete;
        private bool _trackF1;
        private bool _trackBeancan;
        private bool _allowPlayerDrawing;
        private string _playerPerm;
        private string _szPlayerChatCommand;
        private float _playerDistance;
        private bool _allowPlayerExplosionMessages;
        private float _playerRestrictionTime;
        private bool _showConsoleMessages;
        private int _messageColor;
        private static bool _sendDiscordMessages;
        private static string _webhookUrl;
        private static bool usePopups;
        private static float popupDuration;
        private bool _trackExplosiveAmmo;
        private float _trackExplosiveAmmoTime;
        private bool _trackExplosiveDeathsOnly;
        private bool _trackSupply;
        private static bool _hideSelfFeed;
        private bool _trackSatchel;
        private bool _trackEcoRaiding;
        private static bool _ignoreNoOwners;
        private static bool _ignoreTeamMates;
        private bool _trackTwigDestruction;
        private static bool _notifyOwner;
        private static bool _notifyAuthed;

        private void LoadMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Not Allowed"] = "You are not allowed to use this command.",
                ["Wiped"] = "Explosion data wiped",
                ["Removed"] = "Removed <color=orange>{0}</color> explosions for <color=orange>{1}</color>",
                ["RemovedM"] = "Removed <color=orange>{0}</color> explosions within <color=orange>{1}m</color>",
                ["Removed After"] = "Removed <color=orange>{0}</color> explosions logged after <color=orange>{1}</color>",
                ["Removed Before"] = "Removed <color=orange>{0}</color> explosions logged before <color=orange>{1}</color>",
                ["None Logged"] = "No explosions logged",
                ["None Deleted"] = "No explosions found within <color=orange>{0}m</color>",
                ["Explosions"] = "<color=#{0}>{1} ({2})</color> @ {3} ({4}m) [{5}] Entities Hit: {6} Entity Hit: {7}",
                ["Explosions Listed"] = "<color=orange>{0}</color> explosions listed within <color=orange>{1}m</color>.",
                ["None Found"] = "No explosions detected within <color=orange>{0}m</color>. Try specifying a larger range.",
                ["None In Radius"] = "No explosions found within the delete radius (<color=orange>{0}m</color>).",
                ["None Dated After"] = "No explosions dated after: {0}",
                ["None Dated Before"] = "No explosions dated before: {0}",
                ["No Manual Wipe"] = "Server owner has disabled manual wipe of explosion data.",
                ["Invalid Date"] = "Invalid date specified. Example: /{0} deldate \"{1}\"",
                ["Cannot Use From Server Console"] = "You cannot use this command from the server console.",
                ["Help Wipe"] = "Wipe all explosion data",
                ["Help Del Id"] = "Delete all explosions for <color=orange>ID</color>",
                ["Help Delm"] = "Delete all explosions within <color=orange>{0}m</color>",
                ["Help After"] = "Delete all explosions logged after the date specified. <color=orange>Example</color>: /{0} delafter \"{1}\"",
                ["Help Before"] = "Delete all explosions logged before the date specified. <color=orange>Example</color>: /{0} delbefore \"{1}\"",
                ["Help Distance"] = "Show explosions from <color=orange>X</color> distance",
                ["None Owned"] = "No explosions found near entities you own or have owned.",
                ["Showing Owned"] = "Showing list of explosions to entities which you own or have owned...",
                ["Embed_MessageTitle"] = "Player Message",
                ["Embed_MessagePlayer"] = "Player",
                ["Embed_MessageMessage"] = "Message",
                ["ExplosionMessage"] = "[Explosion] {AttackerName} ({AttackerId}) @ {EndPos} ({Distance}m) {Weapon}: {EntityHit} - Victim: {VictimName} ({OwnerID})",
                ["C4"] = "C4",
                ["Satchel Charge"] = "Satchel Charge",
                ["Rocket"] = "Rocket",
                ["High Velocity Rocket"] = "High Velocity Rocket",
                ["Incendiary Rocket"] = "Incendiary Rocket",
                ["Beancan Grenade"] = "Beancan Grenade",
                ["F1 Grenade"] = "F1 Grenade",
                ["Low Wall"] = "Low Wall",
                ["Half Wall"] = "Half Wall",
                ["Doorway"] = "Doorway",
                ["Wall Frame"] = "Wall Frame",
                ["Window"] = "Window",
                ["Wall"] = "Wall",
                ["Triangle Foundation"] = "Triangle Foundation",
                ["Foundation Steps"] = "Foundation Steps",
                ["Foundation"] = "Foundation",
                ["Triangle Floor"] = "Triangle Floor",
                ["Floor Frame"] = "Floor Frame",
                ["Floor"] = "Floor",
                ["Roof"] = "Roof",
                ["Pillar"] = "Pillar",
                ["L Shape Stairs"] = "L Shape Stairs",
                ["U Shape Stairs"] = "U Shape Stairs",
                ["PXHelp"] = "<color=#FFA500>Allow DDRAW</color>, or <color=#FFA500>Show Explosions</color> must be enabled in the config to use this command.",
            }, this);
        }

        private void SendHelp(BasePlayer player)
        {
            Player.Message(player, string.Format("/{0} wipe - {1}", _chatCommand, msg("Help Wipe", player.UserIDString)));
            Player.Message(player, string.Format("/{0} del id - {1}", _chatCommand, msg("Help Del Id", player.UserIDString)));
            Player.Message(player, string.Format("/{0} delm - {1}", _chatCommand, msg("Help Delm", player.UserIDString, _delRadius)));
            Player.Message(player, string.Format("/{0} delafter date - {1}", _chatCommand, msg("Help After", player.UserIDString, _chatCommand, DateTime.Now.Subtract(new TimeSpan(_daysBeforeDelete, 0, 0, 0)).ToString())));
            Player.Message(player, string.Format("/{0} delbefore date - {1}", _chatCommand, msg("Help Before", player.UserIDString, _chatCommand, DateTime.Now.ToString())));
            Player.Message(player, string.Format("/{0} <distance> - {1}", _chatCommand, msg("Help Distance", player.UserIDString)));
        }

        private void LoadVariables()
        {
            _authorized = GetConfig("Settings", "Authorized List", new List<object>()) as List<object>;

            if (_authorized != null)
            {
                foreach (var auth in _authorized.ToList())
                {
                    if (auth == null || !auth.ToString().IsSteamId())
                    {
                        PrintWarning("{0} is not a valid steam id. Entry removed.", auth == null ? "null" : auth);
                        _authorized.Remove(auth);
                    }
                }
            }

            _showConsoleMessages = Convert.ToBoolean(GetConfig("Settings", "Output Detailed Explosion Messages To Client Console", true));
            _authLevel = _authorized?.Count == 0 ? Convert.ToInt32(GetConfig("Settings", "Auth Level", 1)) : int.MaxValue;
            _defaultMaxDistance = Convert.ToInt32(GetConfig("Settings", "Show Explosions Within X Meters", 50));
            _delRadius = Convert.ToInt32(GetConfig("Settings", "Delete Radius", 50));
            _maxNamedExplosions = Convert.ToInt32(GetConfig("Settings", "Max Names To Draw", 25));
            _invokeTime = Convert.ToInt32(GetConfig("Settings", "Time In Seconds To Draw Explosions", 60f));
            _showExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Show Explosion Messages To Player", true));
            _maxMessagesToPlayer = Convert.ToInt32(GetConfig("Settings", "Max Explosion Messages To Player", 10));
            _outputExplosionMessages = Convert.ToBoolean(GetConfig("Settings", "Print Explosions To Server Console", true));
            _drawArrows = Convert.ToBoolean(GetConfig("Settings", "Draw Arrows", true));
            _showMillisecondsTaken = Convert.ToBoolean(GetConfig("Settings", "Print Milliseconds Taken To Track Explosives In Server Console", false));
            _automateWipes = Convert.ToBoolean(GetConfig("Settings", "Wipe Data When Server Is Wiped (Recommended)", true));
            _allowManualWipe = Convert.ToBoolean(GetConfig("Settings", "Allow Manual Deletions and Wipe of Explosion Data", true));
            _colorByWeaponType = Convert.ToBoolean(GetConfig("Settings", "Color By Weapon Type Instead Of By Player Name", false));
            _daysBeforeDelete = Convert.ToInt32(GetConfig("Settings", "Automatically Delete Each Explosion X Days After Being Logged", 0));
            _applyInactiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When No Date Is Set", true));
            _applyActiveChanges = Convert.ToBoolean(GetConfig("Settings", "Apply Retroactive Deletion Dates When Days To Delete Is Changed", true));
            _sendDiscordNotifications = Convert.ToBoolean(GetConfig("Discord", "Send Notifications", false));
            _sendSlackNotifications = Convert.ToBoolean(GetConfig("Slack", "Send Notifications", false));
            _slackMessageStyle = Convert.ToString(GetConfig("Slack", "Message Style (FancyMessage, SimpleMessage, Message, TicketMessage)", "FancyMessage"));
            _slackChannel = Convert.ToString(GetConfig("Slack", "Channel", "general"));
            _chatCommand = Convert.ToString(GetConfig("Settings", "Explosions Command", "x"));
            _hideSelfFeed = Convert.ToBoolean(GetConfig("Settings", "Hide Self Inflicted Feed", true));
            _ignoreNoOwners = Convert.ToBoolean(GetConfig("Settings", "Ignore Damage To Non-Player Items", true));
            _ignoreTeamMates = Convert.ToBoolean(GetConfig("Settings", "Ignore Damage By Team Mates", true));

            _trackF1 = Convert.ToBoolean(GetConfig("Additional Tracking", "Track F1 Grenades", false));
            _trackBeancan = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Beancan Grenades", false));
            _trackExplosiveAmmo = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Explosive Ammo", false));
            _trackExplosiveAmmoTime = Convert.ToSingle(GetConfig("Additional Tracking", "Track Explosive Ammo Time", 30f));
            _trackExplosiveDeathsOnly = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Explosive Deaths Only", true));
            _trackSupply = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Supply Signals", false));
            _trackSatchel = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Satchel", false));
            _trackEcoRaiding = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Eco Raiding", false));
            _trackTwigDestruction = Convert.ToBoolean(GetConfig("Additional Tracking", "Track Twig Destruction", false));

            _allowPlayerExplosionMessages = Convert.ToBoolean(GetConfig("Players", "Show Explosions", false));
            _allowPlayerDrawing = Convert.ToBoolean(GetConfig("Players", "Allow DDRAW", false));
            _playerPerm = Convert.ToString(GetConfig("Players", "Permission Name", "raidtracker.use"));
            _szPlayerChatCommand = Convert.ToString(GetConfig("Players", "Command Name", "px"));
            _playerDistance = Convert.ToSingle(GetConfig("Players", "Show Explosions Within X Meters", 50f));
            _playerRestrictionTime = Convert.ToSingle(GetConfig("Players", "Limit Command Once Every X Seconds", 60f));
            _notifyAuthed = Convert.ToBoolean(GetConfig("Players", "Notify Authed Users On Explosion", false));
            _notifyOwner = Convert.ToBoolean(GetConfig("Players", "Notify Owner On Explosion", false));

            _messageColor = Convert.ToInt32(GetConfig("DiscordMessages", "Message - Embed Color (DECIMAL)", 3329330));
            _webhookUrl = Convert.ToString(GetConfig("DiscordMessages", "Message - Webhook URL", "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks"));
            _sendDiscordMessages = _webhookUrl != "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks" && !string.IsNullOrEmpty(_webhookUrl);

            usePopups = Convert.ToBoolean(GetConfig("PopupNotifications", "Use Popups", false));
            popupDuration = Convert.ToSingle(GetConfig("PopupNotifications", "Duration", 0f));

            if (_playerRestrictionTime < 0f)
            {
                _allowPlayerDrawing = false;
                _allowPlayerExplosionMessages = false;
            }

            if (!string.IsNullOrEmpty(_szPlayerChatCommand) && !string.IsNullOrEmpty(_playerPerm))
            {
                permission.RegisterPermission(_playerPerm, this);
                cmd.AddChatCommand(_szPlayerChatCommand, this, cmdPX);
            }

            permission.RegisterPermission("raidtracker.see", this);

            if (!string.IsNullOrEmpty(_chatCommand))
            {
                cmd.AddChatCommand(_chatCommand, this, cmdX);
                //cmd.AddConsoleCommand(szChatCommand, this, "ccmdX");
            }

            if (_changed)
            {
                SaveConfig();
                _changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                _changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                _changed = true;
            }
            return value;
        }

        public string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        public string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        #endregion
    }
}