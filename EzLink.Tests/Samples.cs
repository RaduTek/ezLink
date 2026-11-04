using System;
using System.Collections.Generic;
using EzLink.Protocol;

namespace EzLink.Tests
{
    /// <summary>One fully populated instance of every message type, shared by the tests.</summary>
    internal static class Samples
    {
        public static readonly DateTime Time =
            new DateTime(2026, 11, 5, 12, 30, 15, DateTimeKind.Utc).AddTicks(1234567);

        public static readonly Guid Id = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        public static FileEntry Entry(string name)
        {
            FileEntry e = new FileEntry();
            e.Name = name;
            e.Attributes = 0x20;
            e.Size = 5L * 1024 * 1024 * 1024 + 7;
            e.Created = Time;
            e.Modified = Time.AddSeconds(30);
            return e;
        }

        public static byte[] Hash()
        {
            byte[] hash = new byte[32];
            for (int i = 0; i < hash.Length; i++)
            {
                hash[i] = (byte)(i + 1);
            }
            return hash;
        }

        public static byte[] Bytes(int count)
        {
            byte[] data = new byte[count];
            for (int i = 0; i < count; i++)
            {
                data[i] = (byte)(i * 7);
            }
            return data;
        }

        /// <summary>Messages whose payload is "the rest of the frame", so truncation or padding is not detectable.</summary>
        public static bool IsRawPayload(Message m)
        {
            return m is FileDataReply || m is WriteDataRequest;
        }

        public static T Get<T>() where T : Message
        {
            foreach (Message m in All())
            {
                if (m is T)
                {
                    return (T)m;
                }
            }
            throw new InvalidOperationException("No sample of type " + typeof(T).Name);
        }

        public static List<Message> All()
        {
            List<Message> list = new List<Message>();

            HelloMessage hello = new HelloMessage();
            hello.SessionType = SessionTypes.Files;
            hello.MachineId = Id;
            hello.ComputerName = "ANA-LAPTOP \u00e9\u4e2d";
            hello.AppVersion = "1.0.0";
            hello.Capabilities.Add(Capabilities.Files);
            hello.Capabilities.Add("sync/1");
            hello.Auth = new byte[] { 1, 2, 3 };
            list.Add(hello);

            HelloReplyMessage reply = new HelloReplyMessage();
            reply.MachineId = Id;
            reply.ComputerName = "RADU-PC";
            reply.AppVersion = "1.0.0";
            reply.Capabilities.Add(Capabilities.Files);
            reply.Status = HelloStatus.PendingApproval;
            reply.GrantedPermissions = Permissions.Browse | Permissions.Read;
            list.Add(reply);

            list.Add(new PingMessage());
            list.Add(new PongMessage());
            list.Add(new OkMessage());
            list.Add(new ErrorMessage(ErrorCode.PermissionNotGranted, "Write permission required"));
            list.Add(new CancelMessage(0xDEADBEEF));

            TrustChangedMessage trust = new TrustChangedMessage();
            trust.Granted = Permissions.Browse | Permissions.Write;
            list.Add(trust);

            list.Add(new UnsupportedMessage(0x42, 0x99));

            list.Add(new GetRootsRequest());
            RootsReply roots = new RootsReply();
            DriveEntry drive = new DriveEntry();
            drive.Letter = 'D';
            drive.DriveType = 3;
            drive.Label = "Data";
            drive.FreeBytes = 123456789012;
            drive.TotalBytes = 999999999999;
            roots.Drives.Add(drive);
            roots.Drives.Add(new DriveEntry());
            list.Add(roots);

            list.Add(new ListDirRequest(@"C:\Users\Ana", true));
            DirListingReply listing = new DirListingReply();
            listing.EndOfListing = false;
            listing.Entries.Add(Entry("a.txt"));
            listing.Entries.Add(Entry("Folder"));
            list.Add(listing);

            list.Add(new ListTreeRequest(@"C:\Users\Ana\Documents"));
            TreeEntriesReply tree = new TreeEntriesReply();
            tree.Entries.Add(Entry(@"sub\b.txt"));
            list.Add(tree);
            list.Add(new TreeEndReply());

            list.Add(new StatRequest(@"C:\x"));
            StatResultReply stat = new StatResultReply();
            stat.Exists = true;
            stat.Attributes = 0x10;
            stat.Size = 42;
            stat.Created = Time;
            stat.Modified = Time;
            list.Add(stat);

            list.Add(new ReadFileRequest(@"C:\big.iso", 5L * 1024 * 1024 * 1024));
            FileDataReply data = new FileDataReply();
            data.Data = Bytes(64 * 1024);
            list.Add(data);
            FileEndReply end = new FileEndReply();
            end.Sha256 = Hash();
            list.Add(end);

            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = @"D:\upload\f.bin";
            begin.Size = 1000;
            begin.Created = Time;
            begin.Modified = Time;
            begin.Attributes = 0x20;
            begin.Mode = WriteMode.Overwrite;
            list.Add(begin);
            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = Bytes(1000);
            list.Add(chunk);
            WriteEndRequest writeEnd = new WriteEndRequest();
            writeEnd.Sha256 = Hash();
            list.Add(writeEnd);

            list.Add(new CreateDirRequest(@"C:\new"));
            list.Add(new DeleteRequest(@"C:\old"));
            list.Add(new RenameRequest(@"C:\a", @"C:\b"));

            return list;
        }
    }
}
