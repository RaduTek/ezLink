using System;
using System.IO;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class PathPolicyTests
    {
        private static string Windows
        {
            get { return Environment.GetEnvironmentVariable("SystemRoot"); }
        }

        private static string SystemDrive
        {
            get { return Path.GetPathRoot(Windows); }
        }

        [TearDown]
        public void RestoreProfile()
        {
            PathPolicy.SetProfileFolder(null);
        }

        [Test]
        public void TheWindowsAndProgramFilesFoldersAreProtected()
        {
            Assert.IsTrue(PathPolicy.IsProtected(Windows));
            Assert.IsTrue(PathPolicy.IsProtected(Path.Combine(Windows, @"System32\drivers")));
            Assert.IsTrue(PathPolicy.IsProtected(Path.Combine(Windows, "system32").ToUpperInvariant()));
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            Assert.IsTrue(PathPolicy.IsProtected(Path.Combine(programFiles, "Anything")));
            Assert.IsTrue(PathPolicy.IsProtected(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)));
        }

        [Test]
        public void SimilarlyNamedFoldersAreNotProtected()
        {
            Assert.IsFalse(PathPolicy.IsProtected(Windows + "Extra"));
            Assert.IsFalse(PathPolicy.IsProtected(SystemDrive));
        }

        [Test]
        public void TheDocumentsAndSettingsJunctionIsNotProtected()
        {
            // Blocking it would break Windows XP, where it is the real profile location.
            Assert.IsFalse(PathPolicy.IsProtected(Path.Combine(SystemDrive, "Documents and Settings")));
        }

        [Test]
        public void AppDataOfTheProfileIsProtectedButTheRestOfTheProfileIsNot()
        {
            string profile = Path.Combine(Path.GetTempPath(), "ezlink-policy-" + Guid.NewGuid().ToString("N"));
            PathPolicy.SetProfileFolder(profile);
            Assert.IsTrue(PathPolicy.IsProtected(Path.Combine(profile, @"AppData\Local\x")));
            Assert.IsTrue(PathPolicy.IsProtected(Path.Combine(profile, @"Application Data\x")));
            Assert.IsFalse(PathPolicy.IsProtected(Path.Combine(profile, @"Documents\x.txt")));
            Assert.IsFalse(PathPolicy.IsProtected(profile), "the profile itself stays reachable");
        }

        [Test]
        public void SystemNamesAtTheRootOfADriveAreProtected()
        {
            Assert.IsTrue(PathPolicy.IsProtected(SystemDrive + @"$Recycle.Bin\S-1-5"));
            Assert.IsTrue(PathPolicy.IsProtected(SystemDrive + "System Volume Information"));
            Assert.IsTrue(PathPolicy.IsProtected(SystemDrive + "pagefile.sys"));
            Assert.IsFalse(PathPolicy.IsProtected(SystemDrive + "MyFolder"));
        }

        [Test]
        public void AFolderHoldingAProtectedOneIsDetected()
        {
            Assert.IsTrue(PathPolicy.ContainsProtected(SystemDrive.TrimEnd('\\')));
            Assert.IsFalse(PathPolicy.ContainsProtected(Path.Combine(SystemDrive, "Surely Not A Folder")));
        }

        [Test]
        public void TheProfileNameIsItsFolderName()
        {
            PathPolicy.SetProfileFolder(@"C:\Users\Ana");
            Assert.AreEqual("Ana", PathPolicy.ProfileName);
        }
    }
}
