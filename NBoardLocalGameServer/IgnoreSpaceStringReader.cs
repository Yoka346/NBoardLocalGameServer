using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBoardLocalGameServer
{
    /// <summary>
    /// 空白を無視してstringを読むクラス.
    /// </summary>
    internal class IgnoreSpaceStringReader
    {
        static ReadOnlySpan<char> WHITESPACE => " \n\r\t\f\v";

        readonly string STR;
        int position = 0;

        public int Position => position;

        public IgnoreSpaceStringReader(string str) => this.STR = str;
        public IgnoreSpaceStringReader(ReadOnlySpan<char> str) => this.STR = str.ToString();

        public int Peek() => (this.position >= this.STR.Length - 1) ? -1 : this.STR[this.position + 1];

        /// <summary>
        /// offsetから空白文字以外の文字が始まる位置を探し、そこから次の空白文字までの部分文字列を返す.
        /// </summary>
        /// <returns></returns>
        public string Read()
        {
            var canRead = false;
            for (; this.position < this.STR.Length; this.position++)
                if (!WHITESPACE.Contains(this.STR[this.position]))
                {
                    canRead = true;
                    break;
                }

            if (!canRead)
                return "\0";

            int count;
            for (count = 1; this.position + count < this.STR.Length && this.STR[this.position + count] != ' '; count++) ;

            var start = this.position;
            this.position += count;
            return this.STR.AsSpan(start, count).ToString();
        }

        /// <summary>
        /// offsetから空白文字以外の文字が始まる位置を探し、そこから終端までの部分文字列を返す.
        /// </summary>
        /// <returns></returns>
        public string ReadToEnd()
        {
            var canRead = false;
            for (; this.position < this.STR.Length; this.position++)
                if (!WHITESPACE.Contains(this.STR[this.position]))
                {
                    canRead = true;
                    break;
                }

            if (!canRead)
                return "\0";

            var ret = this.STR.AsSpan(this.position, this.STR.Length - this.position);
            this.position = this.STR.Length;
            return ret.ToString();
        }
    }
}
