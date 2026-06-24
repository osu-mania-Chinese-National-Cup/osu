// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Tournament.Components;
using osu.Game.Tournament.Models;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tournament.Screens.Gameplay.Components
{
    public partial class FourTeamMatchHeader : CompositeDrawable
    {
        private readonly BindableList<TournamentMatchSlot> slot = new BindableList<TournamentMatchSlot>();
        private readonly Bindable<TournamentMatch?> currentMatch = new Bindable<TournamentMatch?>();

        public FourTeamMatchHeader(BindableList<TournamentMatchSlot>? slot) // test purpose
        {
            if (slot != null)
                this.slot.BindTo(slot);

            RelativeSizeAxes = Axes.X;
            Height = 95;
        }

        [BackgroundDependencyLoader]
        private void load(LadderInfo info)
        {
            currentMatch.BindValueChanged(m =>
            {
                if (m.OldValue != null)
                {
                    slot.UnbindBindings();
                }

                if (m.NewValue == null || m.NewValue.StructureType.Value != MatchStructureType.FourTeams)
                {
                    Hide();
                    return;
                }

                Show();
                slot.BindTo(m.NewValue.TeamSlots);
                m.NewValue.StartMatch();
            });

            slot.BindCollectionChanged((_, _) =>
            {
                updateDisplay();
            }, true);

            currentMatch.BindTo(info.CurrentMatch);
        }

        private void updateDisplay()
        {
            InternalChild = new FillFlowContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new TeamDisplay(slot.FirstOrDefault(s => s.Colour.Value == TeamColour.Red)),
                    new TeamDisplay(slot.FirstOrDefault(s => s.Colour.Value == TeamColour.Blue)),
                    new FillFlowContainer
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Padding = new MarginPadding(10),
                        Spacing = new Vector2(5),
                        Children = new Drawable[]
                        {
                            new DrawableTournamentHeaderLogo
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new DrawableTournamentHeaderText
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                            },
                            new MatchRoundDisplay
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Scale = new Vector2(0.4f)
                            },
                        }
                    },
                    new TeamDisplay(slot.FirstOrDefault(s => s.Colour.Value == TeamColour.Yellow), true),
                    new TeamDisplay(slot.FirstOrDefault(s => s.Colour.Value == TeamColour.Green), true),
                }
            };
        }

        private partial class TeamDisplay : DrawableTournamentTeam
        {
            private readonly TournamentMatchSlot? slot;
            private readonly bool rightSide;
            private readonly Bindable<int?> currentTeamScore = new Bindable<int?>();

            private TournamentSpriteTextWithBackground scoreText = null!;
            private TournamentSpriteTextWithBackground seedText = null!;

            public TeamDisplay(TournamentMatchSlot? slot, bool rightSide = false)
                : base(slot?.Team.Value)
            {
                Height = 95;
                Width = 300;
                this.slot = slot;
                this.rightSide = rightSide;
                Anchor = Anchor.TopCentre;
                Origin = Anchor.TopCentre;

                Masking = true;
                CornerRadius = 5;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                if (slot == null || Team == null)
                    return;

                InternalChildren = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Direction = FillDirection.Horizontal,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Children = new Drawable[]
                        {
                            Flag.With(f =>
                            {
                                f.Margin = new MarginPadding(10);
                                f.Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight;
                                f.Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight;
                                f.BorderColour = TournamentGame.GetTeamColour(slot.Colour.Value);
                                f.BorderThickness = 5;
                            }),
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.Y,
                                AutoSizeAxes = Axes.X,
                                Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(10),
                                Children = new Drawable[]
                                {
                                    new TournamentSpriteTextWithBackground(Team?.FullName.Value ?? "???")
                                    {
                                        Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                        Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                        Text =
                                        {
                                            Anchor = Anchor.Centre,
                                            Origin = Anchor.Centre,
                                            Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 20),
                                            Padding = new MarginPadding { Left = 10, Right = 10 },
                                        },
                                    },
                                    new FillFlowContainer
                                    {
                                        AutoSizeAxes = Axes.Both,
                                        Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                        Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(10),
                                        Children = new Drawable[]
                                        {
                                            seedText = new TournamentSpriteTextWithBackground($"#{Team?.Seed.Value ?? "seed"}")
                                            {
                                                Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                                Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                                Text =
                                                {
                                                    Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 20),
                                                    Padding = new MarginPadding { Left = 10, Right = 10 },
                                                },
                                            },
                                            scoreText = new TournamentSpriteTextWithBackground("??")
                                            {
                                                Anchor = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                                Origin = !rightSide ? Anchor.CentreLeft : Anchor.CentreRight,
                                                Text =
                                                {
                                                    Font = OsuFont.Torus.With(weight: FontWeight.SemiBold, size: 20),
                                                    Padding = new MarginPadding { Left = 10, Right = 10 },
                                                },
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                currentTeamScore.BindValueChanged(s =>
                {
                    scoreText.Text.Text = s.NewValue.ToString() ?? string.Empty;
                });
                currentTeamScore.BindTo(slot.Score);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Flag.CornerRadius = 0;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                switch (e.Button)
                {
                    case MouseButton.Left:
                        currentTeamScore.Value++;
                        return true;

                    case MouseButton.Right:
                        if (currentTeamScore.Value > 0)
                            currentTeamScore.Value--;
                        return true;
                }

                return base.OnMouseDown(e);
            }
        }
    }
}
