using Oxide.Core;
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
        enum Rank
        {
            ENEMY = -2,
            BANISHED = -1,
            NONE = 0,
            INVITEE = 1,
            GUEST = 2,
            CITIZEN = 3,
            BUILDER = 4,
            BOOT = 5,
            HEAD = 6,
            DICTATOR = 7
        }

        // Cached Data Storage
        public Dictionary<ulong, Nation> Lookup { get; }
        public List<Nation> Nations { get; }
        public Dictionary<ulong, string> OriginalDisplayNames { get; }

        Loaded()
        {

        }

        public class Nation
        {
            public string Tag { get; set; }
            public string Name { get; set; }
            public Dictionary<ulong, Rank> Members { get; }
            public Dictionary<ulong, String> MemberNotes { get; }
        }
        
        public static class DataManager
        {
            public Nations plugin { set; }
        }

        public static class GroupManager
        {
            public Nations plugin { set; }
        }

        public static class PermissionManager
        {
            public Nations plugin { set; }
        }

        public static class UIManager
        {
            public Nations plugin { set; }
        }

        public static class ConsoleManager
        {
            public Nations plugin { set; }
        }
        public static class ChatManager
        {
            public Nations plugin { set; }
        }
    }
}
