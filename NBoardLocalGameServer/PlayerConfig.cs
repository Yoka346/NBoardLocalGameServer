using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text.Json;

using NBoardLocalGameServer.Engine;

namespace NBoardLocalGameServer
{
    internal class PlayerConfig
    {
        public string Path { get; set; }
        public string Arguments { get; set; }
        public string WorkDir { get; set; }

        /// <summary>
        /// エンジン起動時にまとめて送るコマンド.
        /// 対局前にエンジンの設定をしたいときに用いる.
        /// </summary>
        public string[] InitialCommands { get; set; }

        public GameTime ThinkingTime { get; set; }

        public PlayerConfig() : this("", "", "", Enumerable.Empty<string>()) { }

        public PlayerConfig(string path, string args, string workDir, IEnumerable<string> initialCmds)
        {
            this.Path = path;
            this.Arguments = args;
            this.WorkDir = workDir;
            this.InitialCommands = initialCmds.ToArray();
        }

        public PlayerConfig(PlayerConfig config)
        {
            this.Path = config.Path;
            this.Arguments = config.Arguments;
            this.WorkDir = config.WorkDir;
            this.InitialCommands = (string[])config.InitialCommands.Clone();
        }

        public static PlayerConfig? Load(string path)
            => JsonSerializer.Deserialize<PlayerConfig>(File.ReadAllText(path));

        public void Save(string path)
            => File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
