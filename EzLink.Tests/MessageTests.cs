using System;
using System.Collections.Generic;
using System.IO;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class MessageTests
    {
        private static Frame ThroughTheWire(Frame frame)
        {
            MemoryStream ms = new MemoryStream();
            FrameStream.Write(ms, frame);
            ms.Position = 0;
            return FrameStream.Read(ms);
        }

        private static Frame WithPayload(Message original, byte[] payload)
        {
            Frame f = original.ToFrame(1);
            return new Frame(f.CommandSet, f.Command, f.RequestId, payload);
        }

        [Test]
        public void EveryMessageRoundTripsThroughTheWire()
        {
            foreach (Message original in Samples.All())
            {
                string name = original.GetType().Name;
                Frame sent = original.ToFrame(1234);
                Frame received = ThroughTheWire(sent);

                Assert.AreEqual(original.CommandSet, received.CommandSet, name);
                Assert.AreEqual(original.Command, received.Command, name);
                Assert.AreEqual(1234u, received.RequestId, name);

                Message parsed = MessageFactory.Parse(received);
                Assert.IsNotNull(parsed, name);
                Assert.AreSame(original.GetType(), parsed.GetType(), name);
                // Re-encoding the parsed message must reproduce the original bytes exactly,
                // which proves every field survived (including timestamps and 64-bit sizes).
                Assert.AreEqual(sent.Payload, parsed.ToFrame(1234).Payload, name);
            }
        }

        [Test]
        public void EveryMessageTypeHasASample()
        {
            // 9 general + 18 file commands.
            Assert.AreEqual(27, Samples.All().Count);
            List<string> keys = new List<string>();
            foreach (Message m in Samples.All())
            {
                string key = m.CommandSet + "/" + m.Command;
                Assert.IsFalse(keys.Contains(key), "duplicate command " + key);
                keys.Add(key);
            }
        }

        [Test]
        public void EveryTruncatedPayloadIsRejected()
        {
            foreach (Message original in Samples.All())
            {
                if (Samples.IsRawPayload(original))
                {
                    continue; // the payload is the data itself, any length is valid
                }
                byte[] payload = original.ToFrame(1).Payload;
                for (int cut = 0; cut < payload.Length; cut++)
                {
                    byte[] part = new byte[cut];
                    Array.Copy(payload, part, cut);
                    Frame frame = WithPayload(original, part);
                    Assert.Throws<ProtocolException>(
                        delegate { MessageFactory.Parse(frame); },
                        original.GetType().Name + " cut at " + cut);
                }
            }
        }

        [Test]
        public void EveryPayloadWithTrailingBytesIsRejected()
        {
            foreach (Message original in Samples.All())
            {
                if (Samples.IsRawPayload(original))
                {
                    continue;
                }
                byte[] payload = original.ToFrame(1).Payload;
                byte[] longer = new byte[payload.Length + 1];
                Array.Copy(payload, longer, payload.Length);
                Frame frame = WithPayload(original, longer);
                Assert.Throws<ProtocolException>(
                    delegate { MessageFactory.Parse(frame); },
                    original.GetType().Name);
            }
        }

        [Test]
        public void UnknownCommandSetOrCommandYieldsNull()
        {
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.General, 0x7F, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.General, 0x00, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.Files, 0x13, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.Sync, 0x01, 1, new byte[] { 1, 2, 3 })));
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.RemoteControl, 0x01, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(0x04, 0x01, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(CommandSets.FirstExperimental, 0x01, 1, null)));
            Assert.IsNull(MessageFactory.Parse(new Frame(0xFF, 0xFF, 1, null)));
        }

        [Test]
        public void UnsupportedMessageEchoesSetAndCommand()
        {
            Frame unknown = new Frame(0x42, 0x99, 555, null);
            Assert.IsNull(MessageFactory.Parse(unknown));

            Frame reply = new UnsupportedMessage(unknown.CommandSet, unknown.Command).ToFrame(unknown.RequestId);
            UnsupportedMessage parsed = (UnsupportedMessage)MessageFactory.Parse(ThroughTheWire(reply));
            Assert.AreEqual(0x42, parsed.UnsupportedSet);
            Assert.AreEqual(0x99, parsed.UnsupportedCommand);
            Assert.AreEqual(555u, reply.RequestId);
        }

        [Test]
        public void HelloCarriesAllFields()
        {
            HelloMessage hello = (HelloMessage)MessageFactory.Parse(
                ThroughTheWire(Samples.Get<HelloMessage>().ToFrame(9)));
            Assert.AreEqual(ProtocolInfo.Version, hello.ProtocolVersion);
            Assert.AreEqual("files", hello.SessionType);
            Assert.AreEqual(Samples.Id, hello.MachineId);
            Assert.AreEqual("ANA-LAPTOP \u00e9\u4e2d", hello.ComputerName);
            Assert.AreEqual("1.0.0", hello.AppVersion);
            Assert.AreEqual(new string[] { "files/1", "sync/1" }, hello.Capabilities.ToArray());
            Assert.AreEqual(new byte[] { 1, 2, 3 }, hello.Auth);
        }

        [Test]
        public void HelloStartsWithTheSessionMagic()
        {
            byte[] payload = new HelloMessage().ToFrame(1).Payload;
            Assert.AreEqual(new byte[] { (byte)'E', (byte)'Z', (byte)'L', (byte)'S', 1, 0 },
                new byte[] { payload[0], payload[1], payload[2], payload[3], payload[4], payload[5] });
        }

        [Test]
        public void HelloWithWrongMagicIsRejected()
        {
            byte[] payload = new HelloMessage().ToFrame(1).Payload;
            payload[0] = (byte)'X';
            Frame frame = new Frame(CommandSets.General, GeneralCommands.Hello, 1, payload);
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(frame); });
        }

        [Test]
        public void HelloOfAnotherProtocolVersionStillParsesSoTheListenerCanAnswerVersionMismatch()
        {
            HelloMessage future = new HelloMessage();
            future.ProtocolVersion = 2;
            HelloMessage parsed = (HelloMessage)MessageFactory.Parse(future.ToFrame(1));
            Assert.AreEqual(2, parsed.ProtocolVersion);
        }

        [Test]
        public void HelloReplyCarriesStatusAndPermissions()
        {
            HelloReplyMessage reply = (HelloReplyMessage)MessageFactory.Parse(
                ThroughTheWire(Samples.Get<HelloReplyMessage>().ToFrame(9)));
            Assert.AreEqual(HelloStatus.PendingApproval, reply.Status);
            Assert.AreEqual(Permissions.Browse | Permissions.Read, reply.GrantedPermissions);
            Assert.AreEqual("RADU-PC", reply.ComputerName);
        }

        [Test]
        public void EveryHelloStatusRoundTripsAndUnknownOnesAreRejected()
        {
            foreach (HelloStatus status in Enum.GetValues(typeof(HelloStatus)))
            {
                HelloReplyMessage m = new HelloReplyMessage();
                m.Status = status;
                HelloReplyMessage back = (HelloReplyMessage)MessageFactory.Parse(m.ToFrame(1));
                Assert.AreEqual(status, back.Status);
            }

            HelloReplyMessage bad = new HelloReplyMessage();
            byte[] payload = bad.ToFrame(1).Payload;
            payload[payload.Length - 5] = 0x77; // the status byte precedes the 4 permission bytes
            Frame frame = new Frame(CommandSets.General, GeneralCommands.HelloReply, 1, payload);
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(frame); });
        }

        [Test]
        public void ErrorKeepsCodeAndText()
        {
            ErrorMessage e = (ErrorMessage)MessageFactory.Parse(
                new ErrorMessage(ErrorCode.NotFound, "no such file").ToFrame(1));
            Assert.AreEqual(ErrorCode.NotFound, e.Code);
            Assert.AreEqual("no such file", e.Text);
        }

        [Test]
        public void ErrorWithACodeFromANewerPeerStillParses()
        {
            ErrorMessage e = (ErrorMessage)MessageFactory.Parse(
                new ErrorMessage((ErrorCode)999, "future").ToFrame(1));
            Assert.AreEqual(999, (int)e.Code);
        }

        [Test]
        public void DirListingKeepsEntriesAndEndFlag()
        {
            DirListingReply listing = (DirListingReply)MessageFactory.Parse(
                ThroughTheWire(Samples.Get<DirListingReply>().ToFrame(5)));
            Assert.IsFalse(listing.EndOfListing);
            Assert.AreEqual(2, listing.Entries.Count);
            Assert.AreEqual("a.txt", listing.Entries[0].Name);
            Assert.AreEqual(5L * 1024 * 1024 * 1024 + 7, listing.Entries[0].Size);
            Assert.AreEqual(Samples.Time, listing.Entries[0].Created);
            Assert.AreEqual(Samples.Time.AddSeconds(30), listing.Entries[0].Modified);
            Assert.AreEqual(0x20u, listing.Entries[0].Attributes);
        }

        [Test]
        public void EmptyListingsRoundTrip()
        {
            DirListingReply empty = new DirListingReply();
            DirListingReply back = (DirListingReply)MessageFactory.Parse(empty.ToFrame(1));
            Assert.AreEqual(0, back.Entries.Count);
            Assert.IsTrue(back.EndOfListing);

            RootsReply roots = (RootsReply)MessageFactory.Parse(new RootsReply().ToFrame(1));
            Assert.AreEqual(0, roots.Drives.Count);
        }

        [Test]
        public void ForgedEntryCountsAreRejectedWithoutAllocating()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteBool(true);
            w.WriteUInt32(int.MaxValue);
            Frame frame = new Frame(CommandSets.Files, FileCommands.DirListing, 1, w.ToArray());
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(frame); });

            PayloadWriter drives = new PayloadWriter();
            drives.WriteUInt32(uint.MaxValue);
            Frame rootsFrame = new Frame(CommandSets.Files, FileCommands.Roots, 1, drives.ToArray());
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(rootsFrame); });
        }

        [Test]
        public void RootsKeepDriveDetails()
        {
            RootsReply roots = (RootsReply)MessageFactory.Parse(
                ThroughTheWire(Samples.Get<RootsReply>().ToFrame(5)));
            Assert.AreEqual(2, roots.Drives.Count);
            Assert.AreEqual('D', roots.Drives[0].Letter);
            Assert.AreEqual(3, roots.Drives[0].DriveType);
            Assert.AreEqual("Data", roots.Drives[0].Label);
            Assert.AreEqual(123456789012, roots.Drives[0].FreeBytes);
            Assert.AreEqual(999999999999, roots.Drives[0].TotalBytes);
        }

        [Test]
        public void FileDataCarriesAFullChunkUnchanged()
        {
            FileDataReply chunk = new FileDataReply();
            chunk.Data = Samples.Bytes(64 * 1024);
            FileDataReply back = (FileDataReply)MessageFactory.Parse(ThroughTheWire(chunk.ToFrame(8)));
            Assert.AreEqual(chunk.Data, back.Data);
        }

        [Test]
        public void ZeroLengthFileDataIsValid()
        {
            FileDataReply back = (FileDataReply)MessageFactory.Parse(new FileDataReply().ToFrame(8));
            Assert.AreEqual(0, back.Data.Length);
        }

        [Test]
        public void FileDataLargerThanAFrameIsRejectedOnSend()
        {
            FileDataReply tooBig = new FileDataReply();
            tooBig.Data = new byte[ProtocolInfo.MaxFrameLength];
            Assert.Throws<ProtocolException>(delegate { tooBig.ToFrame(1); });
        }

        [Test]
        public void HashesMustBeExactly32Bytes()
        {
            FileEndReply end = new FileEndReply();
            end.Sha256 = new byte[31];
            Assert.Throws<ProtocolException>(delegate { end.ToFrame(1); });
            end.Sha256 = null;
            Assert.Throws<ProtocolException>(delegate { end.ToFrame(1); });

            WriteEndRequest write = new WriteEndRequest();
            write.Sha256 = new byte[33];
            Assert.Throws<ProtocolException>(delegate { write.ToFrame(1); });

            Frame shortHash = new Frame(CommandSets.Files, FileCommands.FileEnd, 1, new byte[31]);
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(shortHash); });
        }

        [Test]
        public void ReadFileOffsetBeyondFourGigabytesSurvives()
        {
            ReadFileRequest back = (ReadFileRequest)MessageFactory.Parse(
                new ReadFileRequest(@"C:\big.iso", 6L * 1024 * 1024 * 1024).ToFrame(1));
            Assert.AreEqual(@"C:\big.iso", back.Path);
            Assert.AreEqual(6L * 1024 * 1024 * 1024, back.Offset);
        }

        [Test]
        public void NegativeOffsetsAndSizesAreRejected()
        {
            PayloadWriter w = new PayloadWriter();
            w.WriteString(@"C:\f");
            w.WriteInt64(-1);
            Frame read = new Frame(CommandSets.Files, FileCommands.ReadFile, 1, w.ToArray());
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(read); });

            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Size = -10;
            Frame write = begin.ToFrame(1);
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(write); });
        }

        [Test]
        public void WriteBeginKeepsEveryFieldAndRejectsUnknownModes()
        {
            WriteBeginRequest back = (WriteBeginRequest)MessageFactory.Parse(
                ThroughTheWire(Samples.Get<WriteBeginRequest>().ToFrame(4)));
            Assert.AreEqual(@"D:\upload\f.bin", back.Path);
            Assert.AreEqual(1000, back.Size);
            Assert.AreEqual(Samples.Time, back.Created);
            Assert.AreEqual(Samples.Time, back.Modified);
            Assert.AreEqual(0x20u, back.Attributes);
            Assert.AreEqual(WriteMode.Overwrite, back.Mode);

            WriteBeginRequest bad = new WriteBeginRequest();
            bad.Mode = (WriteMode)200;
            Frame frame = bad.ToFrame(1);
            Assert.Throws<ProtocolException>(delegate { MessageFactory.Parse(frame); });
        }

        [Test]
        public void RenameKeepsBothPaths()
        {
            RenameRequest back = (RenameRequest)MessageFactory.Parse(
                new RenameRequest(@"C:\a", @"C:\b").ToFrame(1));
            Assert.AreEqual(@"C:\a", back.Path);
            Assert.AreEqual(@"C:\b", back.NewPath);
        }

        [Test]
        public void CancelAndTrustChangedCarryTheirValues()
        {
            CancelMessage cancel = (CancelMessage)MessageFactory.Parse(new CancelMessage(0xDEADBEEF).ToFrame(0));
            Assert.AreEqual(0xDEADBEEF, cancel.TargetRequestId);

            TrustChangedMessage trust = new TrustChangedMessage();
            trust.Granted = Permissions.Browse | Permissions.Read | Permissions.Write;
            TrustChangedMessage back = (TrustChangedMessage)MessageFactory.Parse(trust.ToFrame(0));
            Assert.AreEqual(Permissions.Browse | Permissions.Read | Permissions.Write, back.Granted);
        }

        [Test]
        public void PathsWithUnicodeAndLongNamesSurvive()
        {
            string path = "C:\\Users\\\u00c4na\\" + new string('x', 400) + "\u4e2d.txt";
            StatRequest back = (StatRequest)MessageFactory.Parse(new StatRequest(path).ToFrame(1));
            Assert.AreEqual(path, back.Path);
        }
    }
}
