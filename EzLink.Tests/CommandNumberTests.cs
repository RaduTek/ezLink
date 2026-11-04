using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>Pins the numbers in the plan's command tables: changing one breaks every deployed peer.</summary>
    [TestFixture]
    public class CommandNumberTests
    {
        [Test]
        public void CommandSetNumbers()
        {
            Assert.AreEqual(0x00, CommandSets.General);
            Assert.AreEqual(0x01, CommandSets.Files);
            Assert.AreEqual(0x02, CommandSets.Sync);
            Assert.AreEqual(0x03, CommandSets.RemoteControl);
            Assert.AreEqual(0x80, CommandSets.FirstExperimental);
        }

        [Test]
        public void GeneralCommandNumbers()
        {
            Assert.AreEqual(0x01, GeneralCommands.Hello);
            Assert.AreEqual(0x02, GeneralCommands.HelloReply);
            Assert.AreEqual(0x03, GeneralCommands.Ping);
            Assert.AreEqual(0x04, GeneralCommands.Pong);
            Assert.AreEqual(0x05, GeneralCommands.Ok);
            Assert.AreEqual(0x06, GeneralCommands.Error);
            Assert.AreEqual(0x07, GeneralCommands.Cancel);
            Assert.AreEqual(0x08, GeneralCommands.TrustChanged);
            Assert.AreEqual(0x09, GeneralCommands.Unsupported);
        }

        [Test]
        public void FileCommandNumbers()
        {
            Assert.AreEqual(0x01, FileCommands.GetRoots);
            Assert.AreEqual(0x02, FileCommands.Roots);
            Assert.AreEqual(0x03, FileCommands.ListDir);
            Assert.AreEqual(0x04, FileCommands.DirListing);
            Assert.AreEqual(0x05, FileCommands.ListTree);
            Assert.AreEqual(0x06, FileCommands.TreeEntries);
            Assert.AreEqual(0x07, FileCommands.TreeEnd);
            Assert.AreEqual(0x08, FileCommands.Stat);
            Assert.AreEqual(0x09, FileCommands.StatResult);
            Assert.AreEqual(0x0A, FileCommands.ReadFile);
            Assert.AreEqual(0x0B, FileCommands.FileData);
            Assert.AreEqual(0x0C, FileCommands.FileEnd);
            Assert.AreEqual(0x0D, FileCommands.WriteBegin);
            Assert.AreEqual(0x0E, FileCommands.WriteData);
            Assert.AreEqual(0x0F, FileCommands.WriteEnd);
            Assert.AreEqual(0x10, FileCommands.CreateDir);
            Assert.AreEqual(0x11, FileCommands.Delete);
            Assert.AreEqual(0x12, FileCommands.Rename);
        }

        [Test]
        public void ProtocolConstants()
        {
            Assert.AreEqual(1, ProtocolInfo.Version);
            Assert.AreEqual(1024 * 1024, ProtocolInfo.MaxFrameLength);
            Assert.AreEqual("EZLS", ProtocolInfo.SessionMagic);
            Assert.AreEqual("EZLB", ProtocolInfo.BeaconMagic);
            Assert.AreEqual("files/1", Capabilities.Files);
            Assert.AreEqual(10, Frame.HeaderSize);
        }

        [Test]
        public void PermissionAndErrorValues()
        {
            Assert.AreEqual(1u, (uint)Permissions.Browse);
            Assert.AreEqual(2u, (uint)Permissions.Read);
            Assert.AreEqual(4u, (uint)Permissions.Write);
            Assert.AreEqual(1, (int)ErrorCode.NotFound);
            Assert.AreEqual(4, (int)ErrorCode.PermissionNotGranted);
            Assert.AreEqual(6, (int)ErrorCode.Cancelled);
        }
    }
}
