﻿using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord.WebSocket;
using Discord.Commands;
using OCUnion;
using OC.DiscordBotServer.Commands;
using OC.DiscordBotServer.Models;
using OC.DiscordBotServer.Repositories;

//https://discord.foxbot.me/docs/api/
namespace OC.DiscordBotServer
{
    class Program
    {
        private DiscordSocketClient _discordClient;
        private CommandService _commands;
        //private SqlLiteProvider _sqlLiteProvider;
        private IServiceProvider _services;

        /// <summary>
        /// Prefix OC Online City 
        /// </summary>
        public const string PX = "!OC";
        public const string PathToDb = "Filename=..\\BotDB.sqlite3";
        static void Main(string[] args)
        {
            if (args == null || args.Length < 1)
            {
                Console.WriteLine("DiscordChatBotServer.exe BotToken [PathToLog]");
                return;
            }

            if (args.Length == 2)
            {
                Loger.PathLog = args[1];
            }
            else
            {
                Loger.PathLog = Environment.CurrentDirectory;
            }

            new Program().RunBotAsync(args[0]).GetAwaiter().GetResult();
        }

        public async Task RunBotAsync(string botToken)
        {
            _discordClient = new DiscordSocketClient();
            _commands = new CommandService();

            var services = new ServiceCollection()
                .AddSingleton<DiscordSocketClient>(_discordClient)
                .AddSingleton<ApplicationContext>()
                .AddSingleton<BotDataContext>(new BotDataContext(PathToDb));

            services
                .AddSingleton<OCUserRepository>()
                .AddSingleton<Chanel2ServerRepository>()
                .AddSingleton<IRepository<OCUser>>(x => x.GetService<OCUserRepository>())
            .AddSingleton<IRepository<Chanel2Server>>(x => x.GetService<Chanel2ServerRepository>());

            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsClass)
                {
                    continue;
                }

                if (type.GetInterface("ICommand") != null)
                {
                    services.AddSingleton(type);
                }
            }
            _services = services
                .AddSingleton(_commands)
                .AddSingleton<ChatListener>()
                .BuildServiceProvider();


            _discordClient.Log += _discordClient_Log;

            await RegisterCommandAsync();
            await _discordClient.LoginAsync(Discord.TokenType.Bot, botToken);
            await _discordClient.StartAsync();

            var listener = _services.GetService<ChatListener>();
            const int WAIT_LOGIN_DISCORD_TIME = 3000;
            const int REFRESH_TIME = 500;
            var t = new System.Threading.Timer((a) => { listener.UpdateChats (); } , null, WAIT_LOGIN_DISCORD_TIME, REFRESH_TIME);

            await Task.Delay(-1);
        }

        private Task _discordClient_Log(Discord.LogMessage arg)
        {
            if (arg.Message != null)
            {
                Task.Factory.StartNew(() => { Loger.Log(arg.Message); });
                Console.WriteLine(arg.Message);
            }

            if (arg.Exception != null)
            {
                Task.Factory.StartNew(() => { Loger.Log(arg.Exception.ToString()); });
            }

            return Task.CompletedTask;
        }

        public async Task RegisterCommandAsync()
        {
            _discordClient.MessageReceived += _discordClient_MessageReceived;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private async Task _discordClient_MessageReceived(SocketMessage arg)
        {
            var message = arg as SocketUserMessage;


            if (message is null || message.Author.IsBot) return;
            int argPos = 0;

            if (message.HasStringPrefix(PX, ref argPos) || message.HasMentionPrefix(_discordClient.CurrentUser, ref argPos))
            {
                var context = new SocketCommandContext(_discordClient, message);

                var result = await _commands.ExecuteAsync(context, argPos + 1, _services);
                if (!result.IsSuccess)
                {
                    Console.WriteLine(result.ErrorReason);
                }
            }
        }
    }
}
