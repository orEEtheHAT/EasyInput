# Standalone performance, 2026-11-12

Unity 6000.3.22f1, Windows x64 Development/Mono players, Intel Core i7-14700KF. The main player was built from Games Factory; the clean player contains EasyInput and Input System without other Easy packages. Both ran as separate hidden players with `-batchmode -nographics`; these are CPU measurements, not GPU/rendering or physical-input-latency measurements.

The benchmark calibrates Unity's GC.Alloc recorder and GC Allocated In Frame counter with a 4096-byte array. Both players measured **4128 bytes** for that control allocation. It then measures synchronous sections without yielding a frame. A zero-allocation result is checked by both byte count and allocation-event count. `GC.GetAllocatedBytesForCurrentThread` was not used for the reported results because it returned zero for allocating workloads in this runtime.

| Workload | Games Factory player | Clean player | GC allocation |
|---|---:|---:|---:|
| Idle PlayerInputSettings.Update, mean of 60 frames | 14.32 µs | 18.33 µs | Not isolated by this marker |
| Read action + convert look, 100,000 reads | 0.59–0.61 µs/read | 0.59–0.60 µs/read | 0 bytes / 0 allocation events |
| Settings change, 1 subscriber | 0.94 µs | 0.96 µs | 40 bytes/change |
| Settings change, 100 subscribers | 1.70 µs | 1.32 µs | 40 bytes/change |
| Settings change, 2000 subscribers | 9.46 µs | 7.81 µs | 40 bytes/change |
| Profile/presentation change, 1 prompt | 13.91 µs | 13.11 µs | 226 bytes/change |
| Profile/presentation change, 100 prompts | 427.90 µs | 421.02 µs | 5374 bytes/change |
| Profile/presentation change, 2000 prompts | 7.08 ms | 7.52 ms | 104174 bytes/change |

Presentation changes rebuild display strings; prompts otherwise do not poll each frame. Core presentation results are cached per action/binding and invalidated on relevant events. The small settings-change allocation comes from value validation, not event dispatch. Rebinding, save JSON, subscription changes and building UI are setup/user operations and are not allocation-free APIs.

At 2000 prompts the main player's tracked Unity allocation was about 87.88 MB and managed memory about 5.33 MB; the clean player was about 78.26 MB / 3.92 MB. These are snapshots after measured updates, including Unity/profiler state and uncollected strings, not OS working-set measurements or isolated retained memory. Creation and warmup are excluded from the CPU sections. Runs were sequential on a shared development workstation; background development activity and profiler overhead affect timing, so these are observed runs rather than hardware-isolated bounds.

Both players verified live virtual-gamepad input and zero settings callbacks after subscriber removal. The numbers do not justify Jobs/Burst or a more complex input API. Measure your actual UI rendering, hardware and player count before adding further optimization.

Raw reports: [main player](Reports/benchmark-main.json), [clean player](Reports/benchmark-clean.json). Reproduction is in Samples~/Benchmarks. Use a Development Build so allocation counters are available.
