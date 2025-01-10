using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiendBot.Commands
{
    public class Reload : ICommand
    {
        public Task<bool> CanExecute(BotContext context, string message)
        {
            return Task.FromResult(message.ToLower().StartsWith("!fiendotabot reload"));
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

            context.db.Reload();
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, reloaded DB.");
        }
    }
}
