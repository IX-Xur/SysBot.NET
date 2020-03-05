using System;
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
        private static readonly string[] PriorityCooldowns = SysCordInstance.Manager.Config.Discord.PriorityCooldowns.Split(new[] { ", ", "," }, StringSplitOptions.RemoveEmptyEntries);
        private static readonly int DefaultCooldownPeriod = SysCordInstance.Manager.Config.Discord.DefaultPriorityCooldown;

        public static void GetInitialCooldowns()
        {
            if (File.Exists(CooldownPath))
            {
                LogUtil.LogInfo("Loading cooldowns file...", "DiscordPriority");
                var lines = File.ReadAllText(CooldownPath);
                cooldowns = JsonConvert.DeserializeObject<Dictionary<string,string>>(lines);

            }
        }

        private static int GetCooldownForRole(string role)
        {
            try
            {
                return int.TryParse(PriorityCooldowns[Array.IndexOf(PriorityRoles, role)], out int result) ? result : DefaultCooldownPeriod;
            } catch (IndexOutOfRangeException)
            {
                LogUtil.LogError("Roles and Cooldowns do not match!", "DiscordPriority");
                return DefaultCooldownPeriod;
            }
        }

        private static string GetHighestRole(IUser user)
        {
            foreach (string role in PriorityRoles)
            {
                if (((SocketGuildUser)user).Roles.Any(r => r.Name == role))
                {
                    return role;
                }
            }
            return "";
        }

        private static uint PriorityHelper(IUser user, out string highestRole, out bool hasPriority, out int cooldownPeriod)
        {
            highestRole = GetHighestRole(user);
            hasPriority = !highestRole.Equals("");
            cooldownPeriod = hasPriority ? GetCooldownForRole(highestRole) : DefaultCooldownPeriod;
            return hasPriority ? (uint)Array.IndexOf(PriorityRoles, highestRole) + 2 : PokeTradeQueue<PK8>.TierFree;
        }

        private static int PriorityHelper(IUser user)
        {
            var highestRole = GetHighestRole(user);
            var hasPriority = !highestRole.Equals("");
            return hasPriority ? GetCooldownForRole(highestRole) : DefaultCooldownPeriod;
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

            var priority = PriorityHelper(user, out string highestRole, out bool hasPriority, out int CooldownPeriod);
            
            if (cooldowns.ContainsKey(user.Id.ToString()) && CooldownPeriod != -1)  // Ignore if cooldown period is disabled
            {
                TimeSpan timePassed = CheckTimePassed(user);
                if (!(timePassed.TotalMinutes > CooldownPeriod))
                {
                    LogUtil.LogInfo("Cooldown Not Met!", "DiscordPriority");
                    return PokeTradeQueue<PK8>.TierFree;
                } 
                else
                {
                    LogUtil.LogInfo("Cooldown Removed!", "DiscordPriority");
                    cooldowns.Remove(user.Id.ToString());
                }
            }

            if (hasPriority)
            {
                LogUtil.LogInfo(string.Format("Assigning user {0} priority {1} from role {2}", user.Username, priority, highestRole), "DiscordPriority");
            } else
            {
                LogUtil.LogInfo(string.Format("User {0} does not have a priority role", user.Username), "DiscordPriority");
            }

            return priority;
        }

        public static void Timestamp(this IUser user)
        {
            if (!cooldowns.ContainsKey(user.Id.ToString()) && !user.GetIsSudo())  // Sudo Ignores Cooldown
            {
                LogUtil.LogInfo(string.Format("User {0} added to cooldown list", user.Username), "DiscordPriority");
                cooldowns.Add(user.Id.ToString(), DateTime.Now.ToString());
                var lines = JsonConvert.SerializeObject(cooldowns, Formatting.Indented);
                File.WriteAllText(CooldownPath, lines);
            } 
            else
            {
                LogUtil.LogError(string.Format("Error: User {0} is already on cooldown!", user.Username), "DiscordPriority");
            }
        }

        public static TimeSpan CheckTimePassed(this IUser user)
        {
            if (cooldowns.ContainsKey(user.Id.ToString()))
            {
                return DateTime.Now.Subtract(DateTime.Parse(cooldowns[user.Id.ToString()]));
            } 
            else
            {
                LogUtil.LogError("Error: User does not have a cooldown!", "DiscordPriority");
                return new TimeSpan();
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

        public static void CheckUserCooldown(this IUser user, out string msg)
        {
            if (cooldowns.ContainsKey(user.Id.ToString()))
            {
                var CooldownPeriod = PriorityHelper(user);
                var timePassed = CheckTimePassed(user).TotalMinutes;
                var timeLeft = Math.Max(0, CooldownPeriod - timePassed);  // If time left is negative, set to 0
                msg = string.Format("You last traded {0:F2} minutes ago! Your priority cooldown has {1:F2} minutes left!", timePassed, timeLeft);
            }
            else
            {
                msg = "You do not have a priority cooldown!";
            }
        }
    }
}
