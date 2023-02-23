using System;

namespace NBoardLocalGameServer
{
    internal class NBoardProtocolException : Exception
    {
        public NBoardProtocolException(string msg) : base(msg) { }
    }

    internal class EngineConnectionException : Exception
    {
        const string MSG = "Connection to {0} has not been established.";
        public EngineConnectionException(string engineName) : base(string.Format(MSG, engineName)) { }
    }
}
