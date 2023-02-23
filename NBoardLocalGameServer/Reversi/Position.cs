using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NBoardLocalGameServer.Reversi
{
    using static Constants;

    /// <summary>
    /// リバーシの盤面.
    /// </summary>
    internal class Position
    {
        public DiscColor SideToMove 
        { 
            get => this.sideToMove;
            set
            {
                if (value != this.sideToMove)
                    Pass();
                this.sideToMove = value;
            }
        }

        public DiscColor OpponentColor => ReversiTypes.ToOpponent(this.sideToMove); 
        public int EmptySquareCount => this.bitboard.EmptyCount;
        public int PlayerDiscCount => this.bitboard.PlayerDiscCount;
        public int OpponentDiscCount => this.bitboard.OpponentDiscCount; 
        public int DiscCount => this.bitboard.DiscCount;

        public bool CanPass => BitOperations.PopCount(this.bitboard.CalculatePlayerMobility()) == 0
                            && BitOperations.PopCount(this.bitboard.CalculateOpponentMobility()) != 0;

        public bool IsGameOver => BitOperations.PopCount(this.bitboard.CalculatePlayerMobility()) == 0
                               && BitOperations.PopCount(this.bitboard.CalculateOpponentMobility()) == 0;

        BitBoard bitboard;
        DiscColor sideToMove;
        readonly Stack<Move> moveHistory = new();

        public Position()
        {
            // デフォルトはクロス配置.
            this.bitboard = new BitBoard(COORD_TO_BIT[(byte)BoardCoordinate.E4] | COORD_TO_BIT[(byte)BoardCoordinate.D5],
                                         COORD_TO_BIT[(byte)BoardCoordinate.D4] | COORD_TO_BIT[(byte)BoardCoordinate.E5]);
            this.sideToMove = DiscColor.Black;
        }

        public Position(BitBoard bitboard, DiscColor sideToMove)
        {
            this.bitboard = bitboard;
            this.sideToMove = sideToMove;
        }

        public Position(Position pos)
        {
            this.bitboard = pos.bitboard;
            this.sideToMove = pos.sideToMove;
            foreach (var move in pos.moveHistory.Reverse())
                this.moveHistory.Push(move);
        }

        public static bool operator ==(Position left, Position right) => left.sideToMove == right.sideToMove && left.bitboard == right.bitboard;
        public static bool operator !=(Position left, Position right) => !(left == right);

        public override bool Equals(object? obj) => obj is Position pos && this == pos;

        // 警告抑制のためのコード.
        public override int GetHashCode() => base.GetHashCode();

        public int GetDiscCountOf(DiscColor color)
        {
            if (color == DiscColor.Null)
                return 0;

            return (this.sideToMove == color) ? this.PlayerDiscCount : this.OpponentDiscCount;
        }

        /// <summary>
        /// 指定された座標にある石が現在の手番の石なのか, 相手の石なのか, それとも石が存在しないのかを返す.
        /// </summary>
        /// <param name="coord"></param>
        /// <returns></returns>
        public Player GetSquareOwnerAt(BoardCoordinate coord) 
            => (Player)(2 - 2 * ((this.bitboard.Player >> (byte)coord) & 1) - ((this.bitboard.Opponent >> (byte)coord) & 1));

        public DiscColor GetSquareColorAt(BoardCoordinate coord)
        {
            var owner = GetSquareOwnerAt(coord);
            if (owner == Player.Null)
                return DiscColor.Null;
            return (owner == Player.Current) ? this.sideToMove : this.OpponentColor;
        }

        public IEnumerable<(DiscColor color, BoardCoordinate coord)> EnumeratePastMoves(bool firstPlayFirstOut = true)
        {
            var e = this.moveHistory.Select(x => (x.Color, x.Coord));
            return firstPlayFirstOut ? e.Reverse() : e;
        }

        public bool IsLegal(BoardCoordinate coord) 
            => (coord == BoardCoordinate.PA) ? this.CanPass : ((this.bitboard.CalculatePlayerMobility() & COORD_TO_BIT[(byte)coord]) != 0);

        public void Pass()
        {
            this.sideToMove = this.OpponentColor;
            this.bitboard.Swap();
            this.moveHistory.Push(new Move(this.OpponentColor, BoardCoordinate.PA));
        }

        public void PutPlayerDiscAt(BoardCoordinate coord) => this.bitboard.PutPlayerDiscAt(coord);
        public void PutOpponentDiscAt(BoardCoordinate coord) => this.bitboard.PutOpponentDiscAt(coord);

        public void PutDiscAt(DiscColor color, BoardCoordinate coord)
        {
            if (color == DiscColor.Null)
                return;

            if (this.sideToMove == color)
                PutPlayerDiscAt(coord);
            else
                PutOpponentDiscAt(coord);
        }

        public void RemoveDiscAt(BoardCoordinate coord) => this.bitboard.RemoveDiscAt(coord);

        public bool Update(BoardCoordinate move)
        {
            if(!IsLegal(move))
                return false;

            if(move == BoardCoordinate.PA)
            {
                Pass();
                return true;
            }

            var m = new Move
            {
                Color= this.sideToMove,
                Coord = move,
                Flipped = this.bitboard.CalculateFlippedDiscs(move)
            };
            this.sideToMove = this.OpponentColor;
            this.bitboard.Update(m.Coord, m.Flipped);
            this.moveHistory.Push(m);
            return true;
        }

        public bool Undo()
        {
            if (this.moveHistory.Count == 0)
                return false;

            this.sideToMove = this.OpponentColor;
            var move = this.moveHistory.Pop();
            this.bitboard.Undo(move.Coord, move.Flipped);
            return true;
        }

        public DiscColor GetWinner()
        {
            if (!this.IsGameOver)
                return DiscColor.Null;

            var diff = this.bitboard.PlayerDiscCount - this.bitboard.OpponentDiscCount;
            if (diff == 0)
                return DiscColor.Null; 

            return (diff > 0) ? this.SideToMove : this.OpponentColor;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("  ");
            for (var i = 0; i < BOARD_SIZE; i++)
                sb.Append((char)('A' + i)).Append(' ');

            var p = this.bitboard.Player;
            var o = this.bitboard.Opponent;
            var mask = 1UL << (SQUARE_NUM - 1);
            for (var y = 0; y < BOARD_SIZE; y++)
            {
                sb.Append('\n').Append(y + 1).Append(' ');
                for (var x = 0; x < BOARD_SIZE; x++)
                {
                    if ((p & mask) != 0)
                        sb.Append((this.SideToMove == DiscColor.Black) ? "X " : "O ");
                    else if ((o & mask) != 0)
                        sb.Append((this.SideToMove != DiscColor.Black) ? "X " : "O ");
                    else
                        sb.Append(". ");
                    mask >>= 1;
                }
            }
            return sb.ToString();
        }
    }
}
