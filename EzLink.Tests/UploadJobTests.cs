using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using EzLink;
using EzLink.Protocol;
using ezExplorer;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>The explorer's upload job against a real file server over loopback.</summary>
    [TestFixture]
    public class UploadJobTests
    {
        private const int Port = 49801;
        private static readonly Guid ServerId = new Guid("00000000-0000-0000-0000-0000000000c1");
        private static readonly Guid ClientId = new Guid("00000000-0000-0000-0000-0000000000d2");
        private static readonly IPAddress Mask = IPAddress.Parse("255.255.255.255");

        private sealed class Host : ISessionHost
        {
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
                new FileServer(session, delegate
                {
                    return Permissions.Browse | Permissions.Read | Permissions.Write;
                });
                return true;
            }
        }

        private sealed class Source : ITransferClientSource
        {
            public FileClient Client;

            public FileClient GetTransferClient()
            {
                return Client;
            }
        }

        private sealed class FakeHost : ITransferHost
        {
            public ConflictChoice Conflict = ConflictChoice.Skip;
            public ErrorChoice Error = ErrorChoice.Cancel;
            public volatile bool CancelNow;
            public readonly List<string> Conflicts = new List<string>();
            public readonly List<string> Errors = new List<string>();

            public bool Cancelled
            {
                get { return CancelNow; }
            }

            public ConflictChoice AskConflict(string name, long existingSize, DateTime existingModified,
                long newSize, DateTime newModified)
            {
                Conflicts.Add(name);
                return Conflict;
            }

            public ErrorChoice AskError(string name, string message)
            {
                Errors.Add(name);
                return Error;
            }
        }

        private string local;
        private string remote;
        private SessionListener listener;
        private FileClient client;
        private Source source;

        [SetUp]
        public void Start()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "ezlink-upload-" + Guid.NewGuid().ToString("N"));
            local = Path.Combine(baseDir, "local");
            remote = Path.Combine(baseDir, "remote");
            Directory.CreateDirectory(local);
            Directory.CreateDirectory(remote);

            listener = new SessionListener(IPAddress.Loopback, Mask, Port, ServerId, new Host());
            listener.Start();
            HelloReplyMessage reply;
            client = FileClient.Connect(IPAddress.Loopback, new IPEndPoint(IPAddress.Loopback, Port), ClientId,
                "CLIENT", "1.0.0", out reply);
            Assert.IsNotNull(client);
            source = new Source();
            source.Client = client;
        }

        [TearDown]
        public void Stop()
        {
            client.Close();
            listener.Stop();
            Thread.Sleep(50);
            try
            {
                Directory.Delete(Path.GetDirectoryName(local), true);
            }
            catch (IOException)
            {
            }
        }

        private TransferOutcome Upload(FakeHost host, params string[] sources)
        {
            UploadJob job = new UploadJob(source, sources, remote);
            return job.Run(new TransferState(), host);
        }

        private static string Join(string a, string b, string c)
        {
            return Path.Combine(Path.Combine(a, b), c);
        }

        private static byte[] Hash(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            {
                return SHA256.Create().ComputeHash(stream);
            }
        }

        private static void Write(string path, string text, DateTime modified)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, Encoding.ASCII);
            File.SetCreationTimeUtc(path, modified.AddDays(-3));
            File.SetLastWriteTimeUtc(path, modified);
        }

        [Test]
        public void ANestedFolderOf1000FilesIsRecreatedExactly()
        {
            string tree = Path.Combine(local, "tree");
            Directory.CreateDirectory(tree);
            Random random = new Random(42);
            for (int i = 0; i < 1000; i++)
            {
                string folder = tree;
                int depth = i % 4;
                for (int level = 0; level < depth; level++)
                {
                    folder = Path.Combine(folder, "d" + level + "_" + (i % 5));
                }
                byte[] data = new byte[random.Next(0, 3000)];
                random.NextBytes(data);
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, "f" + i + ".bin");
                File.WriteAllBytes(path, data);
                File.SetCreationTimeUtc(path, new DateTime(2019, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i));
                File.SetLastWriteTimeUtc(path, new DateTime(2020, 6, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i));
            }
            Directory.CreateDirectory(Path.Combine(tree, "empty"));
            // Folder times last: creating the files above changed them.
            DateTime stamp = new DateTime(2018, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            foreach (string dir in Directory.GetDirectories(tree, "*", SearchOption.AllDirectories))
            {
                Directory.SetCreationTimeUtc(dir, stamp);
                Directory.SetLastWriteTimeUtc(dir, stamp.AddHours(1));
            }
            Directory.SetCreationTimeUtc(tree, stamp);
            Directory.SetLastWriteTimeUtc(tree, stamp.AddHours(1));

            FakeHost host = new FakeHost();
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, tree));
            Assert.AreEqual(0, host.Conflicts.Count);
            Assert.AreEqual(0, host.Errors.Count);

            string copy = Path.Combine(remote, "tree");
            string[] sourceFiles = Directory.GetFiles(tree, "*", SearchOption.AllDirectories);
            string[] copyFiles = Directory.GetFiles(copy, "*", SearchOption.AllDirectories);
            Assert.AreEqual(1000, sourceFiles.Length);
            Assert.AreEqual(1000, copyFiles.Length);
            foreach (string file in sourceFiles)
            {
                string relative = file.Substring(tree.Length);
                string target = copy + relative;
                Assert.IsTrue(File.Exists(target), relative);
                Assert.AreEqual(Hash(file), Hash(target), relative);
                Assert.AreEqual(File.GetLastWriteTimeUtc(file), File.GetLastWriteTimeUtc(target), relative);
                Assert.AreEqual(File.GetCreationTimeUtc(file), File.GetCreationTimeUtc(target), relative);
            }

            List<string> sourceDirs = new List<string>(Directory.GetDirectories(tree, "*", SearchOption.AllDirectories));
            sourceDirs.Add(tree);
            Assert.AreEqual(sourceDirs.Count, Directory.GetDirectories(copy, "*", SearchOption.AllDirectories).Length + 1);
            foreach (string dir in sourceDirs)
            {
                string target = copy + dir.Substring(tree.Length);
                Assert.IsTrue(Directory.Exists(target), target);
                Assert.AreEqual(Directory.GetLastWriteTimeUtc(dir), Directory.GetLastWriteTimeUtc(target), target);
                Assert.AreEqual(Directory.GetCreationTimeUtc(dir), Directory.GetCreationTimeUtc(target), target);
            }
            Assert.IsTrue(Directory.Exists(Path.Combine(copy, "empty")));
            Assert.AreEqual(0, Directory.GetFiles(remote, "*.ezlink-part", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void ReplaceOverwritesAndSkipKeepsTheExistingFile()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string file = Path.Combine(local, "a.txt");
            Write(file, "new", when);
            string existing = Path.Combine(remote, "a.txt");
            Write(existing, "old", when);

            FakeHost host = new FakeHost();
            host.Conflict = ConflictChoice.Skip;
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, file));
            Assert.AreEqual("old", File.ReadAllText(existing));
            CollectionAssert.AreEqual(new string[] { "a.txt" }, host.Conflicts);

            host.Conflict = ConflictChoice.Replace;
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, file));
            Assert.AreEqual("new", File.ReadAllText(existing));
        }

        [Test]
        public void KeepBothPicksTheFirstFreeName()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            string file = Path.Combine(local, "a.txt");
            Write(file, "new", when);
            Write(Path.Combine(remote, "a.txt"), "old", when);
            Write(Path.Combine(remote, "a (2).txt"), "older", when);

            FakeHost host = new FakeHost();
            host.Conflict = ConflictChoice.KeepBoth;
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, file));
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(remote, "a.txt")));
            Assert.AreEqual("older", File.ReadAllText(Path.Combine(remote, "a (2).txt")));
            Assert.AreEqual("new", File.ReadAllText(Path.Combine(remote, "a (3).txt")));
        }

        [Test]
        public void DroppingOnAnExistingFolderMergesIntoIt()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Write(Join(local, "dir", "new.txt"), "n", when);
            Write(Join(remote, "dir", "kept.txt"), "k", when);

            FakeHost host = new FakeHost();
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, Path.Combine(local, "dir")));
            Assert.AreEqual("n", File.ReadAllText(Join(remote, "dir", "new.txt")));
            Assert.AreEqual("k", File.ReadAllText(Join(remote, "dir", "kept.txt")));
            Assert.AreEqual(0, host.Conflicts.Count);
        }

        [Test]
        public void CancellingStopsTheJobAndLeavesNoPartFile()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 20; i++)
            {
                Write(Join(local, "many", "f" + i + ".txt"), "x", when);
            }
            FakeHost host = new FakeHost();
            host.CancelNow = true;
            Assert.AreEqual(TransferOutcome.Cancelled, Upload(host, Path.Combine(local, "many")));
            Assert.AreEqual(0, Directory.GetFiles(remote, "*", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void AFolderBlockedByAFileOffersSkipAndCopiesNothingBelowIt()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Write(Join(local, "thing", "inside.txt"), "x", when);
            Write(Path.Combine(remote, "thing"), "i am a file", when);

            FakeHost host = new FakeHost();
            host.Error = ErrorChoice.Skip;
            Assert.AreEqual(TransferOutcome.Completed, Upload(host, Path.Combine(local, "thing")));
            Assert.AreEqual(1, host.Errors.Count);
            Assert.AreEqual("i am a file", File.ReadAllText(Path.Combine(remote, "thing")));
        }

        [Test]
        public void AnErrorAnswerOfCancelEndsTheJob()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Write(Join(local, "thing", "inside.txt"), "x", when);
            Write(Path.Combine(remote, "thing"), "i am a file", when);

            FakeHost host = new FakeHost();
            host.Error = ErrorChoice.Cancel;
            Assert.AreEqual(TransferOutcome.Cancelled, Upload(host, Path.Combine(local, "thing")));
        }

        [Test]
        public void ALostConnectionIsReportedAsSuch()
        {
            DateTime when = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            Write(Path.Combine(local, "a.txt"), "x", when);
            client.Close();
            Thread.Sleep(100);
            FakeHost host = new FakeHost();
            Assert.AreEqual(TransferOutcome.ConnectionLost, Upload(host, Path.Combine(local, "a.txt")));
        }
    }
}
