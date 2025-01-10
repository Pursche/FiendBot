using System;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TwitchLib.Api;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Client;
using TwitchLib.Client.Interfaces;
using TwitchLib.Client.Models;

namespace FiendBot
{
    internal class Program
    {
        private static BotContext _context = new BotContext();

        private static Task LogDiscord(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        static async Task Main(string[] args)
        {
            _context.config = new Config("config.yaml");
            _context.db = new Config("db.yaml");

            // Twitch Setup
            _context.channelName = _context.config.GetField<string>("twitch.channelName");
            string twitchBotUsername = _context.config.GetField<string>("twitch.bot.name");
            string twitchAccessToken = _context.config.GetField<string>("twitch.bot.token");
            string twitchClientId = _context.config.GetField<string>("twitch.api.clientId");
            string twitchClientSecret = _context.config.GetField<string>("twitch.api.clientSecret");

            _context.twitchClient = new TwitchClient();
            _context.twitchClient.Initialize(new ConnectionCredentials(twitchBotUsername, twitchAccessToken), _context.channelName);
            _context.twitchClient.OnMessageReceived += OnTwitchMessageReceived;
            _context.twitchClient.Connect();

            // Twitch API Setup
            _context.twitchApi = new TwitchAPI();
            _context.twitchApi.Settings.ClientId = twitchClientId;
            _context.twitchApi.Settings.AccessToken = await TwitchUtils.GetTwitchApiAccessToken(twitchClientId, twitchClientSecret);

            // Discord Setup
            _context.discordClient = new DiscordSocketClient();
            _context.discordBotToken = _context.config.GetField<string>("discord.bot.token");
            await _context.discordClient.LoginAsync(TokenType.Bot, _context.discordBotToken);
            await _context.discordClient.StartAsync();

            _context.discordClient.Log += LogDiscord;

            while (_context.discordClient.ConnectionState != ConnectionState.Connected)
            {
                Console.WriteLine("Not connected to discord yet, waiting 5 seconds");
                await Task.Delay(5000);
            }

            // Monitor Stream Status
            await MonitorStreamStatus();

            Console.WriteLine("Bot is running...");
            await Task.Delay(-1); // Keep the bot running
        }

        private static async Task MonitorStreamStatus()
        {
            var channelInfo = await _context.twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { _context.channelName });
            var channelId = channelInfo.Users.FirstOrDefault()?.Id;

            if (channelId == null)
            {
                Console.WriteLine("Twitch channel not found.");
                return;
            }

            int streamStatusPollSpeed = _context.db.GetField("streamStatusPollSpeed",  60);

            while (true)
            {
                var streamInfo = await _context.twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> { channelId });

                if (streamInfo.Streams.Any())
                {
                    var stream = streamInfo.Streams.First();
                    // Store the stream start time in UTC
                    DateTime streamStartTimeUtc = stream.StartedAt.ToUniversalTime();

                    if (streamStartTimeUtc != _context.streamStartTimeUtc)
                    {
                        _context.streamStartTimeUtc = streamStartTimeUtc;
                        Console.WriteLine("Fetching latest VOD...");
                        bool newStream = await PostLatestVODLink(channelId);
                        if (newStream)
                        {
                            await PostLiveMessage(channelId);
                        }
                    }
                }
                else if (_context.streamStartTimeUtc != DateTime.MinValue)
                {
                    Console.WriteLine("Stream went offline!");
                    _context.streamStartTimeUtc = DateTime.MinValue;
                }

                await Task.Delay(streamStatusPollSpeed * 1000);
            }
        }

        private static async Task PostLiveMessage(string channelId)
        {
            ulong discordChannelId = _context.config.GetField<ulong>("discord.liveChannelId");
            var channel = _context.discordClient.GetChannel(discordChannelId) as ITextChannel;

            if (channel != null)
            {
                string channelLink = $" https://twitch.tv/{_context.channelName}";
                string liveMessage = _context.db.GetField<string>("liveMessage", "Went live: ") + channelLink;

                // Post the message
                await channel.SendMessageAsync($"{liveMessage}");
            }
        }

        private static async Task<bool> PostLatestVODLink(string channelId)
        {
            // Fetch the latest VOD
            var videos = await _context.twitchApi.Helix.Videos.GetVideosAsync(userId: channelId, first: 1);

            if (videos.Videos.Length > 0)
            {
                _context.vodUrl = videos.Videos.First().Url;

                ulong discordChannelId = _context.config.GetField<ulong>("discord.vodChannelId");
                var channel = _context.discordClient.GetChannel(discordChannelId) as ITextChannel;

                if (channel != null)
                {
                    // Check if the VOD URL was already posted
                    var messages = await channel.GetMessagesAsync(50).FlattenAsync();
                    var existingMessage = messages.FirstOrDefault(m => m.Content.Contains(_context.vodUrl));

                    if (existingMessage != null)
                    {
                        // If it was posted, try to get its thread ID
                        if (existingMessage.Thread != null)
                        {
                            _context.vodThreadId = existingMessage.Thread.Id.ToString();
                            Console.WriteLine($"VOD already posted. Thread ID: {_context.vodThreadId}");
                        }
                        else
                        {
                            Console.WriteLine("VOD already posted, but no thread found.");
                        }
                    }
                    else
                    {
                        string newVODMessage = _context.db.GetField<string>("vodMessage", "New VOD:");

                        // If not posted yet, post the message
                        var message = await channel.SendMessageAsync($"{newVODMessage} {_context.vodUrl}");
                        await Task.Delay(5000);
                        await DiscordUtils.CreateThread(_context, discordChannelId, message.Id, "Timestamps", _context.discordBotToken);
                        return true;
                    }
                }
            }
            else
            {
                Console.WriteLine("No VOD found yet.");
            }
            return false;
        }

        // In your main/handler class (make sure it can handle async)
        private static List<Commands.ICommand> _commands = new List<Commands.ICommand>
        {
            new Commands.Timestamp(),
            new Commands.SetCooldown(),
            new Commands.SetMessage(),
            new Commands.Blacklist(),
            new Commands.Unblacklist(),
            new Commands.Reload()
        };

        private static async void OnTwitchMessageReceived(object sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            foreach (var command in _commands)
            {
                if (await command.CanExecute(_context, e.ChatMessage.Message))
                {
                    await command.Execute(_context, e);
                    break;
                }
            }
        }
    }
}