using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;
using System.Threading;

using NBoardLocalGameServer.Reversi;

namespace NBoardLocalGameServer.Engine
{
    internal class EngineConfig
    {
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkDir { get; set; }

        /// <summary>
        /// エンジン起動時にまとめて送るコマンド.
        /// 対局前にエンジンの設定をしたいときに用いる.
        /// </summary>
        public string[] InitialCommands { get; set; }

        public EngineConfig() : this("", "", "", Enumerable.Empty<string>()) { }

        public EngineConfig(string path, string args, string workDir, IEnumerable<string> initialCmds)
        {
            this.Path = path;
            this.Arguments = args;
            this.WorkDir = workDir;
            this.InitialCommands = initialCmds.ToArray();
        }

        public EngineConfig(EngineConfig config)
        {
            this.Path = config.Path;
            this.Arguments = config.Arguments;
            this.WorkDir = config.WorkDir;
            this.InitialCommands = (string[])config.InitialCommands.Clone();
        }

        public static EngineConfig? Load(string path)
            => JsonSerializer.Deserialize<EngineConfig>(File.ReadAllText(path));

        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

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

        readonly EngineConfig CONFIG;
        EngineProcess? process;

        volatile int isThinking = 0;
        volatile bool quitCommandWasSent = false;
        volatile bool isBeingKilled = false;
        volatile bool wasKilled = false;

        int pingCount = 0;

        public NBoardEngine(EngineConfig config) => this.CONFIG = new(config);

        public bool Run()
        {
            this.process = EngineProcess.Start(this.CONFIG.Path, this.CONFIG.Arguments, this.CONFIG.WorkDir);
            if (this.process is null)
                return false;

            this.process.Exited += Process_Exited;
            this.process.OnNonResponceTextRecieved += Process_OnNonResponceTextRecieved;

            SendCommand($"nboard {NBOARD_VERSION}");

            foreach (var cmd in this.CONFIG.InitialCommands)
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

            var endTime = Environment.TickCount;

            var sr = new IgnoreSpaceStringReader(responce.Result);
            sr.Read();
            var moveStr = sr.ReadToEnd();
            var idx = moveStr.IndexOf('/');
            if (idx != -1)
                moveStr = moveStr[..idx];

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
