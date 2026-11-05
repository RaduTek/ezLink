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
            Load(path, Mode.Push);
        }

        public void GoBack()
        {
            if (CanGoBack)
            {
                Load(back[back.Count - 1], Mode.Back);
            }
        }

        public void GoForward()
        {
            if (CanGoForward)
            {
                Load(forward[forward.Count - 1], Mode.Forward);
            }
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
            Load(current == null ? RemotePaths.Root : current, Mode.Refresh);
        }

        private void Load(string path, Mode mode)
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
                    Commit(path, items, mode);
                    return;
                }

                RemoteException remote = error as RemoteException;
                if (mode == Mode.Refresh && remote != null && remote.Code == ErrorCode.NotFound
                    && RemotePaths.GetParent(path) != null)
                {
                    Load(RemotePaths.GetParent(path), Mode.Refresh); // the folder was deleted: climb up
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

        private void Commit(string path, List<RemoteItem> items, Mode mode)
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
                    back.RemoveAt(back.Count - 1);
                    forward.Add(current);
                    break;
                case Mode.Forward:
                    forward.RemoveAt(forward.Count - 1);
                    back.Add(current);
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
