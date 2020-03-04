﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Discord;
using PKHeX.Core;
using Discord.WebSocket;
using SysBot.Base;

namespace SysBot.Pokemon.Discord
{
    public static class PriorityUtil
    {
        private static readonly string CooldownPath = Path.Combine(Directory.GetCurrentDirectory(), "cooldowns.json");
        private static Dictionary<string, string> cooldowns = new Dictionary<string, string>();
        private static readonly string[] PriorityRoles = SysCordInstance.Manager.Config.Discord.PriorityRoles.Split(new[] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries);
        private static readonly int CooldownPeriod = GetCooldownPeriod();

        private static int GetCooldownPeriod()
        {
            try
            {
                return int.Parse(SysCordInstance.Manager.Config.Discord.PriorityCooldown);
            } catch (FormatException)
            {
                LogUtil.LogInfo("Cooldown period was not a number!", "DiscordPriority");
                return 60;
            }
        }

        public static void GetInitialCooldowns()
        {
            if (File.Exists(CooldownPath))
            {
                LogUtil.LogInfo("Loading cooldowns file...", "DiscordPriority");
                var lines = File.ReadAllText(CooldownPath);
                cooldowns = JsonConvert.DeserializeObject<Dictionary<string,string>>(lines);

            }
        }

        public static uint EvaluatePriority(this IUser user)
        {
            if (user.GetIsSudo())
            {
                LogUtil.LogInfo(string.Format("User {0} has a sudo role", user.Username), "DiscordPriority");
                return PokeTradeQueue<PK8>.Tier1;
            }
            
            if (PriorityRoles.Equals(new[] { "DISABLE" }) || PriorityRoles.Equals(new string[] { } ))
            {
                LogUtil.LogInfo("Priority Roles Disabled!", "DiscordPriority");
                return PokeTradeQueue<PK8>.TierFree;
            }
            
            if (cooldowns.ContainsKey(user.Id.ToString()) && CooldownPeriod != -1)  // Ignore if cooldown period is disabled
            {
                TimeSpan timePassed = DateTime.Now.Subtract(DateTime.Parse(cooldowns[user.Id.ToString()]));
                if (!(timePassed.TotalMinutes > CooldownPeriod))
                {
                    LogUtil.LogInfo("Cooldown Not Met!", "DiscordPriority");
                    return PokeTradeQueue<PK8>.TierFree;
                } else
                {
                    LogUtil.LogInfo("Cooldown Removed!", "DiscordPriority");
                    cooldowns.Remove(user.Id.ToString());
                }
            }

            foreach (string role in PriorityRoles)
            {
                if (((SocketGuildUser)user).Roles.Any(r => r.Name == role))
                {
                    var priority = (uint)(Array.IndexOf(PriorityRoles, role) + 2);
                    Timestamp(user);
                    LogUtil.LogInfo(string.Format("Assigning user {0} priority {1} from role {2}", user.Username, priority, role), "DiscordPriority");
                    return priority;
                }
            }

            LogUtil.LogInfo(string.Format("User {0} does not have a priority role", user.Username), "DiscordPriority");
            return PokeTradeQueue<PK8>.TierFree;
        }

        public static void Timestamp(this IUser user)
        {
            if (!cooldowns.ContainsKey(user.Id.ToString()) && !user.GetIsSudo())  // Sudo Ignores Cooldown
            {
                LogUtil.LogInfo(string.Format("User {0} added to cooldown list", user.Username), "DiscordPriority");
                cooldowns.Add(user.Id.ToString(), DateTime.Now.ToString());
                var lines = JsonConvert.SerializeObject(cooldowns, Formatting.Indented);
                File.WriteAllText(CooldownPath, lines);
            } else
            {
                LogUtil.LogError(string.Format("Error: User {0} is already on cooldown!", user.Username), "DiscordPriority");
            }
        }

        public static void ClearCooldown(this IUser user)
        {
            if (cooldowns.ContainsKey(user.Id.ToString()))
            {
                LogUtil.LogInfo(string.Format("Cleared cooldown for user {0}", user.Username), "DiscordPriority");
                cooldowns.Remove(user.Id.ToString());
            }
        }

        public static void ClearAllCooldowns()
        {
            cooldowns.Clear();
        }


    }
}
