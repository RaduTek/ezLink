# Third-party notices

## Oxygen icons

The status, tray and toolbar images of `ezLink.exe` (`ezLink/Resources/*.png`) are taken unmodified from the
[Oxygen icon theme](https://invent.kde.org/frameworks/oxygen-icons), licensed under the
**GNU Lesser General Public License, version 3 or later** (`ezLink/licenses/oxygen-icons-LGPL-3.0-or-later.txt`,
copied next to `ezLink.exe` at build time).

Copyright 2007 Nuno Pinheiro, David Vignoni, David Miller, Johann Ollivier Lapeyre, Kenneth Wimer and
Riccardo Iaconelli.

| ezLink image | Oxygen icon |
| --- | --- |
| `status-nocable`, `tray-nocable` | `devices/drive-removable-media-usb` |
| `status-waiting`, `tray-waiting` | `actions/chronometer` |
| `status-searching`, `tray-searching` | `actions/system-search` |
| `status-connecting`, `tray-connecting` | `actions/view-refresh` |
| `status-connected`, `tray-connected` | `places/network-workgroup` |
| `status-error`, `tray-error` | `status/dialog-error` |
| `tool-explore` | `places/folder-open` |
| `tool-sync` | `actions/folder-sync` |
| `tool-options` | `actions/configure` |

The toolbar images of `ezExplorer.exe` (`ezExplorer/Resources/*.png`, 22 px) are likewise unmodified Oxygen icons;
the license text is copied next to `ezExplorer.exe` at build time as well.

| ezExplorer image | Oxygen icon |
| --- | --- |
| `nav-back` | `actions/go-previous` |
| `nav-forward` | `actions/go-next` |
| `nav-up` | `actions/go-up` |
| `nav-refresh` | `actions/view-refresh` |
| `view-details` | `actions/view-list-details` |

In the Oxygen repository `places/folder-open` (32 px) is a link to `status/folder-open`, and `places/network-workgroup`
(16 px) is a link to `mimetypes/application-x-smb-workgroup`; the linked images are what is embedded.

## EasyMusicSync

`ezLink/NativeToolStrip/` and `ezExplorer/NativeToolStrip/` are adapted from the `NativeToolStrip` classes of EasyMusicSync (MIT licensed, same
author). The namespace was changed; the sources carry no license header.
