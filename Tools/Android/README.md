# Android frame measurement

Measure the Unity SurfaceView's actual presentation times while the game is in the foreground.
Android `gfxinfo` measures the activity UI and can report zero frames for this Vulkan player.

```powershell
python Tools/Android/measure_frames.py --seconds 10 --minimum-fps 115 --output Logs/android/gameplay-fps.json
```

The script uses `ADB`, an `adb` executable on PATH, or this project's bundled Unity 2022.3 SDK.
Use `--adb <absolute-path>` for another SDK. It discards the initial compositor history, deduplicates
present timestamps, and rejects incomplete captures when the player is stopped or replaced.
Do not install another APK or switch applications during capture. Exit code 1 means the requested
minimum FPS was not met (or that the capture was invalid); the threshold is an explicit test target,
not a claim about device capability.

Compare the same song, passage, reading speed and play mode before/after a change. Reports contain
average FPS, median/p95/max frame interval and intervals above 25 ms. They do not measure touch
hardware latency, CPU/GPU execution time, thermal stability or sustained battery performance.

The independent single-finger regression entry point is
`GeometryRhythm.Editor.AndroidGameplayValidation.RunBatch`. It exercises the controller's real
projection hit test and consecutive-frame judgement/expiry path. `DemoBuildTools.BuildAndroid`
runs it along with the existing rule checks before packaging.

## Xiaomi high-refresh regression

On the tested Xiaomi Pad (HyperOS OS3.0.304.0.WNXCNXM), a Surface/window request alone
can remain capped at 60 Hz. The player now retains its highest supported frame target
and sends PowerKeeper's optional, package-scoped game FPS request on launch/resume.
This vendor extension is best-effort; other firmware can ignore it and use the standard
Android display hints. The APK does not write system settings or need elevated permissions.

For an actual cold-start regression, first stop **this game only**, then use ADB to set
its vendor FPS override to 60, and launch the final APK. Do not send a 144 FPS override
from ADB during the positive test: the APK must request it itself.
Set `$adb` below to your Android SDK's `adb.exe` path.

```powershell
& $adb shell am force-stop com.geometryrhythm.demo
& $adb shell am broadcast -a com.xiaomi.joyose.OVERRIDE_GAME_FRESHRATE -p com.miui.powerkeeper `
  --es override_pkg_name com.geometryrhythm.demo --ei override_freshrate 60
& $adb shell am start -n com.geometryrhythm.demo/com.unity3d.player.UnityPlayerActivity
# Enter the same song/passage, then measure:
python Tools/Android/measure_frames.py --seconds 30 --minimum-fps 137 --output Logs/android/high-refresh-regression.json
```

Repeat after returning from the background, resetting the override to 60 while the game
is in the background. These tests exercise the real OEM policy and Surface presentation;
an editor-only assertion of `Application.targetFrameRate` cannot verify either.
