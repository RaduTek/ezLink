using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ezLink.NativeToolStrip {
    public class NativeContextMenuStrip : ContextMenuStrip {
        public NativeContextMenuStrip() {
            this.Renderer = new NativeToolStripRenderer(_Theme);
        }

        public NativeContextMenuStrip(IContainer container) : this() {
            if (container != null) container.Add(this);
        }

        private NativeToolStripTheme _Theme = NativeToolStripTheme.Toolbar;
        public NativeToolStripTheme Theme {
            get { return _Theme; }
            set {
                _Theme = value;
                this.Renderer = new NativeToolStripRenderer(_Theme);
            }
        }
    }
}
