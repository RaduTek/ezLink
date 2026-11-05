using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;
using ezExplorer;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>
    /// Downloading: the plan, the virtual-file data object with its streams (what Explorer pulls from), and the
    /// copy-to-folder job, all against a real file server over loopback.
    /// </summary>
    [TestFixture]
    public class DownloadTests
    {
        private const int Port = 49802;
        private static readonly Guid ServerId = new Guid("00000000-0000-0000-0000-0000000000c3");
        private static readonly Guid ClientId = new Guid("00000000-0000-0000-0000-0000000000d4");
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
                new FileServer(session, delegate { return Permissions.Browse | Permissions.Read; });
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
            public volatile bool CancelNow;
            public readonly List<string> Conflicts = new List<string>();

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
                return ErrorChoice.Cancel;
            }
        }

        private string remote;
        private string local;
        private SessionListener listener;
        private FileClient client;
        private Source source;

        [SetUp]
        public void Start()
        {
            string baseDir = Path.Combine(Path.GetTempPath(), "ezlink-download-" + Guid.NewGuid().ToString("N"));
            remote = Path.Combine(baseDir, "remote");
            local = Path.Combine(baseDir, "local");
            Directory.CreateDirectory(remote);
            Directory.CreateDirectory(local);

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
                Directory.Delete(Path.GetDirectoryName(remote), true);
            }
            catch (IOException)
            {
            }
        }

        // ---- helpers ----

        private static readonly DateTime Stamp = new DateTime(2020, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        private string WriteRemote(string relative, byte[] data, int minutes)
        {
            string path = Path.Combine(remote, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, data);
            File.SetCreationTimeUtc(path, Stamp.AddDays(-2).AddMinutes(minutes));
            File.SetLastWriteTimeUtc(path, Stamp.AddMinutes(minutes));
            return path;
        }

        private static byte[] RandomBytes(int length, int seed)
        {
            byte[] data = new byte[length];
            new Random(seed).NextBytes(data);
            return data;
        }

        private static RemoteItem ItemFor(string path)
        {
            bool folder = Directory.Exists(path);
            RemoteItem item = new RemoteItem();
            item.Name = Path.GetFileName(path);
            item.DisplayName = item.Name;
            item.Path = path;
            item.IsFolder = folder;
            item.Attributes = (uint)(folder ? FileAttributes.Directory : FileAttributes.Normal);
            return item;
        }

        private static List<RemoteItem> Items(params string[] paths)
        {
            List<RemoteItem> items = new List<RemoteItem>();
            foreach (string path in paths)
            {
                items.Add(ItemFor(path));
            }
            return items;
        }

        private DownloadPlan Plan(params string[] paths)
        {
            PrepareDownloadJob job = new PrepareDownloadJob(source, Items(paths));
            Assert.AreEqual(TransferOutcome.Completed, job.Run(new TransferState(), new FakeHost()));
            return job.Plan;
        }

        private static byte[] Hash(byte[] data)
        {
            return SHA256.Create().ComputeHash(data);
        }

        private DownloadOperation lastOperation;

        /// <summary>Builds the data object on another thread, so that reads from the test thread are not "UI thread" reads.</summary>
        private VirtualFileDataObject NewDataObject(DownloadPlan plan)
        {
            VirtualFileDataObject data = null;
            Thread builder = new Thread(delegate() { data = new VirtualFileDataObject(plan, source); });
            builder.Start();
            builder.Join();
            lastOperation = null;
            data.OperationStarted += delegate(DownloadOperation op) { lastOperation = op; };
            return data;
        }

        private static FORMATETC Format(string name, TYMED tymed, int index)
        {
            FORMATETC format = new FORMATETC();
            format.cfFormat = (short)DataFormats.GetFormat(name).Id;
            format.dwAspect = DVASPECT.DVASPECT_CONTENT;
            format.lindex = index;
            format.tymed = tymed;
            return format;
        }

        private static byte[] ReadAll(IStream stream)
        {
            IntPtr read = Marshal.AllocHGlobal(4);
            try
            {
                MemoryStream result = new MemoryStream();
                byte[] buffer = new byte[100000];
                while (true)
                {
                    stream.Read(buffer, buffer.Length, read);
                    int n = Marshal.ReadInt32(read);
                    if (n == 0)
                    {
                        return result.ToArray();
                    }
                    result.Write(buffer, 0, n);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(read);
            }
        }

        private static IStream StreamOf(System.Runtime.InteropServices.ComTypes.IDataObject data, int index)
        {
            FORMATETC format = Format("FileContents", TYMED.TYMED_ISTREAM, index);
            STGMEDIUM medium;
            data.GetData(ref format, out medium);
            Assert.AreEqual(TYMED.TYMED_ISTREAM, medium.tymed);
            return (IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
        }

        // ---- the plan ----

        [Test]
        public void ThePlanListsFoldersBeforeTheirContentsWithRelativeNames()
        {
            WriteRemote(@"tree\a.txt", RandomBytes(10, 1), 1);
            WriteRemote(@"tree\sub\b.bin", RandomBytes(2000, 2), 2);
            Directory.CreateDirectory(Path.Combine(remote, @"tree\empty"));
            string single = WriteRemote("single.dat", RandomBytes(77, 3), 3);

            DownloadPlan plan = Plan(Path.Combine(remote, "tree"), single);

            Assert.AreEqual(3, plan.FileCount);
            Assert.AreEqual(10 + 2000 + 77, plan.TotalBytes);
            List<string> names = new List<string>();
            foreach (DownloadEntry entry in plan.Entries)
            {
                names.Add(entry.Relative);
            }
            Assert.AreEqual("tree", names[0]);
            Assert.IsTrue(plan.Entries[0].IsFolder);
            CollectionAssert.Contains(names, @"tree\a.txt");
            CollectionAssert.Contains(names, @"tree\sub");
            CollectionAssert.Contains(names, @"tree\sub\b.bin");
            CollectionAssert.Contains(names, @"tree\empty");
            Assert.AreEqual("single.dat", names[names.Count - 1]);
            Assert.Less(names.IndexOf(@"tree\sub"), names.IndexOf(@"tree\sub\b.bin"));
        }

        [Test]
        public void PreparingAMissingItemFails()
        {
            PrepareDownloadJob job = new PrepareDownloadJob(source, Items(Path.Combine(remote, "nope.txt")));
            Assert.AreEqual(TransferOutcome.Failed, job.Run(new TransferState(), new FakeHost()));
        }

        // ---- the data object ----

        [Test]
        public void ItIsAsynchronousAndOffersTheDescriptorsAndTheDropEffect()
        {
            WriteRemote(@"tree\a.txt", RandomBytes(10, 1), 1);
            WriteRemote(@"tree\sub\b.bin", RandomBytes(70000, 2), 2);
            DownloadPlan plan = Plan(Path.Combine(remote, "tree"));
            VirtualFileDataObject data = NewDataObject(plan);

            bool async;
            ((IAsyncOperation)data).GetAsyncMode(out async);
            Assert.IsTrue(async);

            FORMATETC format = Format("FileGroupDescriptorW", TYMED.TYMED_HGLOBAL, -1);
            Assert.AreEqual(0, data.QueryGetData(ref format));
            STGMEDIUM medium;
            data.GetData(ref format, out medium);
            byte[] bytes = Ole.ReadGlobal(medium.unionmember);
            Ole.ReleaseStgMedium(ref medium);

            Assert.AreEqual(4 + plan.Entries.Count * Ole.FileDescriptorSize, bytes.Length);
            BinaryReader r = new BinaryReader(new MemoryStream(bytes));
            Assert.AreEqual(plan.Entries.Count, r.ReadInt32());
            for (int i = 0; i < plan.Entries.Count; i++)
            {
                DownloadEntry entry = plan.Entries[i];
                uint flags = r.ReadUInt32();
                Assert.AreEqual(Ole.FD_ATTRIBUTES | Ole.FD_FILESIZE | Ole.FD_WRITESTIME, flags, "flags " + i);
                Assert.AreEqual(0, flags & 0x4000, "no FD_PROGRESSUI");
                r.ReadBytes(16 + 8 + 8);
                uint attributes = r.ReadUInt32();
                Assert.AreEqual(entry.IsFolder, (attributes & 0x10) != 0, "directory flag " + i);
                r.ReadInt64();
                r.ReadInt64();
                long written = r.ReadInt64();
                Assert.AreEqual(entry.Modified.ToFileTimeUtc(), written);
                uint high = r.ReadUInt32();
                uint low = r.ReadUInt32();
                Assert.AreEqual(entry.IsFolder ? 0L : entry.Size, ((long)high << 32) | low);
                string name = Encoding.Unicode.GetString(r.ReadBytes(Ole.MaxPath * 2)).TrimEnd('\0');
                Assert.AreEqual(entry.Relative, name);
            }

            FORMATETC effect = Format("Preferred DropEffect", TYMED.TYMED_HGLOBAL, -1);
            data.GetData(ref effect, out medium);
            Assert.AreEqual(Ole.DROPEFFECT_COPY, BitConverter.ToInt32(Ole.ReadGlobal(medium.unionmember), 0));
            Ole.ReleaseStgMedium(ref medium);
        }

        [Test]
        public void StreamsDeliverEveryFileExactlyAndTheOperationCompletes()
        {
            byte[] small = RandomBytes(1234, 5);
            byte[] empty = new byte[0];
            // Bigger than the 4 MiB read-ahead buffer, so the filler has to wait for the reader.
            byte[] big = RandomBytes(11 * 1024 * 1024 + 17, 6);
            WriteRemote(@"tree\small.bin", small, 1);
            WriteRemote(@"tree\deep\empty.bin", empty, 2);
            WriteRemote(@"tree\deep\big.bin", big, 3);

            DownloadPlan plan = Plan(Path.Combine(remote, "tree"));
            VirtualFileDataObject data = NewDataObject(plan);

            ((IAsyncOperation)data).StartOperation(null);
            DownloadOperation operation = lastOperation;
            Assert.IsNotNull(operation);

            for (int i = 0; i < plan.Entries.Count; i++)
            {
                DownloadEntry entry = plan.Entries[i];
                if (entry.IsFolder)
                {
                    continue;
                }
                IStream stream = StreamOf(data, i);
                byte[] got = ReadAll(stream);
                Assert.AreEqual(entry.Size, got.Length, entry.Relative);
                Assert.AreEqual(Hash(File.ReadAllBytes(entry.RemotePath)), Hash(got), entry.Relative);
            }

            ((IAsyncOperation)data).EndOperation(0, null, Ole.DROPEFFECT_COPY);
            Assert.IsTrue(operation.IsEnded);
            Assert.AreEqual(TransferOutcome.Completed, operation.Run(null, null));
            TransferState.Values v = operation.SharedState.Snapshot();
            Assert.AreEqual(1234 + big.Length, v.TotalDone);
            Assert.AreEqual(3, v.ItemsDone);
        }

        [Test]
        public void AFolderHasNoContentsToRequest()
        {
            WriteRemote(@"tree\a.txt", RandomBytes(10, 1), 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "tree"));
            VirtualFileDataObject data = NewDataObject(plan);

            FORMATETC format = Format("FileContents", TYMED.TYMED_ISTREAM, 0); // index 0 is the folder
            STGMEDIUM medium;
            COMException error = null;
            try
            {
                data.GetData(ref format, out medium);
            }
            catch (COMException ex)
            {
                error = ex;
            }
            Assert.IsNotNull(error);
            Assert.AreEqual(Ole.DV_E_FORMATETC, error.ErrorCode);

            format = Format("FileContents", TYMED.TYMED_ISTREAM, 99);
            Assert.AreEqual(Ole.DV_E_FORMATETC, data.QueryGetData(ref format));
        }

        [Test]
        public void SeekAnswersPositionQueriesOnly()
        {
            WriteRemote("a.bin", RandomBytes(5000, 7), 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "a.bin"));
            VirtualFileDataObject data = NewDataObject(plan);
            IStream stream = StreamOf(data, 0);

            IntPtr position = Marshal.AllocHGlobal(8);
            try
            {
                stream.Seek(0, 1, position);
                Assert.AreEqual(0L, Marshal.ReadInt64(position));

                byte[] buffer = new byte[100];
                stream.Read(buffer, 100, IntPtr.Zero);
                stream.Seek(0, 1, position);
                Assert.AreEqual(100L, Marshal.ReadInt64(position));

                COMException error = null;
                try
                {
                    stream.Seek(0, 0, IntPtr.Zero); // back to the start: not possible
                }
                catch (COMException ex)
                {
                    error = ex;
                }
                Assert.IsNotNull(error);
                Assert.AreEqual(Ole.STG_E_INVALIDFUNCTION, error.ErrorCode);

                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                stream.Stat(out stat, Ole.STATFLAG_NONAME);
                Assert.AreEqual(5000L, stat.cbSize);
                Assert.AreEqual(Ole.STGTY_STREAM, stat.type);
            }
            finally
            {
                Marshal.FreeHGlobal(position);
            }
            ((RemoteFileStream)stream).Abort();
        }

        [Test]
        public void CancellingMakesReadsFailQuicklyAndEndsAsCancelled()
        {
            byte[] big = RandomBytes(40 * 1024 * 1024, 8);
            WriteRemote("big.bin", big, 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "big.bin"));
            VirtualFileDataObject data = NewDataObject(plan);

            ((IAsyncOperation)data).StartOperation(null);
            DownloadOperation operation = lastOperation;
            IStream stream = StreamOf(data, 0);
            byte[] buffer = new byte[65536];
            stream.Read(buffer, buffer.Length, IntPtr.Zero); // the transfer is under way

            DateTime before = DateTime.UtcNow;
            operation.Cancel();
            COMException error = null;
            try
            {
                stream.Read(buffer, buffer.Length, IntPtr.Zero);
            }
            catch (COMException ex)
            {
                error = ex;
            }
            Assert.IsNotNull(error, "the next read fails");
            Assert.AreEqual(Ole.HR_CANCELLED, error.ErrorCode);

            ((IAsyncOperation)data).EndOperation(Ole.E_ABORT, null, 0);
            Assert.AreEqual(TransferOutcome.Cancelled, operation.Run(null, null));
            Assert.Less((DateTime.UtcNow - before).TotalSeconds, 2.0);

            // The session is free again for the next request, and nothing of the cancelled read is left over.
            FileClient c = source.GetTransferClient();
            Assert.IsTrue(c.Stat(Path.Combine(remote, "big.bin")).Exists);
        }

        [Test]
        public void EachPasteOfTheSameObjectStartsANewOperation()
        {
            byte[] data1 = RandomBytes(3000, 9);
            WriteRemote("a.bin", data1, 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "a.bin"));
            VirtualFileDataObject data = NewDataObject(plan);

            DownloadOperation first = null;
            for (int round = 0; round < 2; round++)
            {
                ((IAsyncOperation)data).StartOperation(null);
                DownloadOperation operation = lastOperation;
                Assert.AreNotSame(first, operation);
                first = operation;
                Assert.AreEqual(Hash(data1), Hash(ReadAll(StreamOf(data, 0))));
                ((IAsyncOperation)data).EndOperation(0, null, Ole.DROPEFFECT_COPY);
                Assert.AreEqual(TransferOutcome.Completed, operation.Run(null, null));
            }
        }

        [Test]
        public void TheDragHelperCanStoreItsFormatsInTheObject()
        {
            WriteRemote("a.bin", RandomBytes(10, 1), 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "a.bin"));
            VirtualFileDataObject data = NewDataObject(plan);

            FORMATETC format = Format("DragContext", TYMED.TYMED_HGLOBAL, -1);
            STGMEDIUM medium = new STGMEDIUM();
            medium.tymed = TYMED.TYMED_HGLOBAL;
            medium.unionmember = Ole.AllocGlobal(new byte[] { 1, 2, 3, 4 });
            data.SetData(ref format, ref medium, true);

            STGMEDIUM back;
            data.GetData(ref format, out back);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, Ole.ReadGlobal(back.unionmember));
            Ole.ReleaseStgMedium(ref back);

            IEnumFORMATETC formats = data.EnumFormatEtc(DATADIR.DATADIR_GET);
            FORMATETC[] one = new FORMATETC[1];
            int count = 0;
            int[] fetched = new int[1];
            while (formats.Next(1, one, fetched) == 0)
            {
                count++;
            }
            Assert.AreEqual(4, count); // descriptors, contents, drop effect, and the stored one
        }

        [Test]
        public void TheDragHelperCanAlsoStoreAStream()
        {
            WriteRemote("a.bin", RandomBytes(10, 1), 1);
            DownloadPlan plan = Plan(Path.Combine(remote, "a.bin"));
            VirtualFileDataObject data = NewDataObject(plan);

            byte[] payload = RandomBytes(5000, 3);
            FORMATETC format = Format("DragWindow", TYMED.TYMED_ISTREAM, -1);
            STGMEDIUM medium = new STGMEDIUM();
            medium.tymed = TYMED.TYMED_ISTREAM;
            medium.unionmember = Ole.NewStreamOn(payload);
            data.SetData(ref format, ref medium, true);

            STGMEDIUM back;
            data.GetData(ref format, out back);
            Assert.AreEqual(TYMED.TYMED_ISTREAM, back.tymed);
            CollectionAssert.AreEqual(payload, Ole.ReadStream(back.unionmember));
            Ole.ReleaseStgMedium(ref back);
        }

        // ---- copy to a folder ----

        [Test]
        public void CopyToFolderRecreatesTheTreeWithTimes()
        {
            byte[] a = RandomBytes(500, 11);
            byte[] b = RandomBytes(9000, 12);
            WriteRemote(@"tree\a.txt", a, 1);
            WriteRemote(@"tree\sub\b.bin", b, 2);
            Directory.CreateDirectory(Path.Combine(remote, @"tree\empty"));
            DateTime folderStamp = new DateTime(2018, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            foreach (string dir in Directory.GetDirectories(Path.Combine(remote, "tree"), "*",
                SearchOption.AllDirectories))
            {
                Directory.SetCreationTimeUtc(dir, folderStamp);
                Directory.SetLastWriteTimeUtc(dir, folderStamp.AddHours(1));
            }
            Directory.SetCreationTimeUtc(Path.Combine(remote, "tree"), folderStamp);
            Directory.SetLastWriteTimeUtc(Path.Combine(remote, "tree"), folderStamp.AddHours(1));

            DownloadToFolderJob job = new DownloadToFolderJob(source, Items(Path.Combine(remote, "tree")), local);
            Assert.AreEqual(TransferOutcome.Completed, job.Run(new TransferState(), new FakeHost()));

            string copy = Path.Combine(local, "tree");
            Assert.AreEqual(Hash(a), Hash(File.ReadAllBytes(Path.Combine(copy, "a.txt"))));
            Assert.AreEqual(Hash(b), Hash(File.ReadAllBytes(Path.Combine(copy, @"sub\b.bin"))));
            Assert.IsTrue(Directory.Exists(Path.Combine(copy, "empty")));
            Assert.AreEqual(Stamp.AddMinutes(2), File.GetLastWriteTimeUtc(Path.Combine(copy, @"sub\b.bin")));
            Assert.AreEqual(Stamp.AddDays(-2).AddMinutes(1), File.GetCreationTimeUtc(Path.Combine(copy, "a.txt")));
            Assert.AreEqual(folderStamp.AddHours(1), Directory.GetLastWriteTimeUtc(Path.Combine(copy, "sub")));
            Assert.AreEqual(folderStamp, Directory.GetCreationTimeUtc(copy));
            Assert.AreEqual(0, Directory.GetFiles(local, "*.ezlink-part", SearchOption.AllDirectories).Length);
        }

        [Test]
        public void CopyToFolderAsksAboutExistingFilesAndKeepsBoth()
        {
            WriteRemote("a.txt", Encoding.ASCII.GetBytes("remote"), 1);
            File.WriteAllText(Path.Combine(local, "a.txt"), "mine");

            FakeHost host = new FakeHost();
            host.Conflict = ConflictChoice.KeepBoth;
            DownloadToFolderJob job = new DownloadToFolderJob(source, Items(Path.Combine(remote, "a.txt")), local);
            Assert.AreEqual(TransferOutcome.Completed, job.Run(new TransferState(), host));
            Assert.AreEqual("mine", File.ReadAllText(Path.Combine(local, "a.txt")));
            Assert.AreEqual("remote", File.ReadAllText(Path.Combine(local, "a (2).txt")));
            CollectionAssert.AreEqual(new string[] { "a.txt" }, host.Conflicts);

            host.Conflict = ConflictChoice.Replace;
            job = new DownloadToFolderJob(source, Items(Path.Combine(remote, "a.txt")), local);
            Assert.AreEqual(TransferOutcome.Completed, job.Run(new TransferState(), host));
            Assert.AreEqual("remote", File.ReadAllText(Path.Combine(local, "a.txt")));
        }

        [Test]
        public void CopyToFolderStopsWhenCancelled()
        {
            WriteRemote("a.txt", RandomBytes(10, 1), 1);
            FakeHost host = new FakeHost();
            host.CancelNow = true;
            DownloadToFolderJob job = new DownloadToFolderJob(source, Items(Path.Combine(remote, "a.txt")), local);
            Assert.AreEqual(TransferOutcome.Cancelled, job.Run(new TransferState(), host));
            Assert.AreEqual(0, Directory.GetFileSystemEntries(local).Length);
        }
    }
}
