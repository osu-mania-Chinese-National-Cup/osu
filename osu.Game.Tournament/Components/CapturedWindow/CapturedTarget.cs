// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Tournament.Components.CapturedWindow
{
    public sealed class CaptureTarget
    {
        public int ProcessId { get; }

        public IntPtr WindowHandle { get; }

        public bool IsAlive { get; set; }

        public CaptureTarget(
            int processId,
            IntPtr hwnd)
        {
            ProcessId = processId;
            WindowHandle = hwnd;
        }
    }
}
