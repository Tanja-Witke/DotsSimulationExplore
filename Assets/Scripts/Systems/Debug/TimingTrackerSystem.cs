#if DEBUG_SYSTEM_DURATION || DEBUG_SYSTEM_ORDER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Unity.Entities;

[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(ApplyCollisionEventImpactSystem))]
[UpdateBefore(typeof(SpawnSystem))]
[UpdateBefore(typeof(InDirectionStrategySystem))]
[UpdateBefore(typeof(RandomDirectionStrategySystem))]
[UpdateBefore(typeof(CollisionSystem))]
[UpdateBefore(typeof(ColorSystem))]
[UpdateBefore(typeof(DeathSystem))]
[UpdateBefore(typeof(InitDotSystem))]
[UpdateBefore(typeof(InitGhostSystem))]
[UpdateBefore(typeof(LevelSystem))]
[UpdateBefore(typeof(ShootSystem))]
[UpdateBefore(typeof(TriggerSystem))]
[UpdateBefore(typeof(VelocitySystem))]
[UpdateAfter(typeof(TickSystem))]
public partial struct TimingTrackerSystem : ISystem
{
    private static readonly ConcurrentQueue<(int tick, string name, long timestamp, bool isStart, double additionalTime)> _events = new();
    private static readonly Dictionary<int, Dictionary<string, double>> _tickLogs = new();
    private static readonly Dictionary<int, double> _wholeTickDurations = new();
    private static readonly SortedSet<string> _allSystems = new();
    private static bool _running = false;
    private static float _remainingSeconds = 10f;
    private static bool _exported = false;
    private static string _filePath = string.Empty;
    private static int _tick;
    private static int _lastTick = -1;
    private static long _firstStartTimestamp = -1;
    private static long _lastStopTimestamp = -1;
    private static bool _hasStarted;

    private static int _activeSystemCounter = 0;
    private static long _tickTimerStart = -1;
    private static double _accumulatedTickTime = 0;

    public static void Start(string systemName)
    {
        if (!_hasStarted || _exported)
        {
            return;
        }

        long timestamp = Stopwatch.GetTimestamp();
#if DEBUG_SYSTEM_ORDER
        UnityEngine.Debug.Log($"[TimingTracker] Start {systemName}");
#endif

        if (_firstStartTimestamp < 0 || _tick != _lastTick)
        {
            if (_firstStartTimestamp >= 0 && _lastStopTimestamp >= 0 && _lastTick >= 0)
            {
                _wholeTickDurations[_lastTick] = _accumulatedTickTime;
            }
            _firstStartTimestamp = timestamp;
            _lastTick = _tick;
            _accumulatedTickTime = 0;
        }

        if (_activeSystemCounter == 0)
        {
            _tickTimerStart = timestamp;
        }

        _activeSystemCounter++;
        _events.Enqueue((_tick, systemName, timestamp, true, 0));
    }

    public static void Stop(string systemName, double additionalTime = 0.0)
    {
        if (!_hasStarted || _exported)
        {
            return;
        }
#if DEBUG_SYSTEM_ORDER
        UnityEngine.Debug.Log($"[TimingTracker] ________________Stop {systemName}");
#endif
        long timestamp = Stopwatch.GetTimestamp();
        _lastStopTimestamp = timestamp;

        _activeSystemCounter--;
        if (_activeSystemCounter == 0 && _tickTimerStart > 0)
        {
            double delta = (timestamp - _tickTimerStart) / (double)Stopwatch.Frequency * 1000.0;
            _accumulatedTickTime += delta;
            _tickTimerStart = -1;
        }

        _events.Enqueue((_tick, systemName, timestamp, false, additionalTime));
    }


    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationTick>();

        if (string.IsNullOrEmpty(_filePath))
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            Directory.CreateDirectory("Logs");
            _filePath = $"Logs/timing_{timestamp}.csv";
            UnityEngine.Debug.Log("[TimingTracker] Initialized.");
        }
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_exported) return;

        if (!_running)
        {
            _running = true;
            return;
        }

        if (!_hasStarted)
        {
            var settings = SystemAPI.GetSingleton<GameSettingsBlobComponent>().Blob;
            var counter = SystemAPI.GetSingleton<AliveDotCounterComponent>().AliveDotCounter;
            if (counter >= settings.Value.MaxDots)
            {
                _hasStarted = true;
            }
            else
            {
                return;
            }
        }

        _remainingSeconds -= SystemAPI.Time.DeltaTime;

        _tick = SystemAPI.GetSingleton<SimulationTick>().Value;

        if (_remainingSeconds <= 0f)
        {
            if (_firstStartTimestamp >= 0 && _lastStopTimestamp >= 0 && !_wholeTickDurations.ContainsKey(_tick))
            {
                _wholeTickDurations[_tick] = _accumulatedTickTime;
            }

            ProcessEvents();
            ExportToCsv();
            _exported = true;
        }
    }

    private static void ProcessEvents()
    {
        var activeTimers = new Dictionary<string, long>();

        while (_events.TryDequeue(out var ev))
        {
            if (!_tickLogs.ContainsKey(ev.tick))
                _tickLogs[ev.tick] = new Dictionary<string, double>();

            if (ev.isStart)
            {
                activeTimers[ev.name] = ev.timestamp;
                _allSystems.Add(ev.name);
            }
            else if (activeTimers.TryGetValue(ev.name, out var startTime))
            {
                double ms = (ev.timestamp - startTime) / (double)Stopwatch.Frequency * 1000.0;
                _tickLogs[ev.tick][ev.name] = ms + ev.additionalTime;
            }
        }
    }

    private static void ExportToCsv()
    {
#if DEBUG_SYSTEM_DURATION
        var allSystems = new List<string> { "WholeTick" };
        allSystems.AddRange(_allSystems);

        var systemValues = new Dictionary<string, List<double>>();
        foreach (var sys in allSystems)
            systemValues[sys] = new List<double>();

        var allTicks = _tickLogs.Keys.Concat(_wholeTickDurations.Keys).Distinct().OrderBy(t => t).ToList();

        foreach (var tick in allTicks)
        {
            foreach (var sys in _allSystems)
            {
                if (_tickLogs.TryGetValue(tick, out var tickData) && tickData.TryGetValue(sys, out var value))
                    systemValues[sys].Add(value);
                else
                    systemValues[sys].Add(0.0);
            }

            if (_wholeTickDurations.TryGetValue(tick, out var wt))
                systemValues["WholeTick"].Add(wt);
            else
                systemValues["WholeTick"].Add(0.0);
        }

        using StreamWriter writer = new StreamWriter(_filePath);

        void WriteStatRow(string label, Func<List<double>, double> statFunc)
        {
            writer.Write(label);
            foreach (var sys in allSystems)
            {
                var values = systemValues[sys];
                var stat = values.Count > 0 ? statFunc(values) : 0.0;
                writer.Write($",{stat.ToString("F4", CultureInfo.InvariantCulture)}");
            }
            writer.WriteLine();
        }

        WriteStatRow("Sum", values => values.Sum());
        WriteStatRow("Avg", values => values.Count > 0 ? values.Average() : 0);
        WriteStatRow("Median", values =>
        {
            if (values.Count == 0) return 0;
            var sorted = values.OrderBy(v => v).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0 ? (sorted[mid - 1] + sorted[mid]) / 2.0 : sorted[mid];
        });
        WriteStatRow("Min", values => values.Count == 0 ? 0 : values.Min());
        WriteStatRow("Max", values => values.Count == 0 ? 0 : values.Max());

        writer.Write("Tick");
        foreach (var sys in allSystems)
            writer.Write($",{sys}");
        writer.WriteLine();

        for (int i = 0; i < allTicks.Count; i++)
        {
            writer.Write(allTicks[i]);
            foreach (var sys in allSystems)
            {
                var values = systemValues[sys];
                writer.Write($",{values[i].ToString("F4", CultureInfo.InvariantCulture)}");
            }
            writer.WriteLine();
        }

        UnityEngine.Debug.Log($"[TimingTracker] Exported to {_filePath}");
#endif
    }
}
#endif