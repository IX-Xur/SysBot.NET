using PKHeX.Core;

namespace SysBot.Pokemon.Twitch
{
    public static class TwitchCommandsHelper
    {
        // Helper functions for commands
        public static bool AddToWaitingList(string setstring, string display, string username, out string msg)
        {
            ShowdownSet set = TwitchShowdownUtil.ConvertToShowdown(setstring);

            if (set.InvalidLines.Count != 0)
            {
                msg = $"Skipping trade: Unable to parse Showdown Set:\n{string.Join("\n", set.InvalidLines)}";
                return false;
            }

            var sav = AutoLegalityWrapper.GetTrainerInfo(PKX.Generation);
            PKM pkm = sav.GetLegal(set, out _);
            var valid = new LegalityAnalysis(pkm).Valid;
            if (valid && pkm is PK8 p8)
            {
                var tq = new TwitchQueue(p8, new PokeTradeTrainerInfo(display),
                    username);
                TwitchBot.QueuePool.Add(tq);
                msg = "Added you to the waiting list. Please whisper to me your trade code! Your request from the waiting list will be removed if you are too slow!";
                return true;
            }

            msg = "Skipping trade: Unable to legalize the Pok�mon.";
            return false;
        }

        public static string ClearTrade(bool sudo, ulong userid)
        {
            var allowed = sudo || TwitchBot.Info.GetCanQueue();
            if (!allowed)
                return "Sorry, you are not permitted to use this command!";

            var userID = userid;
            var result = TwitchBot.Info.ClearTrade(userID);
            return result switch
            {
                QueueResultRemove.CurrentlyProcessing => "Looks like you're currently being processed! Removed from queue.",
                QueueResultRemove.Removed => "Removed you from the queue.",
                _ => "Sorry, you are not currently in the queue."
            };
        }
    }
}
