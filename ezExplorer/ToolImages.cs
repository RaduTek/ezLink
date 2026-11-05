using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace ezExplorer
{
    /// <summary>Loads the toolbar PNGs embedded in the assembly (Oxygen icons, see THIRD-PARTY-NOTICES.md).</summary>
    internal static class ToolImages
    {
        public static Bitmap Load(string name)
        {
            Assembly assembly = typeof(ToolImages).Assembly;
            using (Stream stream = assembly.GetManifestResourceStream("ezExplorer.Resources." + name + ".png"))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("Missing embedded image " + name);
                }
                using (Bitmap raw = new Bitmap(stream))
                {
                    return new Bitmap(raw); // detach from the stream
                }
            }
        }
    }
}
