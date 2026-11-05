using System;
using System.Net;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// The command line: /peer:&lt;ip&gt; /port:&lt;port&gt; /uuid:&lt;peer uuid&gt; /name:&lt;computer name&gt;
    /// /main:&lt;ezLink IPC window handle&gt;. Two optional extras let a debug loopback instance say who it is:
    /// /self:&lt;this computer's uuid&gt; and /selfname:&lt;this computer's name&gt;.
    /// </summary>
    internal sealed class ExplorerArgs
    {
        public IPAddress PeerAddress;
        public ushort PeerPort = Constants.SessionPort;
        public Guid PeerId = Guid.Empty;
        public string PeerName = string.Empty;
        public IntPtr MainWindow = IntPtr.Zero;
        public Guid SelfId = Guid.Empty;
        public string SelfName = string.Empty;

        /// <summary>Returns null if the mandatory arguments are missing or malformed.</summary>
        public static ExplorerArgs Parse(string[] args)
        {
            ExplorerArgs result = new ExplorerArgs();
            foreach (string arg in args)
            {
                if (arg.Length < 2 || arg[0] != '/')
                {
                    continue;
                }
                int colon = arg.IndexOf(':');
                if (colon < 0)
                {
                    continue;
                }
                string key = arg.Substring(1, colon - 1).ToLowerInvariant();
                string value = arg.Substring(colon + 1);
                try
                {
                    switch (key)
                    {
                        case "peer":
                            result.PeerAddress = IPAddress.Parse(value);
                            break;
                        case "port":
                            result.PeerPort = ushort.Parse(value);
                            break;
                        case "uuid":
                            result.PeerId = new Guid(value);
                            break;
                        case "name":
                            result.PeerName = value;
                            break;
                        case "main":
                            result.MainWindow = new IntPtr(long.Parse(value));
                            break;
                        case "self":
                            result.SelfId = new Guid(value);
                            break;
                        case "selfname":
                            result.SelfName = value;
                            break;
                    }
                }
                catch (FormatException)
                {
                    return null;
                }
                catch (OverflowException)
                {
                    return null;
                }
            }

            if (result.PeerAddress == null || result.PeerId == Guid.Empty)
            {
                return null;
            }
            if (result.PeerName.Length == 0)
            {
                result.PeerName = result.PeerAddress.ToString();
            }
            if (result.SelfId == Guid.Empty)
            {
                result.SelfId = MachineIdentity.GetOrCreate();
            }
            if (result.SelfName.Length == 0)
            {
                result.SelfName = Environment.MachineName;
            }
            return result;
        }
    }
}
