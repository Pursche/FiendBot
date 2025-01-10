using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Communication.Interfaces;

namespace FiendBot.Commands
{
    public class Timestamp : ICommand
    {
        public Task<bool> CanExecute(BotContext context, string message)
        {
            return Task.FromResult(message.ToLower().StartsWith("!timestamp "));
        }

        public async Task Execute(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            if (context.streamStartTimeUtc == DateTime.MinValue)
            {
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, the stream is offline.");
                return;
            }

            // Check user permissions
            bool isBroadcaster = e.ChatMessage.IsBroadcaster;
            bool isModerator = e.ChatMessage.IsModerator;
            bool isSubscriber = e.ChatMessage.IsSubscriber;
            bool isVip = e.ChatMessage.Badges != null && e.ChatMessage.Badges.Any(b => b.Key == "vip");

            if (!isBroadcaster && !isModerator && !isSubscriber && !isVip)
            {
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, you do not have permission to use this command.");
                return;
            }

            // Check if the user is in the blacklist
            List<string> blacklist = context.db.GetStringList("blacklist");
            if (blacklist.Contains(e.ChatMessage.Username.ToLower()))
            {
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, you have been blacklisted from using this command.");
                return;
            }

            // Check for cooldown
            long messageTimeMs = long.Parse(e.ChatMessage.TmiSentTs);
            int cooldown = context.db.GetField<int>("cooldown", 30);
            if (messageTimeMs < context.lastTimestamp + cooldown * 1000)
            {
                return;
            }
            context.lastTimestamp = messageTimeMs;

            // Get comment text after "!timestamp "
            string comment = e.ChatMessage.Message.Substring("!timestamp ".Length)
                .Replace("@", string.Empty)
                .Replace("/", string.Empty);

            // Convert the TmiSentTs to a DateTime in UTC
            DateTime messageTimeUtc = DateTimeOffset
                .FromUnixTimeMilliseconds(messageTimeMs)
                .UtcDateTime;

            // Reply in Twitch chat to confirm
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                $"@{e.ChatMessage.Username}, timestamp bookmarked: \"{comment}\"");

            // If you need to await something, do it here (e.g., network IO)
            string message = e.ChatMessage.Username + ": " + comment;
            DiscordUtils.PostToThread(context, message, messageTimeUtc);
        }
    }
}
