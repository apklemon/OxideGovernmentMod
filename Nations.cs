﻿using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Rust;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins 
{
    [Info("Nations", "BodyweightEnergy", "0.0.1", ResourceId = 0)]
    public class Nations : RustPlugin
    {
        #region Cached Variables

        private const string NationDataFilename = "GovernmentData";
        private const string NationSettingsFilename = "GovernmentSettings";

        FieldInfo displayName = typeof(BasePlayer).GetField("_displayName", (BindingFlags.Instance | BindingFlags.NonPublic));

        private static Dictionary<string, Nation> nations;
        private static Dictionary<string, Nation> lookup;
        private static Dictionary<string, string> originalNames;
        private static Dictionary<string, List<string>> permissionList;
        private static List<string> RankList;
        private static Dictionary<string, Dictionary<string, float>> damageScaleTable;
        private static Dictionary<string, string> assignPermAssociation = new Dictionary<string, string> 
        {
            {"DICTATOR","modify_dictator"}, 
            {"HEAD", "modify_head"},
            {"BOOT", "modify_boot"},
            {"WORKER", "modify_worker"},
            {"CITIZEN", "modify_citizen"}
        };
        

        #endregion

        #region Data Management

        public void SaveData()
        {
            var data = Interface.GetMod().DataFileSystem.GetDatafile(NationDataFilename);
            var settings = Interface.GetMod().DataFileSystem.GetDatafile(NationSettingsFilename);
            // Saving Rank List Data
            var rankData = new List<object>();
            foreach (var rank in RankList) rankData.Add(rank);
            settings["ranks"] = rankData;

            // Saving Permission Data
            var permissionsData = new Dictionary<string, object>();
            foreach (var permission in permissionList)
            {
                var permitList = new List<object>();
                foreach (var rank in permission.Value) permitList.Add(rank);
                permissionsData.Add(permission.Key, permitList);
            }
            settings["permissions"] = permissionsData;

            // Saving Damage Scale Data
            // TODO

            // Saving Government Data
            var nationsData = new Dictionary<string, object>();
            foreach (var nation in nations)
            {
                var nationData = new Dictionary<string, object>();
                nationData.Add("name", nation.Value.Name);
                var members = new Dictionary<string, object>();
                foreach (var imember in nation.Value.Members)
                    members.Add(imember.Key, imember.Value);
                var guests = new List<object>();
                foreach (var iguest in nation.Value.Guests)
                    guests.Add(iguest);
                var inviteds = new List<object>();
                foreach (var iinvited in nation.Value.Inviteds)
                    inviteds.Add(iinvited);
                nationData.Add("members", members);
                nationData.Add("guests", guests);
                nationData.Add("inviteds", inviteds);
                nationsData.Add(nation.Key, nationData);
            }
            data["governments"] = nationsData;
            Interface.GetMod().DataFileSystem.SaveDatafile(NationDataFilename);
            Interface.GetMod().DataFileSystem.SaveDatafile(NationSettingsFilename);
        }

        public void LoadData()
        {
            nations.Clear();
            lookup.Clear();
            RankList.Clear();
            permissionList.Clear();
            var data = Interface.GetMod().DataFileSystem.GetDatafile(NationDataFilename);
            var settings = Interface.GetMod().DataFileSystem.GetDatafile(NationSettingsFilename);

            // Load Rank List
            if (settings["ranks"] != null)
            {
                var rankList = (List<object>)Convert.ChangeType(settings["ranks"], typeof(List<object>));
                foreach (var irank in rankList) 
                { 
                    RankList.Add((string)irank); 
                }
            }

            // Load Permissions List
            if (settings["permissions"] != null)
            {
                var permissionData = (Dictionary<string, object>)Convert.ChangeType(settings["permissions"], typeof(Dictionary<string, object>));
                foreach (var ipermission in permissionData)
                {
                    var permitList = (List<object>)Convert.ChangeType(ipermission.Value, typeof(List<object>));
                    var newPermitList = new List<string>();
                    foreach (var permit in permitList) newPermitList.Add((string)permit);
                    permissionList.Add(ipermission.Key, newPermitList);
                }
            }
            else
            {

            }

            // Load Damage Scale Types Table
            if(settings["damageScales"] != null)
            {
                var damageScaleData = (Dictionary<string, object>)Convert.ChangeType(settings["damageScales"], typeof(Dictionary<string, object>));
                foreach(var attackerData in damageScaleData)
                {
                    var attackerRank = attackerData.Key;
                    var victimData = (Dictionary<string, object>)attackerData.Value;
                    var victims = new Dictionary<string, float>();
                    foreach(var victim in victimData)
                    {
                        var victimRank = victim.Key;
                        var damageScaleValue = (float) Convert.ChangeType(victim.Value, typeof(float));
                        victims.Add(victimRank, damageScaleValue);
                    }
                    damageScaleTable.Add(attackerRank, victims);
                }
            }
            else
            {

            }

            // Load Government Data
            if (data["governments"] != null)
            {
                var govsData = (Dictionary<string, object>)Convert.ChangeType(data["governments"], typeof(Dictionary<string, object>));
                foreach (var igov in govsData)
                {
                    var gov = (Dictionary<string, object>)igov.Value;
                    var tag = (string)igov.Key;
                    var name = (string)gov["name"];
                    var membersData = (Dictionary<string, object>)gov["members"];
                    var members = new Dictionary<string, string>();
                    foreach (var imember in membersData)
                    {
                        var memberID = (string)imember.Key;
                        var memberRank = (string)imember.Value;
                        members.Add(memberID, memberRank);
                    }
                    var guestsData = (List<object>)gov["guests"];
                    var guests = new List<string>();
                    foreach (var iguest in guestsData)
                    {
                        guests.Add(iguest.ToString());
                    }
                    var invitedsData = (List<object>)gov["inviteds"];
                    var inviteds = new List<string>();
                    foreach (var iinvited in invitedsData)
                    {
                        inviteds.Add(iinvited.ToString());
                    }
                    var newGov = new Nation() { Tag = tag, Name = name};
                    foreach (var m in members) newGov.AddMember(m.Key, m.Value);
                    newGov.Guests = guests;
                    newGov.Inviteds = inviteds;
                    nations.Add(tag, newGov);
                }
            }
            Puts("Successfully loaded (" + nations.Count + ") governments.");

        }


        #endregion //Data Management

        #region Plugin Methods

        private bool nationNameExists(string name)
        {
            var exists = false;
            foreach (var gov in nations)
            {
                if (gov.Value.Name == name)
                {
                    exists = true;
                }
            }
            return exists;
        }
        public static string Rank(string rank_str)
        {
            if (RankList.Contains(rank_str))
            {
                return rank_str;
            }
            throw (new Exception("Attempted to assign invalid rank \"" + rank_str + "\"."));
            return null;
        }
        public Nation getNationByUserID (string playerId)
        {
            if (nations.ContainsKey(playerId))
            {
                return nations[playerId];
            }
            return null;
        }
        private string GetRank(string playerId)
        {
            return lookup[playerId].Members[playerId];
        }
        public bool isMemberOfNation (string playerId, string tag)
        {
            return (nations[tag].isMember(playerId));
        }
        public bool isGuestOfNation (string playerId, string tag)
        {
            return (nations[tag].isGuest(playerId));
        }
        public bool isInvitedOfNation(string playerId, string tag)
        {
            return (nations[tag].isInvited(playerId));
        }

        private BasePlayer FindPlayerByPartialName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            BasePlayer player = null;
            name = name.ToLower();
            var allPlayers = BasePlayer.activePlayerList.ToArray();
            // Try to find an exact match first
            foreach (var p in allPlayers)
            {
                if (p.displayName == name)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            if (player != null)
                return player;
            // Otherwise try to find a partial match
            foreach (var p in allPlayers)
            {
                if (p.displayName.ToLower().IndexOf(name) >= 0)
                {
                    if (player != null)
                        return null; // Not unique
                    player = p;
                }
            }
            return player;
        }
        private string StripTag(string name, Nation nation)
        {
            if (nation == null)
                return name;
            var re = new Regex(@"^\[" + nation.Tag + @"\]\s");
            while (re.IsMatch(name))
                name = name.Substring(nation.Tag.Length + 3);

            Puts("StripTag result = " + name);
            return name;
        }
        public static string FindPlayerNameByID(string playerId)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(playerId));
            if (player == null) return "NULL";
            else return player.displayName;
        }

        private bool HasPermission(string playerId, string permissionType)
        {
            if (lookup.ContainsKey(playerId)) 
            {
                var memberRank = lookup[playerId].Members[playerId];
                if (permissionList[permissionType] != null)
                {
                    if (permissionList[permissionType].Contains(memberRank))
                    {
                        return true;
                        Puts("Player " + playerId + " has permission to " + permissionType + ".");
                    }
                }
            }
            return false;
        }

        private void SetupPlayer(BasePlayer player)
        {
            var prevName = player.displayName;
            var playerId = player.userID.ToString();
            var gov = getNationByUserID(playerId);
            displayName.SetValue(player, StripTag(player.displayName, gov));
            if (gov == null)
            {
                return;
            }
            else
            {
                var tag = "[" + gov.Tag + "] ";
                if (!player.displayName.StartsWith(tag))
                    displayName.SetValue(player, tag + prevName);
            }
            if (player.displayName != prevName)
                player.SendNetworkUpdate();
        }
        private void SetupPlayers(List<string> playerIds)
        {
            foreach (var playerId in playerIds)
            {
                var uid = Convert.ToUInt64(playerId);
                var player = BasePlayer.FindByID(uid);
                if (player != null)
                    SetupPlayer(player);
                else
                {
                    player = BasePlayer.FindSleeping(uid);
                    if (player != null)
                        SetupPlayer(player);
                }
            }
        } 

        private void CreateNation(string tag, string name, string creatorID)
        {
            var newGov = new Nation() { Tag = tag, Name = name };
            nations.Add(tag, newGov);
            newGov.AddMember(creatorID, Rank("DICTATOR"));
        }
        private void DisbandNation (string tag)
        {
            if(nations.ContainsKey(tag))
            {
                nations.Remove(tag);
            }
        }

        #endregion

        #region Server & Plugin Hooks

        [HookMethod("OnServerInitialized")]
        private void OnServerInitialized()
        {
            try
            {
            }
            catch (Exception ex)
            {
                Error("OnServerInitialized failed", ex);
            }
        }

        [HookMethod("OnPluginLoaded")]
        private void OnPluginLoaded()
        {
            try
            {
                lookup = new Dictionary<string, Nation>();
                nations = new Dictionary<string, Nation>();
                permissionList = new Dictionary<string, List<string>>();
                RankList = new List<string>();
                damageScaleTable = new Dictionary<string, Dictionary<string, float>>();
                LoadData();
            }
            catch (Exception ex)
            {
                Error("OnPluginLoaded failed ", ex);
            }
        }

        [HookMethod("OnServerSave")]
        private void OnServerSave()
        {
            try
            {
                SaveData();
            }
            catch (Exception ex)
            {
                Error("OnServerSave failed", ex);
            }
        }

        [HookMethod("OnPluginUnloaded")]
        private void OnPluginUnloaded()
        {
            try
            {
                SaveData();
            }
            catch (Exception ex)
            {
                Error("OnPluginLoaded failed", ex);
            }
        }

        [HookMethod("OnEntityTakeDamage")]
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            try
            {
                float damageScale = 1.0f;
                var sb = new StringBuilder();
                if (entity as BasePlayer == null || hitInfo == null) return;
                var attackerPlayer = hitInfo.Initiator as BasePlayer;
                var victimPlayer = entity as BasePlayer;
                var victimID = victimPlayer.userID.ToString();
                var attackerID = attackerPlayer.userID.ToString();
                if (lookup.ContainsKey(attackerID) && lookup.ContainsKey(victimID))
                {
                    var attackerGov = lookup[attackerID];
                    var victimGov = lookup[victimID];
                    if (attackerGov == victimGov)
                    {
                        var attackerRank = attackerGov.Members[attackerID];
                        var victimRank = victimGov.Members[victimID];
                        damageScale = damageScaleTable[attackerRank][victimRank];
                        hitInfo.damageTypes.ScaleAll(damageScale);
                        //sb.Append(attackerID + " attacked " + victimID + 
                        //    " and caused " + damageScale.ToString() + " damage scale of type " + hitInfo.damageTypes.GetMajorityDamageType().ToString());
                    }
                }
                SendReply(hitInfo.Initiator as BasePlayer, sb.ToString());
            } catch (NullReferenceException ex)
            {

            }
        }

        #endregion 

        #region Console Commands

        [ConsoleCommand("gov.dump")]
        private void cmdConsoleDumpData(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            SaveData();
            LoadData();
            // Dump Message Header
            sb.Append("**** Dumping Government Data & Settings ****\n\n");
            // Rank List Dump
            sb.Append("Rank List = [");
            foreach (var rank in RankList)
            {
                sb.Append(rank + ", ");
            }
            sb.Append("]\n\n");
            // Permission List Dump
            sb.Append("Permission List:\n");
            foreach(var perm in permissionList)
            {
                sb.Append(perm.Key + " = [");
                foreach(var rank in perm.Value)
                {
                    sb.Append(rank + ", ");
                }
                sb.Append("]\n");
            }
            sb.Append("\n");
            // Governments Data Dump
            sb.Append("Governments Data:\n\n");
            foreach(var gov in nations)
            {
                sb.Append("[" + gov.Key + "] " + gov.Value.Name + "\n");
                foreach(var member in gov.Value.Members) sb.Append(member.Key + "\t" + member.Value + "\t" + FindPlayerNameByID(member.Key) + "\n");
                foreach (var guest in gov.Value.Guests) sb.Append(guest + "\tGUEST\t" + FindPlayerNameByID(guest) + "\n");
                foreach (var invited in gov.Value.Inviteds) sb.Append(invited + "\tINVITED\t" + FindPlayerNameByID(invited) + "\n");
                {

                }
            }
                sb.Append("\n");
            // Lookup Data Dump
                sb.Append("Lookup Data:\n");
            foreach(var pair in lookup)
            {
                sb.Append(pair.Key + "\t" + pair.Value.Tag + "\t" + pair.Value.Members[pair.Key] +"\t"+ FindPlayerNameByID(pair.Key) + "\n");
            }
            
            PrintToConsole(arg.Player(), sb.ToString());
        }

        #endregion

        #region Chat Commands

        [ChatCommand("gov_create")]
        private void cmdChatGovCreate(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if(args.Length != 2)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (lookup.ContainsKey(playerId))
            {
                sb.Append("You are already a member of a government.");
            }
            else if (nations.ContainsKey(args[0]))
            {
                sb.Append("This tag has already been taken. Try another tag.");
            }
            else if (nationNameExists(args[1]))
            {
                sb.Append("This name has already been taken. Try another name.");
            }
            else
            {
                CreateNation(args[0], args[1], player.userID.ToString());
                sb.Append("You have successfully created the " + args[1] + " government, and you are the dictator of it.");
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_invite")]
        private void cmdChatGovInvite(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 1)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any government.");
            }
            else if (!HasPermission(playerId, "modify_citizen"))
            {
                sb.Append("You do not have permission to invite.");
            }
            else
            {
                var invitedPlayer = FindPlayerByPartialName(args[0]);
                var invitedID = invitedPlayer.userID.ToString();
                var invitingGov = lookup[playerId];
                if (invitingGov.Inviteds.Contains(invitedID)) sb.Append("You already invited this player.");
                else
                {
                    invitingGov.Inviteds.Add(invitedID);
                    sb.Append("You have invited " + invitedPlayer.displayName + " to join your domain.");
                    sb.Append(" They must leave their existing government first (if they are a member of one)");
                    sb.Append(", then type \"/gov_join " + invitingGov.Tag + "\" to join yours.");
                    SendReply(invitedPlayer, "You have been invited to become a citizen in the " + invitingGov.Name + ". To join, you must leave any government you are part of, then type /gov_join " + invitingGov.Tag + ".");
                }
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_uninvite")]
        private void cmdChatGovUninvite(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 1)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any government.");
            }
            else if (!HasPermission(playerId, "modify_citizen"))
            {
                sb.Append("You do not have permission to invite.");
            }
            else
            {
                var invitedPlayer = FindPlayerByPartialName(args[0]);
                var invitedID = invitedPlayer.userID.ToString();
                var invitingGov = lookup[playerId];
                if (!invitingGov.Inviteds.Contains(invitedID)) sb.Append("This player is already not invited to join your government.");
                else
                {
                    invitingGov.Inviteds.Remove(invitedID);
                    sb.Append("You have withdrawn your invitation for " + invitedPlayer.displayName + " to join your domain.");
                    SendReply(invitedPlayer, "The invitation to join the " + invitingGov.Name + " has been withdrawn.");
                }
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_kick")]
        private void cmdChatGovKick(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 1)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any government.");
            }
            else
            {
                var kickedPlayer = FindPlayerByPartialName(args[0]);
                var kickedPlayerName = kickedPlayer.displayName;
                var kickedPlayerID = kickedPlayer.userID.ToString();
                if (kickedPlayer != null)
                {
                    if (!lookup.ContainsKey(kickedPlayerID)) sb.Append("This player is not a member of your domain.");
                    else if (lookup[kickedPlayerID] != lookup[playerId]) sb.Append("This player is not a member of your domain.");
                    else
                    {
                        var kickedPlayerRank = GetRank(kickedPlayerID);
                        var permission = assignPermAssociation[kickedPlayerRank];
                        if (!HasPermission(playerId, permission))
                        {
                            sb.Append("You do not have permission to kick this player.");
                        }
                        else
                        {
                            lookup[playerId].RemoveMember(kickedPlayerID);
                            sb.Append("You have successfully kicked " + kickedPlayerName + " from your government.");
                            lookup[playerId].Broadcast(kickedPlayerName + " has been banished from the " + lookup[playerId].Name + " government.");
                            SendReply(kickedPlayer, "You have been banished from the " + lookup[playerId].Name + " government.");
                        }
                    }
                }
                else sb.Append("Player name does not exist or isn't unique.");
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_assign_rank")]
        private void cmdChatGovAssignRank(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 2)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any government.");
            }
            else
            {
                var assignedPlayer = FindPlayerByPartialName(args[0]).userID.ToString();
                var assignedPlayerName = FindPlayerByPartialName(args[0]).displayName;
                var assignedRank = Rank(args[1]);
                if (assignedPlayer == null) sb.Append("Player name does not exist or is not unique.");
                else if (lookup[assignedPlayer] != lookup[playerId]) sb.Append("The player is not a member of your government.");
                else if (!HasPermission(playerId, assignPermAssociation[assignedRank])) sb.Append("You do not have permission to " + assignPermAssociation[assignedRank] + ".");
                else
                {
                    lookup[playerId].Members[assignedPlayer] = assignedRank;
                    sb.Append(assignedPlayerName + " has been successfully assigned as " + assignedRank + ".");
                }

            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_join")]
        private void cmdChatGovJoin(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 1)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (lookup.ContainsKey(playerId))
            {
                sb.Append("To join another government, you must leave this one first by typing");
                sb.Append(" \"/gov_leave\".");
            }
            else if(!nations.ContainsKey(args[0]))
            {
                sb.Append("No such government exists.");
            }
            else if (!nations[args[0]].isInvited(playerId))
            {
                sb.Append("You were not invited to join this government. Make sure a permitted member of that government has already invited you.");
            }
            else
            {
                var invitingGov = nations[args[0]];
                invitingGov.AddMember(playerId, Rank("CITIZEN"));
                invitingGov.Inviteds.Remove(playerId);
                sb.Append("You are now a " + GetRank(playerId) + " of the " + lookup[playerId].Name + " government.");
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_leave")]
        private void cmdChatGovLeave(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 0)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any governments.");
            }
            else
            {
                var leftGov = lookup[playerId];
                leftGov.RemoveMember(playerId);
                sb.Append("You are no longer a member of the " + leftGov.Name + " government.");
                leftGov.Broadcast(FindPlayerNameByID(playerId) + " has left the " + leftGov.Name + " government.");
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_broadcast")]
        private void cmdChatGovBroadcast(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args == null)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if (!HasPermission(playerId, "broadcast_all")) sb.Append("You do not have permission to broadcast.");
            else
            {
                var bsb = new StringBuilder();
                foreach(var arg in args)
                {
                    bsb.Append(arg + " ");
                }
                lookup[playerId].Broadcast(bsb.ToString());
                sb.Append("Broadcast sent successfully.");
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_info")]
        private void cmdChatGovInfo(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            var playerId = player.userID.ToString();
            if (args.Length != 0)
            {
                sb.Append("Invalid command. Type /gov_help for more info.");
            }
            else if(!lookup.ContainsKey(playerId))
            {
                sb.Append("You are not a member of any government.");
            }
            else
            {
                var gov = lookup[playerId];
                sb.Append("Your Government's Info:\n");
                sb.Append("[" + gov.Tag + "] " + gov.Name + "\n");
                var sortedList = gov.GetSortedMemberList();
                foreach (var member in sortedList) sb.Append(member.Key + "\t" + member.Value + "\t" + FindPlayerNameByID(member.Key) + "\n");
                foreach (var guest in gov.Guests) sb.Append(guest + "\tGUEST\t" + FindPlayerNameByID(guest) + "\n");
                foreach (var invited in gov.Inviteds) sb.Append(invited + "\tINVITED\t" + FindPlayerNameByID(invited) + "\n");      
            }
            SendReply(player, sb.ToString());
            SaveData();
        }

        [ChatCommand("gov_help")]
        private void cmdChatGovHelp(BasePlayer player, string command, string[] args)
        {
            var sb = new StringBuilder();
            sb.Append("Available Commands:\n");
            sb.Append("<color=#ffd479>/gov_create <GOV TAG> <GOV NAME></color>\n");
            sb.Append("<color=#ffd479>/gov_invite <PARTIAL PLAYER NAME></color>\n");
            sb.Append("<color=#ffd479>/gov_join <GOV TAG></color>\n");
            sb.Append("<color=#ffd479>/gov_assign_rank <PARTIAL PLAYER NAME> <RANK></color>\n");
            sb.Append("<color=#ffd479>/gov_kick <PARTIAL PLAYER NAME></color>\n");
            sb.Append("<color=#ffd479>/gov_leave</color>\n");
            sb.Append("<color=#ffd479>/gov_broadcast <MESSAGE></color>\n");
            sb.Append("<color=#ffd479>/gov_info</color>\n");
            SendReply(player, sb.ToString());
            SaveData();
        }

        #endregion

        #region class Nation

        public class Nation
        {
            // Private Members
            public string Tag { get; set; }
            public string Name { get; set; }
            public Dictionary<string, string> Members { get; set; }
            public List<string> Guests { get; set; }
            public List<string> Inviteds { get; set; }
            // Properties
            public string Dictator
            {
                get
                {
                    foreach (var member in Members)
                    {
                        if(member.Value == "DICTATOR")
                        {
                            return member.Key;
                        }
                    }
                    return null;
                }
                set
                {
                    var newCrownId = value;
                    bool crownExists = false;
                    foreach(var member in Members)
                    {
                        if (member.Value == "DICTATOR")
                        {
                            crownExists = true;
                        }
                    }
                    if(!crownExists)
                    {
                        Members[newCrownId] = "DICTATOR";
                    }
                }
            }
            // Default Constructor
            public Nation ()
            {
                Members = new Dictionary<string, string>();
                Guests = new List<string>();
                Inviteds = new List<string>();
            }
            // Methods
            public bool isMember(string playerId)
            {
                return (Members.ContainsKey(playerId) && lookup.ContainsKey(playerId));
            }
            public bool isGuest(string playerId)
            {
                return (Guests.Contains(playerId));
            }
            public bool isInvited (string playerId)
            {
                return (Inviteds.Contains(playerId));
            }

            public void AddMember(string playerId, string rank)
            {
                if (!isMember(playerId) || !lookup.ContainsKey(playerId))
                {
                    Members.Add(playerId, Rank(rank));
                    lookup.Add(playerId, this);
                }
            }
            public void ModifyMember(string playerId, string rank)
            {
                if (isMember(playerId) && lookup.ContainsKey(playerId))
                {
                    Members[playerId] = rank;
                }
            }
            public void RemoveMember (string playerId)
            {
                if (isMember(playerId) && lookup.ContainsKey(playerId))
                {
                    Members.Remove(playerId);
                    lookup.Remove(playerId);
                }
            }

            public List<KeyValuePair<string,string>> GetSortedMemberList()
            {
                List<KeyValuePair<string, string>> myList = Members.ToList();

                myList.Sort((firstPair, nextPair) =>
                {
                    return firstPair.Value.CompareTo(nextPair.Value);
                }
                );
                return myList;
            }

            public void Broadcast(string message)
            {
                string message_header = "<color=#a1ff46>(GOV BROADCAST)</color> ";

                // Send message to members
                foreach (var member in Members)
                {
                    var player = BasePlayer.FindByID(Convert.ToUInt64(member.Key));
                    if (player == null)
                        continue;
                    player.SendConsoleCommand("chat.add", "", message_header + message);
                }

            }
        }

        #endregion

        #region Utility Methods

        private void Log(string message) {
            Interface.Oxide.LogInfo("{0}: {1}", Title, message);
        }

        private void Warn(string message) {
            Interface.Oxide.LogWarning("{0}: {1}", Title, message);
        }

        private void Error(string message, Exception ex = null) {
            if (ex != null)
                Interface.Oxide.LogException(string.Format("{0}: {1}", Title, message), ex);
            else
                Interface.Oxide.LogError("{0}: {1}", Title, message);
        }

        #endregion

        #region Settings Defaults

        //private const Dictionary<string, Dictionary<string, float>> damageScaleTable_default = 
        //{

        //};

        #endregion
    }
}
