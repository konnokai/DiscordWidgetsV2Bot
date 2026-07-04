using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordWidgetsV2Bot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig { GatewayIntents = GatewayIntents.None }));
builder.Services.AddSingleton(sp => new InteractionService(sp.GetRequiredService<DiscordSocketClient>()));
builder.Services.AddSingleton<WidgetService>();
var host = builder.Build();

var client = host.Services.GetRequiredService<DiscordSocketClient>();
var interactions = host.Services.GetRequiredService<InteractionService>();
var config = host.Services.GetRequiredService<IConfiguration>();

Task Log(LogMessage msg) { Console.WriteLine(msg.ToString()); return Task.CompletedTask; }
client.Log += Log;
interactions.Log += Log;

client.Ready += () => interactions.RegisterCommandsGloballyAsync();
client.InteractionCreated += i => interactions.ExecuteCommandAsync(new SocketInteractionContext(client, i), host.Services);

var token = config["Discord:Token"];
if (string.IsNullOrWhiteSpace(token))
    throw new InvalidOperationException("缺少設定 Discord:Token（環境變數 DISCORD__TOKEN）");

await interactions.AddModulesAsync(typeof(WidgetModule).Assembly, host.Services);
await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();
await host.RunAsync();
