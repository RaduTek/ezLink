using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class ShareTableTests
    {
        private string root;
        private SharingSettings settings;

        [SetUp]
        public void Setup()
        {
            root = Path.Combine(Path.GetTempPath(), "ezlink-shares-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "home"));
            Directory.CreateDirectory(Path.Combine(root, "Photos"));
            PathPolicy.SetProfileFolder(Path.Combine(root, "home"));
            settings = new SharingSettings();
            settings.ShareUserProfile = false;
            settings.ShareFixedDrives = false;
            settings.ShareRemovableDrives = false;
        }

        [TearDown]
        public void Cleanup()
        {
            PathPolicy.SetProfileFolder(null);
            Directory.Delete(root, true);
        }

        private string P(string relative)
        {
            return Path.Combine(root, relative);
        }

        private List<SharedFolder> Folders(params SharedFolder[] folders)
        {
            return new List<SharedFolder>(folders);
        }

        [Test]
        public void TheProfileIsSharedUnderItsFolderName()
        {
            settings.ShareUserProfile = true;
            ShareTable table = ShareTable.Build(settings);
            Assert.AreEqual(1, table.Shares.Count);
            Assert.AreEqual("home", table.Shares[0].Name);
            Assert.AreEqual(P("home"), table.Shares[0].FullPath);
            Assert.IsFalse(table.Shares[0].ReadOnly, "the profile folder is always writable");
            Assert.IsFalse(table.Shares[0].IsDrive);
        }

        [Test]
        public void ExtraFoldersUseTheirFolderNameAndKeepTheirReadOnlyFlag()
        {
            settings.SetFolders(Folders(new SharedFolder(P("Photos"), true), new SharedFolder(P("home") + @"\", false)));
            ShareTable table = ShareTable.Build(settings);
            Assert.AreEqual("Photos", table.Shares[0].Name);
            Assert.IsTrue(table.Shares[0].ReadOnly);
            Assert.AreEqual("home", table.Shares[1].Name);
            Assert.AreEqual(P("home"), table.Shares[1].FullPath, "a trailing backslash is dropped");
        }

        [Test]
        public void ADriveRootIsNamedByItsLetter()
        {
            string drive = Path.GetPathRoot(root);
            settings.SetFolders(Folders(new SharedFolder(drive.ToLowerInvariant(), false)));
            Share share = ShareTable.Build(settings).Shares[0];
            Assert.AreEqual(drive.Substring(0, 2).ToUpperInvariant(), share.Name);
            Assert.IsTrue(share.IsDrive);
            Assert.AreEqual((byte)DriveType.Fixed, share.DriveType);
        }

        [Test]
        public void SharesWithTheSameNameCannotCoexistAndTheFirstWins()
        {
            settings.ShareUserProfile = true; // "home"
            Directory.CreateDirectory(P(@"other\Home"));
            settings.SetFolders(Folders(new SharedFolder(P(@"other\Home"), false), new SharedFolder(P("Photos"), false)));
            ShareTable table = ShareTable.Build(settings);
            Assert.AreEqual(2, table.Shares.Count);
            Assert.AreEqual(P("home"), table.Find("HOME").FullPath, "the profile came first; names ignore case");
            Assert.IsNotNull(table.Find("photos"));
        }

        [Test]
        public void TheAllDrivesBoxesShareEveryDriveOfThatTypeByLetter()
        {
            settings.ShareFixedDrives = true;
            settings.FixedDrivesReadOnly = true;
            ShareTable table = ShareTable.Build(settings);
            Share drive = table.Find(Path.GetPathRoot(root).Substring(0, 2));
            Assert.IsNotNull(drive);
            Assert.IsTrue(drive.ReadOnly);
        }

        [Test]
        public void ARepeatedDriveInTheListOverridesTheBoxFlag()
        {
            string drive = Path.GetPathRoot(root);
            settings.ShareFixedDrives = true;
            settings.FixedDrivesReadOnly = true;
            settings.SetFolders(Folders(new SharedFolder(drive, false)));
            Assert.IsFalse(ShareTable.Build(settings).Find(drive.Substring(0, 2)).ReadOnly, "the list entry came first");
        }

        [Test]
        public void ResolveMapsNamedPathsIntoTheShare()
        {
            settings.SetFolders(Folders(new SharedFolder(P("Photos"), false)));
            ShareTable table = ShareTable.Build(settings);
            Share share;
            Assert.AreEqual(P("Photos"), table.Resolve("Photos", out share));
            Assert.AreEqual("Photos", share.Name);
            Assert.AreEqual(P(@"Photos\2020\a.jpg"), table.Resolve(@"photos\2020\a.jpg", out share));
            Assert.AreEqual(P(@"Photos\2020"), table.Resolve("Photos/2020", out share));
        }

        [Test]
        public void ResolveRefusesUnknownNamesEscapesAndOddPaths()
        {
            settings.SetFolders(Folders(new SharedFolder(P("Photos"), false)));
            ShareTable table = ShareTable.Build(settings);
            Share share;
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve("Music", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(@"Photos\..", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(@"Photos\..\home", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(@"Photos\a:b", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve("", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(@"\\server\share", out share); });
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(P("Photos"), out share); },
                "a full path of a folder share is not an address");
        }

        [Test]
        public void ADriveKeepsItsLetterFormAndNeedsToBeShared()
        {
            string drive = Path.GetPathRoot(root);
            string file = P(@"Photos\a.txt");
            Share share;

            ShareTable none = ShareTable.Build(settings);
            Assert.Throws(typeof(PathRejectedException), delegate { none.Resolve(file, out share); });

            settings.SetFolders(Folders(new SharedFolder(drive, true)));
            ShareTable table = ShareTable.Build(settings);
            Assert.AreEqual(file, table.Resolve(file, out share));
            Assert.IsTrue(share.IsDrive);
            Assert.AreEqual(drive, table.Resolve(drive, out share));
            Assert.Throws(typeof(PathRejectedException), delegate { table.Resolve(drive.Substring(0, 2), out share); },
                "a bare drive letter is not an address");
        }

        [Test]
        public void ShareRootsAreRecognised()
        {
            settings.SetFolders(Folders(new SharedFolder(P("Photos"), false)));
            ShareTable table = ShareTable.Build(settings);
            Assert.IsTrue(table.IsShareRoot(P("Photos")));
            Assert.IsTrue(table.IsShareRoot(P("Photos") + @"\"));
            Assert.IsFalse(table.IsShareRoot(P(@"Photos\sub")));
        }

        [Test]
        public void TheFoldersListSurvivesTheRegistryTextFormat()
        {
            List<SharedFolder> list = Folders(new SharedFolder(@"D:\Photos", true), new SharedFolder(@"E:\", false));
            string[] lines = SharedFolder.FormatAll(list);
            CollectionAssert.AreEqual(new string[] { @"R|D:\Photos", @"W|E:\" }, lines);

            List<SharedFolder> back = SharedFolder.ParseAll(new string[] { lines[0], "garbage", "R|relative", lines[1], "" });
            Assert.AreEqual(2, back.Count);
            Assert.AreEqual(@"D:\Photos", back[0].Path);
            Assert.IsTrue(back[0].ReadOnly);
            Assert.AreEqual("Photos", back[0].Name);
            Assert.AreEqual("E:", back[1].Name);
            Assert.IsTrue(back[1].IsDriveRoot);
            Assert.AreEqual(0, SharedFolder.ParseAll(null).Count);
        }
    }
}
