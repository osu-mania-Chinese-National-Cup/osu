// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.Models;
using osuTK;

namespace osu.Game.Tournament.Screens.TeamWin
{
    public partial class TeamWinScreen : TournamentMatchScreen
    {
        private Container mainContainer = null!;

        private readonly Bindable<bool> currentCompleted = new Bindable<bool>();
        private readonly Bindable<TeamColour?> winner = new Bindable<TeamColour?>();

        private TourneyVideo blueWinVideo = null!;
        private TourneyVideo redWinVideo = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                blueWinVideo = new TourneyVideo("teamwin-blue")
                {
                    Alpha = 1,
                    RelativeSizeAxes = Axes.Both,
                    Loop = true,
                },
                redWinVideo = new TourneyVideo("teamwin-red")
                {
                    Alpha = 0,
                    RelativeSizeAxes = Axes.Both,
                    Loop = true,
                },
                mainContainer = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                new ControlPanel
                {
                    Children = new Drawable[]
                    {
                        new TourneyButton
                        {
                            Text = "Force red win",
                            Action = () => winner.Value = TeamColour.Red
                        },
                        new TourneyButton
                        {
                            Text = "Force blue win",
                            Action = () => winner.Value = TeamColour.Blue
                        },
                        new TourneyButton
                        {
                            Text = "Force yellow win",
                            Action = () => winner.Value = TeamColour.Yellow
                        },
                        new TourneyButton
                        {
                            Text = "Force green win",
                            Action = () => winner.Value = TeamColour.Green
                        },
                    }
                }
            };

            currentCompleted.BindValueChanged(_ => update());
            winner.BindValueChanged(_ => update());
        }

        protected override void CurrentMatchChanged(ValueChangedEvent<TournamentMatch?> match)
        {
            base.CurrentMatchChanged(match);

            currentCompleted.UnbindBindings();

            if (match.NewValue == null)
                return;

            currentCompleted.BindTo(match.NewValue.Completed);

            firstDisplay = false;
            update();
        }

        private bool firstDisplay = true;

        private void update() => Scheduler.AddOnce(() =>
        {
            var match = CurrentMatch.Value;

            if (match == null || (winner.Value == null && match.StructureType.Value == MatchStructureType.HeadToHead))
            {
                mainContainer.Clear();
                return;
            }

            TournamentTeam? winnerTeam;

            if (match.StructureType.Value == MatchStructureType.HeadToHead)
            {
                winnerTeam = winner.Value == TeamColour.Red ? match.Team1.Value : match.Team2.Value;
            }
            else
            {
                winnerTeam = match.TeamSlots.FirstOrDefault(t => t.Colour.Value == winner.Value)?.Team.Value;
            }

            redWinVideo.Alpha = winner.Value == TeamColour.Red || winner.Value == TeamColour.Yellow ? 1 : 0;
            blueWinVideo.Alpha = winner.Value == TeamColour.Blue || winner.Value == TeamColour.Green ? 1 : 0;

            if (firstDisplay)
            {
                if (winner.Value == TeamColour.Red)
                    redWinVideo.Reset();
                else
                    blueWinVideo.Reset();
                firstDisplay = false;
            }

            mainContainer.Children = new Drawable[]
            {
                new DrawableTeamFlag(winnerTeam)
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Position = new Vector2(-300, 10),
                    Scale = new Vector2(2f)
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    X = 260,
                    Children = new Drawable[]
                    {
                        new RoundDisplay(match)
                        {
                            Margin = new MarginPadding { Bottom = 30 },
                        },
                        new TournamentSpriteText
                        {
                            Text = "WINNER",
                            Font = OsuFont.Torus.With(size: 100, weight: FontWeight.Bold),
                            Margin = new MarginPadding { Bottom = 50 },
                        },
                        new DrawableTeamWithPlayers(winnerTeam, winner.Value.Value)
                    }
                },
            };
            mainContainer.FadeOut();
            mainContainer.Delay(2000).FadeIn(1600, Easing.OutQuint);
        });
    }
}
