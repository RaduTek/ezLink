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
    internal sealed class OptionsForm : Form
    {
        private readonly CheckBox startAtLogon = new CheckBox();
        private readonly TrustManager trust;
        private readonly ListView trustedList = new ListView();
        private readonly Button editButton = new Button();
        private readonly Button removeButton = new Button();
        private readonly Dictionary<Guid, TrustEntry> staged = new Dictionary<Guid, TrustEntry>();
        private readonly List<Guid> removed = new List<Guid>();

        public OptionsForm(Guid machineId, string computerName, TrustManager trust)
        {
            this.trust = trust;
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Strings.Get("OptionsTitle");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 360);

            startAtLogon.Text = Strings.Get("OptionsStartAtLogon");
            startAtLogon.AutoSize = true;
            startAtLogon.Location = new Point(14, 14);
            startAtLogon.Checked = StartupRegistration.IsEnabled;
            Controls.Add(startAtLogon);

            AddReadOnlyField(Strings.Get("OptionsComputerName"), computerName, 48);
            AddReadOnlyField(Strings.Get("OptionsComputerId"), machineId.ToString("D").ToUpperInvariant(), 78);

            BuildTrustedList();

            Button ok = new Button();
            ok.Text = Strings.Get("ButtonOk");
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(250, 320);
            ok.Size = new Size(75, 26);
            ok.Click += OnOk;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = Strings.Get("ButtonCancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(331, 320);
            cancel.Size = new Size(75, 26);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void BuildTrustedList()
        {
            GroupBox group = new GroupBox();
            group.Text = Strings.Get("OptionsTrusted");
            group.Location = new Point(14, 110);
            group.Size = new Size(392, 196);
            Controls.Add(group);

            trustedList.View = View.Details;
            trustedList.FullRowSelect = true;
            trustedList.HideSelection = false;
            trustedList.MultiSelect = false;
            trustedList.Location = new Point(10, 22);
            trustedList.Size = new Size(372, 128);
            trustedList.Columns.Add(Strings.Get("OptionsColName"), 110);
            trustedList.Columns.Add(Strings.Get("OptionsColAccess"), 150);
            trustedList.Columns.Add(Strings.Get("OptionsColLastSeen"), 96);
            trustedList.SelectedIndexChanged += delegate { UpdateButtons(); };
            trustedList.DoubleClick += delegate { EditSelected(); };
            group.Controls.Add(trustedList);

            editButton.Text = Strings.Get("OptionsEdit");
            editButton.Location = new Point(226, 158);
            editButton.Size = new Size(75, 26);
            editButton.Click += delegate { EditSelected(); };
            group.Controls.Add(editButton);

            removeButton.Text = Strings.Get("OptionsRemove");
            removeButton.Location = new Point(307, 158);
            removeButton.Size = new Size(75, 26);
            removeButton.Click += delegate { RemoveSelected(); };
            group.Controls.Add(removeButton);

            if (trust != null)
            {
                foreach (TrustEntry entry in trust.Store.GetAll())
                {
                    staged[entry.PeerId] = entry;
                }
            }
            RefreshList(Guid.Empty);
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

        private void AddReadOnlyField(string caption, string value, int top)
        {
            Label label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Location = new Point(14, top + 3);
            Controls.Add(label);

            TextBox box = new TextBox();
            box.Text = value;
            box.ReadOnly = true;
            box.Location = new Point(160, top);
            box.Size = new Size(246, 23);
            Controls.Add(box);
        }

        private void OnOk(object sender, EventArgs e)
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
