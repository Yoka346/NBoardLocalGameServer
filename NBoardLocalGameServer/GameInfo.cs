using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using NBoardLocalGameServer.Reversi;

namespace NBoardLocalGameServer.Engine
{
    internal struct GameTime
    {
        /// <summary>
        /// 持ち時間. 
        /// </summary>
        public int MainTimeMs { get; set; }
        
        /// <summary>
        /// 1手ごとに持ち時間に加算される時間.
        /// </summary>
        public int IncrementTimeMs { get; set; }

        /// <summary>
        /// 秒読み. 持ち時間を使い切ったら1手ByoyomiTimeMs[ms]以内に着手しなければならない.
        /// </summary>
        public int ByoYomiMs { get; set; }

        public override string ToString()
        {
            var ts = TimeSpan.FromMilliseconds(MainTimeMs);
            return $"{ts.Hours}:{ts.Minutes}:{ts.Seconds}";     
        }
    }

    internal class GameInfo
    {
        public string BlackPlayerName { get; set; } = string.Empty;
        public string WhitePlayerName { get; set; } = string.Empty;
        public GameTime BlackThinkingTimeMs { get; set; } 
        public GameTime WhiteThinkingTimeMs { get; set; } 
        public Position Position = new();
        public DateTime DateTime { get; set; } = DateTime.Now;

        public string ToGGFString()
        {
            var sb = new StringBuilder("(;GM[Othello]PC[");
            sb.Append(Assembly.GetExecutingAssembly().GetName().Name).Append("]DT[");
            sb.Append(this.DateTime.ToString()).Append("]PB[");
            sb.Append(this.BlackPlayerName).Append("]PW[");
            sb.Append(this.WhitePlayerName).Append("]RE[?]BT[");
            sb.Append(this.BlackThinkingTimeMs.ToString()).Append("]WT[");
            sb.Append(this.WhiteThinkingTimeMs.ToString()).Append("]TY[");
            sb.Append(Constants.BOARD_SIZE).Append("]BO[").Append(Constants.BOARD_SIZE).Append(' ');

            // 盤面情報の構成.
            Span<char> discs = stackalloc char[3] { '*', 'O', '-' };
            for (var coord = BoardCoordinate.A1; coord <= BoardCoordinate.H8; coord++)
                sb.Append(discs[(int)this.Position.GetSquareColorAt(coord)]);
            sb.Append(' ').Append(discs[(int)this.Position.SideToMove]).Append(']');

            return sb.Append(";)").ToString();
        }
    }
}
