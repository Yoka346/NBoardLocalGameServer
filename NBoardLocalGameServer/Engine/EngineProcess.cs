using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NBoardLocalGameServer.Engine
{
    /// <summary>
    /// 思考エンジンのプロセス.
    /// このクラスのオブジェクトを介してコマンドの送受信を行う.
    /// </summary>
    internal class EngineProcess
    {
        public string Name => this.PROCESS.ProcessName;
        public int PID => this.PROCESS.Id;
        public bool HasExited => this.PROCESS.HasExited;
        public event EventHandler Exited { add => this.PROCESS.Exited += value; remove => this.PROCESS.Exited -= value; }

        /// <summary>
        /// エンジンから受け取ったテキストが, 以前に送ったどのコマンドの応答にも該当しない場合に呼び出されるイベントハンドラ. 
        /// 例えば, エンジンから送られてくるset mynameコマンドは, エンジンが任意のタイミングで送るので, このイベントハンドラが呼び出される.
        /// </summary>
        public event EventHandler<string> OnNonResponceTextRecieved = delegate { };

        readonly Process PROCESS;
        readonly LinkedList<(string regex, ResponceValueFuture value)> waitingResponceList = new();

        /// <summary>
        /// 実際にエンジンのプロセスはEngineProcess.Startメソッドで行うので, コンストラクタはprivate.
        /// </summary>
        /// <param name="process"></param>
        EngineProcess(Process process)
        {
            this.PROCESS = process;
            this.PROCESS.OutputDataReceived += Process_OutputDataReceived;
            this.PROCESS.BeginOutputReadLine();
        }

        /// <summary>
        /// 与えられたエンジンのパスからプロセスを生成
        /// </summary>
        /// <param name="path">エンジンのパス(Windowsならexeファイルのパスなど)</param>
        public static EngineProcess? Start(string path, string args = "", string workDir = "")
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            if (workDir != string.Empty)
                psi.WorkingDirectory = workDir;

            var process = Process.Start(psi);
            return process is null ? null : new EngineProcess(process);
        }

        public Responce SendCommand(string cmd, string? responceRegex = null)
        {
            Debug.WriteLine($"Server -> {this.PROCESS.ProcessName}(PID: {this.PROCESS.Id}): {cmd}");

            if (responceRegex is null)
            {
                this.PROCESS.StandardInput.WriteLine(cmd);
                return new Responce(cmd);
            }

            var responceFuture = new ResponceValueFuture();
            var responce = new Responce(cmd, responceFuture);
            lock (this.waitingResponceList)
                this.waitingResponceList.AddFirst(new LinkedListNode<(string, ResponceValueFuture)>((responceRegex, responceFuture)));
            this.PROCESS.StandardInput.WriteLine(cmd);
            return responce;
        }

        public void WaitForExit(int timeoutMs) => this.PROCESS.WaitForExit(timeoutMs);

        public void Kill() => this.PROCESS.Kill();

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is null)
                return;

            Debug.WriteLine($"{this.PROCESS.ProcessName}(PID: {this.PROCESS.Id}) -> Server: {e.Data}");

            lock (this.waitingResponceList)
            {
                var responce = this.waitingResponceList.Where(x => Regex.IsMatch(e.Data, x.regex)).LastOrDefault();
                if (responce != default)
                {
                    this.waitingResponceList.Remove(responce);
                    responce.value.Value = e.Data;
                }
                else
                    this.OnNonResponceTextRecieved.Invoke(this, e.Data);
            }
        }

        /// <summary>
        /// エンジンに送ったコマンドからの応答を格納するクラス.
        /// </summary>
        public class Responce
        {
            /// <summary>
            /// 送信したコマンド.
            /// </summary>
            public string Command { get; private set; }

            /// <summary>
            /// コマンドの応答結果. まだ応答結果を得ていない場合は待機.
            /// </summary>
            public string Result
            {
                get
                {
                    while (this.result.Value is null)
                        Thread.Yield();
                    return this.result.Value;
                }
            }

            public bool HasResult => this.result.Value is not null;

            readonly ResponceValueFuture result;

            /// <summary>
            /// コンストラクタ.
            /// </summary>
            /// <param name="cmd">コマンド.</param>
            /// <param name="resultFuture">実際の応答結果が格納されるオブジェクト.</param>
            public Responce(string cmd, ResponceValueFuture resultFuture)
            {
                this.Command = cmd;
                this.result = resultFuture;
            }

            public Responce(string cmd) => (this.Command, this.result) = (cmd, new ResponceValueFuture { Value = string.Empty });

            public bool Wait(int timeoutMs)
            {
                var start = Environment.TickCount;
                while (this.result.Value is null && Environment.TickCount - start < timeoutMs)
                    Thread.Yield();
                return this.HasResult;
            }
        }

        /// <summary>
        /// エンジンからの応答文字列は, このクラスのオブジェクトに包む. 
        /// Responceクラスのコンストラクタには, ResponceValueオブジェクトを渡す. このようにすることで, ResponceValueオブジェクトを介してエンジンの応答結果をResponceオブジェクトに渡せるので, 
        /// Responce.Resultのsetterをpublicにする必要がなくなる.
        /// </summary>
        public class ResponceValueFuture { public string? Value { get; set; } = null; }
    }
}
