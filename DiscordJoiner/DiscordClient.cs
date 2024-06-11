using System.Text.RegularExpressions;
using System.Net.WebSockets;
using System.Data.SQLite;
using Newtonsoft.Json;
using System.Text;
using System.Net;
using RestSharp;

namespace DiscordJoiner;

public partial class DiscordClient
{
	private readonly string token;
	readonly ClientWebSocket socket;
	private string? sessionId;
	private readonly RestClient client;
	private readonly CustomHttpClient httpClient;

	public DiscordClient(string token)
	{
		client = new("https://discord.com/api/v9/invites");
		this.token = token;
		httpClient = new();
		sessionId = null;
		socket = new();

		Main().GetAwaiter().GetResult();
	}

	private static int GetNativeBuild()
	{
		RestClient client = new();
		RestRequest request = new("https://updates.discord.com/distributions/app/manifests/latest?channel=stable&platform=win&arch=x64", Method.Get);
		var response = client.Execute(request);
		return ((dynamic)JsonConvert.DeserializeObject(response.Content!)!).metadata_version;
	}

	public static void Init()
	{
		var data = new
		{
			os = "Windows",
			browser = "Discord Client",
			release_channel = "stable",
			client_version = "1.0.9146",
			os_version = "10.0.19045",
			os_arch = "x64",
			app_arch = "x64",
			system_locale = "en-US",
			browser_user_agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) discord/1.0.9146 Chrome/120.0.6099.291 Electron/28.2.10 Safari/537.36",
			browser_version = "28.2.10",
			client_build_number = GetBuildNumber(),
			native_build_number = GetNativeBuild(),
			client_event_source = "null",
			design_id = 0
		};

		string jsonData = JsonConvert.SerializeObject(data).Replace("\"null\"", "null");

		Consts.xprops = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonData));
	}

	private async Task Main()
	{
		await socket.ConnectAsync(new Uri("wss://gateway.discord.gg/?encoding=json&v=9&compress=none"), CancellationToken.None);
		await ReceiveMessageAsync(socket);
		await SendMessageAsync(socket, JsonConvert.SerializeObject(new
		{
			op = 2,
			d = new
			{
				token,
				capabilities = 16381,
				properties = new
				{
					os = "Windows",
					browser = "Firefox",
					device = "",
					system_locale = "en",
					browser_user_agent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0",
					browser_version = "125.0",
					os_version = "10",
					referrer = "",
					referring_domain = "",
					referrer_current = "https://discord.com/",
					referring_domain_current = "discord.com",
					release_channel = "stable",
					client_build_number = 287034,
					client_event_source = "null",
					design_id = 0
				},
				presence = new
				{
					status = "unknown",
					since = 0,
					activities = new List<string>(),
					afk = false
				},
				compress = false,
				client_state = new
				{
					guild_versions = new { }
				}
			}
		}).Replace("\"null\"", "null"));

		while (true)
		{
			dynamic result = JsonConvert.DeserializeObject(await ReceiveMessageAsync(socket))!;
			if (result.t == "READY")
			{
				sessionId = result.d.session_id;
				break;
			}
		}
	}

	private static int GetBuildNumber()
	{
		RestClient client = new("https://discord.com");
		RestRequest contentRequest = new("app");
		contentRequest.AddHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:125.0) Gecko/20100101 Firefox/125.0");
		var contentResponse = client.Execute(contentRequest);
		var scripts = MyRegex().Matches(contentResponse.Content!).Reverse();
		foreach(var script in scripts)
		{
			RestRequest scriptRequest = new(script.Value);
			var response = client.Execute(scriptRequest);
			if (response.Content!.Contains("build_number:\""))
			{
				return int.Parse(response.Content!.Split("build_number:\"")[1].Split("\"")[0]);
			}
		}
		return 0;
	}

	public void JoinServer(string invite)
	{
		invite = invite.Replace("https://discord.gg/", "");

		string cookieString = "";
		RestRequest cookieRequest = new("https://discord.com");
		var cookieResponse = client.Execute(cookieRequest);
		
		foreach(Cookie cookie in cookieResponse.Cookies!)
		{
			cookieString += $"{cookie.Name}={cookie.Value}; ";
		}
		cookieString = cookieString[0..^1];

		RestRequest propertiesReqest = new(invite);
		var propertiesResponse = client.Execute(propertiesReqest);
		dynamic propsResponse = JsonConvert.DeserializeObject(propertiesResponse.Content!)!;

		string contextProps = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(new
		{
			location = "Join Guild",
			location_guild_id = propsResponse.guild.id,
			location_channel_id = propsResponse.channel.id,
			location_channel_type = propsResponse.channel.type
		})));

		string useragent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) discord/1.0.9146 Chrome/120.0.6099.291 Electron/28.2.10 Safari/537.36";

		List<string> inviteHeaders = [
			"authority: discord.com",
			"accept: */*",
			"accept-language: en-US,en-DE;q=0.9,en-GB;q=0.8",
			$"authorization: {token}",
			$"cookie: {cookieString}",
			"origin: https://discord.com",
			"referer: https://discord.com/channels/@me",
			"sec-ch-ua: \"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\"",
			"sec-ch-ua-mobile: ?0",
			"sec-ch-ua-platform: \"Windows\"",
			"sec-fetch-dest: empty",
			"sec-fetch-mode: cors",
			"sec-fetch-site: same-origin",
			$"user-agent: {useragent}",
			$"x-context-properties: {contextProps}",
			"x-debug-options: bugReporterEnabled",
			"x-discord-locale: en-US",
			"x-discord-timezone: America/Los_Angeles",
			$"x-super-properties: {Consts.xprops}"
		];

		var response = httpClient.Post($"https://discord.com/api/v9/invites/{invite}", JsonConvert.SerializeObject(new
		{
			session_id = sessionId
		}), inviteHeaders)!;
		if (response == null)
		{
			Console.WriteLine($"Failed to make request!");
			return;
		}
		if (response.StatusCode == HttpStatusCode.OK)
		{
			Console.WriteLine("Successfully joined Server!");
		} else
		{
			Console.WriteLine($"Failed to join Server! Resposne Body: {response.Content}");
		}
	}

	private static async Task<string> ReceiveMessageAsync(ClientWebSocket webSocket)
	{
		byte[] buffer = new byte[1024];
		while (webSocket.State == WebSocketState.Open)
		{
			WebSocketReceiveResult result;
			using (var ms = new MemoryStream())
			{
				do
				{
					result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
					ms.Write(buffer, 0, result.Count);
				} while (!result.EndOfMessage);

				if (result.MessageType == WebSocketMessageType.Text)
				{
					string receivedMessage = Encoding.UTF8.GetString(ms.ToArray());
					return receivedMessage;
				}
				else if (result.MessageType == WebSocketMessageType.Close)
				{
					await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
				}
			}

			if (result.EndOfMessage && result.Count != buffer.Length)
			{
				buffer = new byte[result.Count];
			}
		}
		return "";
	}

	private static async Task SendMessageAsync(ClientWebSocket webSocket, string message)
	{
		byte[] buffer = Encoding.UTF8.GetBytes(message);
		await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
	}

	[GeneratedRegex(@"/assets/.{26}\.js", RegexOptions.IgnoreCase, "en-BI")]
	private static partial Regex MyRegex();
}
