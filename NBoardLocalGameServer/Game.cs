using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

using NBoardLocalGameServer.Engine;
using NBoardLocalGameServer.Reversi;

namespace NBoardLocalGameServer
{
    internal class PlayerStatistic
    {
        public string Label { get; }
        public int[] WinCount { get; } = new int[2];
        public int[] LossCount { get; } = new int[2];
        public int[] DrawCount { get; } = new int[2];

        public int TotalWinCount => this.WinCount.Sum();
        public int TotalLossCount => this.LossCount.Sum();
        public int TotalDrawCount => this.DrawCount.Sum();
        public int TotalGameCount => this.TotalWinCount + this.TotalLossCount + this.TotalDrawCount;
        public double TotalWinRate => (this.TotalWinCount + this.TotalDrawCount * 0.5) / this.TotalGameCount;

        public int GameCountWhenBlack => GetGameCountWhen(DiscColor.Black);
        public int GameCountWhenWhite => GetGameCountWhen(DiscColor.White);
        public double WinRateWhenBlack => GetWinRateWhen(DiscColor.Black);
        public double WinRateWhenWhite => GetWinRateWhen(DiscColor.White);

        public PlayerStatistic(string label) => this.Label = label;

        public int GetGameCountWhen(DiscColor color) => this.WinCount[(int)color] + this.LossCount[(int)color] + this.DrawCount[(int)color];

        public double GetWinRateWhen(DiscColor color) 
        {
            var gameCount = GetGameCountWhen(color);
            return (gameCount != 0) ? (this.WinCount[(int)color] + this.DrawCount[(int)color] * 0.5) / gameCount : 0.0; 
        }
    }

    internal class Player
    {
        public PlayerStatistic Stats { get; }
        public NBoardEngine Engine { get; }

        public Player(string label, PlayerConfig config) 
            => (this.Stats, this.Engine) = (new PlayerStatistic(label), new NBoardEngine(config.Path, config.Arguments, config.WorkDir, config.InitialCommands));
    }

    /// <summary>
    /// 対局を管理するクラス.
    /// </summary>
    internal class Game
    {
        readonly GameConfig CONFIG;
        readonly PlayerConfig[] PLAYER_CONFIGS;

        public Game(GameConfig gameConfig, PlayerConfig engineConfig_0, PlayerConfig engineConfig_1)
        {
            this.CONFIG = gameConfig;
            this.PLAYER_CONFIGS = new PlayerConfig[2] { engineConfig_0, engineConfig_1 };
        }

        public void StartMainloop(int gameNum)
        {
            var players = this.PLAYER_CONFIGS.Select((x, idx) => new Player($"Player_{idx}", x)).ToArray();
            for (var i = 0; i < players.Length; i++)
                if (!players[i].Engine.Run())
                {
                    Console.Error.WriteLine($"Error: Cannot execute \"{this.PLAYER_CONFIGS[i].Path}\".");
                    return;
                }

            var book = (this.CONFIG.OpeningBookPath != string.Empty) ? new OpeningBook(this.CONFIG.OpeningBookPath) : OpeningBook.Empty;
            book.Shuffle();

            Console.WriteLine($"Game start:\t{players[0].Engine.Name ?? players[0].Engine.ProcName} v.s. {players[1].Engine.Name ?? players[1].Engine.ProcName}");
            if (!Mainloop(gameNum, book, players))
                Console.WriteLine("Game was suspended.");

            QuitEngines(players.Select(x => x.Engine));
        }

        static void QuitEngines(IEnumerable<NBoardEngine> engines)
        {
            const int TIMEOUT_MS = 10000;
            foreach (var e in engines)
                e.Quit(TIMEOUT_MS);
        }

        bool Mainloop(int gameNum, OpeningBook book, Player[] players)
        {
            var gameLog = new StreamWriter(this.CONFIG.GameLogPath);
            if (gameLog is null) 
            {
                Console.Error.WriteLine($"Error: Cannot create or open \"{this.CONFIG.GameLogPath}\"");
                return true;
            }

            Position? pos = null;
            for (var gameID = 0; gameID < gameNum; gameID++)
            {
                if (!(this.CONFIG.SwapPlayer && this.CONFIG.UseSamePositionWhenSwapPlayer) || gameID % 2 == 0)
                    pos = InitPosition(book);            

                if (pos is null)
                    return false;

                Console.WriteLine($"Game: {gameID}");
                Console.WriteLine($"Initial Position:\n{pos}");

                if (!PlayOneGame(pos, (this.CONFIG.SwapPlayer && gameID % 2 == 1) ? players.Reverse().ToArray() : players, gameLog))
                    return false;

                Console.WriteLine("////////////////////");
                    foreach ((var engine, var stats) in players.Select(x => (x.Engine, x.Stats)))
                        Console.WriteLine($"{engine.Name ?? engine.ProcName}: {stats.TotalWinCount}-{stats.TotalDrawCount}-{stats.TotalLossCount} (WinRate: {stats.TotalWinRate * 100.0}%)");
                Console.WriteLine("////////////////////");

                SaveStats(players);
            }

            return true;
        }

        bool PlayOneGame(Position initPos, Player[] players, StreamWriter gameLog)
        {
            var gameInfo = new GameInfo
            {
                Position = initPos,
                BlackPlayerName = players[0].Engine.Name ?? players[0].Engine.ProcName ?? "Player_0",
                WhitePlayerName = players[1].Engine.Name ?? players[1].Engine.ProcName ?? "Player_1",
                BlackThinkingTime = this.PLAYER_CONFIGS[0].ThinkingTime,
                WhiteThinkingTime = this.PLAYER_CONFIGS[1].ThinkingTime
            };

            foreach (var p in players)
            {
                p.Engine.SetGameInfo(gameInfo);
                p.Engine.SetTime(DiscColor.Black, gameInfo.BlackThinkingTime);
                p.Engine.SetTime(DiscColor.White, gameInfo.WhiteThinkingTime);
            }

            Player player, opponent;
            var pos = new Position(initPos);
            while (true)
            {
                (player, opponent) = (players[(int)pos.SideToMove], players[(int)pos.OpponentColor]);
                var move = player.Engine.Think();
                if (!pos.Update(move.coord))
                {
                    Console.Error.WriteLine($"Error: move {move.coord} is illegal.");
                    return false;
                }
                gameInfo.Moves.Add(new Move { Color = pos.OpponentColor, Coord = move.coord });

                player.Engine.SendMove(move.coord);
                opponent.Engine.SendMove(move.coord);

                Console.WriteLine($"\nPosition:\n{pos}\n(last_move, ellapsed_ms) = ({move.coord}, {move.ellapsedMs})");

                if (pos.IsGameOver)
                {
                    var winner = pos.GetWinner();
                    if (winner == DiscColor.Null)
                    {
                        Console.WriteLine("Game over: Draw.");
                        for (var i = 0; i < players.Length; i++)
                            players[i].Stats.DrawCount[i]++;
                        break;
                    }

                    Console.WriteLine($"Game over: {players[(int)winner].Engine.Name} wins");
                    var loser = ReversiTypes.ToOpponent(winner);
                    players[(int)winner].Stats.WinCount[(int)winner]++;
                    players[(int)loser].Stats.LossCount[(int)loser]++;
                    break;
                }
            }
            SaveGame(gameLog, gameInfo);
            return true;
        }

        Position? InitPosition(OpeningBook book)
        {
            if (book == OpeningBook.Empty)
                return new Position();

            (var minEmptyCount, var maxEmptyCount) = (this.CONFIG.MinInitialEmptySquareNum, this.CONFIG.MaxInitialEmptySquareNum);
            var first = book.GetItem();
            var item = first;
            while(item.Position.EmptySquareCount < minEmptyCount || (item.Position.EmptySquareCount - item.Moves.Count) > maxEmptyCount)
            {
                item = book.GetItem();
                if(item == first)
                {
                    Console.Error.WriteLine($"Error: The number of empty squares of any positions in an opening book was not within [{this.CONFIG.MinInitialEmptySquareNum}, {this.CONFIG.MaxInitialEmptySquareNum}].");
                    return null;
                }
            }

            var minMoveNum = Math.Max(item.Position.EmptySquareCount - maxEmptyCount, 0);
            var maxMoveNum = Math.Min(item.Position.EmptySquareCount - minEmptyCount, item.Moves.Count);
            var moveNum = Random.Shared.Next(minMoveNum, maxMoveNum + 1);
            var pos = new Position(item.Position);
            for (var i = 0; i < moveNum; i++)
                if (!pos.CanPass)
                    pos.Update(item.Moves[i]);
                else
                    pos.Pass();
            return pos;
        }

        void SaveStats(Player[] players)
            => File.WriteAllText(this.CONFIG.GameStatsPath, JsonSerializer.Serialize(players.Select(x => x.Stats).ToArray(), new JsonSerializerOptions { WriteIndented = true }));

        void SaveGame(StreamWriter sw, GameInfo gameInfo)
        {
            sw.WriteLine(gameInfo.ToGGFString());
            sw.Flush();
        }
    }
}
