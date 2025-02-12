﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Tools;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using RaceLib;
using static Microsoft.IO.RecyclableMemoryStreamManager;
using DB.Lite;

namespace DB.JSON
{
    public enum SyncWith
    {
        None,
        FPVTrackside,
        MultiGP
    }

    public class Event : DatabaseObjectT<RaceLib.Event>
    {
        public string EventType { get; set; }
        public string Name { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }

        public int Laps { get; set; }

        public int PBLaps { get; set; }

        public TimeSpan RaceLength { get; set; }

        public TimeSpan MinStartDelay { get; set; }

        public TimeSpan MaxStartDelay { get; set; }

        public string PrimaryTimingSystemLocation { get; set; }

        public TimeSpan RaceStartIgnoreDetections { get; set; }

        public TimeSpan MinLapTime { get; set; }

        public DateTime LastOpened { get; set; }

        public PilotChannel[] PilotChannels { get; set; }

        public Guid[] RemovedPilots { get; set; }

        public Guid[] Rounds { get; set; }

        public Guid Club { get; set; }

        public Guid[] Channels { get; set; }
        public string[] ChannelColors { get; set; }
        public string[] ChannelDisplayNames { get; set; }

        public bool Enabled { get; set; }

        public string MultiGPRaceFormat { get; set; }

        public Guid[] Races { get; set; }

        // Legacy
        public SyncWith SyncWith
        {
            set
            {
                switch (value)
                {
                    case SyncWith.FPVTrackside:
                        SyncWithFPVTrackside = true;
                        break;
                    case SyncWith.MultiGP:
                        SyncWithMultiGP = true; 
                        break;
                }
            }
        }

        public bool SyncWithFPVTrackside { get; set; }
        public bool SyncWithMultiGP { get; set; }
        public bool GenerateHeatsMultiGP { get; set; }
        public bool VisibleOnline { get; set; }
        public bool Locked { get; set; }

        public Guid Track { get; set; }
        public Sector[] Sectors { get; set; }

        public int PilotsRegistered { get; set; }
        public DateTime[] Flags { get; set; }

        public Event()
        {
        }

        public Event(RaceLib.Event obj)
           : base(obj)
        {
            var orderedChannels = obj.Channels.OrderBy(c => c.Frequency).ThenBy(r => r.Band);

            if (obj.Rounds != null)
                Rounds = obj.Rounds.Select(c => c.ID).ToArray();

            if (obj.PilotChannels != null)
                PilotChannels = obj.PilotChannels.Convert<PilotChannel>().ToArray();

            if (obj.Channels != null)
                Channels = orderedChannels.Select(c => c.ID).ToArray();

            if (obj.RemovedPilots != null)
                RemovedPilots = obj.RemovedPilots.Select(c => c.ID).ToArray();

            if (obj.Club != null)
                Club = obj.Club.ID;

            ExternalID = obj.ExternalID;

            ChannelDisplayNames = orderedChannels.Select(c => c.DisplayName).ToArray();
            ChannelColors = obj.ChannelColors;
            SyncWithFPVTrackside = obj.SyncWithFPVTrackside;
            SyncWithMultiGP = obj.SyncWithMultiGP;
            GenerateHeatsMultiGP = obj.GenerateHeatsMultiGP;
            VisibleOnline = obj.VisibleOnline;

            if (obj.Track != null)
                Track = obj.Track.ID;

            ReflectionTools.Copy(obj.Sectors, out DB.JSON.Sector[] temp);
            Sectors = temp;

            if (obj.PilotChannels != null)
            {
                PilotsRegistered = obj.PilotChannels.Where(pc => pc != null && pc.Pilot != null && pc.Pilot.PracticePilot == false).Count();
            }
        }

        public override RaceLib.Event GetRaceLibObject(ICollectionDatabase database)
        {
            RaceLib.Event ev = base.GetRaceLibObject(database);
            ev.Channels = Channels.Convert<RaceLib.Channel>(database).ToArray();

            for (int i = 0; i < ChannelDisplayNames.Length && i < ev.Channels.Length; i++)
            {
                ev.Channels[i].DisplayName = ChannelDisplayNames[i];
            }

            ev.Club = Club.Convert<RaceLib.Club>(database);
            ev.PilotChannels = PilotChannels.Convert(database).Where(pc => pc != null && pc.Pilot != null).ToList();
            ev.Rounds = Rounds.Convert<RaceLib.Round>(database).ToList();
            ev.RemovedPilots = RemovedPilots.Convert<RaceLib.Pilot>(database).ToList();
            ev.SyncWithFPVTrackside = SyncWithFPVTrackside;
            ev.SyncWithMultiGP = SyncWithMultiGP;
            ev.GenerateHeatsMultiGP = GenerateHeatsMultiGP;
            ev.VisibleOnline = VisibleOnline;

            ev.Track = Track.Convert<RaceLib.Track>(database);

            ReflectionTools.Copy(Sectors, out RaceLib.Sector[] temp);
            ev.Sectors = temp;

            return ev;
        }

        public RaceLib.SimpleEvent GetSimpleEvent(ICollectionDatabase database)
        {
            SimpleEvent simpleEvent = new SimpleEvent(ID);
            ReflectionTools.Copy(this, simpleEvent);

            if (Channels != null)
            {
                List<string> channelNames = new List<string>();

                IDatabaseCollection<RaceLib.Channel> channelCollection = database.GetCollection<RaceLib.Channel>();
                for (int i = 0; i < Channels.Length; i++) 
                {
                    if (ChannelDisplayNames != null && ChannelDisplayNames.Length > i)
                    {
                        channelNames.Add(ChannelDisplayNames[i]);
                    }
                    else
                    {
                        Guid id = Channels[i];
                        channelNames.Add(channelCollection.GetObject(id).DisplayName);
                    }
                }

                simpleEvent.ChannelsString = string.Join(", ", channelNames);
            }
            return simpleEvent;
        }
    }
}
