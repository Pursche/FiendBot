using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FiendBot
{
    public static class DiscordUtils
    {
        public static async Task CreateThread(BotContext context, ulong channelId, ulong messageId, string threadName, string botToken)
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
                context.vodThreadId = threadData.Id; // Save the thread ID
                Console.WriteLine($"Thread created successfully: {context.vodThreadId}");
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

        public static async void PostToThread(BotContext context, string comment, DateTime messageTimeUtc)
        {
            if (context.vodThreadId != "")
            {
                ulong threadId = ulong.Parse(context.vodThreadId);
                var thread = context.discordClient.GetChannel(threadId) as ITextChannel;

                if (thread != null)
                {
                    // Time since stream start
                    TimeSpan offset = messageTimeUtc - context.streamStartTimeUtc;

                    // Format: 0h0m0s
                    string offsetFormatted = $"{(int)offset.TotalHours}h{offset.Minutes}m{offset.Seconds}s";

                    // Build the timestamped VOD URL
                    string timestampLink = $"{context.vodUrl}?t={offsetFormatted}";

                    await thread.SendMessageAsync($"{timestampLink} - {comment}");
                }
            }
        }
    }
}
