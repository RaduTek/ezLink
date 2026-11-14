using System.Collections.Generic;
using EzLink.Protocol;
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
        public void HiddenAndSystemItemsAreListedOnlyWhenTheirOptionIsOn()
        {
            uint hidden = ShellNative.FILE_ATTRIBUTE_HIDDEN;
            uint system = ShellNative.FILE_ATTRIBUTE_SYSTEM;
            Assert.IsFalse(RemotePaths.IsVisible(hidden, false, false));
            Assert.IsTrue(RemotePaths.IsVisible(hidden, true, false));
            Assert.IsFalse(RemotePaths.IsVisible(system, true, false));
            Assert.IsTrue(RemotePaths.IsVisible(system, false, true));
            Assert.IsTrue(RemotePaths.IsVisible(0, false, false));
        }

        [Test]
        public void SharedFoldersAreTopLevelRootsAddressedByName()
        {
            Assert.IsTrue(RemotePaths.IsTopLevel("Photos"));
            Assert.IsTrue(RemotePaths.IsTopLevel(@"C:\"));
            Assert.IsFalse(RemotePaths.IsTopLevel(@"Photos\2020"));
            Assert.IsFalse(RemotePaths.IsTopLevel(@"C:\Users"));
            Assert.IsFalse(RemotePaths.IsTopLevel(""));
            Assert.AreEqual("", RemotePaths.GetParent("Photos"));
            Assert.AreEqual("Photos", RemotePaths.GetParent(@"Photos\2020"));
            Assert.AreEqual(@"Photos\2020", RemotePaths.GetParent(@"Photos\2020\Rock"));
            Assert.AreEqual(@"C:\", RemotePaths.GetParent(@"C:\Users"));
            Assert.AreEqual(@"Photos\2020", RemotePaths.Combine("Photos", "2020"));
            Assert.AreEqual("Rock", RemotePaths.GetName(@"Photos\2020\Rock"));
            Assert.AreEqual("Photos", RemotePaths.GetName("Photos"));
            Assert.AreEqual("Photos", RemotePaths.FromUserText(@" Photos\ "));
            CollectionAssert.AreEqual(new string[] { "Photos", @"Photos\2020" }, RemotePaths.Chain(@"Photos\2020"));
            Assert.AreEqual("Photos", RemotePaths.RootOf(@"Photos\2020"));
            Assert.AreEqual(@"C:\", RemotePaths.RootOf(@"C:\Users\Ana"));
        }

        [Test]
        public void ReadOnlyAndDisplayNamesComeFromTheRootsList()
        {
            List<DriveEntry> entries = new List<DriveEntry>();
            DriveEntry folder = new DriveEntry();
            folder.Name = "Photos";
            folder.Label = "Photos";
            folder.ReadOnly = true;
            entries.Add(folder);
            DriveEntry drive = new DriveEntry();
            drive.Name = "C:";
            drive.DriveType = 3;
            drive.ReadOnly = false;
            entries.Add(drive);
            RemotePaths.RememberRoots(entries);

            Assert.IsTrue(RemotePaths.IsReadOnly("Photos"));
            Assert.IsTrue(RemotePaths.IsReadOnly(@"photos\2020\a.jpg"));
            Assert.IsFalse(RemotePaths.IsReadOnly(@"C:\Users"));
            Assert.IsFalse(RemotePaths.IsReadOnly("Unknown"));
            Assert.IsFalse(RemotePaths.IsReadOnly(""));
            Assert.AreEqual("C:", RemotePaths.DisplayName(@"C:\"));
            Assert.AreEqual("Photos", RemotePaths.DisplayName("Photos"));
            RemotePaths.RememberRoots(new List<DriveEntry>());
        }
    }
}
