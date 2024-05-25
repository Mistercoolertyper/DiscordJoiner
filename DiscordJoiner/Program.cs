using DiscordJoiner;

Console.Write("Invite >");
string invite = Console.ReadLine()!;

string[] tokens = File.ReadAllLines("tokens.txt");

foreach(string token in tokens)
{
	new Thread(() => new DiscordClient(token).JoinServer(invite)).Start();
}
