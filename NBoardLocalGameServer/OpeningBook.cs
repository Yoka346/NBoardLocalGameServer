using System;
using System.Collections.Generic;
using System.IO;

using NBoardLocalGameServer.Reversi;

namespace NBoardLocalGameServer
{
    /// <summary>
    /// 序盤進行集. テキストファイルからロードする.
    /// 
    /// テキストファイルのフォーマットは, 各行に棋譜が記述されたもの.
    /// 棋譜の形式は [盤面('*': 黒, 'O': 白, '-': 空きマス)] [手番('*': 黒, 'O': 白)] [着手(F5D6C3...形式)]
    /// 
    /// ex)
    /// ---------------------------O*------O*--------------------------- F5D6C3 * 
    /// </summary>
    internal class OpeningBook
    {
        public static OpeningBook Empty { get; } = new();

        public int Count => this.book.Count;

        readonly List<BookItem> book;
        int loc = 0;

        public OpeningBook(string path)
        {
            var sr = new StreamReader(path);
            this.book = new List<BookItem>();
            var lineCount = 0;
            while (sr.Peek() != -1)
            {
                lineCount++;

                var line = sr.ReadLine()?.Trim();
                if (line is null || line == string.Empty)
                    continue;

                this.book.Add(ParseBookItem(line, lineCount));
            }
        }

        OpeningBook() => this.book = new List<BookItem>(0);

        public BookItem GetItem() => this.book[loc++ % this.book.Count];
        public void Shuffle() => Shuffle(Random.Shared);
        public void Shuffle(Random rand) => rand.Shuffle(this.book);

        static BookItem ParseBookItem(string str, int lineNum)
        {
            var item = new BookItem();
            var sr = new IgnoreSpaceStringReader(str);
            var posStr = sr.Read();
            for(var coord = BoardCoordinate.A1; coord <= BoardCoordinate.H8; coord++)
            {
                var ch = posStr[(byte)coord];
                if (ch == '*')
                    item.Position.PutDiscAt(DiscColor.Black, coord);
                else if (ch == 'O')
                    item.Position.PutDiscAt(DiscColor.White, coord);
                else if (ch == '-')
                    item.Position.RemoveDiscAt(coord);
                else
                    throw new FormatException($"Charcter \'{ch}\' at line {lineNum} is invalid. It must be '*', 'O' or '-'.");
            }

            var sideToMoveCh = sr.Read()[0];
            if (sideToMoveCh == '*')
                item.Position.SideToMove = DiscColor.Black;
            else if (sideToMoveCh == 'O')
                item.Position.SideToMove = DiscColor.White;
            else
                throw new FormatException($"Charcter \'{sideToMoveCh}\' at line {lineNum} is invalid. It must be '*' or 'O'.");

            var movesStr = sr.Read();
            if (movesStr[0] == '\0')
                return item;

            for(var i = 0; i < movesStr.Length; i += 2)
            {
                var move = ReversiTypes.ParseCoordinate(movesStr[i..2]);
                if (move == BoardCoordinate.Null)
                    throw new FormatException($"String \"{movesStr[i..2]}\" at line {lineNum} could not be parsed as board coordinate.");
                item.Moves.Add(move);
            }

            return item;
        }
    }

    internal class BookItem
    {
        public Position Position { get; set; } = new();
        public List<BoardCoordinate> Moves { get; set; } = new();
    }
}

