using System.Collections.Generic;
using System.Net;

namespace EzLink
{
    /// <summary>
    /// Where discovery learns about adapters. Production uses <see cref="SystemAdapterSource"/>; tests and the
    /// debug-only loopback mode supply fakes so discovery can run without the cable.
    /// </summary>
    public interface IAdapterSource
    {
        /// <summary>The current candidate adapters, whether or not they are up.</summary>
        List<AdapterInfo> GetCandidates();

        /// <summary>
        /// True if the endpoint is one of this machine's own (used to ignore our own broadcasts and the beacons
        /// of this machine's other adapters).
        /// </summary>
        bool IsLocalEndpoint(IPEndPoint endpoint);
    }
}
