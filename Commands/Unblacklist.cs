using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiendBot.Commands
{
    public class Unblacklist : ICommand
    {
        public Task<bool> CanExecute(BotContext context, string message)
        {
            return Task.FromResult(message.ToLower().StartsWith("!fiendotabot unblacklist"));
        }

        void SendUsageError(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, Invalid usage: !fiendotabot unblacklist *username*");
        }

        public async Task Execute(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            // Check user permissions
            bool isBroadcaster = e.ChatMessage.IsBroadcaster;
            bool isModerator = e.ChatMessage.IsModerator;

            if (!isBroadcaster && !isModerator)
            {
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, you do not have permission to use this command.");
                return;
            }

            // Get username text after "!fiendotabot blacklist"
            string username = e.ChatMessage.Message.Substring("!fiendotabot unblacklist".Length).Trim();

            // Add username to blacklist
            context.db.RemoveStringFromList("blacklist", username.ToLower());
            context.db.Save();

            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, Removed {username} from blacklist");
        }
    }
}
