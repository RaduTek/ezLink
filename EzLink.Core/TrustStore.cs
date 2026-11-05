using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using EzLink.Protocol;

namespace EzLink
{
    /// <summary>What this machine has decided about a peer for good.</summary>
    public enum TrustDecision
    {
        AlwaysAllow = 1,
        Deny = 2
    }

    /// <summary>One peer in the trust store.</summary>
    public sealed class TrustEntry
    {
        /// <summary>Only these permission bits are editable by the user; the reserved ones are preserved.</summary>
        public const Permissions FileAccess = Permissions.Browse | Permissions.Read | Permissions.Write;

        public Guid PeerId;
        /// <summary>Last known computer name (display only).</summary>
        public string ComputerName = string.Empty;
        public Permissions Permissions = Permissions.None;
        public TrustDecision Decision = TrustDecision.AlwaysAllow;
        public DateTime FirstSeenUtc = DateTime.UtcNow;
        public DateTime LastSeenUtc = DateTime.UtcNow;

        public TrustEntry Clone()
        {
            return (TrustEntry)MemberwiseClone();
        }

        /// <summary>What the peer may do given this entry: nothing if it is blocked.</summary>
        public Permissions Effective
        {
            get { return Decision == TrustDecision.Deny ? Permissions.None : Permissions; }
        }

        /// <summary>Short text for the Options list: "Blocked", or "Browse, copy from, copy to".</summary>
        public string DescribeAccess()
        {
            if (Decision == TrustDecision.Deny)
            {
                return Strings.Get("AccessBlocked");
            }
            List<string> parts = new List<string>();
            if ((Permissions & Permissions.Browse) != 0)
            {
                parts.Add(Strings.Get("AccessBrowse"));
            }
            if ((Permissions & Permissions.Read) != 0)
            {
                parts.Add(Strings.Get("AccessRead"));
            }
            if ((Permissions & Permissions.Write) != 0)
            {
                parts.Add(Strings.Get("AccessWrite"));
            }
            if (parts.Count == 0)
            {
                return Strings.Get("AccessNone");
            }
            string text = string.Join(", ", parts.ToArray());
            return char.ToUpper(text[0], CultureInfo.CurrentCulture) + text.Substring(1);
        }
    }

    /// <summary>
    /// The per-machine list of peers that were allowed or blocked, kept in
    /// %ALLUSERSPROFILE%\ezLink\trust.xml. It is written by hand with an XmlWriter to a temporary file that then
    /// replaces the real one, so a crash cannot leave a half-written store. A file that cannot be parsed is set
    /// aside (never silently overwritten) and the store starts empty.
    /// </summary>
    public sealed class TrustStore
    {
        public const string FileName = "trust.xml";
        private const string RootName = "ezLinkTrust";
        private const string PeerName = "peer";
        private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        private readonly object sync = new object();
        private readonly string path;
        private readonly Dictionary<Guid, TrustEntry> entries = new Dictionary<Guid, TrustEntry>();

        /// <summary>Raised after the store changed (outside the store's lock).</summary>
        public event EventHandler Changed;

        public TrustStore(string path)
        {
            this.path = path;
            Load();
        }

        public string Path
        {
            get { return path; }
        }

        /// <summary>
        /// The store's normal location. It is the shared data folder if this user can write there, otherwise a
        /// per-user folder (with a warning), so a machine without the setup helper's work still functions.
        /// <paramref name="suffix"/> separates the files of debug instances running side by side.
        /// </summary>
        public static string DefaultPath(string suffix)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(FileName) + suffix
                + System.IO.Path.GetExtension(FileName);

            string shared = Environment.GetEnvironmentVariable("ALLUSERSPROFILE");
            if (!string.IsNullOrEmpty(shared))
            {
                string dir = System.IO.Path.Combine(shared, Constants.AppFolderName);
                if (CanWrite(dir))
                {
                    return System.IO.Path.Combine(dir, name);
                }
            }

            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(local))
            {
                local = System.IO.Path.GetTempPath();
            }
            Log.Warn("The shared data folder is not writable; keeping the trust store in the user profile");
            return System.IO.Path.Combine(System.IO.Path.Combine(local, Constants.AppFolderName), name);
        }

        private static bool CanWrite(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string probe = System.IO.Path.Combine(directory, "write-test-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>A copy of the entry for the peer, or null.</summary>
        public TrustEntry Get(Guid peerId)
        {
            lock (sync)
            {
                TrustEntry entry;
                return entries.TryGetValue(peerId, out entry) ? entry.Clone() : null;
            }
        }

        /// <summary>Copies of all entries, ordered by computer name.</summary>
        public List<TrustEntry> GetAll()
        {
            List<TrustEntry> list = new List<TrustEntry>();
            lock (sync)
            {
                foreach (TrustEntry entry in entries.Values)
                {
                    list.Add(entry.Clone());
                }
            }
            list.Sort(delegate(TrustEntry a, TrustEntry b)
            {
                return string.Compare(a.ComputerName, b.ComputerName, StringComparison.CurrentCultureIgnoreCase);
            });
            return list;
        }

        /// <summary>Adds or replaces the entry and saves.</summary>
        public void Set(TrustEntry entry)
        {
            lock (sync)
            {
                entries[entry.PeerId] = entry.Clone();
                Save();
            }
            RaiseChanged();
        }

        /// <summary>Removes the entry (if present) and saves. Returns whether there was one.</summary>
        public bool Remove(Guid peerId)
        {
            bool removed;
            lock (sync)
            {
                removed = entries.Remove(peerId);
                if (removed)
                {
                    Save();
                }
            }
            if (removed)
            {
                RaiseChanged();
            }
            return removed;
        }

        /// <summary>
        /// Records that the peer was seen now, and refreshes its name. Saves only when the name changed or the
        /// last-seen time is more than a minute old, so frequent sessions do not rewrite the file each time.
        /// </summary>
        public void Touch(Guid peerId, string computerName)
        {
            lock (sync)
            {
                TrustEntry entry;
                if (!entries.TryGetValue(peerId, out entry))
                {
                    return;
                }
                DateTime now = DateTime.UtcNow;
                bool nameChanged = !string.IsNullOrEmpty(computerName) && entry.ComputerName != computerName;
                if (!nameChanged && now - entry.LastSeenUtc < TimeSpan.FromMinutes(1))
                {
                    return;
                }
                if (nameChanged)
                {
                    entry.ComputerName = computerName;
                }
                entry.LastSeenUtc = now;
                Save();
            }
        }

        private void RaiseChanged()
        {
            EventHandler handler = Changed;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        // ---- persistence ----

        private void Load()
        {
            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                XmlDocument document = new XmlDocument();
                document.Load(path);
                XmlElement root = document.DocumentElement;
                if (root == null || root.Name != RootName)
                {
                    throw new XmlException("Unexpected root element");
                }
                Dictionary<Guid, TrustEntry> loaded = new Dictionary<Guid, TrustEntry>();
                foreach (XmlNode node in root.ChildNodes)
                {
                    XmlElement element = node as XmlElement;
                    if (element == null || element.Name != PeerName)
                    {
                        continue;
                    }
                    TrustEntry entry = ReadEntry(element);
                    loaded[entry.PeerId] = entry;
                }
                foreach (KeyValuePair<Guid, TrustEntry> pair in loaded)
                {
                    entries[pair.Key] = pair.Value;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is XmlException) && !(ex is FormatException) && !(ex is OverflowException)
                    && !(ex is ArgumentException) && !(ex is IOException))
                {
                    throw;
                }
                entries.Clear();
                SetAside(ex);
            }
        }

        private static TrustEntry ReadEntry(XmlElement element)
        {
            TrustEntry entry = new TrustEntry();
            entry.PeerId = new Guid(element.GetAttribute("id"));
            entry.ComputerName = element.GetAttribute("name");
            entry.Permissions = (Permissions)uint.Parse(element.GetAttribute("permissions"), CultureInfo.InvariantCulture);
            entry.Decision = (TrustDecision)Enum.Parse(typeof(TrustDecision), element.GetAttribute("decision"));
            if (!Enum.IsDefined(typeof(TrustDecision), entry.Decision))
            {
                throw new FormatException("Unknown decision");
            }
            entry.FirstSeenUtc = ParseDate(element.GetAttribute("firstSeen"));
            entry.LastSeenUtc = ParseDate(element.GetAttribute("lastSeen"));
            return entry;
        }

        private static DateTime ParseDate(string text)
        {
            return DateTime.ParseExact(text, DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        }

        /// <summary>Renames an unreadable file so it is kept for inspection and a fresh store can be written.</summary>
        private void SetAside(Exception reason)
        {
            string aside = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            Log.Error("The trust store " + path + " is unreadable; starting empty and keeping the old file as " + aside, reason);
            try
            {
                File.Move(path, aside);
            }
            catch (Exception ex)
            {
                Log.Error("Could not set the unreadable trust store aside", ex);
            }
        }

        /// <summary>Writes the store; the caller holds the lock. Failures are logged, the in-memory state stays valid.</summary>
        private void Save()
        {
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = new UTF8Encoding(false);
                using (XmlWriter writer = XmlWriter.Create(temp, settings))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement(RootName);
                    writer.WriteAttributeString("version", "1");
                    foreach (TrustEntry entry in entries.Values)
                    {
                        writer.WriteStartElement(PeerName);
                        writer.WriteAttributeString("id", entry.PeerId.ToString("D"));
                        writer.WriteAttributeString("name", entry.ComputerName);
                        writer.WriteAttributeString("permissions",
                            ((uint)entry.Permissions).ToString(CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("decision", entry.Decision.ToString());
                        writer.WriteAttributeString("firstSeen", entry.FirstSeenUtc.ToString(DateFormat, CultureInfo.InvariantCulture));
                        writer.WriteAttributeString("lastSeen", entry.LastSeenUtc.ToString(DateFormat, CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }

                if (File.Exists(path))
                {
                    File.Replace(temp, path, null);
                }
                else
                {
                    File.Move(temp, path);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not save the trust store " + path, ex);
                try
                {
                    File.Delete(temp);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
