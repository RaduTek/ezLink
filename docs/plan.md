# ezLink USB Data Transfer Cable — Implementation Plan

Nov 4, 2026 · @Radu

## Overview

Build a .NET Framework 2.0 file-transfer app for two Windows PCs linked by the ezLink cable: a USB box holding two Realtek RTL8152 Ethernet adapters wired back to back. Each PC sees one USB Ethernet adapter; the app finds the peer, asks for consent, and lets the user browse the other PC and move files by drag and drop with Windows Explorer.

Goals for the first release:

- A tray-resident main app (**ezLink.exe**) in an ActiveSync-like window: toolbar with Explore, Sync (disabled placeholder) and Options, and a large connection-status area.
- A separate remote-browser app (**ezExplorer.exe**) with one Explorer-style pane showing the peer's file system. Files move by drag and drop to and from real Explorer windows.
- Discovery, a session protocol and a UUID-based trust prompt, designed so sync and remote control can be added later as new session types.

**For Claude Code:** work through the milestones in order. Each milestone ends with a runnable build and the listed acceptance checks. Keep to C# 2.0 language features, and ask before adding any third-party dependency. Where this plan says "open question", stop and ask rather than guess.

## Platform and toolchain constraints

Everything must run unchanged from Windows XP SP3 through Windows 10, x86 and x64.

| Constraint | Rule |
| --- | --- |
| Framework | Target .NET Framework 2.0 (`v2.0.50727`). No APIs from 3.0/3.5 (no WCF, no `System.IO.Pipes`, no LINQ). |
| Language | C# 2.0 syntax only: no `var`, lambdas, extension methods or auto-properties. Anonymous delegates are fine. |
| Runtime on new Windows | Each exe's app.config lists `<supportedRuntime version="v2.0.50727"/>` and `<supportedRuntime version="v4.0"/>`, so Windows 10/11 run it on .NET 4.x without installing 3.5. |
| Bitness | Build as AnyCPU. Native interop signatures must be correct for both 32- and 64-bit (use `IntPtr`, never `int`, for handles and pointers). |
| UI | Windows Forms only. |
| Privileges | Both executables run `asInvoker`, never elevated. Admin-only work lives in a separate setup helper (see Setup helper). |
| Serialization | Never use `BinaryFormatter` or .NET Remoting for peer data. All wire formats are hand-written with `BinaryWriter`/`BinaryReader`. |
| Dependencies | None beyond the framework, unless approved. |

Why no elevation: under UAC, Explorer cannot drag files into an elevated window, and `WM_COPYDATA` from a lower to a higher integrity process is blocked. An elevated parent would also launch an elevated ezExplorer.exe.

## Solution layout

One solution, three executables and two shared libraries; file data flows from ezExplorer.exe straight to the peer's ezLink.exe, never through the local main process.

&#91;embedded content: process layout · PC A browsing PC B\]

Both PCs run identical software; the picture shows one direction. When PC B browses PC A, the roles mirror.

| Project | Output | Contents |
| --- | --- | --- |
| EzLink.Protocol | EzLink.Protocol.dll | Encoding helpers, frame reader/writer, all message classes |
| EzLink.Core | EzLink.Core.dll | Constants (ports, hardware ID), logging, `AdapterLocator`, discovery, session client and server base, trust store, file server, Win32 interop declarations |
| ezLink | ezLink.exe | Main window, tray, `LinkStateMachine`, approval prompt, Options, IPC |
| ezExplorer | ezExplorer.exe | Remote pane, upload, `VirtualFileDataObject`, `RemoteFileStream`, IPC |
| ezLinkSetup | ezLinkSetup.exe | Admin-only verbs (see Setup helper) |
| EzLink.Tests | test DLL | NUnit 2.x tests, not shipped |

**Build.** First read `../EasyMusicSync/EasyMusicSync.csproj` and `app.config`. That project already builds a WinForms app with the user's toolchain, so use the same project format, target framework settings and build approach for every ezLink project. If it does not target .NET 2.0, or its format cannot, fall back to SDK-style projects with `<TargetFramework>net20</TargetFramework>`, `<LangVersion>ISO-2</LangVersion>` (enforces C# 2.0) and the `Microsoft.NETFramework.ReferenceAssemblies` package; ask before choosing.

**Manifests.** ezLink.exe and ezExplorer.exe: `asInvoker`, plus the Common Controls 6 dependency for visual styles. ezLinkSetup.exe: `requireAdministrator`. All three declare compatibility with Windows Vista through 10/11 in the `compatibility` section.

## Adapter detection

The adapter's hardware ID is `USB\VID_0BDA&PID_8152` (Realtek RTL8152). Many generic USB Ethernet dongles use the same chip, so a matching ID only makes an adapter a **candidate**. An adapter becomes **the ezLink link** only after a peer answers the discovery beacon on it.

Implement `AdapterLocator` in EzLink.Core:

1. Read `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4D36E972-E325-11CE-BFC1-08002BE10318}`. For each numbered subkey, read `NetCfgInstanceId` (interface GUID) and `MatchingDeviceId`. Skip subkeys that throw `SecurityException` (the `Properties` subkey does).
2. Enumerate `NetworkInterface.GetAllNetworkInterfaces()` and join on `NetworkInterface.Id` = `NetCfgInstanceId`, case-insensitively.
3. A candidate is any interface whose `MatchingDeviceId`, lower-cased, starts with `usb\vid_0bda&pid_8152` (the value may carry a `&rev_xxxx` suffix).
4. Return candidates with their GUID, name, operational status and IPv4 unicast addresses. Do not use `Win32_NetworkAdapter.GUID` (Vista+ only).

Rules:

- Never change the configuration of a candidate that has not been confirmed as the link.
- Re-run detection on `NetworkChange.NetworkAddressChanged` and `NetworkAvailabilityChanged`, and on a 5-second timer as a fallback (XP sometimes misses events).
- Several candidates may exist. Beacon on all of them that are up and have an IPv4 address; the one where a peer answers is the link.
- Keep the hardware-ID prefix in one constant so a future custom VID/PID or serial-number check is a one-line change.

Open question: whether production units will have a distinctive USB serial or custom strings programmed into the RTL8152. If so, add it to the match.

## Network setup and security boundary

There is no authentication, so the only security boundary is that the listener is reachable over the cable alone. Every layer below must hold on its own.

**Addressing.** Version 1 uses whatever IPv4 address the adapter has, normally a self-assigned 169.254.x.x. Treat the link as up only once the adapter has a unicast IPv4 address. Static addressing is an optional later step done by the setup helper (see Setup helper), because XP can take a long time to fall back to a 169.254 address.

**Binding.** Every socket (UDP beacon, TCP listener, outgoing TCP) binds explicitly to the link adapter's IPv4 address. Never bind to `IPAddress.Any`. If the address changes, close and re-open all sockets.

**Connection checks.** On every accepted TCP connection, reject it unless the local endpoint equals the link address and the remote endpoint is in the link's subnet. XP uses the weak host model and may deliver packets for the link address that arrived on another interface, so binding alone is not enough.

**Firewall.** The setup helper adds an inbound rule for ezLink.exe only:

- XP SP2/SP3: `HNetCfg.FwMgr` COM, an authorized application with `RemoteAddresses` = `169.254.0.0/255.255.0.0`.
- Vista and later: `HNetCfg.FwPolicy2`, an inbound allow rule for ezLink.exe, TCP and UDP on the ezLink ports, remote addresses `169.254.0.0/16`. Restrict it to the link interface via the rule's `Interfaces` property once the link adapter is known.

ezExplorer.exe only makes outbound connections and needs no rule.

**Ports.** Fixed defaults: UDP 47770 for discovery, TCP 47771 for sessions. Keep them in one constants class. Open question: confirm these ports, or pick others.

## Discovery

Each ezLink.exe broadcasts a small UDP beacon every 2 seconds on every candidate adapter, and treats a peer as present after one valid beacon and gone after 3 missed intervals (6 seconds) with no open session.

Send from a UDP socket bound to the candidate's address (port 47770, `SO_BROADCAST` and `SO_REUSEADDR` set) to the subnet broadcast address computed from the adapter's address and mask (normally `169.254.255.255`). Ignore beacons whose source address is one of this machine's own addresses or whose UUID equals this machine's UUID.

Beacon payload, little-endian, max 512 bytes:

| Field | Type | Notes |
| --- | --- | --- |
| Magic | 4 bytes | ASCII `EZLB` |
| Beacon version | UInt16 | 1 |
| Machine UUID | 16 bytes | `Guid.ToByteArray()` |
| TCP session port | UInt16 | 47771 by default |
| Computer name | length-prefixed UTF-8 | `Environment.MachineName` |
| App version | length-prefixed UTF-8 | e.g. `1.0.0` |

If a received beacon carries this machine's own UUID from a different address, the two machines share a UUID (cloned disk image). Show an error in the main window and offer to regenerate the UUID (see Identity and trust).

**Who connects.** Both sides see each other's beacon. The side with the numerically lower UUID (compare the 16 bytes in order) opens the `control` session. The other side waits. If no control session arrives within 10 seconds, the higher side connects instead.

## Wire protocol

All peer traffic uses TCP port 47771 with length-prefixed binary frames. One listener in ezLink.exe accepts many sessions; each session declares its type in its first message.

### Encoding

- Little-endian integers via `BinaryWriter`/`BinaryReader`.
- Strings: UInt16 byte length + UTF-8 bytes.
- Timestamps: Int64 FILETIME, UTC.
- GUIDs: 16 bytes from `Guid.ToByteArray()`.
- Paths: full Windows paths on the remote machine (`C:\Users\Ana\Documents`). The server always normalizes them with `Path.GetFullPath` before use.

### Frame

Every frame has a 10-byte header. Commands are addressed by a command set (a feature area) plus a command within that set, like the lingo and command bytes of Apple's iPod Accessory Protocol.

| Field | Type | Notes |
| --- | --- | --- |
| Length | UInt32 | Bytes after this field; reject frames over 1 MiB |
| Command set | UInt8 | Feature area, see Command sets |
| Command | UInt8 | Command within the set |
| Request ID | UInt32 | Chosen by the requester; replies echo it; 0 for unsolicited |
| Payload | bytes | Per command |

### Command sets

| Set | Name | Commands |
| --- | --- | --- |
| 0x00 | General | 0x01 Hello, 0x02 HelloReply, 0x03 Ping, 0x04 Pong, 0x05 Ok, 0x06 Error, 0x07 Cancel, 0x08 TrustChanged, 0x09 Unsupported |
| 0x01 | File transfer | 0x01 GetRoots, 0x02 Roots, 0x03 ListDir, 0x04 DirListing, 0x05 ListTree, 0x06 TreeEntries, 0x07 TreeEnd, 0x08 Stat, 0x09 StatResult, 0x0A ReadFile, 0x0B FileData, 0x0C FileEnd, 0x0D WriteBegin, 0x0E WriteData, 0x0F WriteEnd, 0x10 CreateDir, 0x11 Delete, 0x12 Rename |
| 0x02 | Sync | Reserved |
| 0x03 | Remote control | Reserved |
| 0x04–0x7F | — | Reserved for future sets |
| 0x80–0xFF | Experimental | Never used in release builds |

Rules:

- General (0x00) is always available. `Ok`, `Error` and `Cancel` are general commands used as replies and controls by every set.
- Each capability string in `Hello` maps to one set and its version: `files/1` = set 0x01, version 1. A side never sends commands from a set the peer did not list.
- An unknown set or command gets `Unsupported` (echoing set, command and request ID); the session stays open.
- In code, each set is one handler class implementing an `ICommandSet` interface (set number, capability name, `Handle(frame, session)`), registered with the session dispatcher. New features add a handler, not changes to the dispatcher.
- Define every set and command number as constants in one file in EzLink.Protocol.

Put framing, all message classes and their read/write code in EzLink.Protocol so both executables share one implementation.

### Hello

The connecting side sends `Hello` first; the listener answers `HelloReply` before anything else. Any other first message closes the connection.

| Field | Hello | HelloReply |
| --- | --- | --- |
| Magic `EZLS` + protocol version (UInt16 = 1) | yes | yes |
| Session type: `control`, `files`, later `sync` | yes | — |
| Machine UUID, computer name, app version | yes | yes |
| Capabilities: list of strings, e.g. `files/1` | yes | yes |
| Auth field: length-prefixed bytes, empty in v1 | yes | yes |
| Status: Accepted, PendingApproval, Denied, VersionMismatch | — | yes |
| Granted permissions (bit flags) | — | yes |

`PendingApproval` keeps the connection open while the serving user decides; a second `HelloReply` follows with Accepted or Denied. The client times out after 2 minutes.

### Sessions

- **control**: one per peer pair, opened by ezLink.exe. Carries `Ping`/`Pong` every 5 seconds (close after 15 seconds of silence), trust-state changes (`TrustChanged`) and future feature negotiation. Its loss is what the main window shows as disconnected.
- **files**: opened by ezExplorer.exe. Use two per explorer window: one for browsing requests, one for transfers, so a large copy never delays a directory listing. Requests in one session are handled one at a time, in order.

### File messages

| Request | Reply | Purpose |
| --- | --- | --- |
| `GetRoots` | `Roots` | Drives: letter, type, label, free and total bytes. Only fixed and removable drives in v1. |
| `ListDir(path)` | `DirListing` | Entries: name, attributes, size, created and modified times. A FoldersOnly flag byte limits the result to folders, for the tree. Streamed as several `DirListing` frames with an end flag for large folders. |
| `ListTree(path)` | `TreeEntries`… then `TreeEnd` | Recursive listing with relative paths, sizes and times. Needed to build drag-out descriptors. |
| `Stat(path)` | `StatResult` | Exists, attributes, size, times. Used for conflict checks before upload. |
| `ReadFile(path, offset)` | `FileData`… then `FileEnd(sha256)` | Server streams 64 KiB chunks from `offset` until EOF. |
| `WriteBegin(path, size, times, attributes, mode)` | `Ok` or `Error` | Mode: FailIfExists, Overwrite. Creates missing parent folders. Writes to `<name>.ezlink-part` then renames on success. |
| `WriteData(bytes)` | none | Chunks after `WriteBegin`. |
| `WriteEnd(sha256)` | `Ok` or `Error` | Server checks the hash, renames the part file, applies times and attributes. |
| `CreateDir`, `Delete`, `Rename` | `Ok` or `Error` | Basic file operations for the context menu. Delete of a folder is recursive and needs the Write permission. |
| `Cancel(requestId)` | none | Stops an in-progress `ReadFile` or `WriteBegin`; part files are deleted. |

`Error` carries a numeric code (NotFound, AccessDenied, Exists, PermissionNotGranted, IoError, Cancelled) and a message string. Every file request is checked against the peer's granted permissions before it touches disk.

Set `SO_SNDBUF` and `SO_RCVBUF` to 256 KiB on all session sockets: XP has no TCP window auto-tuning.

## Identity and trust

Each machine has one UUID, and each machine decides on its own which peer UUIDs may access it; there is no cryptography in v1.

**Machine UUID.** The setup helper creates it with `Guid.NewGuid()` and stores it at `HKLM\SOFTWARE\ezLink\MachineId` (string). It identifies the machine, not the user, because the future sync feature needs a stable machine identity. If the value is missing at runtime and ezLink.exe cannot write HKLM, it generates one in `HKCU\Software\ezLink\MachineId` and logs a warning. Regenerating (cloned-image case) goes through the setup helper.

**Trust store.** Per machine, in `%ALLUSERSPROFILE%\ezLink\trust.xml` (created by the setup helper with write access for Users). One entry per peer:

| Field | Notes |
| --- | --- |
| Peer UUID | Key |
| Last known computer name | Display only |
| Permissions | Flags: Browse, Read, Write; Sync and RemoteControl reserved |
| Decision | AlwaysAllow, Deny |
| First and last seen | UTC |

Write with `XmlWriter` by hand, to a temp file then replace, so a crash cannot corrupt it.

**Approval flow.** When a `files` session (or any session other than `control`) arrives from a UUID that is not AlwaysAllow, ezLink.exe replies `PendingApproval` and shows a prompt in front of other windows:

> **ANA-LAPTOP wants to access files on this computer.** Allow: \[x\] Browse and copy files from this computer  \[ \] Copy files to this computer \[Allow once\] \[Always allow\] \[Deny\]

Allow once grants the chosen permissions for the lifetime of the control session. Deny in the trust store auto-denies without prompting; Options lets the user edit or remove entries. Approval is one-directional: A allowing B lets B browse A, not the reverse.

The `control` session itself is always accepted, so both sides can show the peer's name and trust state.

**Known limit.** A UUID in `Hello` is a claim, not proof. The reserved auth field lets a later version add a shared secret issued at first approval and checked with an HMAC-SHA256 challenge, without changing the frame format. Do not implement it in v1, but keep the field.

## ezLink.exe: main app

ezLink.exe is the long-lived process: it owns discovery, the trust store, the TCP listener that serves the peer, and the status window.

### Connection state machine

One `LinkStateMachine` class owns the current state; adapters, discovery, sessions and trust all report events into it, and the UI only renders its state. States:

| State | Meaning | Status text |
| --- | --- | --- |
| NoCable | No candidate adapter present | Plug in the ezLink cable |
| WaitingForNetwork | Candidate present, down or without IPv4 address | Starting the connection… |
| Searching | Adapter up, beaconing, no peer seen | Looking for the other computer… |
| Connecting | Peer beacon seen, control session opening | Connecting to NAME… |
| Connected | Control session open | Connected to NAME |
| Error | Duplicate UUID, port in use, or similar | Specific message plus a fix action |

In Connected, also show two trust lines: "You allow NAME to: …" and "NAME allows you to: …" (the second from the peer's `TrustChanged` messages). Losing the control session returns to Searching.

### Window

Reuse the main window design of the existing EasyMusicSync app ([RaduTek/EasyMusicSync](https://github.com/RaduTek/EasyMusicSync), checked out at `../EasyMusicSync`). Before writing any UI, read its `MainForm.cs`, `MainForm.Designer.cs`, the `NativeToolStrip` folder and `Helpers.cs`, and copy their layout, toolbar rendering, sizing and styling. Only the toolbar buttons and the contents of the status area change:

- Toolbar, using the same `NativeToolStrip` approach: **Explore** (enabled when Connected), **Sync** (always disabled in v1, tooltip "Coming soon"), **Options**.
- Status area: large icon for the state, the status text, the peer's computer name, and the two trust lines.
- Closing the window hides it to the tray; Exit is in the tray menu.
- Copy the reused files into the ezLink project with their namespaces changed; do not reference the EasyMusicSync project. Both are MIT-licensed by the same author, so keep the license header where present.

### Tray and startup

- `NotifyIcon` with a per-state icon; balloon tip on Connected and on disconnect.
- Tray menu: Open, Explore NAME, Exit.
- Start at logon via `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, toggled in Options.
- Single instance per user session via a named mutex `Local\ezLink.Main`. A second launch sends `WM_COPYDATA` to the first to show its window, then exits.

### Options

Model the Options dialog on EasyMusicSync's `SettingsForm`. It holds: start at logon; trusted computers list (edit permissions, remove, unblock); this computer's name and UUID (read-only); a "Repair setup" button that runs the setup helper. Per-user preferences may use a `Settings.settings` file as EasyMusicSync does; the trust store and UUID stay where Identity and trust puts them.

### Server

The listener and session handlers run on background threads. Each session has its own thread for reading frames. File I/O runs on that session's thread. UI updates go through `Control.BeginInvoke`. The approval prompt is shown on the UI thread while the session thread waits on an event.

## ezExplorer.exe: remote browser

ezExplorer.exe shows one Explorer-style pane of the peer's file system; files move only by drag and drop or clipboard with real Explorer windows.

**Launch.** `ezExplorer.exe /peer:<ip> /port:<port> /uuid:<peer uuid> /name:<computer name> /main:<main window handle>`. It opens two `files` sessions (browse, transfer) directly to the peer. If the session is Denied or times out, show the reason and exit.

### Layout

From top to bottom:

1. **Toolbar**, built with the same `NativeToolStrip` approach as the main window: Back, Forward, Up, Refresh, and a Views drop-down (Details, Large icons, List).
2. **Path box**: a full-width text box showing the current remote path (`C:\Users\Ana`). Typing a path and pressing Enter navigates there; an invalid path shows an error and restores the current one.
3. **Split pane** (`SplitContainer`, splitter position remembered): folder tree on the left, file list on the right.
4. **Status bar**: item count and total size of the selection only. It never shows transfer status.

### Navigation model

One `NavigationController` owns the current path and the back/forward history. Every navigation, from any source (tree click, list double-click, path box, toolbar, keyboard, after a rename or delete of the current folder), goes through `NavigateTo(path)`. It then updates the list, the path box, the toolbar button states and the tree, in that order. No control navigates on its own.

### Folder tree

- Root node: the peer's computer name. Its children are the drives from `GetRoots`; under them, folders only.
- Loaded lazily: a node gets a placeholder child until expanded, then `ListDir` with the FoldersOnly flag fills it. Nodes whose folder has no subfolders lose the expand button.
- On every navigation the tree follows the current folder: it expands each level of the path, loading missing levels as needed, then selects and scrolls to the node for the current folder. Use a suppression flag so selecting a node in code does not trigger a second navigation.
- Icons from the system image list (`TVM_SETIMAGELIST` with the `SHGetFileInfo` small list): drive icons per drive type, closed and open folder icons.
- Refresh reloads the expanded nodes along the current path, keeping expansion state where folders still exist.
- Dropping files on a tree node uploads into that folder; dragging a tree node downloads that folder, both through the same code as the list.
- Context menu on folder nodes: Open, Copy, Paste, Rename, Delete, New folder, Refresh.

### File list

- `ListView` in `VirtualMode`, Details view by default, columns Name, Size, Type, Date modified. Click a column header to sort; folders first. The root view (computer name selected in the tree) lists the drives.
- Icons and type names from `SHGetFileInfo` with `SHGFI_USEFILEATTRIBUTES | SHGFI_SYSICONINDEX | SHGFI_TYPENAME`, passing only the name and attributes. Attach the system image lists with `LVM_SETIMAGELIST` (small and large). Cache results per extension.
- On Vista+, `SetWindowTheme(handle, "explorer", null)` on both the list and the tree.
- Keys: Enter opens a folder, Backspace and Alt+Up go up, Alt+Left/Right back/forward, F2 renames, Delete deletes (with confirmation), F5 refreshes, Ctrl+C copies, Ctrl+V pastes, type-to-select. Tab moves focus between tree, list and path box.
- Context menu: Open, Copy, Paste, Rename, Delete, New folder, Refresh. Open downloads the file to `%TEMP%\ezLink\<random>\` through the transfer dialog, then calls `ShellExecute`.
- Disable actions the peer's granted permissions do not allow (no Write: no paste, drop, rename, delete, new folder), in both panes.

### Transfer dialog

Every transfer, in either direction, runs inside a modal `TransferDialog` owned by the explorer window; nothing transfers in the background or reports through the status bar.

- Shows: direction ("Copying from NAME" or "Copying to NAME"), current file name and remote or local path, a progress bar for the current file, a progress bar for the whole job, bytes done and total, speed and time remaining, and a Cancel button.
- Starts in a Preparing state (marquee progress bar, "Counting files…") while folders are listed recursively.
- Conflict prompts (Replace / Skip / Keep both, with "apply to all") appear as child dialogs of the transfer dialog.
- The dialog is modal to the explorer window only: the main ezLink window and other apps stay usable. The explorer window cannot be closed while it is open.
- Errors on a single file offer Retry / Skip / Cancel. On link loss the dialog shows "Connection lost" and closes after the user clicks OK.
- The work itself runs on a worker thread; the dialog updates via `BeginInvoke`, at most 10 times per second.

### Upload: Explorer to pane

- Accept `DataFormats.FileDrop` on drop and on Ctrl+V. Dropping on a list folder item or a tree node uploads into that folder (highlight it during `DragOver`); empty list space uploads into the current folder.
- Open the transfer dialog immediately. It walks dropped folders recursively, then uploads over the transfer session.
- Before each file, `Stat` the target; on conflict ask Replace / Skip / Keep both. Keep both appends `  (2) `, `  (3) ` and so on.
- When the dialog closes, refresh the list and the affected tree nodes.

### Download: pane to Explorer (virtual files)

Implement a custom `System.Runtime.InteropServices.ComTypes.IDataObject` (`VirtualFileDataObject`). WinForms `DataObject` cannot serve per-index `FileContents`, so do not use it here.

1. On drag start or Ctrl+C, run `ListTree` for each selected folder and `Stat` for each file. While this runs, show the transfer dialog in its Preparing state; if it takes over 10 seconds, abort the drag and offer "Copy to folder…" (a folder picker that copies through the same dialog) instead.
2. Offer `CFSTR_FILEDESCRIPTORW` (an array of `FILEDESCRIPTORW`: relative path, `FILE_ATTRIBUTE_DIRECTORY` for folders, 64-bit size split into high and low, write time; flags `FD_ATTRIBUTES | FD_FILESIZE | FD_WRITESTIME`, **without** `FD_PROGRESSUI`, so Explorer does not show a second progress window) and `CFSTR_FILECONTENTS` with `TYMED_ISTREAM` per `lindex`. Also offer `CFSTR_PREFERREDDROPEFFECT` = `DROPEFFECT_COPY`.
3. For each `FileContents` request return a `RemoteFileStream` implementing `IStream` (`Read`, `Stat`, `Seek` for position queries only). A background thread fills a buffer of at least 4 MiB from `ReadFile` chunks; `Read` copies from the buffer and waits at most 50 ms per call. Streams report bytes read to the transfer dialog.
4. Implement `IAsyncOperation` (same GUID as `IDataObjectAsyncCapability`) and return `TRUE` from `GetAsyncMode`, so Explorer pulls data on its own thread and neither app freezes. On `StartOperation`, show the transfer dialog (Copying from NAME) with the descriptor list as the job; on `EndOperation`, close it.
5. Cancel in the transfer dialog makes every open `RemoteFileStream` return an error HRESULT on its next `Read`, so Explorer stops the copy. Link loss does the same.
6. Allow `DROPEFFECT_COPY` only. Start the drag with `DoDragDrop` (P/Invoke from ole32), not `Control.DoDragDrop`.
7. Ctrl+C puts the same object on the clipboard with `OleSetClipboard`, and `OleFlushClipboard` is never called (data stays virtual). The transfer dialog appears when Explorer starts pulling after the paste.

Verify on XP and Windows 10 that Explorer shows no progress window of its own without `FD_PROGRESSUI`. If one still appears for long copies, keep the transfer dialog anyway and note it as a known issue.

Use `IDragSourceHelper` (CLSID\_DragDropHelper) for the drag image and `IDropTargetHelper` on the pane, so drags look like Explorer's.

### Lifetime

The explorer window cannot close while a transfer dialog is open. If ezLink data is still on the clipboard when the window closes (`OleIsCurrentClipboard`), clear it with `OleSetClipboard(null)` before exiting, so a later paste fails cleanly instead of calling into a dead process.

## Inter-process coordination

The two executables never proxy file data through each other; they exchange only a few small notifications via `WM_COPYDATA`.

| Message (dwData) | Direction | Payload | Effect |
| --- | --- | --- | --- |
| `ShowWindow` (1) | any launch → running ezLink.exe | none | Bring the main window to front |
| `ActivateExplorer` (2) | ezLink.exe → ezExplorer.exe | none | Bring the explorer window to front |
| `PeerLost` (3) | ezLink.exe → ezExplorer.exe | peer UUID | Explorer shows "Disconnected" and disables actions |
| `PeerBack` (4) | ezLink.exe → ezExplorer.exe | peer UUID, new IP | Explorer reconnects its sessions and refreshes |
| `ExplorerClosed` (5) | ezExplorer.exe → ezLink.exe | peer UUID | Main app forgets the window handle |

Rules:

- One ezExplorer.exe per peer: named mutex `Local\ezLink.Explorer.<peer uuid>`. Explore with one already open sends `ActivateExplorer` to it.
- Each process creates a hidden message-only window. ezLink.exe passes its handle on the command line (`/main:`); ezExplorer.exe registers its own handle by sending `ExplorerHello` (dwData 6, payload: peer UUID) right after start.
- Payloads are UTF-8 strings. Ignore messages of unknown type, and ignore senders whose process is not ezLink.exe or ezExplorer.exe.
- Both processes run at the same integrity level (not elevated), so UIPI does not block these messages.

## Setup helper

All work that needs administrator rights lives in **ezLinkSetup.exe**, a small console-less WinForms exe with a `requireAdministrator` manifest. The installer runs it once; ezLink.exe launches it (UAC prompt on Vista+) only from Options → Repair setup or the duplicate-UUID fix.

Command-line verbs, each idempotent:

| Verb | Does |
| --- | --- |
| `/install` | Runs `/uuid`, `/firewall`, `/datadir` |
| `/uuid` | Creates `HKLM\SOFTWARE\ezLink\MachineId` if missing |
| `/regenerate-uuid` | Replaces it with a new GUID |
| `/firewall` | Adds or updates the inbound rule for ezLink.exe (see Network setup) |
| `/datadir` | Creates `%ALLUSERSPROFILE%\ezLink\` with modify rights for the Users group |
| `/uninstall` | Removes the firewall rule; leaves UUID and trust store unless `/purge` is also given |

Exit code 0 on success, non-zero with a message written to `%ALLUSERSPROFILE%\ezLink\setup.log` otherwise.

Static addressing of the link adapter (`Win32_NetworkAdapterConfiguration.EnableStatic`, no gateway, no DNS, NetBIOS off, File and Printer Sharing unbound) is **not** in v1. Reserve a `/static-ip:<adapter guid>` verb for it.

**Installer.** Any MSI or Inno Setup script that copies the three executables and EzLink.Core/EzLink.Protocol DLLs to Program Files, runs `ezLinkSetup.exe /install`, and creates Start menu shortcuts. It is out of scope for the first milestones; the setup helper must work standalone.

## Milestones

Build in this order; each milestone ends with a working build and passes its checks before the next starts.

1. **Solution skeleton.** Projects, app.config runtime entries, manifests, constants class, logging to `%LOCALAPPDATA%` (XP: `%USERPROFILE%\Local Settings\Application Data`) `\ezLink\logs`.
   - Check: all exes start on XP SP3 and Windows 10 without installing extra frameworks.
2. **Protocol library.** Encoding helpers, frame reader/writer, every message class with round-trip read/write.
   - Check: unit tests round-trip every message; oversized and truncated frames are rejected.
3. **Adapter detection and discovery.** `AdapterLocator`, beacon sender/receiver, peer table.
   - Check: with the cable between two PCs, each logs the other's name within 10 s of the adapters getting an address; a plain RTL8152 dongle on a LAN shows Searching only and is never reconfigured.
4. **Setup helper.** All verbs.
   - Check: firewall rule visible and scoped on XP and Windows 10; running `/install` twice changes nothing.
5. **Control session and main window.** Listener, hello, ping, state machine, window, tray, single instance.
   - Check: unplug and replug moves through Searching → Connected within 10 s; the state text matches the table at each step.
6. **Trust.** Trust store, approval prompt, `PendingApproval` flow, Options list, duplicate-UUID detection.
   - Check: Deny persists across restarts; Allow once ends when the cable is unplugged.
7. **File server.** `GetRoots`, `ListDir`, `ListTree`, `Stat`, `ReadFile`, write messages, file operations, permission checks, path normalization.
   - Check: a test client copies a 5 GB file both ways with matching SHA-256; requests outside granted permissions get `PermissionNotGranted`.
8. **ezExplorer pane.** Toolbar, path box, folder tree, file list, navigation controller, icons, views, sorting, keyboard, context menus, IPC messages.
   - Check: a folder with 20,000 files lists and scrolls smoothly; navigating by list, path box, Back and Up always leaves the tree expanded to and selected on the current folder.
9. **Upload by drag and drop.** FileDrop, transfer dialog, conflicts, cancel.
   - Check: dropping a nested folder of 1,000 files recreates it exactly, timestamps included.
10. **Download by virtual files.** `VirtualFileDataObject`, `RemoteFileStream`, async mode, drag images, clipboard.
    - Check: dragging a 5 GB file and a nested folder into Explorer shows the ezExplorer transfer dialog (and no Explorer progress window), keeps both apps responsive, and cancelling in Explorer stops the transfer within 2 s.
11. **Hardening.** Link loss mid-transfer, sleep/resume, adapter re-enumeration on a different USB port.
    - Check: every failure ends in a clear message and the app recovers when the cable returns, without a restart.

## Testing

Automated tests cover the protocol and file server without hardware; everything involving the cable, Explorer or UAC is tested by hand on a fixed matrix.

**Automated (EzLink.Tests).** NUnit 2.x, which supports .NET 2.0.

- Message round-trips, malformed frames, oversized strings.
- File server against a temp directory over loopback (tests may bind to 127.0.0.1; production code may not): listings, reads from offset, writes with hash mismatch, cancel, path traversal attempts such as `..\..\Windows` and `\\?\` prefixes, permission denials.
- Trust store save/load and corruption recovery.
- State machine transitions driven by fake events.

**Manual matrix.** Each pair below with the real cable:

| Side A | Side B |
| --- | --- |
| Windows XP SP3 x86 | Windows 10 x64 |
| Windows 7 x64 | Windows 10 x64 |
| Windows XP SP3 x86 | Windows XP SP3 x86 |

On each pair: discovery, approval prompt, browse, upload, download by drag and by clipboard, a 5 GB file, cancel on both sides, unplug mid-transfer, and a second RTL8152 dongle on a LAN present at the same time.

The development machine runs Windows 10, and Windows 11 is not part of testing: it is expected to behave like Windows 10, but nothing may be described as verified on it. For development without two PCs, add a debug-only switch `/loopback-peer` that lets two instances on one machine find each other on 127.0.0.1 with different ports and UUIDs. It must be compiled out of release builds.

## Out of scope for v1

These are deliberately left out, but the design leaves room for each:

- **Sync.** A new `sync` session type and `sync/1` capability, using machine UUIDs to track pairings. The Sync button stays disabled until then.
- **Remote control.** A future session type gated by the reserved RemoteControl permission.
- **Shared-secret authentication.** Uses the reserved auth field in `Hello`.
- **Static link addressing.** The reserved `/static-ip` setup verb.
- **Drive-letter mount or shell namespace extension.** Not planned; the remote pane covers browsing.
- **SMB or Windows file sharing.** Not used at all; the app never changes sharing settings.
- **Localization.** Keep user-facing strings in a resource file from the start so it can be added later.
