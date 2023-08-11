﻿using Composition;
using Composition.Input;
using Composition.Nodes;
using ImageServer;
using Microsoft.Xna.Framework;
using Sound;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Timing;
using Tools;
using UI.Video;
using static UI.OBSRemoteControlManager;

namespace UI.Nodes
{
    public class SystemStatusNode : Node
    {
        private MuteStatusNode tts;

        public SystemStatusNode()
        {
        }

        public void SetupStatuses(TimingSystemManager timingSystemManager, VideoManager videoManager, SoundManager soundManager, OBSRemoteControlManager oBSRemoteControlManager)
        {
            ClearDisposeChildren();

            tts = new MuteStatusNode(soundManager, true);

            AddChild(tts);
            AddChild(new MuteStatusNode(soundManager, false));

            foreach (ITimingSystem timingSystem in timingSystemManager.TimingSystems)
            {
                TimingSystemStatusNode tsn = new TimingSystemStatusNode(timingSystemManager, timingSystem);
                AddChild(tsn);
            }

            foreach (VideoConfig videoConfig in videoManager.VideoConfigs)
            {
                VideoSystemStatusNode vsn = new VideoSystemStatusNode(videoManager, videoConfig);
                AddChild(vsn);
            }

            if (oBSRemoteControlManager != null) 
            {
                OBSStatusNode vsn = new OBSStatusNode(oBSRemoteControlManager);
                AddChild(vsn);
            }

            RequestLayout();
        }

        protected override void LayoutChildren(Rectangle bounds)
        {
            int left = bounds.Left;
            int ItemPadding = 10;
            int ItemHeight = 30;
            int width = bounds.Width;

            tts.Visible = tts.SoundManager.HasSpeech();

            Node[] nodes = VisibleChildren.ToArray();

            int prevBottom = bounds.Y;
            foreach (Node n in nodes)
            {
                n.RelativeBounds = new RectangleF(0, 0, 1, 1);
                n.Layout(new Rectangle(left,
                                       prevBottom + ItemPadding,
                                       width,
                                       ItemHeight));

                prevBottom = n.Bounds.Bottom;
            }
        }
    }

    public class StatusNode : Node, IUpdateableNode
    {
        protected TextNode status;
        protected TextNode name;
        protected ImageNode icon;

        private DateTime recvTimeout;
        private bool statusOK;

        private Color tint;
        public Color Tint
        {
            set
            {
                status.Tint = value;
                name.Tint = value;
                icon.Tint = value;
                tint = value;
            }
            get
            {
                return tint;
            }
        }

        public string Name
        {
            get
            {
                return name.Text;
            }
            set
            {
                name.Text = value;
                if (name.Text.Length > 5)
                {
                    name.Text = name.Text.Substring(0, 5);
                }
            }
        }

        private DateTime lastStatusUpdate;

        protected float updateEverySeconds;

        public StatusNode(string iconFilename)
        {
            updateEverySeconds = 2;

            tint = Color.White;
            icon = new ImageNode(iconFilename);
            icon.Alignment = RectangleAlignment.CenterLeft;
            icon.RelativeBounds = new RectangleF(0, 0, 0.5f, 1f);
            icon.Scale(0.7f);
            AddChild(icon);

            float textLeft = 0.4f;

            name = new TextNode("", Color.White);
            name.RelativeBounds = new RectangleF(textLeft, 0.0f, 1 - textLeft, 0.5f);
            name.Alignment = RectangleAlignment.CenterRight;
            name.OverrideHeight = 14;
            AddChild(name);

            status = new TextNode("", Color.White);
            status.RelativeBounds = new RectangleF(textLeft, name.RelativeBounds.Bottom, 1 - textLeft, 0.5f);
            status.Alignment = RectangleAlignment.CenterRight;
            status.OverrideHeight = name.OverrideHeight;
            AddChild(status);
        }

        public void OnDataRecv()
        {
            recvTimeout = DateTime.Now.AddMilliseconds(100);
        }

        public void SetStatus(string status, bool statusOK)
        {
            this.statusOK = statusOK;
            this.status.Text = status;
        }

        public void Update(GameTime gameTime)
        {
            if ((DateTime.Now - lastStatusUpdate).TotalSeconds > updateEverySeconds)
            {
                StatusUpdate();
                lastStatusUpdate = DateTime.Now;
            }

            Color color = statusOK ? Color.White : Color.Red;

            if (recvTimeout >= DateTime.Now)
            {
                color = Color.Orange;
            }

            status.Tint = color;
            icon.Tint = color;
            name.Tint = color;
        }

        public virtual void StatusUpdate()
        {
        }

    }

    public class TimingSystemStatusNode : StatusNode
    {
        public TimingSystemManager TimingSystemManager { get; private set; }
        public ITimingSystem TimingSystem { get; private set; }

        public TimingSystemStatusNode(TimingSystemManager timingSystemManager, ITimingSystem timingSystem)
            : base(@"img/timing.png")
        {
            TimingSystemManager = timingSystemManager;
            TimingSystem = timingSystem;
            TimingSystemManager.OnDataReceived += TimingSystemManager_OnDataReceived;

            Name = TimingSystemToAcronym(timingSystem.Type);
        }

        private void TimingSystemManager_OnDataReceived(ITimingSystem obj)
        {
            if (obj == TimingSystem)
            {
                OnDataRecv();
            }
        }

        private string TimingSystemToAcronym(TimingSystemType type)
        {
            switch (type)
            {
                case TimingSystemType.Dummy:
                    return "DMY";

                case TimingSystemType.LapRF:
                    return "LRF";

                case TimingSystemType.Video:
                    return "V";

                default:
                    return Maths.AutoAcronym(type.ToString());
            }

        }

        public override void StatusUpdate()
        {
            base.StatusUpdate();

            StatusItem[] statuses = TimingSystem.Status.ToArray();

            if (statuses == null || !statuses.Any())
                return;

            IEnumerable<StatusItem> alarmed = statuses.Where(s => !s.StatusOK);
            if (alarmed.Any())
            {
                StatusItem chosen = alarmed.GetFromCurrentTime(updateEverySeconds);
                SetStatus(chosen.Value, false);
            }
            else 
            {
                StatusItem chosen = statuses.GetFromCurrentTime(updateEverySeconds);
                SetStatus(chosen.Value, TimingSystem.Connected);
            }
        }
    }

    public class VideoSystemStatusNode : StatusNode
    {
        public VideoManager VideoManager { get; private set; }
        public VideoConfig VideoConfig { get; private set; }

        private TextNode recordingIcon;

        public VideoSystemStatusNode(VideoManager videoManager, VideoConfig videoConfig)
            : base(@"img/video.png")
        {
            recordingIcon = new TextNode("●", Color.Red);
            recordingIcon.RelativeBounds = new RectangleF(0.0f, 0.3f, 0.8f, 0.8f);
            icon.AddChild(recordingIcon);

            VideoManager = videoManager;
            VideoConfig = videoConfig;

            IEnumerable<SourceTypes> types = videoConfig.VideoBounds.Select(r => r.SourceType).Distinct();

            if (types.Count() == 1)
            {
                switch(types.First())
                {
                    case SourceTypes.FPVFeed: Name = "FPV"; break;
                    case SourceTypes.Commentators: Name = "COM"; break;
                    case SourceTypes.Launch: Name = "LCH"; break;
                    case SourceTypes.FinishLine: Name = "FIN"; break;
                }
            }
            else
            {
                Name = VideoConfig.DeviceName.AutoAcronym().ToUpper();
            }
        }

        public override void StatusUpdate()
        {
            base.StatusUpdate();
            bool connected, recording;
            int height;
            if (VideoManager.GetStatus(VideoConfig, out connected, out recording, out height))
            {
                SetStatus(height + "p", connected);
                recordingIcon.Visible = recording;
            }
            else
            {
                recordingIcon.Visible = false;
                SetStatus("", false);
            }

        }

        public override bool OnMouseInput(MouseInputEvent mouseInputEvent)
        {
            if (mouseInputEvent.ButtonState == ButtonStates.Released)
            {
                MouseMenu mm = new MouseMenu(this);

                foreach (var source in VideoManager.FrameSources)
                {
                    if (source.VideoConfig == VideoConfig)
                    {
                        mm.AddItem("Repair " + source.VideoConfig.DeviceName, () =>
                        {
                            VideoManager.Initialize(source);
                        });
                    }
                }

                mm.Show(this);
            }

            return base.OnMouseInput(mouseInputEvent);
        }
    }

    public class MuteStatusNode : StatusNode
    {
        public SoundManager SoundManager { get; private set; }

        private CheckboxNode cbn;

        private bool tts;

        public bool Value
        {
            get
            {
                if (tts)
                {
                    return SoundManager.MuteTTS;
                }
                else
                {
                    return SoundManager.MuteWAV;
                }
            }
            set
            {
                if (tts)
                {
                    SoundManager.MuteTTS = value;
                }
                else
                {
                    SoundManager.MuteWAV = value;
                }
            }
        }

        public MuteStatusNode(SoundManager soundManager, bool tts)
            : base("")
        {
            this.SoundManager = soundManager;
            this.tts = tts;

            if (tts)
            {
                name.Text = "TTS";
            }
            else
            {
                name.Text = "WAV";
            }

            cbn = new CheckboxNode();
            cbn.TickFilename = @"img/unmute.png";
            cbn.UnTickFilename = @"img/mute.png";
            cbn.Alignment = icon.Alignment;
            cbn.RelativeBounds = icon.RelativeBounds;
            cbn.ValueChanged += Cbn_ValueChanged;
            AddChild(cbn);
            icon.Dispose();
            icon = cbn;

            SetMute(Value);
        }

        private void SetMute(bool mute)
        {
            SetStatus(mute ? "Mute" : "", !mute);
            Value = mute;
            cbn.Value = !mute;
        }

        private void Cbn_ValueChanged(bool obj)
        {
            SetMute(!obj);
        }
    }

    public class OBSStatusNode : StatusNode
    {
        private OBSRemoteControlManager oBSRemoteControlManager;

        public OBSStatusNode(OBSRemoteControlManager oBSRemoteControlManager)
            : base(@"img/obs.png")
        {
            Name = "OBS RC";
            this.oBSRemoteControlManager = oBSRemoteControlManager;
            oBSRemoteControlManager.Activity += OBSRemoteControlManager_Activity;
        }

        private void OBSRemoteControlManager_Activity(bool obj)
        {
            OnDataRecv();
        }

        public override void StatusUpdate()
        {
            base.StatusUpdate();
            SetStatus("", oBSRemoteControlManager.Connected);

            this.Visible = oBSRemoteControlManager.Enabled;
        }
    }
}
