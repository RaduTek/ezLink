using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace EzLink
{
    /// <summary>One IPv4 unicast address of an adapter, with its subnet mask.</summary>
    public sealed class AdapterAddress
    {
        public readonly IPAddress Address;
        /// <summary>May be null if the system could not report the mask.</summary>
        public readonly IPAddress Mask;

        public AdapterAddress(IPAddress address, IPAddress mask)
        {
            Address = address;
            Mask = mask;
        }

        /// <summary>True for a real IPv4 address (not the 0.0.0.0 XP reports while it is still waiting for DHCP).</summary>
        public bool IsUsable
        {
            get
            {
                return Address != null
                    && Address.AddressFamily == AddressFamily.InterNetwork
                    && !Address.Equals(IPAddress.Any);
            }
        }

        public IPAddress BroadcastAddress
        {
            get { return IpMath.GetBroadcastAddress(Address, Mask); }
        }

        public override string ToString()
        {
            return Address + "/" + (Mask == null ? "?" : Mask.ToString());
        }
    }

    /// <summary>A network interface as seen at one moment: identity, operational status and IPv4 addresses.</summary>
    public sealed class AdapterInfo
    {
        /// <summary>The interface GUID ("{...}"), equal to NetworkInterface.Id and the registry's NetCfgInstanceId.</summary>
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        public readonly OperationalStatus Status;
        public readonly List<AdapterAddress> Addresses;
        /// <summary>The driver's MatchingDeviceId (e.g. "usb\vid_0bda&amp;pid_8152&amp;rev_2000"); null if not looked up.</summary>
        public readonly string MatchingDeviceId;

        public AdapterInfo(string id, string name, string description, OperationalStatus status,
            List<AdapterAddress> addresses, string matchingDeviceId)
        {
            Id = id;
            Name = name;
            Description = description;
            Status = status;
            Addresses = addresses == null ? new List<AdapterAddress>() : addresses;
            MatchingDeviceId = matchingDeviceId;
        }

        public bool IsUp
        {
            get { return Status == OperationalStatus.Up; }
        }

        /// <summary>The addresses discovery may use: the adapter is up and the address is a real IPv4 address.</summary>
        public List<AdapterAddress> UsableAddresses
        {
            get
            {
                List<AdapterAddress> list = new List<AdapterAddress>();
                if (IsUp)
                {
                    foreach (AdapterAddress a in Addresses)
                    {
                        if (a.IsUsable)
                        {
                            list.Add(a);
                        }
                    }
                }
                return list;
            }
        }

        public bool IsUsable
        {
            get { return UsableAddresses.Count > 0; }
        }

        public AdapterInfo WithMatchingDeviceId(string matchingDeviceId)
        {
            return new AdapterInfo(Id, Name, Description, Status, Addresses, matchingDeviceId);
        }

        /// <summary>A string that changes whenever anything a state machine would care about changes.</summary>
        public string Signature
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                sb.Append(Id).Append('|').Append(Status);
                foreach (AdapterAddress a in Addresses)
                {
                    sb.Append('|').Append(a);
                }
                return sb.ToString();
            }
        }

        public override string ToString()
        {
            return Name + " " + Id + " " + Status + " [" + string.Join(", ", AddressStrings()) + "]";
        }

        private string[] AddressStrings()
        {
            string[] result = new string[Addresses.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = Addresses[i].ToString();
            }
            return result;
        }
    }
}
