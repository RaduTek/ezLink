using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>
    /// The file server against a temp folder over loopback (tests may use loopback; production code may not):
    /// a real <see cref="SessionListener"/>, <see cref="FileServer"/> and <see cref="FileClient"/>.
    /// </summary>
    [TestFixture]
    public class FileServerTests
    {
        private const int Port = 49800;
        private static readonly Guid ServerId = new Guid("00000000-0000-0000-0000-0000000000a1");
        private static readonly Guid ClientId = new Guid("00000000-0000-0000-0000-0000000000b2");
        private static readonly IPAddress Mask = IPAddress.Parse("255.255.255.255");
        private const Permissions All = Permissions.Browse | Permissions.Read | Permissions.Write;

        private sealed class Host : ISessionHost
        {
            public volatile Permissions Granted = All;

            public HelloReplyMessage HandleHello(HelloMessage hello, IPEndPoint remote, SessionConnection connection)
            {
                HelloReplyMessage reply = new HelloReplyMessage();
                reply.MachineId = ServerId;
                reply.ComputerName = "SERVER";
                reply.AppVersion = "1.0.0";
                reply.Status = HelloStatus.Accepted;
                reply.Capabilities.Add(Capabilities.Files);
                return reply;
            }

            public bool SessionEstablished(PeerSession session)
            {
                new FileServer(session, delegate { return Granted; });
                return true;
            }
        }

        /// <summary>Records every message a raw files session receives, for tests that speak the protocol by hand.</summary>
        private sealed class RawSession : ICommandSet
        {
            public readonly PeerSession Session;
            private readonly Queue<KeyValuePair<uint, Message>> received = new Queue<KeyValuePair<uint, Message>>();

            public RawSession()
            {
                HelloMessage hello = new HelloMessage();
                hello.SessionType = SessionTypes.Files;
                hello.MachineId = ClientId;
                hello.ComputerName = "RAW";
                hello.AppVersion = "1.0.0";
                HelloReplyMessage reply;
                Session = SessionClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, Port), hello, out reply);
                Session.Register(this);
                Session.ReplyReceived += delegate(object s, MessageEventArgs e) { Add(e.Message, e.RequestId); };
                Session.Start();
            }

            public byte CommandSet { get { return CommandSets.Files; } }
            public string Capability { get { return Capabilities.Files; } }

            public void Handle(Frame frame, PeerSession session)
            {
                Add(MessageFactory.Parse(frame), frame.RequestId);
            }

            private void Add(Message m, uint id)
            {
                lock (received)
                {
                    received.Enqueue(new KeyValuePair<uint, Message>(id, m));
                    Monitor.PulseAll(received);
                }
            }

            public void Send(Message m, uint id)
            {
                Assert.IsTrue(Session.Send(m, id));
            }

            public Message Next(uint expectedId)
            {
                DateTime until = DateTime.UtcNow.AddSeconds(10);
                lock (received)
                {
                    while (received.Count == 0)
                    {
                        TimeSpan left = until - DateTime.UtcNow;
                        Assert.Greater(left.TotalMilliseconds, 0, "timed out waiting for a reply");
                        Monitor.Wait(received, left);
                    }
                    KeyValuePair<uint, Message> pair = received.Dequeue();
                    Assert.AreEqual(expectedId, pair.Key);
                    return pair.Value;
                }
            }

            public ErrorCode NextError(uint id)
            {
                ErrorMessage error = Next(id) as ErrorMessage;
                Assert.IsNotNull(error, "expected an Error");
                return error.Code;
            }
        }

        private string root;
        private Host host;
        private SessionListener listener;
        private FileClient client;
        private readonly List<RawSession> raws = new List<RawSession>();

        [SetUp]
        public void Start()
        {
            root = Path.Combine(Path.GetTempPath(), "ezlink-files-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            host = new Host();
            listener = new SessionListener(IPAddress.Loopback, Mask, Port, ServerId, host);
            listener.Start();
            HelloReplyMessage reply;
            client = FileClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, Port), ClientId, "CLIENT", "1.0.0", out reply);
            Assert.IsNotNull(client);
        }

        [TearDown]
        public void Stop()
        {
            client.Close();
            foreach (RawSession raw in raws)
            {
                raw.Session.Close("test over");
            }
            listener.Stop();
            Thread.Sleep(50);
            RemoveTree(new DirectoryInfo(root));
        }

        /// <summary>Deletes a folder, removing links without following them (Directory.Delete would).</summary>
        private static void RemoveTree(DirectoryInfo folder)
        {
            if ((folder.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                folder.Delete(false);
                return;
            }
            foreach (FileSystemInfo item in folder.GetFileSystemInfos())
            {
                if ((item.Attributes & FileAttributes.Directory) != 0)
                {
                    RemoveTree((DirectoryInfo)item);
                }
                else
                {
                    item.Attributes = FileAttributes.Normal;
                    item.Delete();
                }
            }
            folder.Delete(false);
        }

        private RawSession Raw()
        {
            RawSession raw = new RawSession();
            raws.Add(raw);
            return raw;
        }

        private string P(string relative)
        {
            return Path.Combine(root, relative);
        }

        private string MakeFile(string relative, string text)
        {
            string path = P(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, Encoding.ASCII);
            return path;
        }

        private static byte[] Sha(byte[] data)
        {
            return SHA256.Create().ComputeHash(data);
        }

        private static void AssertRemoteError(ErrorCode expected, TestDelegate action)
        {
            RemoteException ex = (RemoteException)Assert.Throws(typeof(RemoteException), action);
            Assert.AreEqual(expected, ex.Code, ex.Message);
        }

        private byte[] Download(string path, long offset)
        {
            MemoryStream buffer = new MemoryStream();
            client.ReadFile(path, offset, buffer, null);
            return buffer.ToArray();
        }

        private void Upload(string path, byte[] data, WriteMode mode)
        {
            client.WriteFile(path, new MemoryStream(data), data.Length, DateTime.UtcNow, DateTime.UtcNow, 0, mode, null);
        }

        // ---- browsing ----

        [Test]
        public void RootsListFixedDrivesWithSizes()
        {
            List<DriveEntry> drives = client.GetRoots();
            Assert.Greater(drives.Count, 0);
            string systemDrive = Path.GetPathRoot(Environment.SystemDirectory).Substring(0, 1);
            DriveEntry system = drives.Find(delegate(DriveEntry d) { return d.Letter.ToString() == systemDrive.ToUpperInvariant(); });
            Assert.IsNotNull(system, "the system drive is listed");
            Assert.AreEqual((byte)DriveType.Fixed, system.DriveType);
            Assert.Greater(system.TotalBytes, 0);
            foreach (DriveEntry d in drives)
            {
                Assert.IsTrue(d.DriveType == (byte)DriveType.Fixed || d.DriveType == (byte)DriveType.Removable,
                    "only fixed and removable drives, got " + d.Letter + " " + d.DriveType);
            }
        }

        [Test]
        public void ListDirReturnsNamesAttributesSizesAndTimes()
        {
            string file = MakeFile("a.txt", "12345");
            File.SetLastWriteTimeUtc(file, new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc));
            Directory.CreateDirectory(P("sub"));

            List<FileEntry> entries = client.ListDir(root, false);
            Assert.AreEqual(2, entries.Count);
            FileEntry f = entries.Find(delegate(FileEntry e) { return e.Name == "a.txt"; });
            FileEntry d = entries.Find(delegate(FileEntry e) { return e.Name == "sub"; });
            Assert.AreEqual(5, f.Size);
            Assert.AreEqual(new DateTime(2020, 5, 6, 7, 8, 9, DateTimeKind.Utc), f.Modified);
            Assert.AreEqual(0, f.Attributes & (uint)FileAttributes.Directory);
            Assert.AreNotEqual(0, d.Attributes & (uint)FileAttributes.Directory);
            Assert.AreEqual(0, d.Size);
        }

        [Test]
        public void ListDirCanReturnFoldersOnly()
        {
            MakeFile("a.txt", "x");
            Directory.CreateDirectory(P("one"));
            Directory.CreateDirectory(P("two"));
            List<FileEntry> entries = client.ListDir(root, true);
            Assert.AreEqual(2, entries.Count);
            foreach (FileEntry e in entries)
            {
                Assert.AreNotEqual(0, e.Attributes & (uint)FileAttributes.Directory);
            }
        }

        [Test]
        public void AHugeFolderArrivesInSeveralBatches()
        {
            Directory.CreateDirectory(P("big"));
            for (int i = 0; i < 1800; i++)
            {
                File.WriteAllText(P("big\\file-with-a-fairly-long-name-" + i + ".txt"), "x");
            }
            List<FileEntry> entries = client.ListDir(P("big"), false);
            Assert.AreEqual(1800, entries.Count);
        }

        [Test]
        public void ListDirOfAMissingFolderOrAFileIsNotFound()
        {
            MakeFile("a.txt", "x");
            AssertRemoteError(ErrorCode.NotFound, delegate { client.ListDir(P("nope"), false); });
            AssertRemoteError(ErrorCode.NotFound, delegate { client.ListDir(P("a.txt"), false); });
        }

        [Test]
        public void ListTreeIsRecursiveWithRelativePathsAndParentsFirst()
        {
            MakeFile("top.txt", "1");
            MakeFile("a\\b\\deep.txt", "22");
            MakeFile("a\\mid.txt", "333");
            Directory.CreateDirectory(P("empty"));

            List<FileEntry> tree = client.ListTree(root);
            List<string> names = new List<string>();
            foreach (FileEntry e in tree)
            {
                names.Add(e.Name);
            }
            CollectionAssert.AreEquivalent(
                new string[] { "top.txt", "a", "a\\b", "a\\b\\deep.txt", "a\\mid.txt", "empty" }, names);
            Assert.Less(names.IndexOf("a"), names.IndexOf("a\\b"));
            Assert.Less(names.IndexOf("a\\b"), names.IndexOf("a\\b\\deep.txt"));
            Assert.AreEqual(2, tree.Find(delegate(FileEntry e) { return e.Name == "a\\b\\deep.txt"; }).Size);
        }

        [Test]
        public void ListTreeOfAFolderWithManyEntriesIsComplete()
        {
            for (int i = 0; i < 40; i++)
            {
                for (int j = 0; j < 30; j++)
                {
                    MakeFile("d" + i + "\\f" + j + ".txt", "x");
                }
            }
            Assert.AreEqual(40 + 40 * 30, client.ListTree(root).Count);
        }

        [Test]
        public void ListTreeDoesNotFollowFolderLinks()
        {
            MakeFile("real\\inside.txt", "x");
            Directory.CreateDirectory(P("holder"));
            if (!TryMakeJunction(P("holder\\link"), P("real")))
            {
                Assert.Ignore("junctions are not available here");
            }
            List<string> names = new List<string>();
            foreach (FileEntry e in client.ListTree(P("holder")))
            {
                names.Add(e.Name);
            }
            CollectionAssert.AreEqual(new string[] { "link" }, names, "the link is listed, not entered");
        }

        private static bool TryMakeJunction(string link, string target)
        {
            ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"");
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardOutput = true;
            using (Process p = Process.Start(info))
            {
                p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
        }

        [Test]
        public void StatDescribesFilesFoldersAndMissingPaths()
        {
            string file = MakeFile("a.txt", "abc");
            Directory.CreateDirectory(P("sub"));

            StatResultReply f = client.Stat(file);
            Assert.IsTrue(f.Exists);
            Assert.AreEqual(3, f.Size);
            StatResultReply d = client.Stat(P("sub"));
            Assert.IsTrue(d.Exists);
            Assert.AreNotEqual(0, d.Attributes & (uint)FileAttributes.Directory);
            Assert.IsFalse(client.Stat(P("missing.txt")).Exists);
        }

        // ---- reading ----

        [Test]
        public void ReadFileReturnsTheContentAndVerifiesItsHash()
        {
            byte[] data = new byte[300000]; // several chunks and a short last one
            new Random(1).NextBytes(data);
            File.WriteAllBytes(P("data.bin"), data);

            MemoryStream sink = new MemoryStream();
            long lastProgress = 0;
            byte[] hash = client.ReadFile(P("data.bin"), 0, sink, delegate(long done) { lastProgress = done; });
            CollectionAssert.AreEqual(data, sink.ToArray());
            CollectionAssert.AreEqual(Sha(data), hash);
            Assert.AreEqual(data.Length, lastProgress);
        }

        [Test]
        public void ReadFileCanResumeFromAnOffset()
        {
            byte[] data = new byte[200000];
            new Random(2).NextBytes(data);
            File.WriteAllBytes(P("data.bin"), data);

            byte[] tail = Download(P("data.bin"), 150000);
            Assert.AreEqual(50000, tail.Length);
            for (int i = 0; i < tail.Length; i++)
            {
                if (tail[i] != data[150000 + i])
                {
                    Assert.Fail("byte " + i + " differs");
                }
            }
            Assert.AreEqual(0, Download(P("data.bin"), data.Length).Length, "an offset at the end is an empty read");
        }

        [Test]
        public void ReadingAnEmptyFileWorks()
        {
            File.WriteAllBytes(P("empty.bin"), new byte[0]);
            Assert.AreEqual(0, Download(P("empty.bin"), 0).Length);
        }

        [Test]
        public void ReadFileErrors()
        {
            MakeFile("a.txt", "abc");
            Directory.CreateDirectory(P("folder"));
            AssertRemoteError(ErrorCode.NotFound, delegate { Download(P("missing.bin"), 0); });
            AssertRemoteError(ErrorCode.IoError, delegate { Download(P("a.txt"), 4); });
            AssertRemoteError(ErrorCode.IoError, delegate { Download(P("folder"), 0); });
        }

        [Test]
        public void AFileOpenForWritingByAnotherProgramCanStillBeRead()
        {
            string path = MakeFile("log.txt", "hello");
            using (FileStream writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                Assert.AreEqual(5, Download(path, 0).Length);
            }
        }

        [Test]
        public void TheSessionStaysUsableAfterAnError()
        {
            AssertRemoteError(ErrorCode.NotFound, delegate { Download(P("missing.bin"), 0); });
            MakeFile("a.txt", "abc");
            Assert.AreEqual(3, Download(P("a.txt"), 0).Length);
        }

        // ---- writing ----

        [Test]
        public void WriteFileCreatesTheFileWithContentTimesAndAttributes()
        {
            byte[] data = new byte[250000];
            new Random(3).NextBytes(data);
            DateTime modified = new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc);
            DateTime created = new DateTime(2018, 1, 2, 3, 4, 5, DateTimeKind.Utc);

            client.WriteFile(P("up.bin"), new MemoryStream(data), data.Length, created, modified,
                (uint)(FileAttributes.Hidden | FileAttributes.Directory), WriteMode.FailIfExists, null);

            CollectionAssert.AreEqual(data, File.ReadAllBytes(P("up.bin")));
            Assert.AreEqual(modified, File.GetLastWriteTimeUtc(P("up.bin")));
            Assert.AreEqual(created, File.GetCreationTimeUtc(P("up.bin")));
            FileAttributes attributes = File.GetAttributes(P("up.bin"));
            Assert.AreNotEqual(0, attributes & FileAttributes.Hidden);
            Assert.AreEqual(0, (int)(attributes & FileAttributes.Directory), "only the plain file attributes are applied");
            Assert.IsFalse(File.Exists(P("up.bin") + FileServer.PartSuffix), "no part file is left");
            File.SetAttributes(P("up.bin"), FileAttributes.Normal);
        }

        [Test]
        public void WriteFileCreatesMissingParentFolders()
        {
            Upload(P("x\\y\\z\\file.txt"), new byte[] { 1, 2, 3 }, WriteMode.FailIfExists);
            Assert.AreEqual(3, File.ReadAllBytes(P("x\\y\\z\\file.txt")).Length);
        }

        [Test]
        public void WritingAnEmptyFileWorks()
        {
            Upload(P("empty.txt"), new byte[0], WriteMode.FailIfExists);
            Assert.AreEqual(0, new FileInfo(P("empty.txt")).Length);
        }

        [Test]
        public void FailIfExistsLeavesTheExistingFileAlone()
        {
            MakeFile("a.txt", "original");
            AssertRemoteError(ErrorCode.Exists, delegate { Upload(P("a.txt"), new byte[] { 9 }, WriteMode.FailIfExists); });
            Assert.AreEqual("original", File.ReadAllText(P("a.txt")));
            Assert.IsFalse(File.Exists(P("a.txt") + FileServer.PartSuffix));
        }

        [Test]
        public void OverwriteReplacesEvenAReadOnlyFile()
        {
            string path = MakeFile("a.txt", "original");
            File.SetAttributes(path, FileAttributes.ReadOnly);
            Upload(path, Encoding.ASCII.GetBytes("replacement"), WriteMode.Overwrite);
            Assert.AreEqual("replacement", File.ReadAllText(path));
        }

        [Test]
        public void ANameTakenByAFolderCannotBeWritten()
        {
            Directory.CreateDirectory(P("folder"));
            AssertRemoteError(ErrorCode.Exists, delegate { Upload(P("folder"), new byte[1], WriteMode.Overwrite); });
        }

        [Test]
        public void ASecondUploadAfterAFailedOneWorks()
        {
            MakeFile("a.txt", "x");
            AssertRemoteError(ErrorCode.Exists, delegate { Upload(P("a.txt"), new byte[1], WriteMode.FailIfExists); });
            Upload(P("b.txt"), new byte[] { 1, 2 }, WriteMode.FailIfExists);
            Assert.AreEqual(2, new FileInfo(P("b.txt")).Length);
        }

        [Test]
        public void AWrongHashRejectsTheUploadAndLeavesNothingBehind()
        {
            RawSession raw = Raw();
            byte[] data = Encoding.ASCII.GetBytes("hello");
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("bad.txt");
            begin.Size = data.Length;
            raw.Send(begin, 10);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(10));

            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = data;
            raw.Send(chunk, 10);
            WriteEndRequest end = new WriteEndRequest();
            end.Sha256 = new byte[32]; // not the hash of "hello"
            raw.Send(end, 10);

            Assert.AreEqual(ErrorCode.IoError, raw.NextError(10));
            Assert.IsFalse(File.Exists(P("bad.txt")));
            Assert.IsFalse(File.Exists(P("bad.txt") + FileServer.PartSuffix));
        }

        [Test]
        public void MoreDataThanAnnouncedIsRefusedAtOnce()
        {
            RawSession raw = Raw();
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("big.txt");
            begin.Size = 3;
            raw.Send(begin, 11);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(11));

            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = new byte[5];
            raw.Send(chunk, 11);
            Assert.AreEqual(ErrorCode.IoError, raw.NextError(11), "the sender is told as soon as it goes wrong");

            WriteEndRequest end = new WriteEndRequest();
            end.Sha256 = Sha(new byte[3]);
            raw.Send(end, 11);
            Assert.AreEqual(ErrorCode.IoError, raw.NextError(11));
            Assert.IsFalse(File.Exists(P("big.txt")));
            Assert.IsFalse(File.Exists(P("big.txt") + FileServer.PartSuffix));
        }

        [Test]
        public void ATooShortUploadIsRejected()
        {
            RawSession raw = Raw();
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("short.txt");
            begin.Size = 10;
            raw.Send(begin, 12);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(12));
            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = new byte[3];
            raw.Send(chunk, 12);
            WriteEndRequest end = new WriteEndRequest();
            end.Sha256 = Sha(new byte[3]);
            raw.Send(end, 12);

            Assert.AreEqual(ErrorCode.IoError, raw.NextError(12));
            Assert.IsFalse(File.Exists(P("short.txt")));
            Assert.IsFalse(File.Exists(P("short.txt") + FileServer.PartSuffix));
        }

        [Test]
        public void WriteEndWithoutAnUploadIsAnError()
        {
            RawSession raw = Raw();
            WriteEndRequest end = new WriteEndRequest();
            raw.Send(end, 13);
            Assert.AreEqual(ErrorCode.IoError, raw.NextError(13));
        }

        [Test]
        public void CancellingAnUploadDeletesThePartFile()
        {
            RawSession raw = Raw();
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("cancel.bin");
            begin.Size = 100;
            raw.Send(begin, 14);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(14));
            Assert.IsTrue(File.Exists(P("cancel.bin") + FileServer.PartSuffix), "the part file exists while uploading");

            raw.Send(new CancelMessage(14), 0);
            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = new byte[10];
            raw.Send(chunk, 14);

            Assert.AreEqual(ErrorCode.Cancelled, raw.NextError(14));
            Assert.IsFalse(File.Exists(P("cancel.bin") + FileServer.PartSuffix));
            Assert.IsFalse(File.Exists(P("cancel.bin")));
        }

        [Test]
        public void ADroppedConnectionDuringAnUploadDeletesThePartFile()
        {
            RawSession raw = Raw();
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("drop.bin");
            begin.Size = 100;
            raw.Send(begin, 15);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(15));
            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = new byte[10];
            raw.Send(chunk, 15);

            raw.Session.Close("cable pulled");
            DateTime until = DateTime.UtcNow.AddSeconds(5);
            while (File.Exists(P("drop.bin") + FileServer.PartSuffix) && DateTime.UtcNow < until)
            {
                Thread.Sleep(20);
            }
            Assert.IsFalse(File.Exists(P("drop.bin") + FileServer.PartSuffix));
            Assert.IsFalse(File.Exists(P("drop.bin")));
        }

        // ---- cancelling big transfers through the client ----

        private string MakeBigFile(string relative, int megabytes)
        {
            string path = P(relative);
            byte[] block = new byte[1024 * 1024];
            new Random(4).NextBytes(block);
            using (FileStream f = File.Create(path))
            {
                for (int i = 0; i < megabytes; i++)
                {
                    f.Write(block, 0, block.Length);
                }
            }
            return path;
        }

        [Test]
        public void CancellingADownloadStopsItAndTheSessionRecovers()
        {
            string big = MakeBigFile("big.bin", 300);
            MemoryStream sink = new MemoryStream();
            bool cancelled = false;
            AssertRemoteError(ErrorCode.Cancelled, delegate
            {
                client.ReadFile(big, 0, sink, delegate(long done)
                {
                    if (!cancelled)
                    {
                        cancelled = true;
                        client.Cancel();
                    }
                });
            });
            Assert.Less(sink.Length, 300L * 1024 * 1024, "it stopped early");

            MakeFile("small.txt", "abc");
            Assert.AreEqual(3, Download(P("small.txt"), 0).Length, "the session keeps working");
        }

        [Test]
        public void CancellingAnUploadStopsItAndLeavesNothingBehind()
        {
            string source = MakeBigFile("source.bin", 300);
            bool cancelled = false;
            using (FileStream input = File.OpenRead(source))
            {
                AssertRemoteError(ErrorCode.Cancelled, delegate
                {
                    client.WriteFile(P("target.bin"), input, input.Length, DateTime.UtcNow, DateTime.UtcNow, 0,
                        WriteMode.FailIfExists, delegate(long sent)
                        {
                            if (!cancelled)
                            {
                                cancelled = true;
                                client.Cancel();
                            }
                        });
                });
            }
            Thread.Sleep(200);
            Assert.IsFalse(File.Exists(P("target.bin")));
            Assert.IsFalse(File.Exists(P("target.bin") + FileServer.PartSuffix));

            Upload(P("after.txt"), new byte[] { 1 }, WriteMode.FailIfExists);
            Assert.IsTrue(File.Exists(P("after.txt")), "the session keeps working");
        }

        // ---- file operations ----

        [Test]
        public void CreateDirMakesNestedFoldersAndIsRepeatable()
        {
            client.CreateDir(P("a\\b\\c"));
            Assert.IsTrue(Directory.Exists(P("a\\b\\c")));
            client.CreateDir(P("a\\b\\c"));
            MakeFile("file", "x");
            AssertRemoteError(ErrorCode.Exists, delegate { client.CreateDir(P("file")); });
        }

        [Test]
        public void DeleteRemovesFilesAndWholeFolders()
        {
            string readOnly = MakeFile("tree\\ro.txt", "x");
            File.SetAttributes(readOnly, FileAttributes.ReadOnly);
            MakeFile("tree\\sub\\deep.txt", "x");
            MakeFile("single.txt", "x");

            client.Delete(P("single.txt"));
            client.Delete(P("tree"));
            Assert.IsFalse(File.Exists(P("single.txt")));
            Assert.IsFalse(Directory.Exists(P("tree")));
            AssertRemoteError(ErrorCode.NotFound, delegate { client.Delete(P("tree")); });
        }

        [Test]
        public void DeletingAFolderLinkLeavesItsTargetAlone()
        {
            MakeFile("real\\keep.txt", "precious");
            Directory.CreateDirectory(P("holder"));
            if (!TryMakeJunction(P("holder\\link"), P("real")))
            {
                Assert.Ignore("junctions are not available here");
            }
            client.Delete(P("holder"));
            Assert.IsFalse(Directory.Exists(P("holder")));
            Assert.AreEqual("precious", File.ReadAllText(P("real\\keep.txt")));
        }

        [Test]
        public void ADriveRootCannotBeDeletedOrRenamed()
        {
            string drive = Path.GetPathRoot(root);
            AssertRemoteError(ErrorCode.AccessDenied, delegate { client.Delete(drive); });
            AssertRemoteError(ErrorCode.AccessDenied, delegate { client.Rename(drive, P("x")); });
        }

        [Test]
        public void RenameMovesFilesAndFolders()
        {
            MakeFile("old.txt", "x");
            MakeFile("dir\\inner.txt", "y");
            client.Rename(P("old.txt"), P("new.txt"));
            client.Rename(P("dir"), P("dir2"));
            Assert.IsTrue(File.Exists(P("new.txt")));
            Assert.IsFalse(File.Exists(P("old.txt")));
            Assert.IsTrue(File.Exists(P("dir2\\inner.txt")));
        }

        [Test]
        public void RenameRefusesToReplaceAndReportsMissing()
        {
            MakeFile("a.txt", "a");
            MakeFile("b.txt", "b");
            AssertRemoteError(ErrorCode.Exists, delegate { client.Rename(P("a.txt"), P("b.txt")); });
            AssertRemoteError(ErrorCode.NotFound, delegate { client.Rename(P("zzz"), P("yyy")); });
            Assert.AreEqual("a", File.ReadAllText(P("a.txt")));
        }

        [Test]
        public void RenameCanChangeOnlyTheCase()
        {
            MakeFile("name.txt", "x");
            client.Rename(P("name.txt"), P("NAME.txt"));
            string[] files = Directory.GetFiles(root);
            Assert.AreEqual("NAME.txt", Path.GetFileName(files[0]));
        }

        // ---- paths ----

        [Test]
        public void PathsThatEscapeOrAreNotPlainDrivePathsAreRefused()
        {
            string[] bad = new string[]
            {
                string.Empty,
                "..\\..\\Windows",
                "Windows\\System32",
                "C:Windows",
                "C:",
                "\\Windows",
                "\\\\?\\C:\\Windows",
                "\\\\?\\GLOBALROOT\\Device\\HarddiskVolume1",
                "\\\\.\\PhysicalDrive0",
                "\\\\localhost\\c$\\Windows",
                "//server/share/file",
                "C:\\temp\\nul",
                "C:\\temp\\COM1.txt",
                "C:\\temp\\file.txt:hidden",
                "C:\\temp\\bad\0name",
                "Z:\\definitely\\not\\a\\drive\\unless\\you\\have\\one\\" + new string('x', 400)
            };
            foreach (string path in bad)
            {
                string shown = path.Replace("\0", "\\0");
                AssertRemoteError(ErrorCode.AccessDenied, delegate { client.Stat(path); });
                AssertRemoteError(ErrorCode.AccessDenied, delegate { client.ListDir(path, false); });
                AssertRemoteError(ErrorCode.AccessDenied, delegate { Download(path, 0); });
                Assert.IsNotNull(shown);
            }
        }

        [Test]
        public void DotDotInsideAFullPathIsResolvedBeforeUse()
        {
            MakeFile("a.txt", "abc");
            // root\sub\..\a.txt really is root\a.txt: normalization, not rejection.
            Assert.AreEqual(3, Download(P("sub\\..\\a.txt"), 0).Length);
            Assert.AreEqual(3, Download(root.Replace('\\', '/') + "/a.txt", 0).Length, "forward slashes are fine");
        }

        [Test]
        public void ANetworkOrOpticalDriveIsNotReachable()
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.CDRom || drive.DriveType == DriveType.Network)
                {
                    string path = drive.Name;
                    AssertRemoteError(ErrorCode.AccessDenied, delegate { client.ListDir(path, false); });
                    return;
                }
            }
            Assert.Ignore("no optical or network drive on this machine");
        }

        // ---- permissions ----

        [Test]
        public void BrowseOnlyCannotReadOrWrite()
        {
            MakeFile("a.txt", "abc");
            host.Granted = Permissions.Browse;

            Assert.AreEqual(1, client.ListDir(root, false).Count);
            Assert.IsTrue(client.Stat(P("a.txt")).Exists);
            Assert.Greater(client.GetRoots().Count, 0);

            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { Download(P("a.txt"), 0); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.ListTree(root); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { Upload(P("n.txt"), new byte[1], WriteMode.Overwrite); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.CreateDir(P("d")); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.Delete(P("a.txt")); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.Rename(P("a.txt"), P("b.txt")); });
            Assert.IsTrue(File.Exists(P("a.txt")));
            Assert.IsFalse(File.Exists(P("n.txt")));
            Assert.IsFalse(Directory.Exists(P("d")));
        }

        [Test]
        public void WithoutAnyPermissionNothingWorks()
        {
            host.Granted = Permissions.None;
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.GetRoots(); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.ListDir(root, false); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.Stat(root); });
        }

        [Test]
        public void ReadOnlyCannotChangeAnything()
        {
            MakeFile("a.txt", "abc");
            host.Granted = Permissions.Browse | Permissions.Read;
            Assert.AreEqual(3, Download(P("a.txt"), 0).Length);
            Assert.AreEqual(1, client.ListTree(root).Count);
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { Upload(P("n.txt"), new byte[1], WriteMode.Overwrite); });
        }

        [Test]
        public void WriteOnlyCanUploadAndCheckForConflictsButNotBrowseOrRead()
        {
            MakeFile("a.txt", "abc");
            host.Granted = Permissions.Write;
            Upload(P("new.txt"), new byte[] { 1, 2 }, WriteMode.FailIfExists);
            Assert.IsTrue(client.Stat(P("a.txt")).Exists, "the conflict check works");
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { client.ListDir(root, false); });
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { Download(P("a.txt"), 0); });
        }

        [Test]
        public void RevokingAPermissionTakesEffectOnTheNextRequest()
        {
            MakeFile("a.txt", "abc");
            Assert.AreEqual(3, Download(P("a.txt"), 0).Length);
            host.Granted = Permissions.Browse;
            AssertRemoteError(ErrorCode.PermissionNotGranted, delegate { Download(P("a.txt"), 0); });
        }

        [Test]
        public void RevokingWriteMidUploadAbortsIt()
        {
            RawSession raw = Raw();
            WriteBeginRequest begin = new WriteBeginRequest();
            begin.Path = P("revoked.bin");
            begin.Size = 100;
            raw.Send(begin, 16);
            Assert.IsInstanceOf(typeof(OkMessage), raw.Next(16));

            host.Granted = Permissions.Browse | Permissions.Read;
            WriteDataRequest chunk = new WriteDataRequest();
            chunk.Data = new byte[10];
            raw.Send(chunk, 16);

            Assert.AreEqual(ErrorCode.PermissionNotGranted, raw.NextError(16));
            Assert.IsFalse(File.Exists(P("revoked.bin") + FileServer.PartSuffix));
        }

        // ---- protocol behaviour ----

        [Test]
        public void ARepliesOnlyCommandFromThePeerIsUnsupported()
        {
            RawSession raw = Raw();
            raw.Send(new TreeEndReply(), 20);
            UnsupportedMessage u = raw.Next(20) as UnsupportedMessage;
            Assert.IsNotNull(u);
            Assert.AreEqual(FileCommands.TreeEnd, u.UnsupportedCommand);
        }

        [Test]
        public void RequestsInOneSessionAreAnsweredInOrder()
        {
            MakeFile("a.txt", "abc");
            RawSession raw = Raw();
            raw.Send(new StatRequest(P("a.txt")), 31);
            raw.Send(new StatRequest(P("missing")), 32);
            raw.Send(new GetRootsRequest(), 33);
            Assert.IsTrue(((StatResultReply)raw.Next(31)).Exists);
            Assert.IsFalse(((StatResultReply)raw.Next(32)).Exists);
            Assert.IsInstanceOf(typeof(RootsReply), raw.Next(33));
        }

        // ---- the big one (manual) ----

        [Test, Explicit("Copies a 5 GB file both ways over loopback; takes minutes and 10 GB of disk")]
        public void FiveGigabytesBothWaysKeepTheirHash()
        {
            const long size = 5L * 1024 * 1024 * 1024;
            string source = P("five.bin");
            byte[] block = new byte[1024 * 1024];
            new Random(5).NextBytes(block);
            byte[] expected;
            using (HashAlgorithm hash = SHA256.Create())
            using (FileStream f = File.Create(source))
            {
                for (long written = 0; written < size; written += block.Length)
                {
                    f.Write(block, 0, block.Length);
                    hash.TransformBlock(block, 0, block.Length, null, 0);
                }
                hash.TransformFinalBlock(new byte[0], 0, 0);
                expected = hash.Hash;
            }

            Stopwatch clock = Stopwatch.StartNew();
            string downloaded = P("five-down.bin");
            using (FileStream sink = File.Create(downloaded))
            {
                CollectionAssert.AreEqual(expected, client.ReadFile(source, 0, sink, null));
            }
            Console.WriteLine("download: " + clock.Elapsed);
            Assert.AreEqual(size, new FileInfo(downloaded).Length);
            File.Delete(downloaded);

            clock = Stopwatch.StartNew();
            string uploaded = P("five-up.bin");
            using (FileStream input = File.OpenRead(source))
            {
                client.WriteFile(uploaded, input, size, DateTime.UtcNow, DateTime.UtcNow, 0, WriteMode.FailIfExists, null);
            }
            Console.WriteLine("upload: " + clock.Elapsed);
            Assert.AreEqual(size, new FileInfo(uploaded).Length);
            using (FileStream check = File.OpenRead(uploaded))
            using (HashAlgorithm hash = SHA256.Create())
            {
                CollectionAssert.AreEqual(expected, hash.ComputeHash(check));
            }
        }
    }
}
