// Copyright 2022-2024 Niantic.
using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Niantic.Lightship.AR.Utilities.Metrics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.ARSubsystems;

namespace Niantic.Lightship.AR.InternalSamples
{
    public class DebugMetrics : MonoBehaviour
    {
        [Tooltip("Time between performance checks in seconds (0 = every frame)")]
        [SerializeField] private float QueryRate = 0.1f;

        [Tooltip("Time frame for cumulative stats (e.g. averages, min/max, etc) in seconds")]
        [SerializeField] private float CumulativeDuration = 60;

        [Tooltip("Percentile to display as \"Low FPS\" (0.01 = 99% of frames were faster than this fps)")]
        [SerializeField] [Range(0, 1)] private float LowFpsPercentile = 0.01f;

        [Header("UI Elements")]
        [SerializeField] private Text InstFpsValueText;
        [SerializeField] private Text AvgFpsValueText;
        [SerializeField] private Text MaxFpsValueText;
        [SerializeField] private Text LowFpsValueText;
        [SerializeField] private Text MinFpsValueText;
        [SerializeField] private Text DepthFpsValueText;
        [SerializeField] private Text SegmentationFpsValueText;
        [SerializeField] private Text MeshFpsValueText;
        [SerializeField] private Text objectDetectionFpsValueText;
        [SerializeField] private Text CpuLoadValueText;
        [SerializeField] private Text UsedMemoryValueText;
        [SerializeField] private Text FreeMemoryValueText;
        [SerializeField] private Text ProcessMemoryValueText;
        [SerializeField] private Text UnityObjects;
        [SerializeField] private Text UnityHeap;
        [SerializeField] private Text DrawCallsValueText;
        [SerializeField] private Text TriangleCountValueText;
        [SerializeField] private Text TrackingStateValueText;

        // Time elapsed since last query
        private float _timeSinceLastQuery;

        // Unity profiler api
        private ProfilerRecorder _drawCallsRecorder;
        private ProfilerRecorder _triangleCountRecorder;

        // Subsystem data for measuring tracking state
        private XRSessionSubsystem _xrSubsystem;

        // Holds data from a past query for tracking cumulative stats
        private class Snapshot
        {
            public int FrameCount { get; }
            public float Timestamp { get; }
            public float FPS { get; }

            public Snapshot(int frameCount, float timestamp, float fps)
            {
                FrameCount = frameCount;
                Timestamp = timestamp;
                FPS = fps;
            }

            public static readonly Snapshot Zero = new(0, 0, float.NaN);

            public class ByFPS : IComparer<Snapshot>
            {
                public int Compare(Snapshot x, Snapshot y)
                {
                    if (ReferenceEquals(x, y)) return 0;
                    if (ReferenceEquals(null, y)) return 1;
                    if (ReferenceEquals(null, x)) return -1;
                    int res = x.FPS.CompareTo(y.FPS);
                    if (res == 0) return x.FrameCount.CompareTo(y.FrameCount);
                    return res;
                }
            }
        }

        // For accessing the snapshot {CumulativeDuration} seconds ago
        private readonly Queue<Snapshot> _snapshots = new();

        // Keeps only the snapshots with highest/lowest FPS since their timestamp, for accessing a moving max/min
        private readonly LinkedList<Snapshot> _runningMax = new();
        private readonly LinkedList<Snapshot> _runningMin = new();

        // Holds all snapshots in FPS-sorted order, with log(n) insertion and removal, and O(m) access of mth element
        private readonly SortedSet<Snapshot> _sortedFPS = new(new Snapshot.ByFPS());

        // TODO: Reduce overhead from FPSMetricsUtility
        // Utility for calculating FPS from timestamps
#if USE_FPS_METRICS_UTILITY
        private FPSMetricsUtility _fpsMetricsUtility;
#endif

        private void Start()
        {
            // Set up profilers
            _drawCallsRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _triangleCountRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count");

            if (XRGeneralSettings.Instance is { Manager: { activeLoader: { } loader } })
            {
                _xrSubsystem = loader.GetLoadedSubsystem<XRSessionSubsystem>();
            }

#if USE_FPS_METRICS_UTILITY
            _fpsMetricsUtility = new FPSMetricsUtility();
#endif
            _snapshots.Clear();
            _timeSinceLastQuery = 0;
        }

        private void Update()
        {
            // Wait until desired time has passed before re-calculating metrics
            _timeSinceLastQuery += Time.unscaledDeltaTime;
            if (_timeSinceLastQuery < QueryRate) return;
            _timeSinceLastQuery = 0;

            // Add new snapshot and update data structures
            int frameCount = Time.frameCount;
            float timestamp = Time.realtimeSinceStartup;
            float instFps = 1f / Time.deltaTime;
            AddSnapshot(new Snapshot(frameCount, timestamp, instFps));

            // Calculate cumulative stats
            Snapshot oldest = _snapshots.Count > 0 ? _snapshots.Peek() : Snapshot.Zero;
            float avgFps = (frameCount - oldest.FrameCount) / (timestamp - oldest.Timestamp);

            float maxFps = _runningMax.First.Value.FPS;
            float minFps = _runningMin.First.Value.FPS;

            int percentileIdx = (int)(_sortedFPS.Count * LowFpsPercentile);
            float lowFps = _sortedFPS.ElementAt(percentileIdx).FPS;

            // Fetch lightship API stats
            bool gotCpuLoad = MetricsUtility.GetCpuLoad(out float cpuLoad);
            bool gotDeviceMemory = MetricsUtility.GetDeviceMemoryUsage(out ulong usedMemory, out ulong freeMemory);
            bool gotProcessMemory = MetricsUtility.GetProcessMemoryUsage(out ulong processMemory);

            // Fetch Unity API stats
            float unityObjects = Profiler.GetMonoUsedSizeLong() / 1000000f;
            float unityHeap = Profiler.GetMonoHeapSizeLong() / 1000000f;
            float drawCallsCount = _drawCallsRecorder.LastValue;
            float triangleCount = _triangleCountRecorder.LastValue;
            TrackingState state = _xrSubsystem?.trackingState ?? TrackingState.None;

            // FPS Data from FramerateMetricsUtility
#if USE_FPS_METRICS_UTILITY
            float depthFps = _fpsMetricsUtility.GetInstantDepthFPS();
            float segmentationFps = _fpsMetricsUtility.GetInstantSemanticsFPS();
            float meshFps = _fpsMetricsUtility.GetInstantMeshFPS();
            float objectDetectionFps = _fpsMetricsUtility.GetInstantObjectDetectionFPS();
#endif

            // Update UI
            InstFpsValueText.text = $"{instFps:0.00}";
            // AvgFpsValueText.text = $"{avgFps:0.00}";
            // MaxFpsValueText.text = $"{maxFps:0.00}";
            // LowFpsValueText.text = $"{lowFps:0.00}";
            // MinFpsValueText.text = $"{minFps:0.00}";
#if USE_FPS_METRICS_UTILITY
            DepthFpsValueText.text = $"{depthFps:0.00}";
            SegmentationFpsValueText.text = $"{segmentationFps:0.00}";
            MeshFpsValueText.text = $"{meshFps:0.00}";
            objectDetectionFpsValueText.text = $"{objectDetectionFps:0.00}";
#endif
            CpuLoadValueText.text = gotCpuLoad ? $"{cpuLoad:0.00}%" : "--";
            UsedMemoryValueText.text = gotDeviceMemory ? $"{usedMemory:n0} MB" : "--";
            FreeMemoryValueText.text = gotDeviceMemory ? $"{freeMemory:n0} MB" : "--";
            ProcessMemoryValueText.text = gotProcessMemory ? $"{processMemory:n0} MB" : "--";
            UnityObjects.text = $"{unityObjects:n0} MB";
            UnityHeap.text = $"{unityHeap:n0} MB";
            DrawCallsValueText.text = $"{drawCallsCount:n0}";
            TriangleCountValueText.text = $"{triangleCount:n0}";
            TrackingStateValueText.text = _xrSubsystem != null ? $"{Enum.GetName(typeof(TrackingState), state)}" : "--";
        }

        private void AddSnapshot(Snapshot now)
        {
            // Clear snapshots that are older than the duration
            float durationStart = now.Timestamp - CumulativeDuration;
            List<Snapshot> staleSnapshots = new();
            while (_snapshots.Count > 0 && _snapshots.Peek().Timestamp < durationStart)
            {
                staleSnapshots.Add(_snapshots.Dequeue());
            }

            while (_runningMax.Count > 0 && _runningMax.First.Value.Timestamp < durationStart)
            {
                _runningMax.RemoveFirst();
            }

            while (_runningMin.Count > 0 && _runningMin.First.Value.Timestamp < durationStart)
            {
                _runningMin.RemoveFirst();
            }

            foreach (Snapshot stale in staleSnapshots)
            {
                _sortedFPS.Remove(stale);
            }

            // Any future time frame that contains snapshots from before now will necessarily also contain now.
            // Therefore, if now has higher/lower FPS than any previous snapshots, they can never be a max/min.
            // Tracking this way allows the current max/min to be calculated in roughly constant time.
            while (_runningMax.Count > 0 && now.FPS > _runningMax.Last.Value.FPS)
            {
                _runningMax.RemoveLast();
            }

            while (_runningMin.Count > 0 && now.FPS < _runningMin.Last.Value.FPS)
            {
                _runningMin.RemoveLast();
            }

            // Add current snapshot
            _snapshots.Enqueue(now);
            _runningMax.AddLast(now);
            _runningMin.AddLast(now);
            _sortedFPS.Add(now);
        }

        private void OnDestroy()
        {
#if USE_FPS_METRICS_UTILITY
            _fpsMetricsUtility.Dispose();
#endif
        }
    }
}
