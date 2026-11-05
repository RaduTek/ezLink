using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>Answers approval prompts from a script and counts them.</summary>
    internal sealed class ScriptedPrompt : IApprovalPrompt
    {
        private readonly object sync = new object();
        public readonly List<string> Asked = new List<string>();
        public readonly Queue<ApprovalResult> Answers = new Queue<ApprovalResult>();
        public ManualResetEvent Gate;      // when set, Ask waits for it (the user "thinks")
        public ManualResetEvent Entered = new ManualResetEvent(false);
        public volatile bool CancelObserved;

        public ScriptedPrompt Then(ApprovalChoice choice, Permissions permissions)
        {
            Answers.Enqueue(new ApprovalResult(choice, permissions));
            return this;
        }

        public ScriptedPrompt ThenDismiss()
        {
            Answers.Enqueue(null);
            return this;
        }

        public int Count
        {
            get { lock (sync) { return Asked.Count; } }
        }

        public ApprovalResult Ask(Guid peerId, string peerName, string sessionType, WaitHandle cancel)
        {
            lock (sync)
            {
                Asked.Add(peerName);
            }
            Entered.Set();
            if (Gate != null)
            {
                int which = WaitHandle.WaitAny(new WaitHandle[] { Gate, cancel });
                if (which == 1)
                {
                    CancelObserved = true;
                    return null;
                }
            }
            lock (sync)
            {
                return Answers.Count > 0 ? Answers.Dequeue() : null;
            }
        }
    }

    [TestFixture]
    public class TrustManagerTests
    {
        private static readonly Guid Peer = new Guid("00000000-0000-0000-0000-0000000000aa");
        private const Permissions ReadAccess = Permissions.Browse | Permissions.Read;

        private string directory;
        private string path;
        private ManualResetEvent never;

        [SetUp]
        public void CreateFolder()
        {
            directory = Path.Combine(Path.GetTempPath(), "ezlink-trustmgr-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "trust.xml");
            never = new ManualResetEvent(false);
        }

        [TearDown]
        public void DeleteFolder()
        {
            Directory.Delete(directory, true);
        }

        private TrustManager Manager(ScriptedPrompt prompt)
        {
            return new TrustManager(new TrustStore(path), prompt);
        }

        private HelloStatus Authorize(TrustManager manager, out Permissions granted)
        {
            return manager.Authorize(Peer, "ANA-LAPTOP", SessionTypes.Files, null, never, out granted);
        }

        [Test]
        public void AnUnknownPeerIsAskedAndTheAnswerGrantsWhatWasChosen()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;

            Assert.AreEqual(HelloStatus.Accepted, Authorize(manager, out granted));
            Assert.AreEqual(ReadAccess, granted);
            Assert.AreEqual(1, prompt.Count);
        }

        [Test]
        public void ThePeerIsToldToWaitBeforeThePromptIsShown()
        {
            List<string> order = new List<string>();
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;

            manager.Authorize(Peer, "ANA", SessionTypes.Files,
                delegate { order.Add("pending:" + prompt.Count); }, never, out granted);

            Assert.AreEqual(1, order.Count);
            Assert.AreEqual("pending:0", order[0], "PendingApproval goes out before the prompt appears");
        }

        [Test]
        public void KnownPeersAreNotToldToWait()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;
            Authorize(manager, out granted);

            int pending = 0;
            manager.Authorize(Peer, "ANA", SessionTypes.Files, delegate { pending++; }, never, out granted);
            Assert.AreEqual(0, pending);
        }

        [Test]
        public void AllowOnceIsNotAskedAgainUntilItEnds()
        {
            ScriptedPrompt prompt = new ScriptedPrompt()
                .Then(ApprovalChoice.AllowOnce, ReadAccess)
                .Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;

            Authorize(manager, out granted);
            Assert.AreEqual(HelloStatus.Accepted, Authorize(manager, out granted), "second explorer session");
            Assert.AreEqual(ReadAccess, granted);
            Assert.AreEqual(1, prompt.Count);

            manager.ClearOnce(Peer); // the control session ended
            Assert.AreEqual(Permissions.None, manager.GetEffective(Peer));
            Authorize(manager, out granted);
            Assert.AreEqual(2, prompt.Count, "asked again after the grant ended");
        }

        [Test]
        public void AllowOnceIsNeverWrittenToTheStore()
        {
            Permissions granted;
            Authorize(Manager(new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess)), out granted);
            Assert.AreEqual(0, new TrustStore(path).GetAll().Count);
        }

        [Test]
        public void AlwaysAllowSurvivesARestart()
        {
            Permissions granted;
            Authorize(Manager(new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess | Permissions.Write)), out granted);
            Assert.AreEqual(ReadAccess | Permissions.Write, granted);

            ScriptedPrompt silent = new ScriptedPrompt();
            TrustManager restarted = Manager(silent);
            Assert.AreEqual(HelloStatus.Accepted, Authorize(restarted, out granted));
            Assert.AreEqual(ReadAccess | Permissions.Write, granted);
            Assert.AreEqual(0, silent.Count, "no prompt for an always-allowed peer");
        }

        [Test]
        public void DenyPersistsAcrossRestartsAndNeverPromptsAgain()
        {
            Permissions granted;
            Assert.AreEqual(HelloStatus.Denied,
                Authorize(Manager(new ScriptedPrompt().Then(ApprovalChoice.Deny, Permissions.None)), out granted));
            Assert.AreEqual(Permissions.None, granted);

            ScriptedPrompt silent = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess);
            TrustManager restarted = Manager(silent);
            Assert.AreEqual(HelloStatus.Denied, Authorize(restarted, out granted));
            Assert.AreEqual(HelloStatus.Denied, Authorize(restarted, out granted));
            Assert.AreEqual(0, silent.Count, "a denied peer is turned away without a prompt");
            Assert.AreEqual(TrustDecision.Deny, new TrustStore(path).Get(Peer).Decision);
        }

        [Test]
        public void UnblockingMakesThePeerAskAgain()
        {
            Permissions granted;
            Authorize(Manager(new ScriptedPrompt().Then(ApprovalChoice.Deny, Permissions.None)), out granted);

            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            manager.RemoveEntry(Peer);
            Assert.AreEqual(HelloStatus.Accepted, Authorize(manager, out granted));
            Assert.AreEqual(1, prompt.Count);
        }

        [Test]
        public void ADismissedPromptDeniesOnlyThisTime()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().ThenDismiss().Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;

            Assert.AreEqual(HelloStatus.Denied, Authorize(manager, out granted));
            Assert.IsNull(manager.Store.Get(Peer), "nothing is remembered");
            Assert.AreEqual(HelloStatus.Accepted, Authorize(manager, out granted), "it asks again next time");
            Assert.AreEqual(2, prompt.Count);
        }

        [Test]
        public void AllowingNothingCountsAsDenying()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, Permissions.None);
            TrustManager manager = Manager(prompt);
            Permissions granted;
            Assert.AreEqual(HelloStatus.Denied, Authorize(manager, out granted));
        }

        [Test]
        public void ThePromptCannotGrantMoreThanFileAccess()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess | Permissions.RemoteControl);
            TrustManager manager = Manager(prompt);
            Permissions granted;
            Authorize(manager, out granted);
            Assert.AreEqual(ReadAccess, granted);
        }

        [Test]
        public void SessionsArrivingTogetherShareOnePrompt()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            prompt.Gate = new ManualResetEvent(false);
            TrustManager manager = Manager(prompt);

            HelloStatus[] results = new HelloStatus[2];
            Permissions[] grants = new Permissions[2];
            Thread[] threads = new Thread[2];
            for (int i = 0; i < 2; i++)
            {
                int index = i;
                threads[i] = new Thread(delegate() { results[index] = Authorize(manager, out grants[index]); });
                threads[i].Start();
            }
            Assert.IsTrue(prompt.Entered.WaitOne(5000, false));
            Thread.Sleep(200); // let the second session reach the lock
            prompt.Gate.Set();
            foreach (Thread t in threads)
            {
                Assert.IsTrue(t.Join(5000));
            }

            Assert.AreEqual(1, prompt.Count, "the browse and transfer sessions of one explorer window ask once");
            Assert.AreEqual(HelloStatus.Accepted, results[0]);
            Assert.AreEqual(HelloStatus.Accepted, results[1]);
            Assert.AreEqual(ReadAccess, grants[0]);
            Assert.AreEqual(ReadAccess, grants[1]);
        }

        [Test]
        public void ThePromptIsToldWhenTheSessionGoesAway()
        {
            ScriptedPrompt prompt = new ScriptedPrompt();
            prompt.Gate = new ManualResetEvent(false);
            TrustManager manager = Manager(prompt);
            ManualResetEvent gone = new ManualResetEvent(false);

            HelloStatus result = HelloStatus.Accepted;
            Thread t = new Thread(delegate()
            {
                Permissions granted;
                result = manager.Authorize(Peer, "ANA", SessionTypes.Files, null, gone, out granted);
            });
            t.Start();
            Assert.IsTrue(prompt.Entered.WaitOne(5000, false));
            gone.Set();
            Assert.IsTrue(t.Join(5000));

            Assert.IsTrue(prompt.CancelObserved);
            Assert.AreEqual(HelloStatus.Denied, result);
            Assert.IsNull(manager.Store.Get(Peer));
        }

        [Test]
        public void WithoutAPromptUnknownPeersAreDenied()
        {
            Permissions granted;
            Assert.AreEqual(HelloStatus.Denied, Authorize(Manager(null), out granted));
        }

        [Test]
        public void EditingTheStoredEntryTakesEffectAtOnceAndIsAnnounced()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AlwaysAllow, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;
            Authorize(manager, out granted);

            List<Guid> changes = new List<Guid>();
            manager.PermissionsChanged += delegate(object s, TrustEventArgs e) { changes.Add(e.PeerId); };

            TrustEntry entry = manager.Store.Get(Peer);
            entry.Permissions = ReadAccess | Permissions.Write;
            manager.SetEntry(entry);
            Assert.AreEqual(ReadAccess | Permissions.Write, manager.GetEffective(Peer));

            entry.Decision = TrustDecision.Deny;
            manager.SetEntry(entry);
            Assert.AreEqual(Permissions.None, manager.GetEffective(Peer), "blocking removes everything");

            manager.RemoveEntry(Peer);
            Assert.AreEqual(3, changes.Count);
        }

        [Test]
        public void ABlockedPeerLosesAnAllowOnceGrantToo()
        {
            ScriptedPrompt prompt = new ScriptedPrompt().Then(ApprovalChoice.AllowOnce, ReadAccess);
            TrustManager manager = Manager(prompt);
            Permissions granted;
            Authorize(manager, out granted);

            TrustEntry entry = new TrustEntry();
            entry.PeerId = Peer;
            entry.ComputerName = "ANA";
            entry.Decision = TrustDecision.Deny;
            manager.SetEntry(entry);

            Assert.AreEqual(Permissions.None, manager.GetEffective(Peer));
            Assert.AreEqual(HelloStatus.Denied, Authorize(manager, out granted));
        }
    }
}
