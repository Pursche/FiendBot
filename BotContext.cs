using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Api;
using TwitchLib.Client;

namespace FiendBot
{
    public class BotContext
    {
        public Config config; // Configuration, loaded from a file and never modified
        public Config db; // Database, can get modified by chat commands and will get resaved

        public TwitchAPI twitchApi;
        public TwitchClient twitchClient;
        public DiscordSocketClient discordClient;

        public string channelName = ""; // Store the channel name
        public string vodThreadId = ""; // Store the thread ID for VOD comments
        public string vodUrl = ""; // Store the VOD URL
        public string discordBotToken = ""; // Discord bot token
        public DateTime streamStartTimeUtc = DateTime.MinValue;
        public long lastTimestamp = 0;
    }
}
