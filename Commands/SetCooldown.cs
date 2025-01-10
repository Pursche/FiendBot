using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiendBot.Commands
{
    public class SetCooldown : ICommand
    {
        public Task<bool> CanExecute(BotContext context, string message)
        {
            return Task.FromResult(message.ToLower().StartsWith("!fiendotabot setcooldown "));
        }

        void SendUsageError(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, Invalid usage: !fiendotabot setcooldown <seconds>");
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

            // Get message text after "!fiendotabot setcooldown"
            string message = e.ChatMessage.Message.Substring("!fiendotabot setcooldown".Length);

            // Get cooldown value as int
            int cooldown;
            if (!int.TryParse(message, out cooldown))
            {
                SendUsageError(context, e);
                return;
            }

            context.db.SetField("cooldown", cooldown);
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, set cooldown to {cooldown} seconds.");
        }
    }
}
