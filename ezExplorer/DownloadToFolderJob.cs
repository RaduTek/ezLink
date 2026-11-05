using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using EzLink;

namespace ezExplorer
{
    /// <summary>
    /// Copies files and folders from the peer into a local folder, with the same conflict and error prompts as an
    /// upload. It is what "Copy to folder…" and "Open" use; dragging into Explorer goes through
    /// <see cref="VirtualFileDataObject"/> instead. Files are written to <c>name.ezlink-part</c> and renamed once
    /// the SHA-256 matched; folders get their original times last.
    /// </summary>
    internal sealed class DownloadToFolderJob : ClientJob
    {
        private const string PartSuffix = ".ezlink-part";

        private readonly ITransferClientSource source;
        private readonly string destination;
        private DownloadPlan plan;
        private List<RemoteItem> items;
        private readonly List<string> createdFolders = new List<string>();
        private readonly List<DownloadEntry> createdEntries = new List<DownloadEntry>();

        /// <summary>The local path of the first file copied (what "Open" starts).</summary>
        public string FirstFilePath = string.Empty;

        /// <summary>A job for a plan that is ready.</summary>
        public DownloadToFolderJob(ITransferClientSource source, DownloadPlan plan, string destination)
        {
            this.source = source;
            this.plan = plan;
            this.destination = destination;
        }

        /// <summary>A job that lists the items itself first (the plan was not ready, or took too long).</summary>
        public DownloadToFolderJob(ITransferClientSource source, List<RemoteItem> items, string destination)
        {
            this.source = source;
            this.items = items;
            this.destination = destination;
        }

        public override TransferOutcome Run(TransferState state, ITransferHost host)
        {
            try
            {
                Client = source.GetTransferClient();

                if (plan == null)
                {
                    plan = PrepareDownloadJob.BuildPlan(Client, items, state, delegate { return host.Cancelled; });
                }
                state.SetTotals(plan.FileCount, plan.TotalBytes);
                Directory.CreateDirectory(destination);

                List<string> skippedFolders = new List<string>();
                foreach (DownloadEntry entry in plan.Entries)
                {
                    CheckCancel(host);
                    if (InsideAny(entry.Relative, skippedFolders))
                    {
                        if (!entry.IsFolder)
                        {
                            state.BeginItem(RemotePaths.GetName(entry.Relative), string.Empty, entry.Size);
                            state.EndItem(entry.Size);
                        }
                        continue;
                    }

                    if (entry.IsFolder)
                    {
                        if (!CopyFolder(entry, state, host))
                        {
                            skippedFolders.Add(entry.Relative);
                        }
                    }
                    else
                    {
                        CopyFile(entry, state, host);
                    }
                }

                StampFolders(host);
                return TransferOutcome.Completed;
            }
            catch (Abort abort)
            {
                return abort.Outcome;
            }
            catch (Exception ex)
            {
                return Unhandled(ex, host, "Download");
            }
        }

        private static bool InsideAny(string relative, List<string> folders)
        {
            foreach (string folder in folders)
            {
                if (relative.Length > folder.Length && relative[folder.Length] == '\\'
                    && relative.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private string LocalOf(DownloadEntry entry)
        {
            return Path.Combine(destination, entry.Relative);
        }

        private bool CopyFolder(DownloadEntry entry, TransferState state, ITransferHost host)
        {
            string local = LocalOf(entry);
            state.BeginItem(RemotePaths.GetName(entry.Relative), RemotePaths.GetParent(entry.RemotePath), 0);
            bool done = Attempt(host, RemotePaths.GetName(entry.Relative), delegate
            {
                if (File.Exists(local))
                {
                    throw new IOException(string.Format(Strings.Get("TransferFolderBlocked"),
                        RemotePaths.GetName(entry.Relative)));
                }
                if (!Directory.Exists(local))
                {
                    Directory.CreateDirectory(local);
                    createdFolders.Add(local);
                    createdEntries.Add(entry);
                }
            });
            return done;
        }

        private void CopyFile(DownloadEntry entry, TransferState state, ITransferHost host)
        {
            string name = RemotePaths.GetName(entry.Relative);
            string local = LocalOf(entry);
            state.BeginItem(name, RemotePaths.GetParent(entry.RemotePath), entry.Size);

            bool replace = false;
            bool skip = false;
            bool checkedOk = Attempt(host, name, delegate
            {
                if (Directory.Exists(local))
                {
                    throw new IOException(string.Format(Strings.Get("TransferFileBlocked"), name));
                }
                if (!File.Exists(local))
                {
                    return;
                }

                FileInfo existing = new FileInfo(local);
                switch (host.AskConflict(name, existing.Length, existing.LastWriteTimeUtc, entry.Size, entry.Modified))
                {
                    case ConflictChoice.Replace:
                        replace = true;
                        break;
                    case ConflictChoice.KeepBoth:
                        local = FreeName(local);
                        break;
                    default:
                        skip = true;
                        break;
                }
            });
            CheckCancel(host);

            if (checkedOk && !skip)
            {
                bool copied = Attempt(host, name, delegate
                {
                    state.SetFileDone(0);
                    Receive(entry, local, replace, state);
                });
                if (copied && FirstFilePath.Length == 0)
                {
                    FirstFilePath = local;
                }
            }
            state.EndItem(entry.Size);
        }

        private void Receive(DownloadEntry entry, string local, bool replace, TransferState state)
        {
            string part = local + PartSuffix;
            string folder = Path.GetDirectoryName(local);
            if (folder.Length > 0 && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            bool ok = false;
            try
            {
                using (FileStream target = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None,
                    64 * 1024))
                {
                    Client.ReadFile(entry.RemotePath, 0, target, delegate(long bytes) { state.SetFileDone(bytes); });
                }
                if (replace && File.Exists(local))
                {
                    File.Delete(local);
                }
                File.Move(part, local);
                ok = true;
            }
            finally
            {
                if (!ok)
                {
                    try
                    {
                        File.Delete(part);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }

            try
            {
                File.SetCreationTimeUtc(local, entry.Created);
                File.SetLastWriteTimeUtc(local, entry.Modified);
                const FileAttributes keep = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System
                    | FileAttributes.Archive;
                FileAttributes attributes = (FileAttributes)entry.Attributes & keep;
                File.SetAttributes(local, attributes == 0 ? FileAttributes.Normal : attributes);
            }
            catch (Exception ex)
            {
                Log.Warn("Cannot set the times of '{0}': {1}", local, ex.Message); // cosmetic: not worth a prompt
            }
        }

        /// <summary>"name.ext" becomes "name (2).ext", "name (3).ext", ... the first that is free here.</summary>
        private static string FreeName(string local)
        {
            string folder = Path.GetDirectoryName(local);
            string name = Path.GetFileName(local);
            string extension = Path.GetExtension(name);
            string stem = name.Substring(0, name.Length - extension.Length);
            for (int n = 2; ; n++)
            {
                string candidate = Path.Combine(folder, stem + " (" + n + ")" + extension);
                if (!File.Exists(candidate) && !Directory.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        /// <summary>Gives every folder this job created its original times, now that nothing else changes it.</summary>
        private void StampFolders(ITransferHost host)
        {
            for (int i = createdFolders.Count - 1; i >= 0; i--)
            {
                CheckCancel(host);
                try
                {
                    Directory.SetCreationTimeUtc(createdFolders[i], createdEntries[i].Created);
                    Directory.SetLastWriteTimeUtc(createdFolders[i], createdEntries[i].Modified);
                }
                catch (Exception ex)
                {
                    Log.Warn("Cannot set the times of '{0}': {1}", createdFolders[i], ex.Message);
                }
            }
        }
    }
}
