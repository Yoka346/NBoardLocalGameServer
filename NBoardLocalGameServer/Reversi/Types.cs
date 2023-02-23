using System;

namespace NBoardLocalGameServer.Reversi
{
    using static Constants;

    enum DiscColor : byte
    {
        Black = 0,
        White = 1,
        Null = 2
    }

    enum Player : byte
    {
        Current = 0,
        Opponent = 1,
        Null
    }

    enum BoardCoordinate : byte
    {
        A1, B1, C1, D1, E1, F1, G1, H1,
        A2, B2, C2, D2, E2, F2, G2, H2,
        A3, B3, C3, D3, E3, F3, G3, H3,
        A4, B4, C4, D4, E4, F4, G4, H4,
        A5, B5, C5, D5, E5, F5, G5, H5,
        A6, B6, C6, D6, E6, F6, G6, H6,
        A7, B7, C7, D7, E7, F7, G7, H7,
        A8, B8, C8, D8, E8, F8, G8, H8,
        PA, Null
    };

    enum GameResult
    {
        Win,
        Loss,
        Draw,
        NotOver
    }

    struct Move
    {
        public DiscColor Color { get; set; }
        public BoardCoordinate Coord { get; set; }
        public ulong Flipped { get; set; }

        public Move(DiscColor color = DiscColor.Null, BoardCoordinate coord = BoardCoordinate.Null, ulong flipped = 0)
        {
            this.Color = color;
            this.Coord = coord;
            this.Flipped = flipped;
        }
    }

    internal static class ReversiTypes
    {
        public static DiscColor ToOpponent(DiscColor color) => color ^ DiscColor.White;

        public static BoardCoordinate ParseCoordinate(ReadOnlySpan<char> str)
        {
            str = str.Trim();
            if (str.CompareTo("PA", StringComparison.OrdinalIgnoreCase) == 0)
                return BoardCoordinate.PA;

            Span<char> lstr = stackalloc char[str.Length];
            for (var i = 0; i < lstr.Length; i++)
                lstr[i] = char.ToLower(str[i]);

            if (str.Length < 2 || lstr[0] < 'a' || lstr[0] > 'h' || lstr[1] < '1' || lstr[1] > '8')
                return BoardCoordinate.Null;

            return (BoardCoordinate)((lstr[0] - 'a') + (lstr[1] - '1') * BOARD_SIZE);
        }
    }
}
