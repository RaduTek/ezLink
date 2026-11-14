using System;
using System.Collections.Generic;
using EzLink;
using EzLink.Protocol;

namespace ezExplorer
{
    /// <summary>What the navigation controller drives. Called on the UI thread, in the order declared here.</summary>
    internal interface INavigationView
    {
        /// <summary>Step 1: fill the file list.</summary>
        void ShowListing(string path, List<RemoteItem> items);
        /// <summary>Step 2: show the path in the path box.</summary>
        void ShowPath(string path);
        /// <summary>Step 3: enable or disable Back, Forward and Up.</summary>
        void UpdateNavigation(bool canBack, bool canForward, bool canUp);
        /// <summary>Step 4: expand the tree to the folder and select it.</summary>
        void SyncTree(string path);
        /// <summary>The requested folder could not be opened; the current one stays shown.</summary>
        void NavigationFailed(string path, Exception error);
    }

    /// <summary>
    /// Owns the current path and the back/forward history. Every navigation, whatever its source (tree, list,
    /// path box, toolbar, keyboard, a refresh after a delete), goes through here, and the view is only ever
    /// changed from <see cref="Commit"/>. A folder becomes current only once its listing has arrived, so a bad
    /// path leaves everything as it was.
    /// </summary>
    internal sealed class NavigationController
    {
        private enum Mode
        {
            Push,
            Back,
            Forward,
            Refresh
        }

        private readonly RemoteBrowser browser;
        private readonly INavigationView view;
        private readonly List<string> back = new List<string>();
        private readonly List<string> forward = new List<string>();
        private string current;     // null until the first listing arrives
        private int generation;

        public NavigationController(RemoteBrowser browser, INavigationView view)
        {
            this.browser = browser;
            this.view = view;
        }

        /// <summary>The folder shown now; null before the first listing.</summary>
        public string Current
        {
            get { return current; }
        }

        public bool CanGoBack
        {
            get { return back.Count > 0; }
        }

        public bool CanGoForward
        {
            get { return forward.Count > 0; }
        }

        public bool CanGoUp
        {
            get { return current != null && RemotePaths.GetParent(current) != null; }
        }

        public void NavigateTo(string path)
        {
            Load(path, Mode.Push, 1);
        }

        public void GoBack()
        {
            GoBack(1);
        }

        public void GoForward()
        {
            GoForward(1);
        }

        /// <summary>Goes back this many steps in one go (1 is the previous folder).</summary>
        public void GoBack(int steps)
        {
            if (steps >= 1 && steps <= back.Count)
            {
                Load(back[back.Count - steps], Mode.Back, steps);
            }
        }

        /// <summary>Goes forward this many steps in one go (1 is the next folder).</summary>
        public void GoForward(int steps)
        {
            if (steps >= 1 && steps <= forward.Count)
            {
                Load(forward[forward.Count - steps], Mode.Forward, steps);
            }
        }

        /// <summary>The folders Back leads to, nearest first, at most max of them.</summary>
        public List<string> BackHistory(int max)
        {
            return Recent(back, max);
        }

        /// <summary>The folders Forward leads to, nearest first, at most max of them.</summary>
        public List<string> ForwardHistory(int max)
        {
            return Recent(forward, max);
        }

        private static List<string> Recent(List<string> stack, int max)
        {
            List<string> result = new List<string>();
            for (int i = stack.Count - 1; i >= 0 && result.Count < max; i--)
            {
                result.Add(stack[i]);
            }
            return result;
        }

        public void GoUp()
        {
            if (CanGoUp)
            {
                NavigateTo(RemotePaths.GetParent(current));
            }
        }

        /// <summary>
        /// Reloads the current folder. If it no longer exists the nearest parent that does is shown instead.
        /// </summary>
        public void Refresh()
        {
            Load(current == null ? RemotePaths.Root : current, Mode.Refresh, 1);
        }

        private void Load(string path, Mode mode, int steps)
        {
            int ticket = ++generation;
            browser.ListDir(path, delegate(List<RemoteItem> items, Exception error)
            {
                if (ticket != generation)
                {
                    return; // a newer navigation superseded this one
                }
                if (error == null)
                {
                    Commit(path, items, mode, steps);
                    return;
                }

                RemoteException remote = error as RemoteException;
                if (mode == Mode.Refresh && remote != null && remote.Code == ErrorCode.NotFound
                    && RemotePaths.GetParent(path) != null)
                {
                    Load(RemotePaths.GetParent(path), Mode.Refresh, 1); // the folder was deleted: climb up
                    return;
                }

                Log.Warn("Could not open '{0}': {1}", path, error.Message);
                view.NavigationFailed(path, error);
                if (current != null)
                {
                    view.ShowPath(current); // restore what the path box was showing
                }
            });
        }

        private void Commit(string path, List<RemoteItem> items, Mode mode, int steps)
        {
            switch (mode)
            {
                case Mode.Push:
                    if (current != null && !RemotePaths.SamePath(current, path))
                    {
                        back.Add(current);
                        forward.Clear();
                    }
                    break;
                case Mode.Back:
                    // The skipped folders and the current one move to Forward, the nearest ending up on top.
                    steps = Math.Min(steps, back.Count);
                    forward.Add(current);
                    for (int i = back.Count - 1; i > back.Count - steps; i--)
                    {
                        forward.Add(back[i]);
                    }
                    back.RemoveRange(back.Count - steps, steps);
                    break;
                case Mode.Forward:
                    steps = Math.Min(steps, forward.Count);
                    back.Add(current);
                    for (int i = forward.Count - 1; i > forward.Count - steps; i--)
                    {
                        back.Add(forward[i]);
                    }
                    forward.RemoveRange(forward.Count - steps, steps);
                    break;
            }
            current = path;

            view.ShowListing(path, items);
            view.ShowPath(path);
            view.UpdateNavigation(CanGoBack, CanGoForward, CanGoUp);
            view.SyncTree(path);
        }
    }
}
