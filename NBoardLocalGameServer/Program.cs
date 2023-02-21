using System;

using NBoardLocalGameServer.Engine;

namespace NBoardLocalGameServer
{
    static class Program
    {
        static void Main(string[] args)
        {
            NBoardLocalGameServer.Reversi.Position pos = new();
            pos.Update(Reversi.BoardCoordinate.F5);
            pos.Update(Reversi.BoardCoordinate.D6);
            var gameInfo = new GameInfo();
            gameInfo.Position = pos;
            gameInfo.BlackPlayerName = "Kalmia";
            gameInfo.WhitePlayerName = "Logistello";
            Console.WriteLine(gameInfo.ToGGFString());
        }
    }
}