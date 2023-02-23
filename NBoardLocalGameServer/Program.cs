using System;

namespace NBoardLocalGameServer
{
    static class Program
    {
        static void Main(string[] args)
        {
            Load(args, out GameConfig? gameConfig, out PlayerConfig[]? playerConfigs, out int gameNum);

            if (gameConfig is not null && playerConfigs is not null)
            {
                var game = new Game(gameConfig, playerConfigs[0], playerConfigs[1]);
                game.StartMainloop(gameNum);
            }
        }

        static void Load(string[] args, out GameConfig? gameConfig, out PlayerConfig[]? playerConfigs, out int gameNum)
        {
            gameConfig = null;
            playerConfigs = null;

            var gameConfigPath = args[0];
            var playerConfigPathes = new string[] { args[1], args[2] };
            if (!int.TryParse(args[3], out gameNum))
            {
                Console.Error.WriteLine("Error: The number of games must be an integer");
                return;
            }

            if (gameNum <= 0)
            {
                Console.Error.WriteLine("The number of games must be a positive number.");
                return;
            }

            gameConfig = GameConfig.Load(gameConfigPath);
            if (gameConfig is null)
            {
                Console.Error.WriteLine($"Cannot load game config from \"{gameConfigPath}\".");
                return;
            }

            playerConfigs = new PlayerConfig[2];
            for (var i = 0; i < playerConfigs.Length; i++)
            {
                var config = PlayerConfig.Load(playerConfigPathes[i]);
                if (config is null)
                {
                    Console.Error.WriteLine($"Cannot load player config from \"{playerConfigPathes[i]}\"");
                    return;
                }
                playerConfigs[i] = config;
            }

            return;
        }
    }
}