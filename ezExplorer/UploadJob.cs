using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>
    /// Copies local files and folders (dropped or pasted from Explorer) into a folder on the peer, over the
    /// transfer session. Folders are recreated with their contents, files keep their times and attributes, and
    /// each folder gets its own times last, after its contents stopped changing it.
    /// </summary>
    internal sealed class UploadJob : TransferJob
    {
        private sealed class Item
        {
            public string LocalPath;
            public string Name;
            public string RelativePath;     // below the target folder, backslash separated
            public bool IsFolder;
            public long Size;
            public DateTime Created;        // UTC
            public DateTime Modified;       // UTC
            public uint Attributes;
            public Item Parent;
            public bool Skipped;            // a folder that could not be created: nothing below it is copied
            public bool CreatedHere;        // a folder that did not exist on the peer before
        }

        private sealed class Abort : Exception
        {
            public readonly TransferOutcome Outcome;

            public Abort(TransferOutcome outcome)
            {
                Outcome = outcome;
            }
        }

        private readonly ITransferClientSource browser;
        private readonly string[] sources;
        private readonly string targetFolder;
        private volatile FileClient client;

        public UploadJob(ITransferClientSource browser, string[] sources, string targetFolder)
        {
            this.browser = browser;
            this.sources = sources;
            this.targetFolder = targetFolder;
        }

        public string TargetFolder
        {
            get { return targetFolder; }
        }

        public override void Cancel()
        {
            FileClient current = client;
            if (current != null)
            {
                current.Cancel();
            }
        }

        public override TransferOutcome Run(TransferState state, ITransferHost host)
        {
            try
            {
                client = browser.GetTransferClient();

                List<Item> items = Prepare(state, host);
                long total = 0;
                foreach (Item item in items)
                {
                    total += item.Size;
                }
                state.SetTotals(items.Count, total);

                foreach (Item item in items)
                {
                    CheckCancel(host);
                    if (item.Parent != null && item.Parent.Skipped)
                    {
                        item.Skipped = true;
                        state.EndItem(item.Size);
                        continue;
                    }
                    if (item.IsFolder)
                    {
                        CopyFolder(item, state, host);
                    }
                    else
                    {
                        CopyFile(item, state, host);
                    }
                }

                StampFolders(items, state, host);
                return TransferOutcome.Completed;
            }
            catch (Abort abort)
            {
                return abort.Outcome;
            }
            catch (Exception ex)
            {
                if (host.Cancelled)
                {
                    return TransferOutcome.Cancelled;
                }
                if (IsLost())
                {
                    return TransferOutcome.ConnectionLost;
                }
                Log.Warn("Upload failed: {0}", ex.Message);
                FailureMessage = ex.Message;
                return TransferOutcome.Failed;
            }
        }

        private bool IsLost()
        {
            FileClient current = client;
            return current == null || current.Session.IsClosed;
        }

        private void CheckCancel(ITransferHost host)
        {
            if (host.Cancelled)
            {
                throw new Abort(TransferOutcome.Cancelled);
            }
        }

        /// <summary>
        /// Runs one step, offering Retry / Skip / Cancel if it fails. Returns false if the user chose Skip.
        /// A lost connection or a cancel ends the whole job instead.
        /// </summary>
        private bool Attempt(ITransferHost host, string name, MethodInvoker step)
        {
            while (true)
            {
                CheckCancel(host);
                try
                {
                    step();
                    return true;
                }
                catch (Abort)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (host.Cancelled)
                    {
                        throw new Abort(TransferOutcome.Cancelled);
                    }
                    if (IsLost())
                    {
                        throw new Abort(TransferOutcome.ConnectionLost);
                    }
                    Log.Warn("Copying '{0}' failed: {1}", name, ex.Message);
                    ErrorChoice choice = host.AskError(name, ex.Message);
                    if (choice == ErrorChoice.Skip)
                    {
                        return false;
                    }
                    if (choice == ErrorChoice.Cancel)
                    {
                        throw new Abort(TransferOutcome.Cancelled);
                    }
                }
            }
        }

        // ---- preparing: walk what was dropped ----

        private List<Item> Prepare(TransferState state, ITransferHost host)
        {
            List<Item> items = new List<Item>();
            foreach (string source in sources)
            {
                CheckCancel(host);
                string path = source.TrimEnd('\\', '/');
                if (Directory.Exists(source))
                {
                    DirectoryInfo dir = new DirectoryInfo(path);
                    if (dir.Parent != null) // a drive root has no name to copy under
                    {
                        Walk(dir, null, dir.Name, items, state, host);
                    }
                }
                else if (File.Exists(source))
                {
                    FileInfo file = new FileInfo(source);
                    items.Add(FileItem(file, null, file.Name));
                }
            }
            return items;
        }

        private void Walk(DirectoryInfo dir, Item parent, string relative, List<Item> items, TransferState state,
            ITransferHost host)
        {
            CheckCancel(host);
            state.SetPreparing(dir.FullName);

            Item folder = new Item();
            folder.LocalPath = dir.FullName;
            folder.Name = dir.Name;
            folder.RelativePath = relative;
            folder.IsFolder = true;
            folder.Created = dir.CreationTimeUtc;
            folder.Modified = dir.LastWriteTimeUtc;
            folder.Attributes = (uint)dir.Attributes;
            folder.Parent = parent;
            items.Add(folder);

            FileInfo[] files = null;
            DirectoryInfo[] folders = null;
            Attempt(host, dir.FullName, delegate
            {
                files = dir.GetFiles();
                folders = dir.GetDirectories();
            });

            if (files != null)
            {
                foreach (FileInfo file in files)
                {
                    items.Add(FileItem(file, folder, relative + "\\" + file.Name));
                }
            }
            if (folders != null)
            {
                foreach (DirectoryInfo child in folders)
                {
                    // Junctions and symbolic links could loop forever (or leave the tree): they are not followed.
                    if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        Walk(child, folder, relative + "\\" + child.Name, items, state, host);
                    }
                }
            }
        }

        private static Item FileItem(FileInfo file, Item parent, string relative)
        {
            Item item = new Item();
            item.LocalPath = file.FullName;
            item.Name = file.Name;
            item.RelativePath = relative;
            item.Size = file.Length;
            item.Created = file.CreationTimeUtc;
            item.Modified = file.LastWriteTimeUtc;
            item.Attributes = (uint)file.Attributes;
            item.Parent = parent;
            return item;
        }

        // ---- copying ----

        private string RemoteOf(Item item)
        {
            return RemotePaths.Combine(targetFolder, item.RelativePath);
        }

        private void CopyFolder(Item item, TransferState state, ITransferHost host)
        {
            string remote = RemoteOf(item);
            state.BeginItem(item.Name, RemotePaths.GetParent(remote), 0);

            bool done = Attempt(host, item.Name, delegate
            {
                StatResultReply stat = client.Stat(remote);
                if (stat.Exists)
                {
                    if (!RemotePaths.IsFolder(stat.Attributes))
                    {
                        throw new IOException(string.Format(Strings.Get("TransferFolderBlocked"), item.Name));
                    }
                    return; // merge into the folder that is already there
                }
                client.CreateDir(remote);
                item.CreatedHere = true;
            });
            if (!done)
            {
                item.Skipped = true;
            }
            state.EndItem(0);
        }

        private void CopyFile(Item item, TransferState state, ITransferHost host)
        {
            string remote = RemoteOf(item);
            state.BeginItem(item.Name, RemotePaths.GetParent(remote), item.Size);

            WriteMode mode = WriteMode.FailIfExists;
            bool skip = false;

            bool checkedOk = Attempt(host, item.Name, delegate
            {
                StatResultReply stat = client.Stat(remote);
                if (!stat.Exists)
                {
                    return;
                }
                if (RemotePaths.IsFolder(stat.Attributes))
                {
                    throw new IOException(string.Format(Strings.Get("TransferFileBlocked"), item.Name));
                }

                switch (host.AskConflict(item.Name, stat.Size, stat.Modified, item.Size, item.Modified))
                {
                    case ConflictChoice.Replace:
                        mode = WriteMode.Overwrite;
                        break;
                    case ConflictChoice.KeepBoth:
                        remote = FreeName(remote);
                        break;
                    default:
                        skip = true;
                        break;
                }
            });
            CheckCancel(host);

            if (checkedOk && !skip)
            {
                Attempt(host, item.Name, delegate
                {
                    using (FileStream source = new FileStream(item.LocalPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete, 64 * 1024))
                    {
                        long size = source.Length;
                        if (size != item.Size)
                        {
                            // It changed since it was counted.
                            state.AdjustTotal(size - item.Size);
                            item.Size = size;
                            state.BeginItem(item.Name, RemotePaths.GetParent(remote), size);
                        }
                        state.SetFileDone(0);
                        client.WriteFile(remote, source, size, item.Created, item.Modified, item.Attributes, mode,
                            delegate(long bytes) { state.SetFileDone(bytes); });
                    }
                });
            }
            state.EndItem(item.Size);
        }

        /// <summary>"name.ext" becomes "name (2).ext", "name (3).ext", ... the first that is free on the peer.</summary>
        private string FreeName(string remote)
        {
            string folder = RemotePaths.GetParent(remote);
            string name = RemotePaths.GetName(remote);
            string extension = Path.GetExtension(name);
            string stem = name.Substring(0, name.Length - extension.Length);
            for (int n = 2; ; n++)
            {
                string candidate = RemotePaths.Combine(folder, stem + " (" + n + ")" + extension);
                if (!client.Stat(candidate).Exists)
                {
                    return candidate;
                }
            }
        }

        /// <summary>Gives every folder this job created its original times, now that nothing else changes it.</summary>
        private void StampFolders(List<Item> items, TransferState state, ITransferHost host)
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item item = items[i];
                if (!item.IsFolder || !item.CreatedHere)
                {
                    continue;
                }
                CheckCancel(host);
                try
                {
                    client.CreateDir(RemoteOf(item), item.Created, item.Modified);
                }
                catch (Exception ex)
                {
                    if (host.Cancelled)
                    {
                        throw new Abort(TransferOutcome.Cancelled);
                    }
                    if (IsLost())
                    {
                        throw new Abort(TransferOutcome.ConnectionLost);
                    }
                    Log.Warn("Cannot set the times of '{0}': {1}", item.Name, ex.Message); // cosmetic: not worth a prompt
                }
            }
        }
    }
}
