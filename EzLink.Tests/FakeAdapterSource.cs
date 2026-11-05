using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;

namespace EzLink.Tests
{
    /// <summary>
    /// A scriptable adapter list on 127.0.0.1 for tests (production code never uses loopback). A /32 mask makes the
    /// "broadcast" address the loopback address itself. "Local" endpoints are the loopback ports given.
    /// </summary>
    internal sealed class FakeAdapterSource : IAdapterSource
    {
        private readonly object sync = new object();
        private readonly List<int> ownPorts = new List<int>();
        private AdapterInfo adapter;

        public FakeAdapterSource(int ownPort, OperationalStatus status)
        {
            ownPorts.Add(ownPort);
            SetStatus(status);
        }

        public void SetStatus(OperationalStatus status)
        {
            List<AdapterAddress> addresses = new List<AdapterAddress>();
            addresses.Add(new AdapterAddress(IPAddress.Loopback, IPAddress.Parse("255.255.255.255")));
            lock (sync)
            {
                adapter = new AdapterInfo("{FAKE-LOOP}", "Fake loopback", "Fake", status, addresses, "usb\\vid_0bda&pid_8152");
            }
        }

        /// <summary>Simulates unplugging the cable: the adapter disappears.</summary>
        public void RemoveAdapter()
        {
            lock (sync)
            {
                adapter = null;
            }
        }

        /// <summary>Treat beacons from this port as coming from this machine.</summary>
        public void AddOwnPort(int port)
        {
            lock (sync)
            {
                ownPorts.Add(port);
            }
        }

        public List<AdapterInfo> GetCandidates()
        {
            lock (sync)
            {
                List<AdapterInfo> list = new List<AdapterInfo>();
                if (adapter != null)
                {
                    list.Add(adapter);
                }
                return list;
            }
        }

        public bool IsLocalEndpoint(IPEndPoint endpoint)
        {
            lock (sync)
            {
                return endpoint.Address.Equals(IPAddress.Loopback) && ownPorts.Contains(endpoint.Port);
            }
        }
    }
}
