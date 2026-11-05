using System;
using System.Drawing;
using System.Windows.Forms;
using EzLink;

namespace ezLink
{
    /// <summary>
    /// The Options dialog. This milestone has start-at-logon and this computer's name and ID; the trusted
    /// computers list and "Repair setup" arrive with the trust and setup-helper milestones.
    /// </summary>
    internal sealed class OptionsForm : Form
    {
        private readonly CheckBox startAtLogon = new CheckBox();

        public OptionsForm(Guid machineId, string computerName)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = SystemFonts.MessageBoxFont;
            Text = Strings.Get("OptionsTitle");
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(420, 170);

            startAtLogon.Text = Strings.Get("OptionsStartAtLogon");
            startAtLogon.AutoSize = true;
            startAtLogon.Location = new Point(14, 14);
            startAtLogon.Checked = StartupRegistration.IsEnabled;
            Controls.Add(startAtLogon);

            AddReadOnlyField(Strings.Get("OptionsComputerName"), computerName, 48);
            AddReadOnlyField(Strings.Get("OptionsComputerId"), machineId.ToString("D").ToUpperInvariant(), 78);

            Button ok = new Button();
            ok.Text = Strings.Get("ButtonOk");
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(250, 130);
            ok.Size = new Size(75, 26);
            ok.Click += OnOk;
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = Strings.Get("ButtonCancel");
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(331, 130);
            cancel.Size = new Size(75, 26);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
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
