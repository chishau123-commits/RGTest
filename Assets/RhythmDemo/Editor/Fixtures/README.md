# Real touch replay, 2026-09-27

`koi-touch-20260927.json` contains 66 unmatched presses recorded while the user
played Koi Kou Enishi on the 3048×2032 Android tablet, with compact lanes, speed 43
and spawn 60%. Each press had a pending Tap in the nearest lane within ±120 ms.
The original contact region rejected all 66 when replayed through the controller,
including the actual visible-note transforms. These are positional regressions,
not an assertion that every physical press should score.

The fixture preserves chart time, touch coordinates, expected note, and surrounding
note states. Android truncated long diagnostic log lines at 1024 bytes. Complete
press headers were retained; any truncated candidate states were reconstructed
from the unchanged chart and earlier successful presses. The controller replay
confirmed every rejection independently before the fix. Raw capture and analysis
are retained in `Logs/android/feel-before.raw.log` and `feel-before.json`.

Run `GeometryRhythm.Editor.RecordedTouchValidation.RunBatch` in Unity batch mode.
The regression also checks each other lane's target and the expired timing window.
Android builds run the same checks. Source charts provide note times and geometry;
this fixture contains no copied audio or cover art. Independent press replays do
not measure a player's full-song accuracy or their subjective timing feel.
