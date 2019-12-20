﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma.Steam
{
    partial class SteamManager
    {
        public const int STEAMP2P_OWNER_PORT = 30000;

        public const uint AppID = 602960;

        private Facepunch.Steamworks.Client client;
        private Facepunch.Steamworks.Server server;

        private static readonly List<string> initializationErrors = new List<string>();
        public static IEnumerable<string> InitializationErrors
        {
            get { return initializationErrors; }
        }

        public const string MetadataFileName = "filelist.xml";

        private readonly Dictionary<string, int> tagCommonness = new Dictionary<string, int>()
        {
            { "submarine", 10 },
            { "item", 10 },
            { "monster", 8 },
            { "art", 8 },
            { "mission", 8 },
            { "event set", 8 },
            { "total conversion", 5 },
            { "environment", 5 },
            { "item assembly", 5 },
            { "language", 5 }
        };

        private readonly List<string> popularTags = new List<string>();
        public static IEnumerable<string> PopularTags
        {
            get
            {
                if (instance == null || !instance.isInitialized) { return Enumerable.Empty<string>(); }
                return instance.popularTags;
            }
        }

        private static SteamManager instance;
        public static SteamManager Instance
        {
            get
            {
#if !USE_STEAM
                return null;
#endif

                if (instance == null) { instance = new SteamManager(); }
                return instance;
            }
        }
                        
        private bool isInitialized;
        public static bool IsInitialized
        {
            get
            {
                return Instance != null && Instance.isInitialized;
            }
        }
        
        public static void Initialize()
        {
#if !USE_STEAM
            DebugConsole.NewMessage("USE_STEAM is not defined; Steam functionality disabled");
            return;
#endif
            instance = new SteamManager();
        }

        public static ulong GetSteamID()
        {
            if (instance == null || !instance.isInitialized)
            {
                return 0;
            }

            if (instance.client != null)
            {
                return instance.client.SteamId;
            }
            else if (instance.server != null)
            {
                return instance.server.SteamId;
            }

            return 0;
        }

        public static string GetUsername()
        {
            if (instance == null || !instance.isInitialized || instance.client == null)
            {
                return "";
            }
            return instance.client.Username;
        }

        public static void OverlayCustomURL(string url)
        {
            if (instance == null || !instance.isInitialized || instance.client == null)
            {
                return;
            }

            instance.client.Overlay.OpenUrl(url);
        }
        
        public static bool UnlockAchievement(string achievementName)
        {
            if (instance == null || !instance.isInitialized || instance.client == null)
            {
                return false;
            }

            DebugConsole.Log("Unlocked achievement \"" + achievementName + "\"");

            bool unlocked = instance.client.Achievements.Trigger(achievementName);
            if (!unlocked)
            {
                //can be caused by an incorrect identifier, but also happens during normal gameplay:
                //SteamAchievementManager tries to unlock achievements that may or may not exist 
                //(discovered[whateverbiomewasentered], kill[withwhateveritem], kill[somemonster] etc) so that we can add
                //some types of new achievements without the need for client-side changes.
#if DEBUG
                DebugConsole.NewMessage("Failed to unlock achievement \"" + achievementName + "\".");
#endif
            }

            return unlocked;
        }


        public static bool IncrementStat(string statName, int increment)
        {
            if (instance == null || !instance.isInitialized || instance.client == null) { return false; }
            DebugConsole.Log("Incremented stat \"" + statName + "\" by " + increment);
            bool success = instance.client.Stats.Add(statName, increment);
            if (!success)
            {
#if DEBUG
                DebugConsole.NewMessage("Failed to increment stat \"" + statName + "\".");
#endif
            }
            return success;
        }

        public static bool IncrementStat(string statName, float increment)
        {
            if (instance == null || !instance.isInitialized || instance.client == null) { return false; }
            DebugConsole.Log("Incremented stat \"" + statName + "\" by " + increment);
            bool success = instance.client.Stats.Add(statName, increment);
            if (!success)
            {
#if DEBUG
                DebugConsole.NewMessage("Failed to increment stat \"" + statName + "\".");
#endif
            }
            return success;
        }
        
        public static void Update(float deltaTime)
        {
            if (instance == null || !instance.isInitialized) { return; }

            instance.client?.Update();
            instance.server?.Update();

            SteamAchievementManager.Update(deltaTime);
        }

        public static void ShutDown()
        {
            if (instance == null) { return; }

            instance.client?.Dispose();
            instance.client = null;
            instance.server?.Dispose();
            instance.server = null;
            instance = null;
        }

        public static UInt64 SteamIDStringToUInt64(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) { return 0; }
            UInt64 retVal;
            if (UInt64.TryParse(str, out retVal) && retVal >(1<<52)) { return retVal; }
            if (str.ToUpper().IndexOf("STEAM_") != 0) { return 0; }
            string[] split = str.Substring(6).Split(':');
            if (split.Length != 3) { return 0; }

            if (!UInt64.TryParse(split[0], out UInt64 universe)) { return 0; }
            if (!UInt64.TryParse(split[1], out UInt64 y)) { return 0; }
            if (!UInt64.TryParse(split[2], out UInt64 accountNumber)) { return 0; }

            UInt64 accountInstance = 1; UInt64 accountType = 1;

            return (universe << 56) | (accountType << 52) | (accountInstance << 32) | (accountNumber << 1) | y;
        }

        public static string SteamIDUInt64ToString(UInt64 uint64)
        {
            UInt64 y = uint64 & 0x1;
            UInt64 accountNumber = (uint64 >> 1) & 0x7fffffff;
            UInt64 universe = (uint64 >> 56) & 0xff;

            return "STEAM_" + universe.ToString() + ":" + y.ToString() + ":" + accountNumber.ToString();
        }
    }
}
