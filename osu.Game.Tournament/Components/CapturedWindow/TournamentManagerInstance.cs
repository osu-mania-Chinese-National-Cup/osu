// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace osu.Game.Tournament.Components.CapturedWindow
{
    public class TournamentManagerInstance
    {
        public Process Process { get; }

        public IntPtr WindowHandle { get; }

        public IReadOnlyList<TournamentClientInstance> Clients { get; }

        public TournamentManagerInstance(
            Process process,
            IntPtr windowHandle,
            IReadOnlyList<TournamentClientInstance> clients)
        {
            Process = process;
            WindowHandle = windowHandle;
            Clients = clients;
        }
    }
}
