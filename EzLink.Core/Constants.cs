namespace EzLink
{
    /// <summary>
    /// Every fixed value the plan names lives here, so changing one is a one-line edit.
    /// </summary>
    public static class Constants
    {
        /// <summary>Folder / registry key name used under LocalAppData, ProgramData and HKLM\SOFTWARE.</summary>
        public const string AppFolderName = "ezLink";

        /// <summary>UDP port for discovery beacons.</summary>
        public const int DiscoveryPort = 47770;

        /// <summary>TCP port for sessions.</summary>
        public const int SessionPort = 47771;

        /// <summary>
        /// Lower-case hardware-ID prefix of the Realtek RTL8152 (USB\VID_0BDA&amp;PID_8152).
        /// Matching it only makes an adapter a candidate; a peer answering the beacon makes it the link.
        /// Keep it in this one constant so a custom VID/PID or serial check is a one-line change.
        /// </summary>
        public const string AdapterHardwareIdPrefix = @"usb\vid_0bda&pid_8152";
    }
}
