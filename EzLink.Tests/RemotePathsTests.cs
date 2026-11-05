using ezExplorer;
using NUnit.Framework;

namespace EzLink.Tests
{
    /// <summary>The path helpers behind ezExplorer's navigation (linked into this project from ezExplorer).</summary>
    [TestFixture]
    public class RemotePathsTests
    {
        [Test]
        public void ParentOfAFolderIsTheFolderAbove()
        {
            Assert.AreEqual(@"C:\Users", RemotePaths.GetParent(@"C:\Users\Ana"));
            Assert.AreEqual(@"C:\", RemotePaths.GetParent(@"C:\Users"));
        }

        [Test]
        public void ParentOfADriveIsTheComputerLevelAndNothingIsAboveThat()
        {
            Assert.AreEqual(RemotePaths.Root, RemotePaths.GetParent(@"C:\"));
            Assert.IsNull(RemotePaths.GetParent(RemotePaths.Root));
        }

        [Test]
        public void ChainListsEveryLevelFromTheDrive()
        {
            Assert.AreEqual(new string[] { @"C:\", @"C:\Users", @"C:\Users\Ana" },
                RemotePaths.Chain(@"C:\Users\Ana").ToArray());
            Assert.AreEqual(new string[] { @"D:\" }, RemotePaths.Chain(@"D:\").ToArray());
            Assert.AreEqual(0, RemotePaths.Chain(RemotePaths.Root).Count);
        }

        [Test]
        public void CombineHandlesDriveRoots()
        {
            Assert.AreEqual(@"C:\Users", RemotePaths.Combine(@"C:\", "Users"));
            Assert.AreEqual(@"C:\Users\Ana", RemotePaths.Combine(@"C:\Users", "Ana"));
            Assert.AreEqual(@"C:\", RemotePaths.Combine(RemotePaths.Root, @"C:\"));
        }

        [Test]
        public void IsInsideDoesNotConfuseSiblingsWithSimilarNames()
        {
            Assert.IsTrue(RemotePaths.IsInside(@"C:\Users\Ana", @"C:\Users"));
            Assert.IsTrue(RemotePaths.IsInside(@"C:\Users", @"C:\Users"));
            Assert.IsTrue(RemotePaths.IsInside(@"c:\users\ana", @"C:\Users"));
            Assert.IsFalse(RemotePaths.IsInside(@"C:\Users2", @"C:\Users"));
            Assert.IsTrue(RemotePaths.IsInside(@"C:\Users", @"C:\"));
            Assert.IsTrue(RemotePaths.IsInside(@"C:\Users", RemotePaths.Root));
        }

        [Test]
        public void TypedTextIsCleanedUp()
        {
            Assert.AreEqual(@"C:\", RemotePaths.FromUserText("c:"));
            Assert.AreEqual(@"C:\Users\Ana", RemotePaths.FromUserText(" c:/Users/Ana/ "));
            Assert.AreEqual(@"C:\Users", RemotePaths.FromUserText(@"""C:\Users\"""));
            Assert.AreEqual(@"C:\", RemotePaths.FromUserText(@"C:\"));
        }

        [Test]
        public void NamesWithPathCharactersAreRejected()
        {
            Assert.IsTrue(RemotePaths.IsValidName("notes.txt"));
            Assert.IsFalse(RemotePaths.IsValidName(""));
            Assert.IsFalse(RemotePaths.IsValidName("  "));
            Assert.IsFalse(RemotePaths.IsValidName(".."));
            Assert.IsFalse(RemotePaths.IsValidName(@"a\b"));
            Assert.IsFalse(RemotePaths.IsValidName("a:b"));
            Assert.IsFalse(RemotePaths.IsValidName("a?b"));
        }

        [Test]
        public void HiddenSystemFilesAreProtectedButHiddenAloneAreNot()
        {
            Assert.IsTrue(RemotePaths.IsProtected(ShellNative.FILE_ATTRIBUTE_HIDDEN | ShellNative.FILE_ATTRIBUTE_SYSTEM));
            Assert.IsFalse(RemotePaths.IsProtected(ShellNative.FILE_ATTRIBUTE_HIDDEN));
            Assert.IsFalse(RemotePaths.IsProtected(ShellNative.FILE_ATTRIBUTE_SYSTEM));
        }
    }
}
