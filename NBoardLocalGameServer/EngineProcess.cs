using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NBoardLocalGameServer
{
    /// <summary>
    /// 思考エンジンのプロセス.
    /// このクラスのオブジェクトを介してコマンドの送受信を行う.
    /// </summary>
    internal class EngineProcess
    {
        public bool HasExited => this.PROCESS.HasExited;
        public event EventHandler Exited { add => this.PROCESS.Exited += value; remove => this.PROCESS.Exited -= value; }

        readonly Process PROCESS;
        readonly Queue<string> recievedLines = new();

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

        public IgnoreSpaceStringReader ReadOutput() => new((this.recievedLines.Count == 0) ? string.Empty : this.recievedLines.Dequeue());

        public void SendCommand(string cmd)
        {
            Debug.WriteLine($"Server -> {this.PROCESS.ProcessName}(PID: {this.PROCESS.Id}): {cmd}");
            this.PROCESS.StandardInput.WriteLine(cmd);
        }

        public void WaitForExit(int timeoutMs) => this.PROCESS.WaitForExit(timeoutMs);

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                Debug.WriteLine($"{this.PROCESS.ProcessName}(PID: {this.PROCESS.Id}) -> Server: {e.Data}");
                this.recievedLines.Enqueue(e.Data);
            }
        }
    }
}
