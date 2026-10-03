"""Generate original, deterministic 36-second metronome and prototype chart.

Only Python stdlib is needed. No reference-video music/assets are redistributed.
"""
import json
import math
from pathlib import Path
import struct
import wave

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / "Assets/RingGame/Resources"
PPQ = 960


def seconds(tick):
    # Tick 0 is audio zero; BPM changes after beat 32 (16 seconds).
    return min(tick, 32 * PPQ) / PPQ * .5 + max(0, tick - 32 * PPQ) / PPQ * .4


def generate():
    chart = {
        "schemaVersion": "prototype-ring-0",
        "timebase": {"ppq": PPQ, "offsetUs": 0, "tempos": [
            {"tick": 0, "bpm": 120}, {"tick": 32 * PPQ, "bpm": 150}]},
        "settings": {"title": "Ring Lab / Original Metronome", "requiredTouches": 3},
        "notes": [], "paths": [], "actions": [], "decorations": []}
    positions = [(-35, 10), (30, -14), (0, 20), (-30, -18), (35, 16), (0, -18)]
    for index, beat in enumerate(range(8, 72, 2)):
        chord = beat in (20, 44, 60)
        targets = [(-34, -12), (0, 17), (34, -12)] if chord else [positions[index % 6]]
        for finger, (x, y) in enumerate(targets):
            note_id = f"n{index:02d}-{finger}"
            arrival = (index + finger) % 2 == 1
            target = {"x": x, "y": y}
            path_id = f"p-{note_id}" if arrival else ""
            chart["notes"].append({"id": note_id, "tick": beat * PPQ,
                "spawnTick": (beat - 2) * PPQ, "motion": "arrival" if arrival else "shrink",
                "target": target, "radius": 6, "pathId": path_id})
            if arrival:
                chart["paths"].append({"id": path_id, "type": "linear",
                    "start": {"x": x - 24 if x >= 0 else x + 24, "y": y + 22}, "end": target})
    # Absolute poses with non-overlapping tick durations; continuous motion at note hits.
    for index, (beat, x, y, rotation, scale) in enumerate([
        (12, 9, -4, 24, 1.06), (20, -8, 4, -25, .90),
        (28, 6, 1, 18, 1.04), (36, -6, -3, -22, .92),
        (44, 8, 2, 27, 1.02), (52, -7, 4, -20, .90),
        (60, 5, -2, 15, 1.0), (68, 0, 0, 0, 1.0)]):
        chart["actions"].append({"id": f"cam-{index}", "eventType": "MoveCamera",
            "tick": beat * PPQ, "durationTicks": 8 * PPQ,
            "position": {"x": x, "y": y}, "rotation": rotation, "scale": scale, "ease": "smooth"})
    (RES / "Charts").mkdir(parents=True, exist_ok=True)
    (RES / "Charts/prototype.json").write_text(json.dumps(chart, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    rate = 48000
    samples = [0.] * (rate * 36)
    for beat in range(82):
        moment = seconds(beat * PPQ)
        start = round(moment * rate)
        for n in range(round(.035 * rate)):
            at = start + n
            if at >= len(samples):
                break
            hz = 1500 if beat % 4 == 0 else 900
            samples[at] += .36 * math.sin(2 * math.pi * hz * n / rate) * math.exp(-n / (rate * .006))
    (RES / "Audio").mkdir(parents=True, exist_ok=True)
    with wave.open(str(RES / "Audio/metronome.wav"), "wb") as wav:
        wav.setparams((1, 2, rate, len(samples), "NONE", "not compressed"))
        wav.writeframes(b"".join(struct.pack("<h", round(max(-1., min(1., value)) * 32767)) for value in samples))
    print(f"Generated {len(chart['notes'])} notes, 3-finger chords, 36s PCM audio")


if __name__ == "__main__":
    generate()
