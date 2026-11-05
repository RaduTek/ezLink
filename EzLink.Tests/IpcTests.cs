using System;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class IpcTests
    {
        [Test]
        public void AMessageWindowCanBeFoundByItsTitle()
        {
            string title = "ezLink.Test.Ipc." + Guid.NewGuid().ToString("N");
            using (IpcWindow window = new IpcWindow(title))
            {
                Assert.AreNotEqual(IntPtr.Zero, window.Handle);
                Assert.AreEqual(window.Handle, IpcWindow.Find(title));
            }
            Assert.AreEqual(IntPtr.Zero, IpcWindow.Find(title), "the window is gone once disposed");
        }

        [Test]
        public void AnUnknownTitleIsNotFound()
        {
            Assert.AreEqual(IntPtr.Zero, IpcWindow.Find("ezLink.Test.Ipc.NoSuchWindow." + Guid.NewGuid().ToString("N")));
        }

        [Test]
        public void MessagesFromOtherProgramsAreIgnored()
        {
            // The test runner is neither ezLink.exe nor ezExplorer.exe, so its notifications must be dropped.
            string title = "ezLink.Test.Ipc." + Guid.NewGuid().ToString("N");
            using (IpcWindow receiver = new IpcWindow(title))
            using (IpcWindow sender = new IpcWindow(title + ".sender"))
            {
                int received = 0;
                receiver.MessageReceived += delegate { received++; };

                bool delivered = IpcWindow.Send(receiver.Handle, sender, IpcMessages.ShowWindow, string.Empty);

                Assert.IsTrue(delivered, "the message itself is delivered to the window");
                Assert.AreEqual(0, received, "but a foreign sender is ignored");
                Assert.IsFalse(IpcWindow.IsEzLinkProcess(sender.Handle));
            }
        }

        [Test]
        public void MessageTypeNumbersMatchThePlan()
        {
            Assert.AreEqual(1, IpcMessages.ShowWindow);
            Assert.AreEqual(2, IpcMessages.ActivateExplorer);
            Assert.AreEqual(3, IpcMessages.PeerLost);
            Assert.AreEqual(4, IpcMessages.PeerBack);
            Assert.AreEqual(5, IpcMessages.ExplorerClosed);
            Assert.AreEqual(6, IpcMessages.ExplorerHello);
        }
    }
}
