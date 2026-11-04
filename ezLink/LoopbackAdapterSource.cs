#if DEBUG
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// Debug-only (compiled out of Release builds): lets two ezLink.exe instances on one PC find each other on
    /// 127.0.0.1 with different UDP ports and UUIDs. Start them with /loopback-peer:1 and /loopback-peer:2.
    /// </summary>
    internal sealed class LoopbackAdapterSource : IAdapterSource
    {
        public const string Switch = "/loopback-peer:";

        private const int PortOne = 47770;
        private const int PortTwo = 47772;

        private readonly int localPort;

        public LoopbackAdapterSource(int index)
        {
            localPort = PortFor(index);
        }

        /// <summary>Returns 1 or 2 if the arguments contain /loopback-peer:1 or :2, otherwise 0.</summary>
        public static int Parse(string[] args)
        {
            foreach (string arg in args)
            {
                if (arg.StartsWith(Switch, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg.Substring(Switch.Length);
                    if (value == "1") return 1;
                    if (value == "2") return 2;
                }
            }
            return 0;
        }

        public static int PortFor(int index)
        {
            return index == 1 ? PortOne : PortTwo;
        }

        public static int OtherPortFor(int index)
        {
            return index == 1 ? PortTwo : PortOne;
        }

        public static Guid IdFor(int index)
        {
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)index);
        }

        public List<AdapterInfo> GetCandidates()
        {
            List<AdapterAddress> addresses = new List<AdapterAddress>();
            // A /32 mask makes the "broadcast" address the loopback address itself.
            addresses.Add(new AdapterAddress(IPAddress.Loopback, IPAddress.Parse("255.255.255.255")));
            List<AdapterInfo> list = new List<AdapterInfo>();
            list.Add(new AdapterInfo("{LOOPBACK-" + localPort + "}", "Loopback peer", "Debug loopback",
                OperationalStatus.Up, addresses, @"usb\vid_0bda&pid_8152"));
            return list;
        }

        public bool IsLocalEndpoint(IPEndPoint endpoint)
        {
            return endpoint.Address.Equals(IPAddress.Loopback) && endpoint.Port == localPort;
        }
    }
}
#endif
