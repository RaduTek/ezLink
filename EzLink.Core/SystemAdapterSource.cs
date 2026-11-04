using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EzLink
{
    /// <summary>The real adapters of this machine.</summary>
    public sealed class SystemAdapterSource : IAdapterSource
    {
        private readonly object sync = new object();
        private List<IPAddress> localAddresses = new List<IPAddress>();

        public List<AdapterInfo> GetCandidates()
        {
            RefreshLocalAddresses();
            return AdapterLocator.FindCandidates();
        }

        /// <summary>Any IPv4 address of any interface of this machine counts as local, whatever the port.</summary>
        public bool IsLocalEndpoint(IPEndPoint endpoint)
        {
            lock (sync)
            {
                if (localAddresses.Contains(endpoint.Address))
                {
                    return true;
                }
            }
            // A just-assigned address may not be cached yet; look again before declaring it foreign.
            RefreshLocalAddresses();
            lock (sync)
            {
                return localAddresses.Contains(endpoint.Address);
            }
        }

        private void RefreshLocalAddresses()
        {
            List<IPAddress> list = new List<IPAddress>();
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            list.Add(info.Address);
                        }
                    }
                }
                catch (NetworkInformationException)
                {
                }
            }
            lock (sync)
            {
                localAddresses = list;
            }
        }
    }
}
