namespace EzLink.Protocol
{
    /// <summary>
    /// Every command set and command number of the wire protocol, in one place.
    /// </summary>
    public static class CommandSets
    {
        public const byte General = 0x00;
        public const byte Files = 0x01;
        public const byte Sync = 0x02;          // reserved
        public const byte RemoteControl = 0x03; // reserved

        /// <summary>Sets 0x80-0xFF are experimental and never used in release builds.</summary>
        public const byte FirstExperimental = 0x80;
    }

    /// <summary>Commands of set 0x00 (General), always available.</summary>
    public static class GeneralCommands
    {
        public const byte Hello = 0x01;
        public const byte HelloReply = 0x02;
        public const byte Ping = 0x03;
        public const byte Pong = 0x04;
        public const byte Ok = 0x05;
        public const byte Error = 0x06;
        public const byte Cancel = 0x07;
        public const byte TrustChanged = 0x08;
        public const byte Unsupported = 0x09;
    }

    /// <summary>Commands of set 0x01 (File transfer), capability "files/1".</summary>
    public static class FileCommands
    {
        public const byte GetRoots = 0x01;
        public const byte Roots = 0x02;
        public const byte ListDir = 0x03;
        public const byte DirListing = 0x04;
        public const byte ListTree = 0x05;
        public const byte TreeEntries = 0x06;
        public const byte TreeEnd = 0x07;
        public const byte Stat = 0x08;
        public const byte StatResult = 0x09;
        public const byte ReadFile = 0x0A;
        public const byte FileData = 0x0B;
        public const byte FileEnd = 0x0C;
        public const byte WriteBegin = 0x0D;
        public const byte WriteData = 0x0E;
        public const byte WriteEnd = 0x0F;
        public const byte CreateDir = 0x10;
        public const byte Delete = 0x11;
        public const byte Rename = 0x12;
    }

    /// <summary>Session types declared in Hello.</summary>
    public static class SessionTypes
    {
        public const string Control = "control";
        public const string Files = "files";
        public const string Sync = "sync"; // reserved
    }

    /// <summary>Capability strings (name/version) exchanged in Hello; each maps to one command set.</summary>
    public static class Capabilities
    {
        public const string Files = "files/1";
    }
}
