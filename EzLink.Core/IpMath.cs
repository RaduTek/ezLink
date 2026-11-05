using System;
using System.Net;

namespace EzLink
{
    /// <summary>IPv4 helpers.</summary>
    public static class IpMath
    {
        /// <summary>
        /// The subnet broadcast address: the address with every host bit set. If the mask is unknown
        /// a /16 is assumed for link-local (169.254.x.x) addresses and a /24 otherwise.
        /// </summary>
        public static IPAddress GetBroadcastAddress(IPAddress address, IPAddress mask)
        {
            byte[] a = address.GetAddressBytes();
            byte[] m = mask != null ? mask.GetAddressBytes() : DefaultMask(a);
            byte[] result = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                result[i] = (byte)(a[i] | ~m[i]);
            }
            return new IPAddress(result);
        }

        /// <summary>True if both addresses are IPv4 and lie in the same subnet under the mask (null mask: same defaults as above).</summary>
        public static bool IsInSubnet(IPAddress address, IPAddress network, IPAddress mask)
        {
            if (address == null || network == null
                || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork
                || network.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return false;
            }
            byte[] a = address.GetAddressBytes();
            byte[] n = network.GetAddressBytes();
            byte[] m = mask != null ? mask.GetAddressBytes() : DefaultMask(n);
            for (int i = 0; i < 4; i++)
            {
                if ((a[i] & m[i]) != (n[i] & m[i]))
                {
                    return false;
                }
            }
            return true;
        }

        private static byte[] DefaultMask(byte[] address)
        {
            bool linkLocal = address[0] == 169 && address[1] == 254;
            return linkLocal ? new byte[] { 255, 255, 0, 0 } : new byte[] { 255, 255, 255, 0 };
        }
    }
}
