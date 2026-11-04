using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using Microsoft.Win32;

namespace EzLink
{
    /// <summary>
    /// Finds candidate ezLink adapters: network interfaces whose driver's MatchingDeviceId starts with the
    /// RTL8152 hardware ID. A candidate only becomes "the link" once a peer answers a beacon on it, and
    /// nothing here ever changes an adapter's configuration.
    /// </summary>
    public static class AdapterLocator
    {
        public const string NetworkClassKey =
            @"SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}";

        /// <summary>Candidates among the interfaces currently present, with status and IPv4 addresses.</summary>
        public static List<AdapterInfo> FindCandidates()
        {
            return SelectCandidates(ReadDeviceIds(), Snapshot(NetworkInterface.GetAllNetworkInterfaces()));
        }

        /// <summary>True if the (case-insensitive) hardware ID starts with the ezLink adapter prefix; a "&amp;rev_xxxx" suffix is fine.</summary>
        public static bool IsCandidateDeviceId(string matchingDeviceId)
        {
            return !string.IsNullOrEmpty(matchingDeviceId)
                && matchingDeviceId.ToLowerInvariant().StartsWith(Constants.AdapterHardwareIdPrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Joins interfaces with the registry's device IDs on the interface GUID (case-insensitive) and keeps
        /// those whose device ID matches. Interfaces with no registry entry are never candidates.
        /// </summary>
        public static List<AdapterInfo> SelectCandidates(IDictionary<string, string> deviceIdByInterfaceId,
            IList<AdapterInfo> interfaces)
        {
            Dictionary<string, string> ids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> pair in deviceIdByInterfaceId)
            {
                ids[pair.Key] = pair.Value;
            }

            List<AdapterInfo> candidates = new List<AdapterInfo>();
            foreach (AdapterInfo nic in interfaces)
            {
                string deviceId;
                if (nic.Id != null && ids.TryGetValue(nic.Id, out deviceId) && IsCandidateDeviceId(deviceId))
                {
                    candidates.Add(nic.WithMatchingDeviceId(deviceId));
                }
            }
            return candidates;
        }

        /// <summary>Converts system interfaces to <see cref="AdapterInfo"/> (IPv4 unicast addresses only).</summary>
        public static List<AdapterInfo> Snapshot(NetworkInterface[] interfaces)
        {
            List<AdapterInfo> list = new List<AdapterInfo>();
            foreach (NetworkInterface nic in interfaces)
            {
                List<AdapterAddress> addresses = new List<AdapterAddress>();
                try
                {
                    foreach (UnicastIPAddressInformation info in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (info.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            addresses.Add(new AdapterAddress(info.Address, ReadMask(info)));
                        }
                    }
                }
                catch (NetworkInformationException ex)
                {
                    Log.Warn("Could not read addresses of {0}: {1}", nic.Name, ex.Message);
                }
                list.Add(new AdapterInfo(nic.Id, nic.Name, nic.Description, nic.OperationalStatus, addresses, null));
            }
            return list;
        }

        private static IPAddress ReadMask(UnicastIPAddressInformation info)
        {
            try
            {
                return info.IPv4Mask;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }

        /// <summary>Reads NetCfgInstanceId -> MatchingDeviceId for every network-class driver key.</summary>
        public static Dictionary<string, string> ReadDeviceIds()
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (RegistryKey classKey = Registry.LocalMachine.OpenSubKey(NetworkClassKey))
            {
                if (classKey == null)
                {
                    Log.Warn("Network class registry key not found");
                    return map;
                }
                foreach (string name in classKey.GetSubKeyNames())
                {
                    try
                    {
                        using (RegistryKey sub = classKey.OpenSubKey(name))
                        {
                            if (sub == null)
                            {
                                continue;
                            }
                            string instanceId = sub.GetValue("NetCfgInstanceId") as string;
                            string deviceId = sub.GetValue("MatchingDeviceId") as string;
                            if (!string.IsNullOrEmpty(instanceId) && !string.IsNullOrEmpty(deviceId))
                            {
                                map[instanceId] = deviceId;
                            }
                        }
                    }
                    catch (SecurityException)
                    {
                        // The "Properties" subkey (and any other protected key) is not readable.
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                    catch (IOException)
                    {
                    }
                }
            }
            return map;
        }
    }
}
