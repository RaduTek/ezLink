using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// The Options dialog: start at logon, this computer's name and ID, and the trusted computers list. Edits
    /// to the list are staged and applied only when OK is pressed. "Repair setup" arrives with the setup helper.
    /// </summary>
    internal sealed partial class OptionsForm : Form
    {
        private readonly TrustManager trust;
        private readonly Dictionary<Guid, TrustEntry> staged = new Dictionary<Guid, TrustEntry>();
        private readonly List<Guid> removed = new List<Guid>();

        public OptionsForm(Guid machineId, string computerName, TrustManager trust)
        {
            this.trust = trust;

            Font = SystemFonts.MessageBoxFont;
            InitializeComponent();

            Text = Strings.Get("OptionsTitle");
            startAtLogon.Text = Strings.Get("OptionsStartAtLogon");
            computerNameLabel.Text = Strings.Get("OptionsComputerName");
            computerIdLabel.Text = Strings.Get("OptionsComputerId");
            trustedGroup.Text = Strings.Get("OptionsTrusted");
            nameColumn.Text = Strings.Get("OptionsColName");
            accessColumn.Text = Strings.Get("OptionsColAccess");
            lastSeenColumn.Text = Strings.Get("OptionsColLastSeen");
            editButton.Text = Strings.Get("OptionsEdit");
            removeButton.Text = Strings.Get("OptionsRemove");
            okButton.Text = Strings.Get("ButtonOk");
            cancelButton.Text = Strings.Get("ButtonCancel");

            startAtLogon.Checked = StartupRegistration.IsEnabled;
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

        private void okButton_Click(object sender, EventArgs e)
        {
            ApplyTrustChanges();
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
