using System;
using System.Collections.Generic;

namespace EzLink.Protocol
{
    /// <summary>One file or folder in a listing. In a tree listing <see cref="Name"/> is a relative path.</summary>
    public sealed class FileEntry
    {
        /// <summary>Smallest encoded size: empty name (2) + attributes (4) + size (8) + two times (16).</summary>
        public const int MinWireSize = 30;

        public string Name = string.Empty;
        public uint Attributes;
        public long Size;
        public DateTime Created;
        public DateTime Modified;

        public void Write(PayloadWriter w)
        {
            w.WriteString(Name);
            w.WriteUInt32(Attributes);
            w.WriteInt64(Size);
            w.WriteFileTime(Created);
            w.WriteFileTime(Modified);
        }

        public static FileEntry Read(PayloadReader r)
        {
            FileEntry e = new FileEntry();
            e.Name = r.ReadString();
            e.Attributes = r.ReadUInt32();
            e.Size = r.ReadNonNegativeInt64();
            e.Created = r.ReadFileTime();
            e.Modified = r.ReadFileTime();
            return e;
        }

        internal static void WriteList(PayloadWriter w, List<FileEntry> entries)
        {
            w.WriteUInt32((uint)entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].Write(w);
            }
        }

        internal static List<FileEntry> ReadList(PayloadReader r)
        {
            int count = r.ReadCount(MinWireSize);
            List<FileEntry> list = new List<FileEntry>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(Read(r));
            }
            return list;
        }
    }

    /// <summary>One drive in a Roots reply.</summary>
    public sealed class DriveEntry
    {
        /// <summary>Smallest encoded size: letter (2) + type (1) + empty label (2) + free (8) + total (8).</summary>
        public const int MinWireSize = 21;

        public char Letter = 'C';
        /// <summary>A System.IO.DriveType value (Fixed = 3, Removable = 2).</summary>
        public byte DriveType;
        public string Label = string.Empty;
        public long FreeBytes;
        public long TotalBytes;

        public void Write(PayloadWriter w)
        {
            w.WriteUInt16(Letter);
            w.WriteByte(DriveType);
            w.WriteString(Label);
            w.WriteInt64(FreeBytes);
            w.WriteInt64(TotalBytes);
        }

        public static DriveEntry Read(PayloadReader r)
        {
            DriveEntry d = new DriveEntry();
            d.Letter = (char)r.ReadUInt16();
            d.DriveType = r.ReadByte();
            d.Label = r.ReadString();
            d.FreeBytes = r.ReadNonNegativeInt64();
            d.TotalBytes = r.ReadNonNegativeInt64();
            return d;
        }
    }

    /// <summary>Shared by requests that carry a single path.</summary>
    public abstract class PathRequest : Message
    {
        public string Path = string.Empty;

        protected PathRequest(byte command)
            : base(CommandSets.Files, command)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteString(Path);
        }
    }

    public sealed class GetRootsRequest : Message
    {
        public GetRootsRequest()
            : base(CommandSets.Files, FileCommands.GetRoots)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
        }

        public static GetRootsRequest Read(PayloadReader r)
        {
            return new GetRootsRequest();
        }
    }

    public sealed class RootsReply : Message
    {
        public List<DriveEntry> Drives = new List<DriveEntry>();

        public RootsReply()
            : base(CommandSets.Files, FileCommands.Roots)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteUInt32((uint)Drives.Count);
            for (int i = 0; i < Drives.Count; i++)
            {
                Drives[i].Write(w);
            }
        }

        public static RootsReply Read(PayloadReader r)
        {
            RootsReply m = new RootsReply();
            int count = r.ReadCount(DriveEntry.MinWireSize);
            for (int i = 0; i < count; i++)
            {
                m.Drives.Add(DriveEntry.Read(r));
            }
            return m;
        }
    }

    public sealed class ListDirRequest : PathRequest
    {
        /// <summary>Return folders only (used to fill the folder tree).</summary>
        public bool FoldersOnly;

        public ListDirRequest()
            : base(FileCommands.ListDir)
        {
        }

        public ListDirRequest(string path, bool foldersOnly)
            : this()
        {
            Path = path;
            FoldersOnly = foldersOnly;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            base.WritePayload(w);
            w.WriteBool(FoldersOnly);
        }

        public static ListDirRequest Read(PayloadReader r)
        {
            ListDirRequest m = new ListDirRequest();
            m.Path = r.ReadString();
            m.FoldersOnly = r.ReadBool();
            return m;
        }
    }

    /// <summary>One chunk of a directory listing; the last chunk has <see cref="EndOfListing"/> set.</summary>
    public sealed class DirListingReply : Message
    {
        public bool EndOfListing = true;
        public List<FileEntry> Entries = new List<FileEntry>();

        public DirListingReply()
            : base(CommandSets.Files, FileCommands.DirListing)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteBool(EndOfListing);
            FileEntry.WriteList(w, Entries);
        }

        public static DirListingReply Read(PayloadReader r)
        {
            DirListingReply m = new DirListingReply();
            m.EndOfListing = r.ReadBool();
            m.Entries = FileEntry.ReadList(r);
            return m;
        }
    }

    public sealed class ListTreeRequest : PathRequest
    {
        public ListTreeRequest()
            : base(FileCommands.ListTree)
        {
        }

        public ListTreeRequest(string path)
            : this()
        {
            Path = path;
        }

        public static ListTreeRequest Read(PayloadReader r)
        {
            ListTreeRequest m = new ListTreeRequest();
            m.Path = r.ReadString();
            return m;
        }
    }

    /// <summary>A batch of recursive-listing entries with paths relative to the requested folder.</summary>
    public sealed class TreeEntriesReply : Message
    {
        public List<FileEntry> Entries = new List<FileEntry>();

        public TreeEntriesReply()
            : base(CommandSets.Files, FileCommands.TreeEntries)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            FileEntry.WriteList(w, Entries);
        }

        public static TreeEntriesReply Read(PayloadReader r)
        {
            TreeEntriesReply m = new TreeEntriesReply();
            m.Entries = FileEntry.ReadList(r);
            return m;
        }
    }

    public sealed class TreeEndReply : Message
    {
        public TreeEndReply()
            : base(CommandSets.Files, FileCommands.TreeEnd)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
        }

        public static TreeEndReply Read(PayloadReader r)
        {
            return new TreeEndReply();
        }
    }

    public sealed class StatRequest : PathRequest
    {
        public StatRequest()
            : base(FileCommands.Stat)
        {
        }

        public StatRequest(string path)
            : this()
        {
            Path = path;
        }

        public static StatRequest Read(PayloadReader r)
        {
            StatRequest m = new StatRequest();
            m.Path = r.ReadString();
            return m;
        }
    }

    public sealed class StatResultReply : Message
    {
        public bool Exists;
        public uint Attributes;
        public long Size;
        public DateTime Created;
        public DateTime Modified;

        public StatResultReply()
            : base(CommandSets.Files, FileCommands.StatResult)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteBool(Exists);
            w.WriteUInt32(Attributes);
            w.WriteInt64(Size);
            w.WriteFileTime(Created);
            w.WriteFileTime(Modified);
        }

        public static StatResultReply Read(PayloadReader r)
        {
            StatResultReply m = new StatResultReply();
            m.Exists = r.ReadBool();
            m.Attributes = r.ReadUInt32();
            m.Size = r.ReadNonNegativeInt64();
            m.Created = r.ReadFileTime();
            m.Modified = r.ReadFileTime();
            return m;
        }
    }

    public sealed class ReadFileRequest : PathRequest
    {
        public long Offset;

        public ReadFileRequest()
            : base(FileCommands.ReadFile)
        {
        }

        public ReadFileRequest(string path, long offset)
            : this()
        {
            Path = path;
            Offset = offset;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            base.WritePayload(w);
            w.WriteInt64(Offset);
        }

        public static ReadFileRequest Read(PayloadReader r)
        {
            ReadFileRequest m = new ReadFileRequest();
            m.Path = r.ReadString();
            m.Offset = r.ReadNonNegativeInt64();
            return m;
        }
    }

    /// <summary>A chunk of file content (the whole payload is the data, normally 64 KiB).</summary>
    public sealed class FileDataReply : Message
    {
        public byte[] Data = new byte[0];

        public FileDataReply()
            : base(CommandSets.Files, FileCommands.FileData)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteRaw(Data);
        }

        public static FileDataReply Read(PayloadReader r)
        {
            FileDataReply m = new FileDataReply();
            m.Data = r.ReadRemaining();
            return m;
        }
    }

    /// <summary>Ends a ReadFile stream; carries the SHA-256 of the bytes sent.</summary>
    public sealed class FileEndReply : Message
    {
        public const int HashLength = 32;

        public byte[] Sha256 = new byte[HashLength];

        public FileEndReply()
            : base(CommandSets.Files, FileCommands.FileEnd)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            if (Sha256 == null || Sha256.Length != HashLength)
            {
                throw new ProtocolException("SHA-256 hash must be " + HashLength + " bytes");
            }
            w.WriteRaw(Sha256);
        }

        public static FileEndReply Read(PayloadReader r)
        {
            FileEndReply m = new FileEndReply();
            m.Sha256 = r.ReadRaw(HashLength);
            return m;
        }
    }

    public sealed class WriteBeginRequest : PathRequest
    {
        public long Size;
        public DateTime Created;
        public DateTime Modified;
        public uint Attributes;
        public WriteMode Mode = WriteMode.FailIfExists;

        public WriteBeginRequest()
            : base(FileCommands.WriteBegin)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            base.WritePayload(w);
            w.WriteInt64(Size);
            w.WriteFileTime(Created);
            w.WriteFileTime(Modified);
            w.WriteUInt32(Attributes);
            w.WriteByte((byte)Mode);
        }

        public static WriteBeginRequest Read(PayloadReader r)
        {
            WriteBeginRequest m = new WriteBeginRequest();
            m.Path = r.ReadString();
            m.Size = r.ReadNonNegativeInt64();
            m.Created = r.ReadFileTime();
            m.Modified = r.ReadFileTime();
            m.Attributes = r.ReadUInt32();
            byte mode = r.ReadByte();
            if (!Enum.IsDefined(typeof(WriteMode), mode))
            {
                throw new ProtocolException("Unknown write mode " + mode);
            }
            m.Mode = (WriteMode)mode;
            return m;
        }
    }

    /// <summary>A chunk of file content after WriteBegin; no reply is sent.</summary>
    public sealed class WriteDataRequest : Message
    {
        public byte[] Data = new byte[0];

        public WriteDataRequest()
            : base(CommandSets.Files, FileCommands.WriteData)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            w.WriteRaw(Data);
        }

        public static WriteDataRequest Read(PayloadReader r)
        {
            WriteDataRequest m = new WriteDataRequest();
            m.Data = r.ReadRemaining();
            return m;
        }
    }

    /// <summary>Finishes an upload; the server checks the SHA-256 before renaming the part file.</summary>
    public sealed class WriteEndRequest : Message
    {
        public byte[] Sha256 = new byte[FileEndReply.HashLength];

        public WriteEndRequest()
            : base(CommandSets.Files, FileCommands.WriteEnd)
        {
        }

        protected override void WritePayload(PayloadWriter w)
        {
            if (Sha256 == null || Sha256.Length != FileEndReply.HashLength)
            {
                throw new ProtocolException("SHA-256 hash must be " + FileEndReply.HashLength + " bytes");
            }
            w.WriteRaw(Sha256);
        }

        public static WriteEndRequest Read(PayloadReader r)
        {
            WriteEndRequest m = new WriteEndRequest();
            m.Sha256 = r.ReadRaw(FileEndReply.HashLength);
            return m;
        }
    }

    /// <summary>
    /// Creates a folder (creating an existing one is fine). Optional times (flagged by one byte) are applied to
    /// the folder, so an upload can stamp a folder after its contents are written.
    /// </summary>
    public sealed class CreateDirRequest : PathRequest
    {
        public bool HasTimes;
        public DateTime Created;
        public DateTime Modified;

        public CreateDirRequest()
            : base(FileCommands.CreateDir)
        {
        }

        public CreateDirRequest(string path)
            : this()
        {
            Path = path;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            base.WritePayload(w);
            w.WriteBool(HasTimes);
            if (HasTimes)
            {
                w.WriteFileTime(Created);
                w.WriteFileTime(Modified);
            }
        }

        public static CreateDirRequest Read(PayloadReader r)
        {
            CreateDirRequest m = new CreateDirRequest();
            m.Path = r.ReadString();
            m.HasTimes = r.ReadBool();
            if (m.HasTimes)
            {
                m.Created = r.ReadFileTime();
                m.Modified = r.ReadFileTime();
            }
            return m;
        }
    }

    /// <summary>Deletes a file, or a folder recursively; needs the Write permission.</summary>
    public sealed class DeleteRequest : PathRequest
    {
        public DeleteRequest()
            : base(FileCommands.Delete)
        {
        }

        public DeleteRequest(string path)
            : this()
        {
            Path = path;
        }

        public static DeleteRequest Read(PayloadReader r)
        {
            DeleteRequest m = new DeleteRequest();
            m.Path = r.ReadString();
            return m;
        }
    }

    public sealed class RenameRequest : PathRequest
    {
        /// <summary>The full new path (<see cref="PathRequest.Path"/> is the old one).</summary>
        public string NewPath = string.Empty;

        public RenameRequest()
            : base(FileCommands.Rename)
        {
        }

        public RenameRequest(string oldPath, string newPath)
            : this()
        {
            Path = oldPath;
            NewPath = newPath;
        }

        protected override void WritePayload(PayloadWriter w)
        {
            base.WritePayload(w);
            w.WriteString(NewPath);
        }

        public static RenameRequest Read(PayloadReader r)
        {
            RenameRequest m = new RenameRequest();
            m.Path = r.ReadString();
            m.NewPath = r.ReadString();
            return m;
        }
    }
}
