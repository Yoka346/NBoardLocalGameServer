using System.IO;
using System.Text.Json;

namespace NBoardLocalGameServer
{
    /// <summary>
    /// 対局の設定.
    /// Jsonファイルからロードする.
    /// </summary>
    internal class GameConfig   // 各設定値はあらかじめデフォルト値をいれておく
                                // (ロードしたJsonファイルにすべての設定項目の値が記述されているとは限らないので).
    {
        /// <summary>
        /// 1ゲームごとに手番を入れ替えるか否か.
        /// </summary>
        public bool SwapPlayer { get; set; } = true;

        /// <summary>
        /// 手番を入れ替えたとき, 手番入れ替える前と同じ局面で再対局するか, もしくは別の局面を用意するか.
        /// SwapPlayerがtrueのときのみ有効.
        /// </summary>
        public bool UseSamePositionWhenSwapPlayer { get; set; } = true;

        /// <summary>
        /// 序盤のBook.
        /// </summary>
        public string OpeningBookPath { get; set; } = string.Empty;

        /// <summary>
        /// 初期局面の空きマスの最小値.
        /// </summary>
        public int MinInitialEmptySquareNum { get; set; } = 40;

        /// <summary>
        /// 初期局面の空きマスの最大値.
        /// </summary>
        public int MaxInitialEmptySquareNum { get; set; } = 60;

        /// <summary>
        /// 棋譜の保存先.
        /// </summary>
        public string GameLogPath { get; set; } = "game.ggf";

        /// <summary>
        /// 対局の統計データの保存先.
        /// </summary>
        public string GameStatsPath { get; set; } = "stats.json";

        public static GameConfig? Load(string path) => JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(path));
        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
