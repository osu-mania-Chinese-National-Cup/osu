// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Online.Multiplayer;

namespace osu.Game.Tournament.Components.CapturedWindow
{
    public partial class TournamentCaptureManager : Component
    {
        private const string manager_window_title = " Tournament Manager";
        private const string client_window_prefix = " Tournament Client ";

        private readonly List<TournamentManagerInstance> instances = new List<TournamentManagerInstance>();

        private readonly Dictionary<int, CapturedWindowSprite> captures = new Dictionary<int, CapturedWindowSprite>();

        public BindableList<TournamentManagerOption?> AvailableManagers { get; } = new BindableList<TournamentManagerOption?>();

        public Bindable<TournamentManagerOption?> SelectedManager { get; } = new Bindable<TournamentManagerOption?>();

        public TournamentCaptureManager()
        {
            SelectedManager.BindValueChanged(_ =>
            {
                RefreshTargets();
            });
        }

        public void RefreshAsync() => Task.Run(Refresh).FireAndForget();

        public void Refresh()
        {
            instances.Clear();

            foreach (IntPtr hwnd in findWindowsByTitle(manager_window_title))
            {
                GetWindowThreadProcessId(hwnd, out uint pid);

                if (pid == 0)
                    continue;

                try
                {
                    var process = Process.GetProcessById((int)pid);

                    var clients = findClients(process);

                    instances.Add(
                        new TournamentManagerInstance(
                            process,
                            hwnd,
                            clients));
                }
                catch
                {
                    // process exited
                }
            }

            Scheduler.Add(() =>
            {
                AvailableManagers.Clear();

                AvailableManagers.AddRange(
                    instances.Select(i =>
                        new TournamentManagerOption(i)));

                // 尝试保持之前选择
                var selected = SelectedManager.Value;

                if (selected != null)
                {
                    var replacement =
                        AvailableManagers.FirstOrDefault(x => x?.Instance.Process.Id ==
                                                              selected.Instance.Process.Id);

                    SelectedManager.Value = replacement;
                }
            });
        }

        public void RegisterCapture(CapturedWindowSprite capture)
        {
            captures[capture.Index] = capture;

            applyTarget(capture);
        }

        public void RefreshTargets()
        {
            foreach (var capture in captures.Values)
                applyTarget(capture);
        }

        private void applyTarget(CapturedWindowSprite capture)
        {
            var manager = SelectedManager.Value?.Instance;

            if (manager == null)
            {
                capture.SetTarget(null);
                return;
            }

            var client = manager.Clients
                                .FirstOrDefault(c => c.Index == capture.Index);

            if (client == null)
            {
                capture.SetTarget(null);
                return;
            }

            capture.SetTarget(
                new CaptureTarget(
                    client.Process.Id,
                    client.WindowHandle));
        }

        private IReadOnlyList<TournamentClientInstance> findClients(Process manager)
        {
            var clients = new List<TournamentClientInstance>();

            foreach (var process in Process.GetProcessesByName(manager.ProcessName.Replace(".exe", "")))
            {
                if (process.Id == manager.Id)
                    continue;

                if (!tryGetParentProcessId(
                        process,
                        out int parentId))
                    continue;

                if (parentId != manager.Id)
                    continue;

                process.Refresh();

                IntPtr hwnd = process.MainWindowHandle;

                if (hwnd == IntPtr.Zero)
                    continue;

                string title = getWindowTitle(hwnd);

                if (!title.StartsWith(
                        client_window_prefix,
                        StringComparison.Ordinal))
                    continue;

                int index = parseClientIndex(title);

                clients.Add(
                    new TournamentClientInstance(
                        process,
                        hwnd,
                        index));
            }

            return clients
                   .OrderBy(c => c.Index)
                   .ToList();
        }

        private static int parseClientIndex(string title)
        {
            return int.TryParse(title.AsSpan(client_window_prefix.Length), out int index) ? index : -1;
        }

        #region Windows API

        private static IEnumerable<IntPtr> findWindowsByTitle(string title)
        {
            var result = new List<IntPtr>();

            EnumWindows((hwnd, _) =>
            {
                if (getWindowTitle(hwnd) == title)
                    result.Add(hwnd);

                return true;
            }, IntPtr.Zero);

            return result;
        }

        private static string getWindowTitle(IntPtr hwnd)
        {
            var builder = new StringBuilder(256);

            GetWindowText(
                hwnd,
                builder,
                builder.Capacity);

            return builder.ToString();
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(
            EnumWindowsProc callback,
            IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(
            IntPtr hwnd,
            StringBuilder text,
            int maxCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(
            IntPtr hwnd,
            out uint pid);

        private delegate bool EnumWindowsProc(
            IntPtr hwnd,
            IntPtr lParam);

        #endregion

        private static bool tryGetParentProcessId(
            Process process,
            out int parentId)
        {
            parentId = 0;

            try
            {
                using var searcher =
                    new System.Management.ManagementObjectSearcher(
                        $"SELECT ParentProcessId FROM Win32_Process WHERE ProcessId={process.Id}");

                foreach (var obj in searcher.Get())
                {
                    parentId =
                        Convert.ToInt32(
                            (uint)obj["ParentProcessId"]);

                    return true;
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
