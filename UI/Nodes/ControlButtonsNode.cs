﻿using Composition.Nodes;
using Microsoft.Xna.Framework;
using RaceLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tools;
using UI.Video;

namespace UI.Nodes
{
    public class ControlButtonsNode : Node
    {
        public IconButtonNode StartButton { get; private set; }
        public IconButtonNode NextButton { get; private set; }
        public IconButtonNode StopButton { get; private set; }
        public IconButtonNode ClearButton { get; private set; }
        public IconButtonNode PasteClipboard { get; private set; }
        public IconButtonNode CopyResultsClipboard { get; private set; }
        public IconButtonNode ResetButton { get; private set; }
        public IconButtonNode ResumeButton { get; private set; }

        public IconButtonNode PilotList { get; private set; }
        public IconButtonNode SyncButton { get; private set; }

        public AutoRunnerControls AutoRunnerControls { get; private set; }

        private EventManager eventManager;
        private TracksideTabbedMultiNode tracksideMultiNode;
        private ChannelsGridNode channelsGridNode;

        public int ItemHeight { get; set; }
        public int ItemPaddingHorizontal { get; set; }
        public int ItemPaddingVertical { get; set; }

        public ControlButtonsNode(EventManager eventManager, ChannelsGridNode channelsGridNode, TracksideTabbedMultiNode tracksideMultiNode, AutoRunner autoRunner)
        {
            ItemHeight = 55;
            ItemPaddingHorizontal = 2;
            ItemPaddingVertical = 10;

            this.eventManager = eventManager;
            this.tracksideMultiNode = tracksideMultiNode;
            this.channelsGridNode = channelsGridNode;

            eventManager.RaceManager.OnRaceReset += UpdateControlButtons;
            eventManager.RaceManager.OnRaceChanged += UpdateControlButtons;
            eventManager.RaceManager.OnRaceStart += UpdateControlButtons;
            eventManager.RaceManager.OnRaceEnd += UpdateControlButtons;
            eventManager.RaceManager.OnPilotAdded += UpdateControlButtons;
            eventManager.RaceManager.OnPilotRemoved += UpdateControlButtons;
            eventManager.RaceManager.OnRaceClear += UpdateControlButtons;
            eventManager.RoundManager.OnRoundAdded += UpdateControlButtons;

            StartButton = new IconButtonNode(@"img\start.png", "Start", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            StartButton.NodeName = "StartRace";
            AddChild(StartButton);

            StopButton = new IconButtonNode(@"img\stop.png", "Stop", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            StopButton.NodeName = "StopRace";
            AddChild(StopButton);

            NextButton = new IconButtonNode(@"img\next.png", "Next", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            AddChild(NextButton);

            ResumeButton = new IconButtonNode(@"img\resume.png", "Resume", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            ResumeButton.NodeName = "ResumeRace";
            AddChild(ResumeButton);

            ResetButton = new IconButtonNode(@"img\restart.png", "Restart", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            AddChild(ResetButton);

            PasteClipboard = new IconButtonNode(@"img\paste.png", "Paste", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            AddChild(PasteClipboard);

            CopyResultsClipboard = new IconButtonNode(@"img\copy.png", "Copy", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            AddChild(CopyResultsClipboard);

            ClearButton = new IconButtonNode(@"img\clear.png", "Clear Race", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            ClearButton.NodeName = "ClearRace";
            AddChild(ClearButton);
            
            PilotList = new IconButtonNode(@"img\pilotlist.png", "Pilots", Theme.Current.RightControls.Foreground, Theme.Current.Hover.XNA, Theme.Current.RightControls.Text.XNA);
            AddChild(PilotList);

            AutoRunnerControls = new AutoRunnerControls(autoRunner);
            AddChild(AutoRunnerControls);

            foreach (IconButtonNode ibm in Children.OfType<IconButtonNode>())
            {
                ibm.ImageNode.Tint = Theme.Current.RightControls.Text.XNA;
            }
        }

        public void AddSyncButton(IconButtonNode button)
        {
            SyncButton = button;
            SyncButton.ImageNode.Tint = Theme.Current.RightControls.Text.XNA;
            AddChild(SyncButton);
        }

        public override void Dispose()
        {
            eventManager.RaceManager.OnRaceReset -= UpdateControlButtons;
            eventManager.RaceManager.OnRaceChanged -= UpdateControlButtons;
            eventManager.RaceManager.OnRaceStart -= UpdateControlButtons;
            eventManager.RaceManager.OnRaceEnd -= UpdateControlButtons;
            eventManager.RaceManager.OnPilotAdded -= UpdateControlButtons;
            eventManager.RaceManager.OnPilotRemoved -= UpdateControlButtons;
            eventManager.RaceManager.OnRaceClear -= UpdateControlButtons;
            eventManager.RoundManager.OnRoundAdded -= UpdateControlButtons;

            base.Dispose();
        }

        private void UpdateControlButtons(PilotChannel pc)
        {
            UpdateControlButtons();
        }

        private void UpdateControlButtons(Race r)
        {
            UpdateControlButtons();
        }

        public void UpdateControlButtons()
        {
            bool inRaceOrPreRace = eventManager.RaceManager.RaceRunning || eventManager.RaceManager.PreRaceStartDelay;

            StartButton.Visible = eventManager.RaceManager.CanRunRace && !eventManager.RaceManager.PreRaceStartDelay;
            StopButton.Visible = inRaceOrPreRace;
            PasteClipboard.Visible = !eventManager.RaceManager.RaceStarted && !eventManager.RaceManager.PreRaceStartDelay;
            CopyResultsClipboard.Visible = eventManager.RaceManager.RaceFinished;
            ResetButton.Visible = eventManager.RaceManager.RaceFinished;
            ResumeButton.Visible = eventManager.RaceManager.RaceFinished;

            bool showingAnyPilots = channelsGridNode.VisibleChildren.Any();
            
            // Override
            if (!tracksideMultiNode.IsOnLive)
            {
                StartButton.Visible = false;
                StopButton.Visible = false;
                PasteClipboard.Visible = false;
                CopyResultsClipboard.Visible = false;
                ResetButton.Visible = false;
                ClearButton.Visible = false;
                ResumeButton.Visible = false;
            }
            
            PilotList.Visible = !inRaceOrPreRace;

            if (SyncButton != null)
            {
                SyncButton.Visible = !inRaceOrPreRace;
            }
            ClearButton.Visible = showingAnyPilots && !inRaceOrPreRace;

            Race nextRace = eventManager.RaceManager.GetNextRace(true);
            NextButton.Visible = !eventManager.RaceManager.RaceRunning 
                              && !eventManager.RaceManager.PreRaceStartDelay
                              && !eventManager.RaceManager.CanRunRace
                              && nextRace != null 
                              && nextRace != eventManager.RaceManager.CurrentRace;

            if (!NextButton.Visible)
            {
                // If we're mid event but have no current race.
                if (eventManager.RaceManager.CurrentRace == null && eventManager.RaceManager.LastFinishedRace() != null)
                {
                    NextButton.Visible = true;
                }
            }

            RequestLayout();
        }

        protected override void LayoutChildren(Rectangle bounds)
        {
            Node[] items = Children;

            int left = ItemPaddingHorizontal + bounds.Left;
            int width = bounds.Width - (ItemPaddingHorizontal * 2);

            int prevTop = bounds.Bottom;
            foreach (Node n in items)
            {
                if (!n.Visible)
                    continue;

                n.RelativeBounds = new RectangleF(0, 0, 1, 1);

                int height = ItemHeight;

                if (n == AutoRunnerControls && AutoRunnerControls.LargeMode)
                {
                    height = (int)(1.7 * ItemHeight);
                }

                Rectangle newBounds = new Rectangle(left, 
                                                    prevTop - (ItemPaddingVertical + height),
                                                    width,
                                                    height);

                n.Layout(newBounds);

                prevTop = n.Bounds.Y;
            }
        }
    }
}
