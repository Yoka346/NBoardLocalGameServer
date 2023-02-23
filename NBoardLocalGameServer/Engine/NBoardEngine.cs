using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

using NBoardLocalGameServer.Reversi;

namespace NBoardLocalGameServer.Engine
{
    /// <summary>
    /// NBoardプロトコルに準拠した思考エンジンとやり取りをするクラス.
    /// </summary>
    internal class NBoardEngine
    {
        const int NBOARD_VERSION = 2;
        const int CONNECTION_CHECK_TIMEOUT_MS = 10000;  

        /// <summary>
        /// set name コマンドによって受け取った思考エンジンの名前.
        /// </summary>
        public string? Name { get; private set; }

        public string? ProcName => this.process?.Name;

        public bool QuitCommandWasSent => this.quitCommandWasSent;
        public bool IsAlive => (this.process is not null) && !this.process.HasExited;

        public bool IsThinking { get => this.isThinking != 0;  }

        /// <summary>
        /// Killメソッドが呼ばれて, プロセスをKillしている最中である.
        /// </summary>
        public bool IsBeingKilled => this.isBeingKilled;

        /// <summary>
        /// Killメソッドが呼ばれて, プロセスがKillされた.
        /// </summary>
        public bool WasKilled => this.wasKilled;

        public event EventHandler ExitedUnexpectedly = delegate { };

        readonly string PATH, ARGS, WORK_DIR_PATH;
        readonly string[] INIT_COMMANDS;
        EngineProcess? process;

        volatile int isThinking = 0;
        volatile bool quitCommandWasSent = false;
        volatile bool isBeingKilled = false;
        volatile bool wasKilled = false;

        int pingCount = 0;

        public NBoardEngine(string path, string args, string workDirPath, IEnumerable<string> initialCommands)
        {
            this.PATH = path;
            this.ARGS = args;
            this.WORK_DIR_PATH = workDirPath;
            this.INIT_COMMANDS = initialCommands.ToArray();
        }

        public bool Run()
        {
            this.process = EngineProcess.Start(this.PATH, this.ARGS, this.WORK_DIR_PATH);
            if (this.process is null)
                return false;

            this.process.Exited += Process_Exited;
            this.process.OnNonResponceTextRecieved += Process_OnNonResponceTextRecieved;

            Thread.Sleep(1000);     // Edaxの場合, 1秒ほど待ってからコマンドを送らないとエラーを出して終了することがある.

            SendCommand($"nboard {NBOARD_VERSION}");

            foreach (var cmd in this.INIT_COMMANDS)
                SendCommand(cmd);

            return true;
        }

        public bool Quit(int timeoutMs)
        {
            if (this.process is null)
                return false;

            this.quitCommandWasSent = true;
            SendCommand("quit");
            this.process.WaitForExit(timeoutMs);
            return !this.IsAlive;
        }

        public bool Kill(int timeoutMs)
        {
            this.isBeingKilled = true;
            this.process?.Kill();
            this.process?.WaitForExit(timeoutMs);
            if (!this.IsAlive)
            {
                this.wasKilled = true;
                this.isBeingKilled = true;
                return true;
            }
            return false;
        }

        public void SetTime(DiscColor color, GameTime time)
            => SendCommand($"set time {color} main {time.MainTimeMs} inc {time.IncrementTimeMs} byoyomi {time.ByoYomiMs}");

        public void SetLevel(int level) => SendCommand($"set depth {level}");

        public void SetGameInfo(GameInfo gameInfo) => SendCommand($"set game {gameInfo.ToGGFString()}");

        public void SendMove(BoardCoordinate move) => SendCommand($"move {move}");

        public (BoardCoordinate coord, int ellapsedMs) Think()
        {
            if (process is null)
                throw new NullReferenceException("Execute Run method at first.");

            if (Interlocked.Exchange(ref this.isThinking, 1) == 1)
                throw new InvalidOperationException("Cannnot execute multiple thinking.");

            var responce = SendCommand("go", "^\\s*===");
            var startTime = Environment.TickCount;

            while (!responce.HasResult && this.IsThinking) 
                Thread.Yield();

            if (!this.IsThinking)
                return (BoardCoordinate.Null, 0);

            Interlocked.Exchange(ref this.isThinking, 0);

            var endTime = Environment.TickCount;

            var sr = new IgnoreSpaceStringReader(responce.Result);
            sr.Read();  // "==="の読み飛ばし.
            var moveStr = sr.Read();
            var move = ReversiTypes.ParseCoordinate(moveStr);
            if (move == BoardCoordinate.Null)
                throw new NBoardProtocolException($"Recieved move string \"{moveStr}\" was invalid.");

            return (move, endTime - startTime);
        }

        EngineProcess.Responce SendCommand(string cmd, string? regex = null)
        {
            if (this.process is null)
                throw new InvalidOperationException("Engine process is not running.");

            if (!CheckConnection())
            {
                var name = this.Name ?? this.process.Name;
                throw new EngineConnectionException($"{name}({this.process.PID})");
            }

            return this.process.SendCommand(cmd ,regex);
        }

        bool CheckConnection(int timeoutMs = CONNECTION_CHECK_TIMEOUT_MS)
        {
            if (this.process is null)
                throw new InvalidOperationException("Engine process is not created.");

            var pingID = this.pingCount++;
            var responce = this.process.SendCommand($"ping {pingID}", $"^\\s*pong\\s+{pingID}");
            return responce.Wait(timeoutMs);
        }

        void Process_OnNonResponceTextRecieved(object? sender, string e)
        {
            var sr = new IgnoreSpaceStringReader(e);
            if (sr.Read().CompareTo("set", StringComparison.OrdinalIgnoreCase) == 0
                && sr.Read().CompareTo("myname", StringComparison.OrdinalIgnoreCase) == 0 && sr.Peek() != -1)
                this.Name = sr.Read().ToString();
        }

        void Process_Exited(object? sender, EventArgs e)
        {
            if (this.quitCommandWasSent || this.isBeingKilled || this.wasKilled)
                return;

            this.ExitedUnexpectedly.Invoke(this, EventArgs.Empty);
            Interlocked.Exchange(ref this.isThinking, 0);
        }
    }
}
