// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;

namespace osu.Game.Tournament.Components.CapturedWindow
{
    public class TournamentClientInstance
    {
        public Process Process { get; }

        public IntPtr WindowHandle { get; }

        public int Index { get; }

        public TournamentClientInstance(
            Process process,
            IntPtr hwnd,
            int index)
        {
            Process = process;
            WindowHandle = hwnd;
            Index = index;
        }
    }
}
