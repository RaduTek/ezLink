using System;

namespace EzLink.Protocol
{
    /// <summary>
    /// The UDP discovery beacon (not a framed message): "EZLB", version, machine UUID,
    /// TCP session port, computer name, app version. At most 512 bytes.
    /// </summary>
    public sealed class Beacon
    {
        public const int MaxLength = 512;

        public ushort Version = ProtocolInfo.BeaconVersion;
        public Guid MachineId;
        public ushort TcpPort;
        public string ComputerName = string.Empty;
        public string AppVersion = string.Empty;

        public byte[] ToBytes()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteMagic(ProtocolInfo.BeaconMagic);
            w.WriteUInt16(Version);
            w.WriteGuid(MachineId);
            w.WriteUInt16(TcpPort);
            w.WriteString(ComputerName);
            w.WriteString(AppVersion);
            byte[] bytes = w.ToArray();
            if (bytes.Length > MaxLength)
            {
                throw new ProtocolException("Beacon of " + bytes.Length + " bytes exceeds " + MaxLength);
            }
            return bytes;
        }

        /// <summary>Parses a received datagram; throws <see cref="ProtocolException"/> if it is not a valid beacon.</summary>
        public static Beacon Parse(byte[] datagram)
        {
            if (datagram == null || datagram.Length > MaxLength)
            {
                throw new ProtocolException("Beacon missing or longer than " + MaxLength + " bytes");
            }
            PayloadReader r = new PayloadReader(datagram);
            Beacon b = new Beacon();
            r.ExpectMagic(ProtocolInfo.BeaconMagic);
            b.Version = r.ReadUInt16();
            if (b.Version != ProtocolInfo.BeaconVersion)
            {
                throw new ProtocolException("Unsupported beacon version " + b.Version);
            }
            b.MachineId = r.ReadGuid();
            b.TcpPort = r.ReadUInt16();
            b.ComputerName = r.ReadString();
            b.AppVersion = r.ReadString();
            r.EnsureEnd();
            return b;
        }
    }
}
