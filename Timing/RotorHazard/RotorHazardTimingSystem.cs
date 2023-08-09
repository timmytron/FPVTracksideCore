﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Tools;
using SocketIOClient;
using System.Reflection;

namespace Timing.RotorHazard
{
    public class RotorHazardTimingSystem : ITimingSystemWithRSSI
    {

        private bool connected;
        public bool Connected
        {
            get
            {
                if ((DateTime.Now - lastBeatTime) > TimeOut)
                {
                    Disconnect();
                    connected = false;
                }

                return connected;
            }
        }

        public TimingSystemType Type { get { return TimingSystemType.RotorHazard; } }

        public Version Version { get; private set; }

        private double voltage;
        private double temperature;

        private bool detecting;

        private Heartbeat lastBeat;
        private DateTime lastBeatTime;

        private DateTime serverEpoch;

        private RotorHazardSettings settings;
        public TimingSystemSettings Settings { get { return settings; } set { settings = value as RotorHazardSettings; } }

        public event DetectionEventDelegate OnDetectionEvent;

        private SocketIO socketIOClient;

        private DateTime piTimeStart;
        private List<PiTimeSample> piTimeSamples;
        private static TimeSpan CaptureTime = TimeSpan.FromSeconds(1);

        public int MaxPilots 
        { 
            get 
            {
                if (lastBeat.frequency != null)
                {
                    return lastBeat.frequency.Length;
                }
                return 4;
            } 
        }

        private List<PassRecord> passRecords;

        public IEnumerable<StatusItem> Status
        {
            get
            {
                if (voltage != 0)
                {
                    yield return new StatusItem() { StatusOK = voltage > settings.VoltageWarning, Value = voltage.ToString("0.0") + "v" };
                }

                if (temperature != 0)
                {
                    yield return new StatusItem() { StatusOK = temperature < settings.TemperatureWarning, Value = temperature.ToString("0.0") + "c" };
                }

                if (connectionCount > 10)
                    yield return new StatusItem() { StatusOK = false, Value = connectionCount.ToString("0") + " disc" };
            }
        }

        private int connectionCount;


        public TimeSpan TimeOut { get; set; }

        public ServerInfo ServerInfo { get; private set; }

        public RotorHazardTimingSystem()
        {
            settings = new RotorHazardSettings();
            passRecords = new List<PassRecord>();
            piTimeSamples = new List<PiTimeSample>();
            TimeOut = TimeSpan.FromSeconds(10);
        }
        
        public void Dispose()
        {
            Disconnect();
        }

        public bool Connect()
        {
            try
            {
                socketIOClient = new SocketIO("http://" + settings.HostName + ":" + settings.Port);
                socketIOClient.OnConnected += Socket_OnConnected;

                lastBeatTime = DateTime.Now;
                connected = true;

                socketIOClient.ConnectAsync();
                return true;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }

            return false;
        }

        private void Socket_OnConnected(object sender, EventArgs e)
        {
            try
            {
                SocketIO socket = (SocketIO)sender;

                socket.On("pass_record", OnPassRecord);
                socket.On("frequency_set", (a) => { });
                socket.On("frequency_data", OnFrequencyData);
                socket.On("environmental_data", OnEnvironmentData);
                socket.On("node_data", OnNodeData);
                socket.On("heartbeat", HeartBeat);
                socket.On("load_all", e =>
                {
                    Logger.TimingLog.Log(this, "Load All");
                });

                socket.On("pi_time", OnServerTime);

                string[] toLog = new string[]
                {
                            "cluster_status",
                            "heat_data",
                            "current_laps",
                            "leaderboard",
                            "race_status",
                            "race_format",
                            "stop_timer",
                            "stage_ready",
                            "node_crossing_change",
                            "message",
                            "first_pass_registered",
                            "priority_message"
                };

                foreach (string tolog in toLog)
                {
                    socket.On(tolog, DebugLog);
                }

                connected = true;
                Logger.TimingLog.Log(this, "Connected");

                socket.EmitAsync("ts_server_info", OnServerInfo);

                TriggerTimeSync();

                connectionCount++;
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
                connected = false;
            }
        }

        public bool Disconnect()
        {
            connected = false;

            if (socketIOClient == null)
                return true;

            if (socketIOClient.Connected)
            {
                socketIOClient.DisconnectAsync();
            }
            return true;
        }

        public bool SetListeningFrequencies(IEnumerable<ListeningFrequency> newFrequencies)
        {
            if (!Connected)
                return false;

            try
            {
                TriggerTimeSync();

                Logger.TimingLog.Log(this, "SetListeningFrequencies", string.Join(", ", newFrequencies.Select(f => f.ToString())));
                int node = 0;
                foreach (ListeningFrequency freqSense in newFrequencies)
                {
                    SetFrequency sf = new SetFrequency() { node = node, frequency = freqSense.Frequency };
                    socketIOClient.EmitAsync("set_frequency", sf);
                    node++;
                }
                return true;
            }
            catch (Exception e)
            {
                socketIOClient.DisconnectAsync();
                Logger.TimingLog.LogException(this, e);
            }
            return false;
        }
        private void OnServerInfo(SocketIOResponse response)
        {
            ServerInfo = response.GetValue<ServerInfo>();
        }

        public void TriggerTimeSync()
        {
            SocketIO socket = socketIOClient;
            if (socket == null)
                return;

            Logger.TimingLog.Log(this, "Syncing Server time");

            lock (piTimeSamples)
            {
                piTimeSamples.Clear();
            }

            piTimeStart = DateTime.Now;
            socketIOClient.EmitAsync("ts_server_time", OnServerTime);
        }

        private void OnServerTime(SocketIOResponse response)
        {
            if (response.Count == 0) return;

            DateTime now = DateTime.Now;
            TimeSpan responseTime = new TimeSpan(Environment.TickCount64);

            try
            {
                double time = response.GetValue<double>();

                TimeSpan delay = DateTime.Now - piTimeStart;
                TimeSpan oneway = delay / 2;

                PiTimeSample piTimeSample = new PiTimeSample()
                {
                    Differential = TimeSpan.FromSeconds(time) - responseTime - oneway,
                    Response = delay
                };

                lock (piTimeSamples)
                {
                    piTimeSamples.Add(piTimeSample);

                    IEnumerable<PiTimeSample> ordered = piTimeSamples.OrderBy(x => x.Response);

                    PiTimeSample first = ordered.FirstOrDefault();
                    var diffMin = first.Differential - first.Response;
                    var diffMax = first.Differential + first.Response;

                    // remove unsuable samples
                    piTimeSamples.RemoveAll(v => v.Differential < diffMin || v.Differential > diffMax);

                    if (piTimeStart + CaptureTime > now)
                    {
                        socketIOClient.EmitAsync("ts_server_time", OnServerTime);
                    }
                    else
                    {
                        IEnumerable<double> orderedSeconds = ordered.Select(x => x.Differential.TotalSeconds);

                        double median = orderedSeconds.Skip(orderedSeconds.Count() / 2).First();

                        serverEpoch = now - TimeSpan.FromSeconds(median);
                        Logger.TimingLog.Log(this, "Epoch", serverEpoch.ToLongTimeString());
                    }
                }
            }
            catch (Exception ex) 
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }

        private void OnEnvironmentData(SocketIOResponse response)
        {
            //{[[{"Core":{"temperature":{"value":47.774,"units":"\u00b0C"}}}]]}

            try
            {
                var result = response.GetValue<EnvironmentData[]>();

                if (result.Length >= 1)
                {
                    EnvironmentData value = result.First();

                    this.voltage = value.Core.voltage.value;
                    this.temperature = value.Core.temperature.value;
                }
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }

        private void HeartBeat(SocketIOResponse response)
        {
            //{[{"current_rssi":[57,57,49,41],"frequency":[5658,5695,5760,5800],"loop_time":[1020,1260,1092,1136],"crossing_flag":[false,false,false,false]}]}

            lastBeat = response.GetValue<Heartbeat>();
            connected = true;
            lastBeatTime = DateTime.Now;
        }

        private void OnNodeData(SocketIOResponse response)
        {
            try
            {
                NodeData nodeData = response.GetValue<NodeData>();

                PassRecord[] temp;
                lock (passRecords)
                {
                    temp = passRecords.ToArray();
                    passRecords.Clear();
                }

                foreach (PassRecord record in temp)
                {
                    DateTime time = serverEpoch.AddMilliseconds(record.timestamp);

                    int rssi = 0;

                    if (nodeData.pass_peak_rssi.Length > record.node)
                    {
                        rssi = nodeData.pass_peak_rssi[record.node];
                    }

                    OnDetectionEvent?.Invoke(this, record.frequency, time, rssi);
                }
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }

        private void DebugLog(SocketIOResponse response)
        {
#if DEBUG
            //Logger.TimingLog.Log(this, "Debug Log: " + response.ToString());
#endif
        }

        private void OnPassRecord(SocketIOResponse response)
        {
            PassRecord passRecord = response.GetValue<PassRecord>();
            lock (passRecords)
            {
                passRecords.Add(passRecord);
            }
        }

        public void OnHeartBeat(SocketIOResponse response)
        {
            try
            {
                lastBeat = response.GetValue<Heartbeat>();
            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }

        private void SecondaryResponse(SocketIOResponse response)
        {
            Logger.TimingLog.Log(this, "Secondary Race Format");
        }

        private void VersionResponse(SocketIOResponse response)
        {
            //{"major":"3","minor":"1"}

            try
            {
                Version = response.GetValue<Version>();
                Logger.TimingLog.Log(this, "Version " + Version.Major + "." + Version.Minor);

            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }

        private void OnFrequencyData(SocketIOResponse response)
        {
            //frequency_data {"fdata":[{"band":null,"channel":null,"frequency":5658},{"band":null,"channel":null,"frequency":5695},{"band":null,"channel":null,"frequency":5760},{"band":null,"channel":null,"frequency":5880}]})
            try
            {
                FrequencyDatas frequencyData = response.GetValue<FrequencyDatas>();
                Logger.TimingLog.Log(this, "Device listening on " + string.Join(", ", frequencyData.fdata.Select(r => r.ToString())));

            }
            catch (Exception ex)
            {
                Logger.TimingLog.LogException(this, ex);
            }
        }
        public bool StartDetection(DateTime time)
        {
            if (!Connected)
                return false;

            lock (passRecords)
            {
                passRecords.Clear();
            }

            TimeSpan serverStartTime = time - serverEpoch;

            Logger.TimingLog.Log(this, "Start detection: Server time: " + serverStartTime.TotalSeconds);
            socketIOClient.EmitAsync("ts_race_stage", GotRaceStart, new RaceStart { start_time_s = serverStartTime.TotalSeconds }); ;

            return true;
        }

        protected void GotRaceStart(SocketIOResponse reponse)
        {
            Logger.TimingLog.Log(this, "Device started Race");
            detecting = true;
        }

        public bool EndDetection()
        {
            if (!Connected)
                return false;

            if (!detecting)
                return false;

            detecting = false;

            socketIOClient.EmitAsync("stop_race", GotRaceStop);

            return true;
        }

        protected void GotRaceStop(SocketIOResponse response)
        {
            Logger.TimingLog.Log(this, "Device stopped Race");
        }

        public IEnumerable<RSSI> GetRSSI()
        {
            if (lastBeat.current_rssi == null ||
                lastBeat.frequency == null ||
                lastBeat.crossing_flag == null)
            {
                yield break;
            }


            int length = (new int[] {
                lastBeat.current_rssi.Length,
                lastBeat.frequency.Length,
                lastBeat.crossing_flag.Length
            }).Min();

            for (int i = 0; i < length; i++)
            {
                RSSI rssi = new RSSI()
                {
                    CurrentRSSI = lastBeat.current_rssi[i],
                    Frequency = lastBeat.frequency[i],
                    Detected = lastBeat.crossing_flag[i],
                    ScaleMax = 200,
                    ScaleMin = 20,
                    TimingSystem = this
                };

                yield return rssi;
            }
        }
    }


}
