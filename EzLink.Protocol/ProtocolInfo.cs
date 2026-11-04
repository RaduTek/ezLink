namespace EzLink.Protocol
{
    /// <summary>
    /// Protocol-level constants. Frame reader/writer, command set numbers and
    /// the message classes arrive in milestone 2.
    /// </summary>
    public static class ProtocolInfo
    {
        /// <summary>Wire protocol version sent in Hello / HelloReply.</summary>
        public const ushort Version = 1;

        /// <summary>Beacon version (UDP discovery payload).</summary>
        public const ushort BeaconVersion = 1;

        /// <summary>Magic of a discovery beacon: ASCII "EZLB".</summary>
        public const string BeaconMagic = "EZLB";

        /// <summary>Magic of a session Hello: ASCII "EZLS".</summary>
        public const string SessionMagic = "EZLS";

        /// <summary>Frames larger than this (bytes after the length field) are rejected.</summary>
        public const int MaxFrameLength = 1024 * 1024;
    }
}
