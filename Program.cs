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
using TwitchLib.Client.Models;

namespace FiendBot
{
    internal class Program
    {
        private static ConfigManager _configManager;
        private static TwitchAPI _twitchApi;
        private static TwitchClient _twitchClient;
        private static DiscordSocketClient _discordClient;
        private static string _vodThreadId = ""; // Store the thread ID for VOD comments
        private static string _vodUrl = ""; // Store the VOD URL
        private static string _discordBotToken = ""; // Discord bot token
        private static DateTime _streamStartTimeUtc = DateTime.MinValue;

        static async Task Main(string[] args)
        {
            _configManager = new ConfigManager("config.yaml");

            // Twitch Setup
            string twitchChannelName = _configManager.GetField<string>("twitch.channelName");
            string twitchBotUsername = _configManager.GetField<string>("twitch.bot.name");
            string twitchAccessToken = _configManager.GetField<string>("twitch.bot.token");
            string twitchClientId = _configManager.GetField<string>("twitch.api.clientId");
            string twitchClientSecret = _configManager.GetField<string>("twitch.api.clientSecret");

            _twitchClient = new TwitchClient();
            _twitchClient.Initialize(new ConnectionCredentials(twitchBotUsername, twitchAccessToken), twitchChannelName);
            _twitchClient.OnMessageReceived += OnTwitchMessageReceived;
            _twitchClient.Connect();

            // Twitch API Setup
            _twitchApi = new TwitchAPI();
            _twitchApi.Settings.ClientId = twitchClientId;
            _twitchApi.Settings.AccessToken = await GetTwitchApiAccessToken(twitchClientId, twitchClientSecret);

            // Discord Setup
            _discordClient = new DiscordSocketClient();
            _discordBotToken = _configManager.GetField<string>("discord.bot.token");
            await _discordClient.LoginAsync(TokenType.Bot, _discordBotToken);
            await _discordClient.StartAsync();

            _discordClient.Log += LogDiscord;

            while (_discordClient.ConnectionState != ConnectionState.Connected)
            {
                Console.WriteLine("Not connected to discord yet, waiting 5 seconds");
                await Task.Delay(5000);
            }

            // Monitor Stream Status
            await MonitorStreamStatus(twitchChannelName);

            Console.WriteLine("Bot is running...");
            await Task.Delay(-1); // Keep the bot running
        }

        private class TwitchTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }
        }

        private static async Task<string> GetTwitchApiAccessToken(string clientId, string clientSecret)
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync(
                $"https://id.twitch.tv/oauth2/token?client_id={clientId}&client_secret={clientSecret}&grant_type=client_credentials",
                null);

            var responseBody = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonSerializer.Deserialize<TwitchTokenResponse>(responseBody);

            // tokenResponse.AccessToken now has the valid access token
            return tokenResponse.AccessToken;
        }

        private static async Task MonitorStreamStatus(string twitchChannelName)
        {
            var channelInfo = await _twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { twitchChannelName });
            var channelId = channelInfo.Users.FirstOrDefault()?.Id;

            if (channelId == null)
            {
                Console.WriteLine("Twitch channel not found.");
                return;
            }

            while (true)
            {
                var streamInfo = await _twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> { channelId });

                if (streamInfo.Streams.Any())
                {
                    var stream = streamInfo.Streams.First();
                    // Store the stream start time in UTC
                    DateTime streamStartTimeUtc = stream.StartedAt.ToUniversalTime();

                    if (streamStartTimeUtc != _streamStartTimeUtc)
                    {
                        _streamStartTimeUtc = streamStartTimeUtc;
                        Console.WriteLine("Stream went live!");
                        Console.WriteLine("Fetching latest VOD...");
                        await PostLatestVODLink(channelId);
                    }
                }
                else if (_streamStartTimeUtc != DateTime.MinValue)
                {
                    Console.WriteLine("Stream went offline!");
                    _streamStartTimeUtc = DateTime.MinValue;
                }

                await Task.Delay(60000); // Check every 60 seconds
            }
        }

        private static async Task PostLatestVODLink(string channelId)
        {
            // Fetch the latest VOD
            var videos = await _twitchApi.Helix.Videos.GetVideosAsync(userId: channelId, first: 1);

            if (videos.Videos.Length > 0)
            {
                _vodUrl = videos.Videos.First().Url;

                ulong discordChannelId = _configManager.GetField<ulong>("discord.channelId");
                var channel = _discordClient.GetChannel(discordChannelId) as ITextChannel;

                if (channel != null)
                {
                    var message = await channel.SendMessageAsync($"New VOD: {_vodUrl}");
                    await Task.Delay(5000);
                    await CreateThread(discordChannelId, message.Id, "VOD Comments", _discordBotToken);
                }
            }
            else
            {
                Console.WriteLine("No VOD found yet. Retrying...");
            }
        }

        private static void OnTwitchMessageReceived(object sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
        {
            // Only proceed if the message starts with "!timestamp "
            if (!e.ChatMessage.Message.StartsWith("!timestamp "))
                return;

            // Check user permissions
            bool isBroadcaster = e.ChatMessage.IsBroadcaster;
            bool isModerator = e.ChatMessage.IsModerator;
            bool isSubscriber = e.ChatMessage.IsSubscriber;
            bool isVip = e.ChatMessage.Badges != null && e.ChatMessage.Badges.Any(b => b.Key == "vip");

            if (!isBroadcaster && !isModerator && !isSubscriber && !isVip)
            {
                // Inform the user they lack permission
                _twitchClient.SendMessage(e.ChatMessage.Channel,
                    $"@{e.ChatMessage.Username}, you do not have permission to use this command.");
                return;
            }

            // Get the comment text after "!timestamp "
            string comment = e.ChatMessage.Message.Substring("!timestamp ".Length);

            // Convert the TmiSentTs to a DateTime in UTC
            long messageTimeMs = long.Parse(e.ChatMessage.TmiSentTs);
            DateTime messageTimeUtc = DateTimeOffset
                .FromUnixTimeMilliseconds(messageTimeMs)
                .UtcDateTime;

            // Reply in Twitch chat to confirm
            _twitchClient.SendMessage(e.ChatMessage.Channel,
                $"@{e.ChatMessage.Username}, timestamp bookmarked: \"{comment}\"");

            // Pass the time and comment along to your posting function
            PostToDiscordThread(comment, messageTimeUtc);
        }

        private static async void PostToDiscordThread(string comment, DateTime messageTimeUtc)
        {
            if (_vodThreadId != "")
            {
                ulong threadId = ulong.Parse(_vodThreadId);
                var thread = _discordClient.GetChannel(threadId) as ITextChannel;

                if (thread != null)
                {
                    // Time since stream start
                    TimeSpan offset = messageTimeUtc - _streamStartTimeUtc;

                    // Format: 0h0m0s
                    string offsetFormatted = $"{(int)offset.TotalHours}h{offset.Minutes}m{offset.Seconds}s";

                    // Build the timestamped VOD URL
                    string timestampLink = $"{_vodUrl}?t={offsetFormatted}";

                    await thread.SendMessageAsync($"{timestampLink} - {comment}");
                }
            }
        }

        private static string GetFormattedTimestamp(string twitchTimestamp)
        {
            TimeSpan t = TimeSpan.FromMilliseconds(long.Parse(twitchTimestamp));
            return $"{t.Hours}h{t.Minutes}m{t.Seconds}s";
        }

        private static Task LogDiscord(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private static async Task CreateThread(ulong channelId, ulong messageId, string threadName, string botToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);

            var payload = new
            {
                name = threadName,
                auto_archive_duration = 1440 // 1 day in minutes
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var endpoint = $"https://discord.com/api/v10/channels/{channelId}/messages/{messageId}/threads";
            var response = await client.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var threadData = JsonSerializer.Deserialize<DiscordThreadResponse>(responseBody);
                _vodThreadId = threadData.Id; // Save the thread ID
                Console.WriteLine($"Thread created successfully: {_vodThreadId}");
            }
            else
            {
                Console.WriteLine($"Failed to create thread: {response.StatusCode}");
                Console.WriteLine(await response.Content.ReadAsStringAsync());
            }
        }

        private class DiscordThreadResponse
        {
            [JsonPropertyName("id")]
            public string Id { get; set; }
        }
    }
}