using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using EzLink.Protocol;
using NUnit.Framework;

namespace EzLink.Tests
{
    [TestFixture]
    public class LinkStateMachineTests
    {
        private static List<AdapterInfo> Adapters(OperationalStatus status, string ip)
        {
            List<AdapterAddress> addresses = new List<AdapterAddress>();
            if (ip != null)
            {
                addresses.Add(new AdapterAddress(IPAddress.Parse(ip), IPAddress.Parse("255.255.0.0")));
            }
            List<AdapterInfo> list = new List<AdapterInfo>();
            list.Add(new AdapterInfo("{A}", "Ethernet 5", "Realtek", status, addresses, null));
            return list;
        }

        private static LinkStateMachine Searching()
        {
            LinkStateMachine m = new LinkStateMachine();
            m.SetAdapters(Adapters(OperationalStatus.Up, "169.254.1.1"));
            return m;
        }

        [Test]
        public void StartsWithNoCable()
        {
            LinkStateMachine m = new LinkStateMachine();
            Assert.AreEqual(LinkState.NoCable, m.State);
            Assert.AreEqual("Plug in the ezLink cable", m.Status.Text);
        }

        [Test]
        public void StatesAndTextsMatchThePlanTable()
        {
            LinkStateMachine m = new LinkStateMachine();

            m.SetAdapters(Adapters(OperationalStatus.Down, null));
            Assert.AreEqual(LinkState.WaitingForNetwork, m.State);
            Assert.AreEqual("Starting the connection…", m.Status.Text);

            m.SetAdapters(Adapters(OperationalStatus.Up, "0.0.0.0"));
            Assert.AreEqual(LinkState.WaitingForNetwork, m.State, "up but no IPv4 address yet");

            m.SetAdapters(Adapters(OperationalStatus.Up, "169.254.1.1"));
            Assert.AreEqual(LinkState.Searching, m.State);
            Assert.AreEqual("Looking for the other computer…", m.Status.Text);

            m.SetPeer("ANA-LAPTOP");
            Assert.AreEqual(LinkState.Connecting, m.State);
            Assert.AreEqual("Connecting to ANA-LAPTOP…", m.Status.Text);

            m.SetControl(true, "ANA-LAPTOP");
            Assert.AreEqual(LinkState.Connected, m.State);
            Assert.AreEqual("Connected to ANA-LAPTOP", m.Status.Text);
        }

        [Test]
        public void ErrorShowsItsOwnMessageAndWinsOverEverythingElse()
        {
            LinkStateMachine m = Searching();
            m.SetPeer("ANA");
            m.SetControl(true, "ANA");

            m.SetError(LinkErrorKind.PortInUse, "port 47771 is busy");

            Assert.AreEqual(LinkState.Error, m.State);
            Assert.AreEqual("port 47771 is busy", m.Status.Text);
            Assert.AreEqual(LinkErrorKind.PortInUse, m.Status.ErrorKind);
        }

        [Test]
        public void ClearErrorOnlyClearsTheMatchingKind()
        {
            LinkStateMachine m = Searching();
            m.SetError(LinkErrorKind.DuplicateUuid, "duplicate");

            m.ClearError(LinkErrorKind.PortInUse);
            Assert.AreEqual(LinkState.Error, m.State);

            m.ClearError(LinkErrorKind.DuplicateUuid);
            Assert.AreEqual(LinkState.Searching, m.State);
        }

        [Test]
        public void LosingTheControlSessionWhileThePeerIsStillHeardMeansReconnecting()
        {
            LinkStateMachine m = Searching();
            m.SetPeer("ANA");
            m.SetControl(true, "ANA");

            m.SetControl(false, null);

            Assert.AreEqual(LinkState.Connecting, m.State);
            Assert.AreEqual("Connecting to ANA…", m.Status.Text);
        }

        [Test]
        public void LosingThePeerReturnsToSearchingThenToNoCable()
        {
            LinkStateMachine m = Searching();
            m.SetPeer("ANA");
            m.SetControl(true, "ANA");
            m.SetControl(false, null);

            m.SetPeer(null);
            Assert.AreEqual(LinkState.Searching, m.State);

            m.SetAdapters(new List<AdapterInfo>());
            Assert.AreEqual(LinkState.NoCable, m.State);
        }

        [Test]
        public void ConnectedKeepsTheNameOfTheControlPeer()
        {
            LinkStateMachine m = Searching();
            m.SetControl(true, "FROM-SESSION");
            Assert.AreEqual("FROM-SESSION", m.Status.PeerName);
            Assert.AreEqual("Connected to FROM-SESSION", m.Status.Text);
        }

        [Test]
        public void TrustLinesDescribeBothDirections()
        {
            LinkStateMachine m = Searching();
            m.SetControl(true, "ANA");

            Assert.AreEqual("You allow ANA to: nothing yet", m.Status.WeAllowText);
            Assert.AreEqual("ANA allows you to: nothing yet", m.Status.PeerAllowsText);

            m.SetWeAllow(Permissions.Browse | Permissions.Read);
            m.SetPeerAllows(Permissions.Browse | Permissions.Read | Permissions.Write);

            Assert.AreEqual("You allow ANA to: browse and copy files from this computer", m.Status.WeAllowText);
            Assert.AreEqual("ANA allows you to: browse and copy files from ANA; copy files to ANA", m.Status.PeerAllowsText);
        }

        [Test]
        public void WhatThePeerAllowedIsForgottenWhenTheSessionEnds()
        {
            LinkStateMachine m = Searching();
            m.SetControl(true, "ANA");
            m.SetPeerAllows(Permissions.Browse);
            m.SetControl(false, null);
            Assert.AreEqual(Permissions.None, m.Status.PeerAllows);
        }

        [Test]
        public void PermissionDescriptions()
        {
            Assert.AreEqual("nothing yet", LinkStatus.DescribePermissions(Permissions.None, "X"));
            Assert.AreEqual("browse files on X", LinkStatus.DescribePermissions(Permissions.Browse, "X"));
            Assert.AreEqual("copy files from X", LinkStatus.DescribePermissions(Permissions.Read, "X"));
            Assert.AreEqual("copy files to X", LinkStatus.DescribePermissions(Permissions.Write, "X"));
        }

        [Test]
        public void StateChangedFiresOnlyWhenTheStatusActuallyChanges()
        {
            LinkStateMachine m = new LinkStateMachine();
            int count = 0;
            m.StateChanged += delegate { count++; };

            m.SetAdapters(Adapters(OperationalStatus.Up, "169.254.1.1"));
            Assert.AreEqual(1, count);

            m.SetAdapters(Adapters(OperationalStatus.Up, "169.254.1.2")); // a different address, same state
            Assert.AreEqual(1, count);

            m.SetPeer("ANA");
            Assert.AreEqual(2, count);

            m.SetPeer("ANA");
            Assert.AreEqual(2, count);

            m.SetPeer("ANA-RENAMED"); // same state, new text
            Assert.AreEqual(3, count);

            m.ClearError(LinkErrorKind.PortInUse); // nothing to clear
            Assert.AreEqual(3, count);
        }
    }
}
