using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace NBoardLocalGameServer
{
    /// <summary>
    /// 思考エンジンのプロセス.
    /// このクラスのオブジェクトを介してコマンドの送受信を行う.
    /// </summary>
    internal class EngineProcess
    {
        public bool HasExited => this.process.HasExited;
        public event EventHandler Exited { add => this.process.Exited += value; remove => this.process.Exited -= value; }

        Process process;
        Queue<string> recievedLines = new();

        EngineProcess(Process process)
        {
            this.process = process;
            this.process.OutputDataReceived += Process_OutputDataReceived;
            this.process.BeginOutputReadLine();
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
            Debug.WriteLine($"Server -> {this.process.ProcessName}(PID: {this.process.Id}): {cmd}");
            this.process.StandardInput.WriteLine(cmd);
        }

        public void WaitForExit(int timeoutMs) => this.process.WaitForExit(timeoutMs);

        void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data is not null)
            {
                Debug.WriteLine($"{this.process.ProcessName}(PID: {this.process.Id}) -> Server: {e.Data}");
                this.recievedLines.Enqueue(e.Data);
            }
        }
    }
}
