using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBoardLocalGameServer
{
    internal class NBoardProtocolException : Exception
    {
        public NBoardProtocolException(string msg) : base(msg) { }
    }
}
