using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace VanitySniper
{
    class Program
    {
        private static readonly string discordHost = "canary.discord.com";
        private static readonly string discordToken = "yetkili token";
        private static readonly string guildId = "sunucu id";
        private static readonly string gatewayUrl = "wss://gateway.discord.gg/?v=9&encoding=json";
        private static readonly string os = "Linux";
        private static readonly string browser = "Firefox";
        private static readonly string device = "nightullah XD";

        private static string vanity;
        private static readonly Dictionary<string, string> guilds = new Dictionary<string, string>();

        private static SslStream sslStream;

        static async Task Main(string[] args)
        {
            while (true)
            {
                await InitializeTLSSocket(8443);
                Thread.Sleep(360000);
            }
        }

        private static async Task InitializeTLSSocket(int port)
        {
            try
            {
                using (var client = new TcpClient(discordHost, port))
                using (sslStream = new SslStream(client.GetStream(), false, new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                {
                    await sslStream.AuthenticateAsClientAsync(discordHost);

                    var websocket = new WebSocket(gatewayUrl);
                    websocket.OnMessage += async (sender, e) => await HandleWebSocketMessage(websocket, e.Data);
                    websocket.OnClose += (sender, e) => Environment.Exit(0);
                    websocket.Connect();

                    SendGetRequest();

                    while (true)
                    {
                        Thread.Sleep(4250);
                        SendGetRequest();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static void SendGetRequest()
        {
            var getRequest = "GET / HTTP/1.1\r\n" +
                             $"Host: {discordHost}\r\n" +
                             "\r\n";
            byte[] requestBytes = Encoding.ASCII.GetBytes(getRequest);
            sslStream.Write(requestBytes);
        }

        private static async Task HandleWebSocketMessage(WebSocket websocket, string messageData)
        {
            var json = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(messageData);
            string t = json.t;
            int op = json.op;

            if (t == "GUILD_UPDATE")
            {
                string guildId = json.d.guild_id;
                string vanityUrlCode = json.d.vanity_url_code;
                if (guilds.TryGetValue(guildId, out var currentVanity) && currentVanity != vanityUrlCode)
                {
                    await UpdateVanityUrl(guildId, currentVanity);
                }
            }
            else if (t == "READY")
            {
                foreach (var guild in json.d.guilds)
                {
                    string guildId = guild.id;
                    string vanityUrlCode = guild.vanity_url_code;
                    if (vanityUrlCode != null)
                    {
                        guilds[guildId] = vanityUrlCode;
                    }
                    else
                    {
                        Console.WriteLine(guild.name);
                    }
                }

                Console.WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(guilds));
            }

            if (op == 10)
            {
                await websocket.SendAsync("{\"op\": 2, \"d\": {\"token\": \"" + discordToken + "\", \"intents\": 1, \"properties\": {\"os\": \"" + os + "\", \"browser\": \"" + browser + "\", \"device\": \"" + device + "\"}}}", null);
                int heartbeatInterval = json.d.heartbeat_interval;
                var heartbeatTimer = new Timer(async _ => await websocket.SendAsync("{\"op\": 1, \"d\": null}", null), null, heartbeatInterval, heartbeatInterval);
            }
            else if (op == 7)
            {
                Environment.Exit(0);
            }
        }

        private static async Task UpdateVanityUrl(string guildId, string vanityCode)
        {
            using (var httpClient = new HttpClient())
            {
                var requestBody = new { code = vanityCode };
                var content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                httpClient.DefaultRequestHeaders.Add("Authorization", discordToken);
                var response = await httpClient.PatchAsync($"https://{discordHost}/api/v9/guilds/{guildId}/vanity-url", content);

                if (response.IsSuccessStatusCode)
                {
                    vanity = vanityCode;
                    Console.WriteLine($"Vanity URL updated to: {vanity}");
                }
                else
                {
                    Console.WriteLine($"Failed to update vanity URL: {response.StatusCode}");
                }
            }
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

    public static class HttpClientExtensions
    {
        public static Task<HttpResponseMessage> PatchAsync(this HttpClient client, string requestUri, HttpContent content)
        {
            var request = new HttpRequestMessage
            {
                Method = new HttpMethod("PATCH"),
                RequestUri = new Uri(requestUri),
                Content = content
            };

            return client.SendAsync(request);
        }
    }
}
