using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiendBot.Commands
{
    public class SetMessage : ICommand
    {
        public Task<bool> CanExecute(BotContext context, string message)
        {
            return Task.FromResult(message.ToLower().StartsWith("!fiendotabot setmessage "));
        }

        void SendUsageError(BotContext context, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, Invalid usage: !fiendotabot setmessage <live/vod> *message*");
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

            // Get message text after "!fiendotabot setmessage"
            string message = e.ChatMessage.Message.Substring("!fiendotabot setmessage ".Length);

            // Split the message on the next word
            string[] parts = message.Split(' ', 2);

            if (parts.Length <= 1)
            {
                SendUsageError(context, e);
                return;
            }

            // Check if the first part is "live" or "vod"
            if (parts[0].ToLower() == "live")
            {
                context.db.SetField("liveMessage", parts[1]);
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, live message set to: \"{parts[1]}\"");
                context.db.Save();
            }
            else if (parts[0].ToLower() == "vod")
            {
                context.db.SetField("vodMessage", parts[1]);
                context.twitchClient.SendMessage(e.ChatMessage.Channel,
                                       $"@{e.ChatMessage.Username}, VOD message set to: \"{parts[1]}\"");
                context.db.Save();
            }
            else
            {
                SendUsageError(context, e);
                return;
            }
        }
    }
}
