﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using PKHeX.Core;
using SysBot.Base;

namespace SysBot.Pokemon.Twitch
{
    public class TwitchBot
    {
        private static PokeTradeHub<PK8> Hub;
        internal static TradeQueueInfo<PK8> Info => Hub.Queues.Info;

        internal static readonly List<TwitchQueue> QueuePool = new List<TwitchQueue>();
        private readonly TwitchClient client;
        private readonly string Channel;
        private readonly TwitchSettings Settings;

        public TwitchBot(TwitchSettings settings, PokeTradeHub<PK8> hub)
        {
            Hub = hub;
            Settings = settings;

            var credentials = new ConnectionCredentials(settings.Username, settings.Token);
            AutoLegalityWrapper.EnsureInitialized(Hub.Config.Legality);

            var clientOptions = new ClientOptions
            {
                MessagesAllowedInPeriod = settings.ThrottleMessages,
                ThrottlingPeriod = TimeSpan.FromSeconds(settings.ThrottleSeconds)
            };

            if (settings.GenerateAssets)
                AddAssetGeneration();

            Channel = settings.Channel;
            WebSocketClient customClient = new WebSocketClient(clientOptions);
            client = new TwitchClient(customClient);
            client.Initialize(credentials, Channel);

            client.OnLog += Client_OnLog;
            client.OnJoinedChannel += Client_OnJoinedChannel;
            client.OnMessageReceived += Client_OnMessageReceived;
            client.OnWhisperReceived += Client_OnWhisperReceived;
            client.OnNewSubscriber += Client_OnNewSubscriber;
            client.OnConnected += Client_OnConnected;
            client.OnError += (_, e) =>
                LogUtil.LogError(e.Exception.Message + Environment.NewLine + e.Exception.Message, "TwitchBot");
            client.OnConnectionError += (_, e) =>
                LogUtil.LogError(e.BotUsername + Environment.NewLine + e.Error.Message, "TwitchBot");

            client.Connect();
        }

        private void AddAssetGeneration()
        {
            void Create(PokeTradeBot b, PokeTradeDetail<PK8> detail)
            {
                try
                {
                    var file = b.Connection.IP;
                    var name = $"({detail.ID}) {detail.Trainer.TrainerName}";
                    File.WriteAllText($"{file}.txt", name);

                    var next = Hub.Queues.Info.GetUserList().Take(Settings.OnDeckCount);
                    File.WriteAllText("ondeck.txt", string.Join(Environment.NewLine, next));
                }
                catch (Exception e)
                {
                    LogUtil.LogError(e.Message, "TwitchBot");
                }
            }
            Info.Hub.Queues.Forwarders.Add(Create);
        }

        public void StartingDistribution(string message)
        {
            Task.Run(async () =>
            {
                client.SendMessage(Channel, "5...");
                await Task.Delay(1_000).ConfigureAwait(false);
                client.SendMessage(Channel, "4...");
                await Task.Delay(1_000).ConfigureAwait(false);
                client.SendMessage(Channel, "3...");
                await Task.Delay(1_000).ConfigureAwait(false);
                client.SendMessage(Channel, "2...");
                await Task.Delay(1_000).ConfigureAwait(false);
                client.SendMessage(Channel, "1...");
                await Task.Delay(1_000).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(message))
                    client.SendMessage(Channel, message);
            });
        }

        private bool AddToTradeQueue(PK8 pk8, int code, OnWhisperReceivedArgs e, bool sudo, PokeRoutineType type, out string msg)
        {
            // var user = e.WhisperMessage.UserId;
            var userID = ulong.Parse(e.WhisperMessage.UserId);
            var name = e.WhisperMessage.DisplayName;

            var trainer = new PokeTradeTrainerInfo(name);
            var notifier = new TwitchTradeNotifier<PK8>(pk8, trainer, code, e.WhisperMessage.Username, client, Channel);
            var tt = type == PokeRoutineType.DuduBot ? PokeTradeType.Dudu : PokeTradeType.Specific;
            var detail = new PokeTradeDetail<PK8>(pk8, trainer, notifier, tt, code: code);
            var trade = new TradeEntry<PK8>(detail, userID, type, name);

            var added = Info.AddToTradeQueue(trade, userID, PokeTradeQueue<PK8>.TierFree, sudo);

            if (added == QueueResultAdd.AlreadyInQueue)
            {
                msg = "Sorry, you are already in the queue.";
                return false;
            }

            msg = $"Added {name} to the queue. Your current position is: {Info.CheckPosition(userID, type).Position}";
            return true;
        }

        private void Client_OnLog(object sender, OnLogArgs e)
        {
            Console.WriteLine($"{DateTime.Now,-19} [{e.BotUsername}] {e.Data}");
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            Console.WriteLine($"{DateTime.Now,-19} [{e.BotUsername}] Connected {e.AutoJoinChannel}");
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            Console.WriteLine($"{DateTime.Now,-19} [{e.BotUsername}] Joined {e.Channel}");
            client.SendMessage(e.Channel, "Connected!");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            var command = e.ChatMessage.Message.Split(' ')[0].Trim();
            var p = Settings.CommandPrefix;

            if (!command.StartsWith(p) || Hub.Config.Twitch.UserBlacklist.Contains(e.ChatMessage.Username))
                return;

            var c = command.Substring(p.Length).ToLower();

            var msg = HandleCommand(c, e);
            if (msg == null)
                return;

            var channel = e.ChatMessage.Channel;
            client.SendMessage(channel, msg);
        }

        private string HandleCommand(string c, OnMessageReceivedArgs e)
        {
            bool sudo = Settings.IsSudo(e.ChatMessage.Username);
            if (!e.ChatMessage.IsSubscriber && Settings.SubOnlyBot && !sudo)
                return null;

            if (c == "trade")
            {
                var chat = e.ChatMessage;
                var _ = TwitchCommandsHelper.AddToWaitingList(chat.Message.Substring(6).Trim(), chat.DisplayName, chat.Username, out string msg);
                return msg;
            }

            switch (c)
            {
                case "tradestatus":
                    return Info.GetPositionString(ulong.Parse(e.ChatMessage.UserId));
                case "tradeclear":
                    return TwitchCommandsHelper.ClearTrade(sudo, ulong.Parse(e.ChatMessage.UserId));

                case "tradeclearall" when !sudo:
                    return "This command is locked for sudo users only!";
                case "tradeclearall":
                    Info.ClearAllQueues();
                    return "Cleared all queues!";

                case "poolreload" when !sudo:
                    return "This command is locked for sudo users only!";
                case "poolreload":
                    return Info.Hub.Ledy.Pool.Reload() ? $"Reloaded from folder. Pool count: {Info.Hub.Ledy.Pool.Count}" : "Failed to reload from folder.";

                case "poolcount" when !sudo:
                    return "This command is locked for sudo users only!";
                case "poolcount":
                    return $"The pool count is: {Info.Hub.Ledy.Pool.Count}";

                default: return null;
            }
        }

        private void Client_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            if (QueuePool.Count > 100)
            {
                var removed = QueuePool[0];
                QueuePool.RemoveAt(0); // First in, first out
                client.SendMessage(Channel, $"Removed {removed.DisplayName} from the waiting list. (list exceeded maximum count)");
            }

            var user = QueuePool.Find(q => q.UserName == e.WhisperMessage.Username);
            if (user == null)
                return;
            QueuePool.Remove(user);
            var msg = e.WhisperMessage.Message;
            try
            {
                int code = int.Parse(msg);
                var _ = AddToTradeQueue(user.Pokemon, code, e, Settings.IsSudo(user.UserName), PokeRoutineType.LinkTrade, out string message);
                client.SendMessage(Channel, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.Message}");
            }
        }

        private void Client_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
        }
    }
}
