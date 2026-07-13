// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Tournament.Components.CapturedWindow
{
    public class TournamentManagerOption
    {
        public TournamentManagerInstance Instance { get; }

        public TournamentManagerOption(TournamentManagerInstance instance)
        {
            Instance = instance;
        }

        public override string ToString()
        {
            return $"PID: {Instance.Process.Id} ({Instance.Clients.Count} Clients)";
        }
    }
}
