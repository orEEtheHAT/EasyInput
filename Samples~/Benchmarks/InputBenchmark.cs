using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Profiling;
using Unity.Profiling;
using Debug = UnityEngine.Debug;

namespace EasyInput.Benchmarks
{
    public sealed class InputBenchmark : MonoBehaviour
    {
        [Serializable] public sealed class Measurement
        {
            public int prompts, subscribers, iterations;
            public double inputReadMicroseconds, settingsEventMicroseconds, promptEventMicroseconds;
            public long inputReadGCBytes, settingsEventGCBytes, promptEventGCBytes, managedMemoryBytes, allocatedMemoryBytes;
            public long inputReadAllocationEvents, settingsAllocationEvents, promptAllocationEvents;
            public long callbacks;
        }
        [Serializable] public sealed class Report
        {
            public string unity, platform, processor, timestamp;
            public bool developmentBuild, virtualInputVerified, disabledSubscribersReleased;
            public long allocationCalibrationBytes;
            public double idleCoreMicroseconds;
            public int idleFrames;
            public string allocationMethod = "Calibrated GC.Alloc event recorder and GC Allocated In Frame CurrentValue deltas; no frame yield inside each measured section.";
            public List<Measurement> measurements = new List<Measurement>();
        }
        private long callbacks;
        private void OnSettingChanged() => callbacks++;
        private IEnumerator Start()
        {
            string destination = null; var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == "-easyInputReport") destination = args[i + 1];
            if (destination == null) { Debug.Log("Run the standalone player with -easyInputReport <absolute-path.json>."); yield break; }
            Application.runInBackground = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            foreach (var existing in FindObjectsByType<PlayerInputSettings>(FindObjectsSortMode.None)) existing.enabled = false;
            var root = new GameObject("Benchmark player"); root.SetActive(false);
            var player = root.AddComponent<PlayerInputSettings>(); player.ControlsCursor = false; root.SetActive(true);
            var keyboard = InputSystem.AddDevice<Keyboard>(); var gamepad = InputSystem.AddDevice<Gamepad>(); player.PairDevices(keyboard, gamepad);
            var report = new Report { unity = Application.unityVersion, platform = Application.platform.ToString(), processor = SystemInfo.processorType,
                timestamp = DateTime.UtcNow.ToString("O"), developmentBuild = Debug.isDebugBuild };
            bool profilerWasEnabled = Profiler.enabled; Profiler.enabled = true;
            var recorderOptions = ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.CollectOnlyOnCurrentThread | ProfilerRecorderOptions.WrapAroundWhenCapacityReached;
            using var allocationEvents = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 1, recorderOptions);
            using var allocationBytes = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            if (!allocationEvents.Valid || !allocationBytes.Valid) throw new InvalidOperationException("Allocation counters require a Development Build.");
            allocationEvents.Start(); long calibrationStart = allocationBytes.CurrentValue;
            var calibration = new byte[4096]; report.allocationCalibrationBytes = allocationBytes.CurrentValue - calibrationStart;
            allocationEvents.Stop(); GC.KeepAlive(calibration);
            if (report.allocationCalibrationBytes < 4096 || allocationEvents.Count == 0 || allocationEvents.GetSample(0).Count < 1)
                throw new InvalidOperationException("Allocation calibration failed. Zero-byte claims would be invalid.");
            using (var idle = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "EasyInput.Update", 64))
            {
                for (int frame = 0; frame < 60; ++frame) yield return null;
                report.idleFrames = idle.Count; long total = 0;
                for (int frame = 0; frame < idle.Count; ++frame) total += idle.GetSample(frame).Value;
                report.idleCoreMicroseconds = idle.Count > 0 ? total / (1000.0 * idle.Count) : -1;
            }
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.right }); yield return null;
            var look = player.Action("Gameplay/Look"); report.virtualInputVerified = look.ReadValue<Vector2>().x > .9f;
            foreach (int count in new[] { 1, 100, 2000 })
            {
                var prompts = new GameObject("Prompts"); prompts.SetActive(false); prompts.transform.SetParent(root.transform);
                for (int i = 0; i < count; ++i)
                {
                    var child = new GameObject("Prompt"); child.transform.SetParent(prompts.transform);
                    var prompt = child.AddComponent<InputPrompt>(); prompt.Player = player;
                    player.SettingsChanged += OnSettingChanged;
                }
                prompts.SetActive(true); yield return null;
                for (int warm = 0; warm < 5; ++warm) { player.MouseSensitivity = warm % 2 == 0 ? 1 : 1.01f; }
                const int reads = 100000; const int changes = 100;
                var m = new Measurement { prompts = count, subscribers = count, iterations = reads };
                var clock = new Stopwatch(); allocationEvents.Reset(); allocationEvents.Start(); long before = allocationBytes.CurrentValue; clock.Start();
                Vector2 sum = Vector2.zero;
                for (int i = 0; i < reads; ++i) sum += player.ReadLookDelta(look, 1f/60);
                clock.Stop(); m.inputReadMicroseconds = clock.Elapsed.TotalMilliseconds * 1000 / reads; m.inputReadGCBytes = allocationBytes.CurrentValue - before;
                allocationEvents.Stop(); m.inputReadAllocationEvents = allocationEvents.Count > 0 ? allocationEvents.GetSample(0).Count : 0;
                callbacks = 0; allocationEvents.Reset(); allocationEvents.Start(); before = allocationBytes.CurrentValue; clock.Restart();
                for (int i = 0; i < changes; ++i) player.MouseSensitivity = i % 2 == 0 ? 1.02f : 1;
                clock.Stop(); m.settingsEventMicroseconds = clock.Elapsed.TotalMilliseconds * 1000 / changes; m.settingsEventGCBytes = allocationBytes.CurrentValue - before; m.callbacks = callbacks;
                allocationEvents.Stop(); m.settingsAllocationEvents = allocationEvents.Count > 0 ? allocationEvents.GetSample(0).Count : 0;
                var standard = player.Profile; var aim = Resources.Load<InputProfile>("EasyInputAiming");
                allocationEvents.Reset(); allocationEvents.Start(); before = allocationBytes.CurrentValue; clock.Restart();
                for (int i = 0; i < changes; ++i) player.SetProfile(i % 2 == 0 ? aim : standard);
                clock.Stop(); m.promptEventMicroseconds = clock.Elapsed.TotalMilliseconds * 1000 / changes; m.promptEventGCBytes = allocationBytes.CurrentValue - before;
                allocationEvents.Stop(); m.promptAllocationEvents = allocationEvents.Count > 0 ? allocationEvents.GetSample(0).Count : 0;
                m.managedMemoryBytes = GC.GetTotalMemory(false); m.allocatedMemoryBytes = Profiler.GetTotalAllocatedMemoryLong(); report.measurements.Add(m);
                prompts.SetActive(false); for (int i = 0; i < count; ++i) player.SettingsChanged -= OnSettingChanged;
                long previous = callbacks; player.MouseSensitivity = 1.5f; report.disabledSubscribersReleased = callbacks == previous;
                Destroy(prompts); yield return null;
                if (sum.x <= 0) report.virtualInputVerified = false;
            }
            InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(gamepad);
            Profiler.enabled = profilerWasEnabled;
            File.WriteAllText(destination, JsonUtility.ToJson(report, true)); Debug.Log("EasyInput benchmark: " + destination); Application.Quit(report.virtualInputVerified && report.disabledSubscribersReleased ? 0 : 1);
        }
    }
}
