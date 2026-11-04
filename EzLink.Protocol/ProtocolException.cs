using System;

namespace EzLink.Protocol
{
    /// <summary>
    /// Thrown when data received from (or about to be sent to) a peer is malformed:
    /// truncated, oversized, bad magic, invalid UTF-8, out-of-range values.
    /// </summary>
    public class ProtocolException : Exception
    {
        public ProtocolException(string message)
            : base(message)
        {
        }

        public ProtocolException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
