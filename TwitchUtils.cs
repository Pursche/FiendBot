using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace FiendBot
{
    public static class TwitchUtils
    {
        internal class TwitchTokenResponse
        {
            [JsonPropertyName("access_token")]
            public string AccessToken { get; set; }

            [JsonPropertyName("expires_in")]
            public int ExpiresIn { get; set; }

            [JsonPropertyName("token_type")]
            public string TokenType { get; set; }
        }

        public static async Task<string> GetTwitchApiAccessToken(string clientId, string clientSecret)
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
    }
}
