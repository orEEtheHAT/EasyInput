# Benchmark

**Compatibility:** EasyInput 0.2.0+. No prerequisite sample imports.

Add InputBenchmark to a scene, include that scene in a Windows Development player, and run the executable with `-batchmode -nographics -easyInputReport <absolute-path.json>`. The player exits after writing JSON. No Editor is required to run the finished player.

The report measures action reading/look conversion, settings event dispatch and profile/prompt updates at 1, 100 and 2000 prompts. It records CPU time, calibrated main-thread GC.Alloc event counts, GC Allocated In Frame CurrentValue byte deltas across synchronous sections, and memory after setup. The byte counter is frame-wide, not thread-attributed; the event recorder is filtered to the calling thread. A positive allocation control must succeed before measurements are accepted. Prompt changes may allocate strings; idle prompts are event-driven and do not poll. Virtual gamepad state verifies live input, and removal of subscribers is checked after disable.

These main-thread snapshots and short loops are not an end-to-end frame budget. Repeat on target hardware with your UI and controllers. Physical device transport latency is not measured.
