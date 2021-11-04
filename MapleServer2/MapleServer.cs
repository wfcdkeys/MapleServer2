﻿using System.Globalization;
using Autofac;
using Maple2Storage.Extensions;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using MaplePacketLib2.Tools;
using MapleServer2.Database;
using MapleServer2.Managers;
using MapleServer2.Network;
using MapleServer2.Servers.Game;
using MapleServer2.Servers.Login;
using MapleServer2.Tools;
using MapleServer2.Types;
using NLog;

namespace MapleServer2
{
    public static class MapleServer
    {
        private static GameServer GameServer;
        private static LoginServer LoginServer;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static async Task Main()
        {
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptionEventHandler);
            currentDomain.ProcessExit += new EventHandler(SaveAll);

            // Force Globalization to en-US because we use periods instead of commas for decimals
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            // Load .env file
            string dotenv = Path.Combine(Paths.SOLUTION_DIR, ".env");

            if (!File.Exists(dotenv))
            {
                throw new ArgumentException(".env file not found!");
            }
            DotEnv.Load(dotenv);

            DatabaseManager.Init();

            DateTimeOffset lastReset = DatabaseManager.ServerInfo.GetLastDailyReset();
            DateTimeOffset now = DateTimeOffset.UtcNow;
            DateTime lastMidnight = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, 0);

            // Check if lastReset is before lastMidnight
            if (lastReset < lastMidnight)
            {
                DailyReset();
            }

            // Schedule daily reset and repeat every 24 hours
            Tools.TaskScheduler.Instance.ScheduleTask(0, 0, 24, () => DailyReset());

            // Load Mob AI files
            string mobAiSchema = Path.Combine(Paths.AI_DIR, "mob-ai.xsd");
            MobAIManager.Load(Paths.AI_DIR, mobAiSchema);

            // Initialize all metadata.
            await MetadataHelper.InitializeAll();

            IContainer loginContainer = LoginContainerConfig.Configure();
            using ILifetimeScope loginScope = loginContainer.BeginLifetimeScope();
            LoginServer = loginScope.Resolve<LoginServer>();
            LoginServer.Start();

            IContainer gameContainer = GameContainerConfig.Configure();
            using ILifetimeScope gameScope = gameContainer.BeginLifetimeScope();
            GameServer = gameScope.Resolve<GameServer>();
            GameServer.Start();

            Logger.Info("Server Started.".ColorGreen());

            // Input commands to the server
            while (true)
            {
                string[] input = (Console.ReadLine() ?? string.Empty).Split(" ", 2);
                switch (input[0])
                {
                    case "exit":
                    case "quit":
                        GameServer.Stop();
                        LoginServer.Stop();
                        return;
                    case "send":
                        if (input.Length <= 1)
                        {
                            break;
                        }
                        string packet = input[1];
                        PacketWriter pWriter = new PacketWriter();
                        pWriter.WriteBytes(packet.ToByteArray());
                        Logger.Info(pWriter);

                        foreach (Session session in GetSessions(LoginServer, GameServer))
                        {
                            Logger.Info($"Sending packet to {session}: {pWriter}");
                            session.Send(pWriter);
                        }

                        break;
                    case "resolve":
                        // How to use inside the PacketStructureResolver class
                        PacketStructureResolver resolver = PacketStructureResolver.Parse(input[1]);
                        if (resolver is null)
                        {
                            break;
                        }
                        GameSession first = GameServer.GetSessions().Single();
                        resolver.Start(first);
                        break;
                    default:
                        Logger.Info($"Unknown command:{input[0]} args:{(input.Length > 1 ? input[1] : "N/A")}");
                        break;
                }
            }
        }

        public static GameServer GetGameServer() => GameServer;

        public static LoginServer GetLoginServer() => LoginServer;

        private static void DailyReset()
        {
            List<Player> players = GameServer.Storage.GetAllPlayers();
            foreach (Player player in players)
            {
                player.GatheringCount = new();
                DatabaseManager.Characters.Update(player);
            }
            DatabaseManager.RunQuery("UPDATE `characters` SET gathering_count = '[]'");

            DatabaseManager.ServerInfo.SetLastDailyReset(DateTimeOffset.UtcNow.UtcDateTime);
        }

        public static void BroadcastPacketAll(PacketWriter packet, GameSession sender = null)
        {
            BroadcastAll(session =>
            {
                if (session == sender)
                {
                    return;
                }
                session.Send(packet);
            });
        }

        public static void BroadcastAll(Action<GameSession> action)
        {
            IEnumerable<GameSession> sessions = GameServer.GetSessions();
            lock (sessions)
            {
                foreach (GameSession session in sessions)
                {
                    action?.Invoke(session);
                }
            }
        }

        // Testing Stuff outside of a main arg
        private static IEnumerable<Session> GetSessions(LoginServer loginServer, GameServer gameServer)
        {
            List<Session> sessions = new List<Session>();
            sessions.AddRange(loginServer.GetSessions());
            sessions.AddRange(gameServer.GetSessions());

            return sessions;
        }

        private static void UnhandledExceptionEventHandler(object sender, UnhandledExceptionEventArgs args)
        {
            SaveAll(sender, args);
            Exception e = (Exception) args.ExceptionObject;
            Logger.Fatal($"Exception Type: {e.GetType()}\nMessage: {e.Message}\nStack Trace: {e.StackTrace}\n");
        }

        private static void SaveAll(object sender, EventArgs e)
        {
            List<Player> players = GameServer.Storage.GetAllPlayers();
            foreach (Player item in players)
            {
                DatabaseManager.Characters.Update(item);
            }

            List<Guild> guilds = GameServer.GuildManager.GetAllGuilds();
            foreach (Guild item in guilds)
            {
                DatabaseManager.Guilds.Update(item);
            }

            List<Home> homes = GameServer.HomeManager.GetAllHomes();
            foreach (Home home in homes)
            {
                DatabaseManager.Homes.Update(home);
            }
        }
    }
}
