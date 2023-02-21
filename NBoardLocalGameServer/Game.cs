using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NBoardLocalGameServer.Engine;

namespace NBoardLocalGameServer
{
    internal class PlayerStatistic
    {
        public int WinCountWhenBlack { get; set; } = 0;
        public int LossCountWhenBlack { get; set; } = 0;
        public int DrawCountWhenBlack { get; set; } = 0;
        public int GameCountWhenBlack => this.WinCountWhenBlack + this.LossCountWhenBlack + this.DrawCountWhenBlack;
        public double WinRateWhenBlack => (this.WinCountWhenBlack + this.DrawCountWhenBlack * 0.5) / this.GameCountWhenBlack;

        public int WinCountWhenWhite { get; set; } = 0;
        public int LossCountWhenWhite { get; set; } = 0;
        public int DrawCountWhenWhite { get; set; } = 0;
        public int GameCountWhenWhite => this.WinCountWhenWhite + this.LossCountWhenWhite + this.DrawCountWhenWhite;
        public double WinRateWhenWhite => (this.WinCountWhenWhite + this.DrawCountWhenWhite * 0.5) / this.GameCountWhenWhite;

        public int WinCount => this.WinCountWhenBlack + this.WinCountWhenWhite;
        public int LossCount => this.LossCountWhenBlack + this.LossCountWhenWhite;
        public int DrawCount => this.DrawCountWhenBlack + this.DrawCountWhenWhite;
        public int GameCount => this.GameCountWhenBlack + this.GameCountWhenWhite;
        public double WinRate => (this.WinCount + this.DrawCount * 0.5) / this.GameCount;
    }

    /// <summary>
    /// 対局を管理するクラス.
    /// </summary>
    internal class Game
    {
        readonly GameConfig CONFIG;
        readonly EngineConfig[] ENGINE_CONFIGS;

        public Game(GameConfig gameConfig, EngineConfig engineConfig_0, EngineConfig engineConfig_1)
        {
            this.CONFIG = gameConfig;
            this.ENGINE_CONFIGS = new EngineConfig[2] { engineConfig_0, engineConfig_1 };
        }

        public void StartMainloop(int gameNum)
        {
            var players = (from config in this.ENGINE_CONFIGS select (new PlayerStatistic(), new NBoardEngine(config))).ToArray();
        }
    }
}
