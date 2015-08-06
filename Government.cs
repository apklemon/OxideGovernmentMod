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
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Government", "BodyweightEnergy", "0.0.1", ResourceId = 0)]
    public class Government : RustPlugin
    {
        public static string[] RankList = new string[] { "CROWN", "HEAD", "BOOT", "WORKER", "CITIZEN" };

        public static string dataFilename = "government_datafile";
        public static Dictionary<string, Domain> lookup;     //contains <playerId, Domain>
        public static Dictionary<string, Domain> domains;    //contains <domainTag, Domain>

        // Saves the data file
        public void SaveData()
        {
            var data = Interface.GetMod().DataFileSystem.GetDatafile(dataFilename);
            var domainsData = new Dictionary<string,object>();
            foreach (var domain in domains)
            {
                var domainData = new Dictionary<string,object>();
                domainData.Add("name", domain.Value.Name);
                var members = new Dictionary<string,object>();
                foreach (var imember in domain.Value.Members)
                {
                    var memberData = new Dictionary<string, object>();
                    memberData.Add("playerRank", imember.Rank);
                    memberData.Add("playerNotes", imember.PlayerNotes);
                    members.Add(imember.UserID, memberData);
                }
                var guests = new List<object>();
                foreach (var iguest in domain.Value.Guests)
                    guests.Add(iguest);
                domainData.Add("members", members);
                domainData.Add("guests", guests);
                domainsData.Add(domain.Value.Tag, domainData);
            }
            data["domains"] = domainsData;
            Interface.GetMod().DataFileSystem.SaveDatafile(dataFilename);
        }

        // Loads the data file
        public void LoadData()
        {
            domains.Clear();
            var data = Interface.GetMod().DataFileSystem.GetDatafile(dataFilename);
            if (data["domains"] != null)
            {
                var domainsData = (Dictionary<string,object>) Convert.ChangeType(data["domains"], typeof(Dictionary<string,object>));
                foreach (var idomain in domainsData) 
                {
                    var domain = (Dictionary<string,object>) idomain.Value;
                    var tag = (string) idomain.Key;
                    var name = (string)domain["name"];
                    var membersData = (Dictionary<string,object>) domain["members"];
                    var members = new List<Member>();
                    foreach (var imemberData in membersData)
                    {
                        var memberData = (Dictionary<string, object>) imemberData.Value;
                        var member = new Member() { UserID = imemberData.Key.ToString(), Rank = memberData["playerRank"].ToString(), PlayerNotes = memberData["playerNotes"].ToString() };
                        members.Add(member);
                    }
                    var guestsData = (List<object>)domain["guests"];
                    var guests = new List<string>();
                    foreach (var iguestData in guestsData)
                    {
                        guests.Add(iguestData.ToString());
                    }
                    var newDomain = new Domain(tag, name);
                    foreach (var member in members)
                    {
                        newDomain.AddMember(member.UserID);
                        newDomain.ChangeMemberRank(member.UserID, member.Rank);
                        newDomain.AssignPlayerNotes(member.UserID, member.PlayerNotes);
                    }
                    newDomain.Guests = guests;
                }
                Puts("Datafile loaded ({0}) domains successfully.", domains.Count);
            }
        }

        
        // Finds a player by partial name
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

        public static string FindPlayerNameByID(string playerId)
        {
            var player = BasePlayer.FindByID(Convert.ToUInt64(playerId));
            if (player == null) return "NULL";
            else return player.displayName;
        }

        private Domain FindPlayerDomain(string playerId)
        {
            if (lookup.ContainsKey(playerId))
            {
                return lookup[playerId];
            }
            return null;
        }
        public static string AssignRank(string rank)
        {

            if (RankList.Contains(rank))
            {
                return rank;
            }
            else
            {
                throw new Exception("Attempt to assign an invalid rank.");
                return null;
            }
        }

        [HookMethod("OnServerInitialized")]
        void OnServerInitialized()
        {
            try
            {
                lookup = new Dictionary<string, Domain>();
                domains = new Dictionary<string, Domain>();
                LoadData();
            }
            catch (Exception ex)
            {
                Error("OnServerInitialized failed", ex);
            }
        }

        [ChatCommand("gov")]
        private void cmdChatDomain(BasePlayer player, string command, string[] args)
        {
            Puts("Entered cmdChatDomain function.");
            string playerId = player.userID.ToString();
            var playerDomain = (lookup.ContainsKey(playerId) ? lookup[playerId] : null);
            var sb = new StringBuilder();

            if (args.Length == 0)
            {
                sb.Append("Your steamID is " + playerId + " and you're a " + playerDomain.GetMemberRank(player.userID.ToString()).ToString() + " to the " + (playerDomain != null ? playerDomain.Name : "null") + " Domain");
            }

            else
            {
                switch (args[0])
                {
                    case "create":
                        Puts("Entered create case.");
                        if (args.Length != 3)
                        {
                            sb.Append("Invalid command syntax. Use /gov create <domainTag> <domainName>");
                        }
                        //else if (playerDomain != null)
                        //{
                        //    sb.Append("You are already a member of a domain.");
                        //}
                        else
                        {
                            var newDomain1 = new Domain(args[1], args[2], playerId);
                            sb.Append("You have successfully created the " + args[2] + " domain.");
                        }
                        break;
                    case "leave":
                        Puts("Entered leave case.");
                        if (args.Length != 1)
                        {
                            sb.Append("Invalid command syntax. Use /gov leave");
                        }
                        else if (playerDomain == null)
                        {
                            sb.Append("You are not a member of any domain.");
                        }
                        else
                        {
                            playerDomain.RemoveMember(playerId);
                            sb.Append("You have successfully left the " + playerDomain.Name + " Domain.");
                        }
                        break;
                    case "dump":
                        if (domains.Count == 0)
                        {
                            sb.Append("No domains in registry.");
                        }
                        else
                        {
                            foreach (var domain in domains)
                            {
                                var dumpedDomain = domain.Value;
                                sb.Append(dumpedDomain.DumpData());
                            }
                        }
                        break;
                    default:
                        Puts("Entered default case.");
                        sb.Append("Error in command.");
                        break;
                }
            }
            SendReply(player, sb.ToString());
            SaveData();

        }

        [ConsoleCommand("gov.dump")]
        private void cmdDumpData(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            if (domains.Count == 0)
            {
                sb.Append("No domains in registry.");
            }
            else
            {
                foreach (var domain in domains)
                {
                    var dumpedDomain = domain.Value;
                    sb.Append(dumpedDomain.DumpData());
                }
            }
            PrintToConsole(arg.Player(), sb.ToString());
            SaveData();
        }

        [ConsoleCommand("gov.lookup")]
        private void cmdDumpLookup(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            if (lookup.Count == 0)
            {
                sb.Append("No players in registry.");
            }
            else foreach (var player in lookup)
                {
                    string domainName = "";
                    if (player.Value == null)
                    {
                        domainName = "NULL_DOMAIN";
                    }
                    else
                    {
                        domainName = player.Value.Name;
                    }
                    sb.Append(player.Key.ToString() + "\t" + domainName + "\n");
                }
            PrintToConsole(arg.Player(), sb.ToString());
        }

        [ConsoleCommand("gov.test")]
        private void cmdGovTest(ConsoleSystem.Arg arg)
        {
            var sb = new StringBuilder();
            if (arg.Args == null)
            {
                sb.Append("Invalid number of arguments.");
            }
            else if (arg.Args.Length == 0)
            {
                sb.Append("Invalid number of arguments.");
            }
            else
            {
                var args = arg.Args;
                var firstArg = args[0];
                switch (firstArg)
                {
                    case "cleardata":
                        domains.Clear();
                        lookup.Clear();
                        SaveData();
                        LoadData();
                        break;
                    case "createdomain":
                        if (args.Length != 3)
                        {
                            sb.Append("Correct syntax: gov.test createdomain <domainTag> <domainName>");
                        }
                        else if (domains.ContainsKey(args[1]))
                        {
                            sb.Append("Domain tag already exists. Choose another.");
                        }
                        else
                        {
                            var newDomain = new Domain(args[1], args[2]);
                            sb.Append("New domain created: [" + args[1] + "] " + args[2]);
                        }
                        break;
                    case "createplayer":
                        if (args.Length != 2 && args.Length != 3 && args.Length != 4)
                        {
                            sb.Append("Correct Syntax: gov.test createplayer <playerId> [<domainTag>]");
                        }
                        else if (args.Length == 2)
                        {
                            var playerId = args[1];
                            lookup.Add(playerId, null);
                            sb.Append("New domainless player " + playerId + " added.");
                        }
                        else
                        {
                            var playerId = args[1];
                            var domainTag = args[2];
                            if (!domains.ContainsKey(domainTag))
                            {
                                sb.Append("No such domain exists.");
                            }
                            else
                            {
                                var domain = domains[domainTag];
                                domain.AddMember(playerId);
                                if (args.Length == 4)
                                {
                                    domain.ChangeMemberRank(playerId, args[3]);
                                    sb.Append("Successfully added player " + playerId + " to the " + domain.Name + " domain as a " + args[3] +".");
                                }
                                else sb.Append("Successfully added player " + playerId + " to the " + domain.Name + " domain as a CITIZEN.");
                            }
                        }
                        break;
                    case "changerank":
                        if (args.Length != 3)
                        {
                            sb.Append("Correct Syntax: gov.test changerank <playerId> <rank>");
                        }
                        else
                        {
                            var playerId = args[1];
                            if (!lookup.ContainsKey(playerId))
                            {
                                sb.Append("Player with ID " + playerId + " was not found.");
                            }
                            else
                            {
                                if (lookup[playerId] == null)
                                {
                                    sb.Append("Cannot modify player's rank since he doesn't belong to any domain.");
                                }
                                else
                                {
                                    var playerDomain = lookup[playerId];
                                    playerDomain.ChangeMemberRank(playerId, args[2]);
                                    sb.Append("Player's rank successfully changed to " + args[2]);
                                }
                            }
                        }
                        break;
                    default:
                        sb.Append("Invalid command.");
                        break;
                }
            }
            PrintToConsole(arg.Player(), sb.ToString());
            SaveData();

        }

        // Represents a member
        public class Member 
        {
            private string userID;
            private string playerRank;
            private string playerNotes;

            public string UserID
            {
                get { return userID; }
                set { userID = value; }
            }
            public string Rank
            {
                get { return playerRank; }
                set { playerRank = value; }
            }
            public string PlayerNotes
            {
                get { return playerNotes; }
                set { playerNotes = value; }
            }
        }

        // Represents a domain
        public class Domain
        {
            private string name;
            private string tag;
            private List<Member> members;
            private List<string> guests;

            public string Name
            {
                get { return name; }
                set { name = value; }
            }
            public string Tag
            {
                get { return tag; }
                set { tag = value; }
            }
            public List<Member> Members
            {
                get { return members; }
                set { members = value; }
            }
            public List<string> Guests
            {
                get { return guests; }
                set { guests = value; }
            }
            public int Size
            {
                get
                {
                    int size = Members.Count;
                    return size;
                }
            }

            public Domain()
            {
                members = new List<Member>();
                guests = new List<string>();
                domains.Add("NOTAG", this);
            }
            public Domain(string tag, string name)
            {
                Tag = tag;
                Name = name;
                members = new List<Member>();
                guests = new List<string>();
                domains.Add(tag, this);
            }
            public Domain(string tag, string name, string crownId)
            {
                Tag = tag;
                Name = name;
                members = new List<Member>();
                guests = new List<string>();
                domains.Add(tag, this);
                AddMember(crownId);
                ChangeMemberRank(crownId, AssignRank("CROWN"));
                AssignPlayerNotes(crownId, "This player originally created the " + Name + " domain.");
            }

            public Member Crown
            {
                get
                {
                    foreach (var member in Members)
                    {
                        if (member.Rank == AssignRank("CROWN"))
                        {
                            return member;
                        }
                    }
                    return null;
                }
            }
            public bool IsMember (string playerId)
            {
                return (lookup[playerId] == this);
            }
            public bool IsGuest(string playerId)
            {
                return guests.Contains(playerId);
            }
            public Member FindMemberByID(string playerId)
            {
                if (IsMember(playerId))
                {
                    foreach (var member in Members)
                    {
                        if (playerId == member.UserID) return member;
                    }
                }
                return null;
            }
            public string GetMemberRank (string playerId)
            {
                var member = FindMemberByID(playerId);
                var rank = member.Rank;
                return rank;
            }
            public bool AddMember(string playerId)
            {
                var newMember = new Member() { UserID = playerId, Rank = AssignRank("CITIZEN"), PlayerNotes = "" };
                Members.Add(newMember);
                lookup.Add(playerId, this);
                if (!Members.Contains(newMember) || !(lookup[playerId] == this))
                {
                    return false;
                }
                SortMemberListByRank();
                return true;
            }
            public bool RemoveMember(string playerId)
            {
                if (!IsMember(playerId))
                {
                    return false;
                }
                else
                {
                    var removedMember = FindMemberByID(playerId);
                    if (!(removedMember == null))
                    {
                        Members.Remove(removedMember);
                        lookup.Remove(playerId);
                        SortMemberListByRank();
                        return true;
                    }
                    else return false;
                }
                return false;
            }
            public bool ChangeMemberRank(string playerId, string newRank)
            {
                var changedMember = FindMemberByID(playerId);
                if (changedMember == null)
                {
                    return false;
                }
                else
                {
                    changedMember.Rank = AssignRank(newRank);
                    SortMemberListByRank();
                    return true;
                }
            }
            public bool AssignPlayerNotes(string playerId, string notes)
            {
                var member = FindMemberByID(playerId);
                if (member != null)
                {
                    member.PlayerNotes = notes;
                    return true;
                }
                return false;
            }
            public void Broadcast(string message) {
                string message_header = "<color=#a1ff46>(DOMAIN BROADCAST)</color> ";

                // Send message to members
                foreach (var member in members) { 
                    var player = BasePlayer.FindByID(Convert.ToUInt64(member.UserID));
                    if (player == null)
                        continue;
                    player.SendConsoleCommand("chat.add", "", message_header + message);
                }

            }
            private void SortMemberListByRank()
            {
                
                List<Member> newMemberList = new List<Member>();
                foreach (var rank in RankList)
                {
                    foreach (var member in Members)
                    {
                        if (member.Rank == rank) newMemberList.Add(member);
                    }
                }
                members = newMemberList;
            }
            public string DumpData()
            {
                var sb = new StringBuilder();
                sb.Append("[" + tag + "] " + name + "\n");
                if (Members.Count == 0)
                {
                    sb.Append("No members.\n");
                }
                else
                {
                    SortMemberListByRank();
                    sb.Append("Members:\n");
                    foreach (var member in Members)
                    {
                        var memberName = FindPlayerNameByID(member.UserID);
                        sb.Append(member.UserID + "\t" + member.Rank + "\t" + memberName + "\t" + member.PlayerNotes);
                        sb.Append("\n");
                    }
                }
                return sb.ToString();
            }
        }


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
    }
}
