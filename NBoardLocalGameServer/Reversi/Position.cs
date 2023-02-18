using System.Collections.Generic;
using System.Numerics;

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

        public DiscColor OpponentColor => this.sideToMove ^ DiscColor.White; 
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

        public bool IsLegal(BoardCoordinate coord) 
            => (coord == BoardCoordinate.Pass) ? this.CanPass : ((this.bitboard.CalculatePlayerMobility() & COORD_TO_BIT[(byte)coord]) != 0);

        public void Pass()
        {
            this.sideToMove = this.OpponentColor;
            this.bitboard.Swap();
            this.moveHistory.Push(new Move(BoardCoordinate.Pass));
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

            var m = new Move
            {
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
    }
}
