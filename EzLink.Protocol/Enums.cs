using System;

namespace EzLink.Protocol
{
    /// <summary>Permission bit flags a machine grants a peer.</summary>
    [Flags]
    public enum Permissions : uint
    {
        None = 0,
        Browse = 1,
        Read = 2,
        Write = 4,
        Sync = 8,            // reserved
        RemoteControl = 16   // reserved
    }

    /// <summary>Outcome carried by HelloReply.</summary>
    public enum HelloStatus : byte
    {
        Accepted = 1,
        PendingApproval = 2,
        Denied = 3,
        VersionMismatch = 4
    }

    /// <summary>Numeric code in an Error message.</summary>
    public enum ErrorCode
    {
        NotFound = 1,
        AccessDenied = 2,
        Exists = 3,
        PermissionNotGranted = 4,
        IoError = 5,
        Cancelled = 6
    }

    /// <summary>How WriteBegin treats an existing target.</summary>
    public enum WriteMode : byte
    {
        FailIfExists = 1,
        Overwrite = 2
    }
}
