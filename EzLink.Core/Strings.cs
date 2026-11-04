using System.Globalization;
using System.Resources;

namespace EzLink
{
    /// <summary>
    /// All user-facing text comes from Strings.resx so localization can be added later.
    /// </summary>
    public static class Strings
    {
        private static readonly ResourceManager manager =
            new ResourceManager("EzLink.Strings", typeof(Strings).Assembly);

        public static string Get(string name)
        {
            string value = manager.GetString(name, CultureInfo.CurrentUICulture);
            return value != null ? value : "[" + name + "]";
        }
    }
}
