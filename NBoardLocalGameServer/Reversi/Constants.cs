using System;
using System.Linq;

namespace NBoardLocalGameServer.Reversi
{
    /// <summary>
    /// リバーシに関連する定数やテーブル.
    /// </summary>
    internal static class Constants
    {
        public const int BOARD_SIZE = 8;
        public const int SQUARE_NUM = BOARD_SIZE * BOARD_SIZE;

        /// <summary>
        /// 盤面の座標をbitに変換するテーブル.
        /// </summary>
        public static ReadOnlySpan<ulong> COORD_TO_BIT => _COORD_TO_BIT;
        static readonly ulong[] _COORD_TO_BIT = (from i in Enumerable.Range(0, SQUARE_NUM) select 1UL << i).ToArray();
    }
}
