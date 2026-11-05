using System;
using System.Collections.Generic;
using System.Threading;
using EzLink.Protocol;

namespace EzLink
{
    public enum ApprovalChoice
    {
        Deny,
        AllowOnce,
        AlwaysAllow
    }

    /// <summary>The answer to an approval prompt.</summary>
    public sealed class ApprovalResult
    {
        public readonly ApprovalChoice Choice;
        public readonly Permissions Permissions;

        public ApprovalResult(ApprovalChoice choice, Permissions permissions)
        {
            Choice = choice;
            Permissions = permissions;
        }
    }

    /// <summary>Asks the serving user whether a peer may use this computer. Implemented by the UI.</summary>
    public interface IApprovalPrompt
    {
        /// <summary>
        /// Blocks until the user decides. Returns null if the prompt was dismissed without a decision, timed
        /// out, or <paramref name="cancel"/> was signalled (the requesting session went away).
        /// </summary>
        ApprovalResult Ask(Guid peerId, string peerName, string sessionType, WaitHandle cancel);
    }

    /// <summary>
    /// Decides what a peer may do on this machine: the stored decisions (always allow, deny), the "allow once"
    /// grants that last for the lifetime of the control session, and the approval prompt for everything else.
    /// Approval is one-directional: what this machine allows says nothing about what the peer allows back.
    /// </summary>
    public sealed class TrustManager
    {
        private readonly TrustStore store;
        private readonly IApprovalPrompt prompt;
        private readonly object decisionLock = new object(); // one prompt at a time, later sessions see its outcome
        private readonly object onceLock = new object();
        private readonly Dictionary<Guid, Permissions> once = new Dictionary<Guid, Permissions>();

        /// <summary>
        /// Raised (on whichever thread caused it) when what a peer is allowed to do may have changed: a prompt
        /// was answered, an Allow once grant ended, or the stored entry was edited.
        /// </summary>
        public event EventHandler<TrustEventArgs> PermissionsChanged;

        public TrustManager(TrustStore store, IApprovalPrompt prompt)
        {
            this.store = store;
            this.prompt = prompt;
        }

        public TrustStore Store
        {
            get { return store; }
        }

        /// <summary>What the peer may do right now.</summary>
        public Permissions GetEffective(Guid peerId)
        {
            TrustEntry entry = store.Get(peerId);
            if (entry != null && entry.Decision == TrustDecision.Deny)
            {
                return Permissions.None;
            }
            Permissions granted = entry != null ? entry.Permissions : Permissions.None;
            lock (onceLock)
            {
                Permissions grant;
                if (once.TryGetValue(peerId, out grant))
                {
                    granted |= grant;
                }
            }
            return granted;
        }

        /// <summary>
        /// Decides a non-control session. <paramref name="onPending"/> is called once, before the prompt is shown,
        /// so the caller can tell the peer to wait (PendingApproval). <paramref name="cancel"/> is signalled if the
        /// session is lost while the user is deciding. Returns Accepted, or Denied.
        /// </summary>
        public HelloStatus Authorize(Guid peerId, string peerName, string sessionType, ThreadStart onPending,
            WaitHandle cancel, out Permissions granted)
        {
            granted = Permissions.None;
            lock (decisionLock)
            {
                TrustEntry entry = store.Get(peerId);
                if (entry != null && entry.Decision == TrustDecision.Deny)
                {
                    Log.Info("{0} is blocked; denying its {1} session without asking", peerName, sessionType);
                    store.Touch(peerId, peerName);
                    return HelloStatus.Denied;
                }

                Permissions known = GetEffective(peerId);
                if (known != Permissions.None)
                {
                    store.Touch(peerId, peerName);
                    granted = known;
                    return HelloStatus.Accepted;
                }

                if (prompt == null)
                {
                    return HelloStatus.Denied;
                }
                if (onPending != null)
                {
                    onPending();
                }

                ApprovalResult result = prompt.Ask(peerId, peerName, sessionType, cancel);
                if (result == null)
                {
                    Log.Info("The approval prompt for {0} ended without a decision; denying this time", peerName);
                    return HelloStatus.Denied;
                }

                // The user can only grant what the prompt offers.
                Permissions chosen = result.Permissions & TrustEntry.FileAccess;
                if (result.Choice == ApprovalChoice.Deny || chosen == Permissions.None)
                {
                    Log.Info("{0} was denied by the user", peerName);
                    RememberDecision(peerId, peerName, TrustDecision.Deny, Permissions.None);
                    RaisePermissionsChanged(peerId);
                    return HelloStatus.Denied;
                }

                if (result.Choice == ApprovalChoice.AlwaysAllow)
                {
                    Log.Info("{0} is always allowed: {1}", peerName, chosen);
                    RememberDecision(peerId, peerName, TrustDecision.AlwaysAllow, chosen);
                }
                else
                {
                    Log.Info("{0} is allowed once: {1}", peerName, chosen);
                    lock (onceLock)
                    {
                        once[peerId] = chosen;
                    }
                }
                granted = GetEffective(peerId);
                RaisePermissionsChanged(peerId);
                return HelloStatus.Accepted;
            }
        }

        /// <summary>Ends the Allow once grant for the peer (the control session ended or the peer left the link).</summary>
        public void ClearOnce(Guid peerId)
        {
            bool had;
            lock (onceLock)
            {
                had = once.Remove(peerId);
            }
            if (had)
            {
                Log.Info("Allow once grant for {0} ended", peerId);
                RaisePermissionsChanged(peerId);
            }
        }

        /// <summary>Adds or replaces a stored entry (Options).</summary>
        public void SetEntry(TrustEntry entry)
        {
            store.Set(entry);
            RaisePermissionsChanged(entry.PeerId);
        }

        /// <summary>Removes a stored entry: the next session from the peer prompts again (Options).</summary>
        public void RemoveEntry(Guid peerId)
        {
            if (store.Remove(peerId))
            {
                RaisePermissionsChanged(peerId);
            }
        }

        private void RememberDecision(Guid peerId, string peerName, TrustDecision decision, Permissions permissions)
        {
            TrustEntry entry = store.Get(peerId);
            if (entry == null)
            {
                entry = new TrustEntry();
                entry.PeerId = peerId;
            }
            entry.ComputerName = peerName;
            entry.Decision = decision;
            entry.Permissions = permissions;
            entry.LastSeenUtc = DateTime.UtcNow;
            store.Set(entry);
        }

        private void RaisePermissionsChanged(Guid peerId)
        {
            EventHandler<TrustEventArgs> handler = PermissionsChanged;
            if (handler != null)
            {
                handler(this, new TrustEventArgs(peerId));
            }
        }
    }

    public sealed class TrustEventArgs : EventArgs
    {
        public readonly Guid PeerId;

        public TrustEventArgs(Guid peerId)
        {
            PeerId = peerId;
        }
    }
}
