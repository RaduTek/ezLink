using System;
using System.IO;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class TrustStoreTests
    {
        private string directory;
        private string path;

        [SetUp]
        public void CreateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "ezlink-trust-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "trust.xml");
        }

        [TearDown]
        public void DeleteFolder()
        {
            Directory.Delete(directory, true);
        }

        private static TrustEntry Entry(string name, TrustDecision decision, Permissions permissions)
        {
            TrustEntry e = new TrustEntry();
            e.PeerId = Guid.NewGuid();
            e.ComputerName = name;
            e.Decision = decision;
            e.Permissions = permissions;
            e.FirstSeenUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            e.LastSeenUtc = new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Utc);
            return e;
        }

        [Test]
        public void AMissingFileIsAnEmptyStore()
        {
            TrustStore store = new TrustStore(path);
            Assert.AreEqual(0, store.GetAll().Count);
            Assert.IsFalse(File.Exists(path), "nothing is written until something changes");
        }

        [Test]
        public void EntriesSurviveAReload()
        {
            TrustEntry allowed = Entry("ANA-LAPTOP", TrustDecision.AlwaysAllow, Permissions.Browse | Permissions.Read);
            TrustEntry blocked = Entry("Zoë <&> \"PC\"", TrustDecision.Deny, Permissions.None);
            TrustStore store = new TrustStore(path);
            store.Set(allowed);
            store.Set(blocked);

            TrustStore reloaded = new TrustStore(path);
            Assert.AreEqual(2, reloaded.GetAll().Count);

            TrustEntry a = reloaded.Get(allowed.PeerId);
            Assert.AreEqual("ANA-LAPTOP", a.ComputerName);
            Assert.AreEqual(Permissions.Browse | Permissions.Read, a.Permissions);
            Assert.AreEqual(TrustDecision.AlwaysAllow, a.Decision);
            Assert.AreEqual(allowed.FirstSeenUtc, a.FirstSeenUtc);
            Assert.AreEqual(allowed.LastSeenUtc, a.LastSeenUtc);

            TrustEntry b = reloaded.Get(blocked.PeerId);
            Assert.AreEqual("Zoë <&> \"PC\"", b.ComputerName);
            Assert.AreEqual(TrustDecision.Deny, b.Decision);
        }

        [Test]
        public void ReservedPermissionBitsAreKept()
        {
            TrustEntry e = Entry("PC", TrustDecision.AlwaysAllow, Permissions.Browse | Permissions.Sync | Permissions.RemoteControl);
            new TrustStore(path).Set(e);
            Assert.AreEqual(e.Permissions, new TrustStore(path).Get(e.PeerId).Permissions);
        }

        [Test]
        public void RemovingAnEntryIsPersisted()
        {
            TrustEntry e = Entry("PC", TrustDecision.Deny, Permissions.None);
            TrustStore store = new TrustStore(path);
            store.Set(e);
            Assert.IsTrue(store.Remove(e.PeerId));
            Assert.IsFalse(store.Remove(e.PeerId), "already gone");
            Assert.IsNull(new TrustStore(path).Get(e.PeerId));
        }

        [Test]
        public void SavingLeavesNoTemporaryFile()
        {
            TrustStore store = new TrustStore(path);
            store.Set(Entry("A", TrustDecision.AlwaysAllow, Permissions.Browse));
            store.Set(Entry("B", TrustDecision.AlwaysAllow, Permissions.Browse)); // second save replaces the file
            Assert.AreEqual(1, Directory.GetFiles(directory).Length, string.Join(", ", Directory.GetFiles(directory)));
        }

        [Test]
        public void GetReturnsCopies()
        {
            TrustEntry e = Entry("PC", TrustDecision.AlwaysAllow, Permissions.Browse);
            TrustStore store = new TrustStore(path);
            store.Set(e);
            store.Get(e.PeerId).Permissions = Permissions.Write;
            e.Permissions = Permissions.Write;
            Assert.AreEqual(Permissions.Browse, store.Get(e.PeerId).Permissions);
        }

        [Test]
        public void GarbageIsSetAsideAndTheStoreStartsEmpty()
        {
            File.WriteAllText(path, "this is not xml <<<");
            TrustStore store = new TrustStore(path);

            Assert.AreEqual(0, store.GetAll().Count);
            Assert.IsFalse(File.Exists(path));
            Assert.AreEqual(1, Directory.GetFiles(directory, "trust.xml.corrupt-*").Length, "the old file is kept");

            TrustEntry e = Entry("PC", TrustDecision.Deny, Permissions.None);
            store.Set(e);
            Assert.IsNotNull(new TrustStore(path).Get(e.PeerId), "the store works again after recovery");
        }

        [Test]
        public void AnEntryWithABadFieldMakesTheWholeFileCorrupt()
        {
            File.WriteAllText(path,
                "<ezLinkTrust version=\"1\"><peer id=\"not-a-guid\" name=\"x\" permissions=\"1\" decision=\"Deny\" "
                + "firstSeen=\"2026-01-01T00:00:00Z\" lastSeen=\"2026-01-01T00:00:00Z\"/></ezLinkTrust>");
            Assert.AreEqual(0, new TrustStore(path).GetAll().Count);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length);
        }

        [Test]
        public void AnUnknownDecisionIsCorrupt()
        {
            File.WriteAllText(path,
                "<ezLinkTrust version=\"1\"><peer id=\"" + Guid.NewGuid() + "\" name=\"x\" permissions=\"1\" decision=\"Maybe\" "
                + "firstSeen=\"2026-01-01T00:00:00Z\" lastSeen=\"2026-01-01T00:00:00Z\"/></ezLinkTrust>");
            Assert.AreEqual(0, new TrustStore(path).GetAll().Count);
        }

        [Test]
        public void TheWrongRootElementIsCorrupt()
        {
            File.WriteAllText(path, "<something/>");
            Assert.AreEqual(0, new TrustStore(path).GetAll().Count);
            Assert.AreEqual(1, Directory.GetFiles(directory, "*.corrupt-*").Length);
        }

        [Test]
        public void TouchOnlyRewritesTheFileWhenSomethingChanged()
        {
            TrustEntry e = Entry("OLD", TrustDecision.AlwaysAllow, Permissions.Browse);
            e.LastSeenUtc = DateTime.UtcNow; // seen just now
            TrustStore store = new TrustStore(path);
            store.Set(e);
            DateTime written = File.GetLastWriteTimeUtc(path);
            System.Threading.Thread.Sleep(50);

            store.Touch(e.PeerId, "OLD");
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(path), "same name, seen a moment ago: no write");

            store.Touch(e.PeerId, "NEW");
            Assert.AreEqual("NEW", new TrustStore(path).Get(e.PeerId).ComputerName);

            store.Touch(Guid.NewGuid(), "stranger"); // unknown peers are not added by Touch
            Assert.AreEqual(1, store.GetAll().Count);
        }

        [Test]
        public void ChangedIsRaisedForSetAndRemove()
        {
            int changes = 0;
            TrustStore store = new TrustStore(path);
            store.Changed += delegate { changes++; };
            TrustEntry e = Entry("PC", TrustDecision.Deny, Permissions.None);
            store.Set(e);
            store.Remove(e.PeerId);
            store.Remove(e.PeerId);
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void AnUnwritableLocationKeepsTheInMemoryStoreUsable()
        {
            // The directory path is occupied by a file, so it can never be created.
            string blocker = Path.Combine(directory, "blocker");
            File.WriteAllText(blocker, "x");
            TrustStore store = new TrustStore(Path.Combine(blocker, "trust.xml"));
            TrustEntry e = Entry("PC", TrustDecision.Deny, Permissions.None);
            store.Set(e);
            Assert.IsNotNull(store.Get(e.PeerId));
        }
    }
}
