using System;
using System.Collections.Generic;

namespace EzLink.Protocol
{
    /// <summary>First message on every session, sent by the connecting side.</summary>
    public sealed class HelloMessage : Message
    {
        public ushort ProtocolVersion = ProtocolInfo.Version;
        public string SessionType = SessionTypes.Control;
        public Guid MachineId;
        public string ComputerName = string.Empty;
        public string AppVersion = string.Empty;
        public List<string> Capabilities = new List<string>();
        /// <summary>Reserved for a later shared-secret scheme; empty in v1.</summary>
        public byte[] Auth = new byte[0];

        public HelloMessage()
            : base(CommandSets.General, GeneralCommands.Hello)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteMagic(ProtocolInfo.SessionMagic);
            w.WriteUInt16(ProtocolVersion);
            w.WriteString(SessionType);
            w.WriteGuid(MachineId);
            w.WriteString(ComputerName);
            w.WriteString(AppVersion);
            w.WriteStringList(Capabilities);
            w.WriteBytes(Auth);
        }

        public static HelloMessage Read(PayloadReader r)
        {
            HelloMessage m = new HelloMessage();
            r.ExpectMagic(ProtocolInfo.SessionMagic);
            m.ProtocolVersion = r.ReadUInt16();
            m.SessionType = r.ReadString();
            m.MachineId = r.ReadGuid();
            m.ComputerName = r.ReadString();
            m.AppVersion = r.ReadString();
            m.Capabilities = r.ReadStringList();
            m.Auth = r.ReadBytes();
            return m;
        }
    }

    /// <summary>The listener's answer to Hello; PendingApproval is followed by a second reply.</summary>
    public sealed class HelloReplyMessage : Message
    {
        public ushort ProtocolVersion = ProtocolInfo.Version;
        public Guid MachineId;
        public string ComputerName = string.Empty;
        public string AppVersion = string.Empty;
        public List<string> Capabilities = new List<string>();
        public byte[] Auth = new byte[0];
        public HelloStatus Status = HelloStatus.Accepted;
        public Permissions GrantedPermissions = Permissions.None;

        public HelloReplyMessage()
            : base(CommandSets.General, GeneralCommands.HelloReply)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteMagic(ProtocolInfo.SessionMagic);
            w.WriteUInt16(ProtocolVersion);
            w.WriteGuid(MachineId);
            w.WriteString(ComputerName);
            w.WriteString(AppVersion);
            w.WriteStringList(Capabilities);
            w.WriteBytes(Auth);
            w.WriteByte((byte)Status);
            w.WriteUInt32((uint)GrantedPermissions);
        }

        public static HelloReplyMessage Read(PayloadReader r)
        {
            HelloReplyMessage m = new HelloReplyMessage();
            r.ExpectMagic(ProtocolInfo.SessionMagic);
            m.ProtocolVersion = r.ReadUInt16();
            m.MachineId = r.ReadGuid();
            m.ComputerName = r.ReadString();
            m.AppVersion = r.ReadString();
            m.Capabilities = r.ReadStringList();
            m.Auth = r.ReadBytes();
            byte status = r.ReadByte();
            if (!Enum.IsDefined(typeof(HelloStatus), status))
            {
                throw new ProtocolException("Unknown Hello status " + status);
            }
            m.Status = (HelloStatus)status;
            m.GrantedPermissions = (Permissions)r.ReadUInt32();
            return m;
        }
    }

    /// <summary>Keep-alive on the control session; the reply is a Pong with the same request id.</summary>
    public sealed class PingMessage : Message
    {
        public PingMessage()
            : base(CommandSets.General, GeneralCommands.Ping)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
        }

        public static PingMessage Read(PayloadReader r)
        {
            return new PingMessage();
        }
    }

    public sealed class PongMessage : Message
    {
        public PongMessage()
            : base(CommandSets.General, GeneralCommands.Pong)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
        }

        public static PongMessage Read(PayloadReader r)
        {
            return new PongMessage();
        }
    }

    /// <summary>Success reply used by every command set.</summary>
    public sealed class OkMessage : Message
    {
        public OkMessage()
            : base(CommandSets.General, GeneralCommands.Ok)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
        }

        public static OkMessage Read(PayloadReader r)
        {
            return new OkMessage();
        }
    }

    /// <summary>Failure reply used by every command set.</summary>
    public sealed class ErrorMessage : Message
    {
        public ErrorCode Code = ErrorCode.IoError;
        public string Text = string.Empty;

        public ErrorMessage()
            : base(CommandSets.General, GeneralCommands.Error)
        {
        }

        public ErrorMessage(ErrorCode code, string text)
            : this()
        {
            Code = code;
            Text = text;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteInt32((int)Code);
            w.WriteString(Text);
        }

        public static ErrorMessage Read(PayloadReader r)
        {
            ErrorMessage m = new ErrorMessage();
            m.Code = (ErrorCode)r.ReadInt32();
            m.Text = r.ReadString();
            return m;
        }
    }

    /// <summary>Stops an in-progress request, identified by its request id. No reply.</summary>
    public sealed class CancelMessage : Message
    {
        public uint TargetRequestId;

        public CancelMessage()
            : base(CommandSets.General, GeneralCommands.Cancel)
        {
        }

        public CancelMessage(uint targetRequestId)
            : this()
        {
            TargetRequestId = targetRequestId;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteUInt32(TargetRequestId);
        }

        public static CancelMessage Read(PayloadReader r)
        {
            CancelMessage m = new CancelMessage();
            m.TargetRequestId = r.ReadUInt32();
            return m;
        }
    }

    /// <summary>Sent on the control session when the sender changes what it allows the receiver to do.</summary>
    public sealed class TrustChangedMessage : Message
    {
        /// <summary>The permissions the sender now grants the receiver.</summary>
        public Permissions Granted = Permissions.None;

        public TrustChangedMessage()
            : base(CommandSets.General, GeneralCommands.TrustChanged)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteUInt32((uint)Granted);
        }

        public static TrustChangedMessage Read(PayloadReader r)
        {
            TrustChangedMessage m = new TrustChangedMessage();
            m.Granted = (Permissions)r.ReadUInt32();
            return m;
        }
    }

    /// <summary>Reply to a command whose set or number the receiver does not know; the session stays open.</summary>
    public sealed class UnsupportedMessage : Message
    {
        public byte UnsupportedSet;
        public byte UnsupportedCommand;

        public UnsupportedMessage()
            : base(CommandSets.General, GeneralCommands.Unsupported)
        {
        }

        public UnsupportedMessage(byte commandSet, byte command)
            : this()
        {
            UnsupportedSet = commandSet;
            UnsupportedCommand = command;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteByte(UnsupportedSet);
            w.WriteByte(UnsupportedCommand);
        }

        public static UnsupportedMessage Read(PayloadReader r)
        {
            UnsupportedMessage m = new UnsupportedMessage();
            m.UnsupportedSet = r.ReadByte();
            m.UnsupportedCommand = r.ReadByte();
            return m;
        }
    }
}
