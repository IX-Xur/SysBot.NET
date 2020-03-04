using System.Threading.Tasks;
using Discord.Commands;

namespace SysBot.Pokemon.Discord
{
    [Summary("Checks, Clears, and Toggles Priorty Role cooldowns")]
    public class PriorityModule : ModuleBase<SocketCommandContext>
    {
        [Command("cooldownClear")]
        [Alias("cc")]
        [Summary("Clears the priority role cooldown for the user")]
        [RequireSudo]
        public async Task ClearUserCooldown()
        {
            Context.User.ClearCooldown();
            await ReplyAsync("User's Cooldown has been cleared.").ConfigureAwait(false);
        }

        [Command("cooldownClearAll")]
        [Alias("cca")]
        [Summary("Clears all priority role cooldowns")]
        [RequireSudo]
        public async Task ClearAllCooldowns()
        {
            PriorityUtil.ClearAllCooldowns();
            await ReplyAsync("All cooldowns have been cleared.").ConfigureAwait(false);
        }

        [Command("cooldownStatus")]
        [Alias("cs")]
        [Summary("Checks the status of your priority cooldown")]
        public async Task CheckCooldownStatus()
        {
            PriorityUtil.CheckUserCooldown(Context.User, out var msg);
            await ReplyAsync(msg).ConfigureAwait(false);
        }
    }
}
