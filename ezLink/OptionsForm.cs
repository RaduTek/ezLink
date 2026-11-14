using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// The Options dialog: start at logon, security and file sharing settings, the trusted computers list and this
    /// computer's name and ID. Edits to the list are staged and applied only when OK is pressed.
    /// </summary>
    internal sealed partial class OptionsForm : Form
    {
        private readonly TrustManager trust;
        private readonly SharingSettings settings;
        private readonly Dictionary<Guid, TrustEntry> staged = new Dictionary<Guid, TrustEntry>();
        private readonly List<Guid> removed = new List<Guid>();
        private List<SharedFolder> sharedStaged = new List<SharedFolder>();

        public OptionsForm(Guid machineId, string computerName, TrustManager trust)
        {
            this.trust = trust;
            settings = trust != null ? trust.Settings : new SharingSettings();

            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("OptionsTitle");
            startAtLogon.Text = Strings.Get("OptionsStartAtLogon");
            computerNameLabel.Text = Strings.Get("OptionsComputerName");
            computerIdLabel.Text = Strings.Get("OptionsComputerId");
            generalTab.Text = Strings.Get("OptionsTabGeneral");
            securityTab.Text = Strings.Get("OptionsTabSecurity");
            trustedDevicesTab.Text = Strings.Get("OptionsTabTrustedDevices");
            fileSharingTab.Text = Strings.Get("OptionsTabFileSharing");
            informationTab.Text = Strings.Get("OptionsTabInformation");
            connectionActionsGroup.Text = Strings.Get("OptionsGroupConnectionActions");
            showWindowOnConnect.Text = Strings.Get("OptionsShowWindowOnConnect");
            openExplorerOnConnect.Text = Strings.Get("OptionsOpenExplorerOnConnect");
            startSyncOnConnect.Text = Strings.Get("OptionsStartSyncOnConnect");
            allowAll.Text = Strings.Get("OptionsAllowAll");
            new ToolTip().SetToolTip(allowAll, Strings.Get("OptionsAllowAllNote"));
            fileTransferGroup.Text = Strings.Get("OptionsGroupFileTransfer");
            browseCopy.Text = Strings.Get("ApprovalBrowseCopy");
            write.Text = Strings.Get("ApprovalWrite");
            showHidden.Text = Strings.Get("OptionsSeeHiddenSystem");
            protectSystem.Text = Strings.Get("OptionsProtectSystem");
            trustedHint.Text = Strings.Get("OptionsTrustedHint");
            shareProfile.Text = Strings.Get("OptionsShareProfile");
            shareFixed.Text = Strings.Get("OptionsShareFixed");
            shareRemovable.Text = Strings.Get("OptionsShareRemovable");
            fixedReadOnly.Text = Strings.Get("OptionsReadOnly");
            removableReadOnly.Text = Strings.Get("OptionsReadOnly");
            sharedFoldersLabel.Text = Strings.Get("OptionsSharedFolders");
            sharedNameColumn.Text = Strings.Get("OptionsColShareName");
            sharedPathColumn.Text = Strings.Get("OptionsColSharePath");
            sharedReadOnlyColumn.Text = Strings.Get("OptionsColShareReadOnly");
            addFolderButton.Text = Strings.Get("OptionsAddFolder");
            addDriveButton.Text = Strings.Get("OptionsAddDrive");
            toggleReadOnlyButton.Text = Strings.Get("OptionsToggleReadOnly");
            removeShareButton.Text = Strings.Get("OptionsRemove");
            nameColumn.Text = Strings.Get("OptionsColName");
            accessColumn.Text = Strings.Get("OptionsColAccess");
            lastSeenColumn.Text = Strings.Get("OptionsColLastSeen");
            editButton.Text = Strings.Get("OptionsEdit");
            removeButton.Text = Strings.Get("OptionsRemove");
            okButton.Text = Strings.Get("ButtonOk");
            cancelButton.Text = Strings.Get("ButtonCancel");

            startAtLogon.Checked = StartupRegistration.IsEnabled;
            allowAll.Checked = settings.AllowAllConnections;
            browseCopy.Checked = settings.AllowBrowseCopy;
            write.Checked = settings.AllowWrite;
            showHidden.Checked = settings.ShowHiddenAndSystem;
            protectSystem.Checked = settings.ProtectSystemFiles;
            shareProfile.Checked = settings.ShareUserProfile;
            shareFixed.Checked = settings.ShareFixedDrives;
            shareRemovable.Checked = settings.ShareRemovableDrives;
            showWindowOnConnect.Checked = settings.ShowWindowOnConnect;
            openExplorerOnConnect.Checked = settings.OpenExplorerOnConnect;
            fixedReadOnly.Checked = settings.FixedDrivesReadOnly;
            removableReadOnly.Checked = settings.RemovableDrivesReadOnly;
            sharedStaged = settings.GetFolders();
            RefreshShares(-1);
            computerNameBox.Text = computerName;
            computerIdBox.Text = machineId.ToString("D").ToUpperInvariant();

            if (trust != null)
            {
                foreach (TrustEntry entry in trust.Store.GetAll())
                {
                    staged[entry.PeerId] = entry;
                }
            }
            RefreshList(Guid.Empty);
        }

        private void trustedList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateButtons();
        }

        private void trustedList_DoubleClick(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void editButton_Click(object sender, EventArgs e)
        {
            EditSelected();
        }

        private void removeButton_Click(object sender, EventArgs e)
        {
            RemoveSelected();
        }

        private void RefreshList(Guid select)
        {
            List<TrustEntry> entries = new List<TrustEntry>(staged.Values);
            entries.Sort(delegate(TrustEntry a, TrustEntry b)
            {
                return string.Compare(a.ComputerName, b.ComputerName, StringComparison.CurrentCultureIgnoreCase);
            });

            trustedList.BeginUpdate();
            trustedList.Items.Clear();
            foreach (TrustEntry entry in entries)
            {
                ListViewItem item = new ListViewItem(entry.ComputerName);
                item.SubItems.Add(entry.DescribeAccess());
                item.SubItems.Add(entry.LastSeenUtc.ToLocalTime().ToString("g"));
                item.Tag = entry;
                item.Selected = entry.PeerId == select;
                trustedList.Items.Add(item);
            }
            trustedList.EndUpdate();
            UpdateButtons();
        }

        private TrustEntry Selected
        {
            get
            {
                return trustedList.SelectedItems.Count == 0 ? null : (TrustEntry)trustedList.SelectedItems[0].Tag;
            }
        }

        private void UpdateButtons()
        {
            TrustEntry entry = Selected;
            editButton.Enabled = entry != null;
            removeButton.Enabled = entry != null;
            removeButton.Text = entry != null && entry.Decision == TrustDecision.Deny
                ? Strings.Get("OptionsUnblock") : Strings.Get("OptionsRemove");
        }

        private void EditSelected()
        {
            TrustEntry entry = Selected;
            if (entry == null)
            {
                return;
            }
            TrustEntry copy = entry.Clone();
            using (TrustEntryForm form = new TrustEntryForm(copy))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    staged[copy.PeerId] = copy;
                    RefreshList(copy.PeerId);
                }
            }
        }

        /// <summary>Removing an entry (or unblocking a blocked one) makes the next session from it ask again.</summary>
        private void RemoveSelected()
        {
            TrustEntry entry = Selected;
            if (entry == null)
            {
                return;
            }
            staged.Remove(entry.PeerId);
            removed.Add(entry.PeerId);
            RefreshList(Guid.Empty);
        }

        private void ApplyTrustChanges()
        {
            if (trust == null)
            {
                return;
            }
            foreach (Guid id in removed)
            {
                trust.RemoveEntry(id);
            }
            foreach (TrustEntry entry in staged.Values)
            {
                TrustEntry current = trust.Store.Get(entry.PeerId);
                if (current == null || current.Decision != entry.Decision || current.Permissions != entry.Permissions)
                {
                    TrustEntry update = current != null ? current : entry;
                    update.Decision = entry.Decision;
                    update.Permissions = entry.Permissions;
                    trust.SetEntry(update);
                }
            }
        }

        /// <summary>Copies the checkboxes into the live settings (so they apply at once) and saves them.</summary>
        private void ApplySettings()
        {
            settings.AllowAllConnections = allowAll.Checked;
            settings.AllowBrowseCopy = browseCopy.Checked;
            settings.AllowWrite = write.Checked;
            settings.ShowHiddenAndSystem = showHidden.Checked;
            settings.ProtectSystemFiles = protectSystem.Checked;
            settings.ShareUserProfile = shareProfile.Checked;
            settings.ShareFixedDrives = shareFixed.Checked;
            settings.ShareRemovableDrives = shareRemovable.Checked;
            settings.ShowWindowOnConnect = showWindowOnConnect.Checked;
            settings.OpenExplorerOnConnect = openExplorerOnConnect.Checked;
            settings.FixedDrivesReadOnly = fixedReadOnly.Checked;
            settings.RemovableDrivesReadOnly = removableReadOnly.Checked;
            settings.SetFolders(sharedStaged);
            settings.Save();
        }

        // ---- shared folders and drives ----

        private void RefreshShares(int select)
        {
            sharedList.BeginUpdate();
            sharedList.Items.Clear();
            for (int i = 0; i < sharedStaged.Count; i++)
            {
                SharedFolder folder = sharedStaged[i];
                ListViewItem item = new ListViewItem(folder.Name);
                item.SubItems.Add(folder.Path);
                item.SubItems.Add(folder.ReadOnly ? Strings.Get("OptionsReadOnly") : string.Empty);
                item.Selected = i == select;
                sharedList.Items.Add(item);
            }
            sharedList.EndUpdate();
            UpdateShareButtons();
        }

        private void UpdateShareButtons()
        {
            bool any = sharedList.SelectedIndices.Count == 1;
            toggleReadOnlyButton.Enabled = any;
            removeShareButton.Enabled = any;
        }

        private void sharedList_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateShareButtons();
        }

        private void addFolderButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = Strings.Get("OptionsShareFolderPrompt");
                dialog.ShowNewFolderButton = false;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    TryAddShare(dialog.SelectedPath);
                }
            }
        }

        /// <summary>Offers the fixed and removable drives that are not in the list yet.</summary>
        private void addDriveButton_Click(object sender, EventArgs e)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)
                {
                    continue;
                }
                string root = drive.Name;
                if (FindShare(root) >= 0)
                {
                    continue;
                }
                string label = string.Empty;
                try
                {
                    label = drive.IsReady ? drive.VolumeLabel : string.Empty;
                }
                catch (IOException)
                {
                }
                ToolStripItem item = menu.Items.Add(label.Length > 0 ? root + "  (" + label + ")" : root);
                item.Tag = root;
                item.Click += delegate(object s, EventArgs args)
                {
                    TryAddShare((string)((ToolStripItem)s).Tag);
                };
            }
            if (menu.Items.Count == 0)
            {
                MessageBox.Show(this, Strings.Get("OptionsShareNoDrives"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            menu.Show(addDriveButton, new Point(0, addDriveButton.Height));
        }

        private void toggleReadOnlyButton_Click(object sender, EventArgs e)
        {
            if (sharedList.SelectedIndices.Count != 1)
            {
                return;
            }
            int index = sharedList.SelectedIndices[0];
            sharedStaged[index].ReadOnly = !sharedStaged[index].ReadOnly;
            RefreshShares(index);
        }

        private void removeShareButton_Click(object sender, EventArgs e)
        {
            if (sharedList.SelectedIndices.Count != 1)
            {
                return;
            }
            sharedStaged.RemoveAt(sharedList.SelectedIndices[0]);
            RefreshShares(-1);
        }

        private int FindShare(string path)
        {
            for (int i = 0; i < sharedStaged.Count; i++)
            {
                if (string.Equals(sharedStaged[i].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            return -1;
        }

        private void TryAddShare(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }
            if (!SharedFolder.IsDriveRootPath(path))
            {
                path = path.TrimEnd('\\');
            }
            if (FindShare(path) >= 0)
            {
                MessageBox.Show(this, Strings.Get("OptionsShareAlreadyShared"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshShares(FindShare(path));
                return;
            }
            sharedStaged.Add(new SharedFolder(path, false));
            string clash = FindNameClash();
            if (clash != null)
            {
                sharedStaged.RemoveAt(sharedStaged.Count - 1);
                MessageBox.Show(this, string.Format(Strings.Get("OptionsShareNameClash"), clash), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RefreshShares(sharedStaged.Count - 1);
        }

        /// <summary>
        /// The first share name used twice by different folders (profile folder, enabled drive boxes and the list),
        /// or null. Names are how the other computer finds a share, so each must be unique, ignoring case.
        /// </summary>
        private string FindNameClash()
        {
            Dictionary<string, string> owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (shareProfile.Checked)
            {
                owners[PathPolicy.ProfileName] = PathPolicy.ProfileFolder;
            }
            foreach (SharedFolder folder in sharedStaged)
            {
                string known;
                if (owners.TryGetValue(folder.Name, out known)
                    && !string.Equals(known, folder.Path, StringComparison.OrdinalIgnoreCase))
                {
                    return folder.Name;
                }
                owners[folder.Name] = folder.Path;
            }
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                bool shared = (drive.DriveType == DriveType.Fixed && shareFixed.Checked)
                    || (drive.DriveType == DriveType.Removable && shareRemovable.Checked);
                if (!shared)
                {
                    continue;
                }
                string name = SharedFolder.NameOf(drive.Name);
                string known;
                if (owners.TryGetValue(name, out known)
                    && !string.Equals(known, drive.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return name;
                }
            }
            return null;
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            string clash = FindNameClash();
            if (clash != null)
            {
                // Keep the dialog open: the checkboxes may have made two shares collide.
                MessageBox.Show(this, string.Format(Strings.Get("OptionsShareNameClash"), clash), Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            ApplyTrustChanges();
            ApplySettings();
            try
            {
                StartupRegistration.Set(startAtLogon.Checked);
            }
            catch (Exception ex)
            {
                Log.Error("Could not update the start-at-logon setting", ex);
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
