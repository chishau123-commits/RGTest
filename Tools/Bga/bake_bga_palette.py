#!/usr/bin/env python3
"""离线烘焙视频 BGA 的色板（paletteKeys）。

为什么要有这个工具：运行时的 Note 颜色目前硬编码在 SceneVisuals.cs 里，而「Note 颜色贴合
视频风格」需要一个权威来源。方案（Docs/VideoBgaBranchingPlan.md 决定 10）定的是**离线烘焙
进谱面**，而不是运行时从画面取色——自动取色会抖，而且会取到低对比度的脏色，直接伤害可读性。
运行时从画面取到的颜色只允许驱动氛围量（雾、泛光）。

做法：用 ffmpeg 把每一秒压成 1×1 像素（等于该秒整帧的平均色），按 segment-seconds 分组求均值，
再由均值派生三个颜色：

    glow  视频自己的平均色 —— 驱动氛围量（雾、泛光、路径线），允许「脏」
    tap   同色相的高饱和强调色，亮度被推到视频亮度的另一端 —— 保证与背景有亮度差
    drag  近乎白、带一点互补色相 —— 保住「长按 = 白色」这个身份

亮度规则是刻意的：画面亮就给暗而饱和的 Note，画面暗就给亮的 Note，这样 Note 在任何一段
视频上都读得出来。谱师可以手工覆盖烘焙结果（决定 10 的「手工兜底」）。

用法：
    python Tools/Bga/bake_bga_palette.py \
        --video BGA/goodtek60/video.mp4 \
        --chart BGA/goodtek60/runtime-chart.json \
        --segment-seconds 8 \
        --out BGA/goodtek60/palette.json \
        --patch-chart          # 顺便把 paletteKeys 写回 chart

需要 PATH 上有 ffmpeg（或用 --ffmpeg 指定）。仓库里没有随附 ffmpeg，见
Docs/VideoBgaSeekSpikeVerification.md §8 列出的本机可用的那几个。
"""

import argparse
import colorsys
import json
import subprocess
import sys


def read_average_colors(video, ffmpeg, fps):
    """每秒一帧、缩到 1×1，读回 RGB 三元组。返回 [(second, (r, g, b)), ...]，分量是 0..1。"""
    command = [
        ffmpeg, "-v", "error", "-i", video,
        "-vf", "fps=%d,scale=1:1:flags=area" % fps,
        "-f", "rawvideo", "-pix_fmt", "rgb24", "pipe:1",
    ]
    raw = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE).stdout
    if len(raw) < 3:
        raise SystemExit("ffmpeg 没有输出任何像素：%s" % video)
    samples = []
    for index in range(len(raw) // 3):
        r, g, b = raw[index * 3], raw[index * 3 + 1], raw[index * 3 + 2]
        samples.append((index / float(fps), (r / 255.0, g / 255.0, b / 255.0)))
    return samples


def luma(rgb):
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def derive_colors(average, tap_luma):
    """由一段视频的平均色派生 glow / tap / drag。

    可读性的对照物是**运行时自己的世界底色**，不是视频：Note 画在浅色暖雾世界之上
    （雾色 ≈ (0.965,0.945,0.91)，亮度约 0.94），所以 tap 只需要保证「亮度不高于 tap_luma」
    就能和背景拉开差距。视频平均色只用来定色相，不用来定亮度——视频亮不亮与 Note 读不读得出来
    是两件事。
    """
    r, g, b = average
    hue, saturation, value = colorsys.rgb_to_hsv(r, g, b)
    tap = colorsys.hsv_to_rgb(hue, 0.88, value_for_luma(hue, 0.88, tap_luma))
    # 长按保持「白」的身份，只掺一点互补色相，避免与 Tap 混成同一族。
    drag_hue = (hue + 0.5) % 1.0
    drag = colorsys.hsv_to_rgb(drag_hue, 0.10, 0.99)
    return tap, drag, average


def value_for_luma(hue, saturation, target_luma):
    """在固定色相/饱和度下二分出最接近目标亮度的 V。亮度对 V 单调，所以二分成立。

    目标亮度在该色相下够不到时（比如纯蓝的亮度上限本来就不高），返回 1.0——那时 Note
    比目标更暗，与世界底色的差距只会更大，是可读的。
    """
    if luma(colorsys.hsv_to_rgb(hue, saturation, 1.0)) <= target_luma:
        return 1.0
    low, high = 0.02, 1.0
    for _ in range(40):
        mid = (low + high) / 2.0
        if luma(colorsys.hsv_to_rgb(hue, saturation, mid)) < target_luma:
            low = mid
        else:
            high = mid
    return (low + high) / 2.0


def mean_rgb(samples):
    count = len(samples)
    if count == 0:
        return (0.0, 0.0, 0.0)
    totals = [0.0, 0.0, 0.0]
    for rgb in samples:
        for i in range(3):
            totals[i] += rgb[i]
    return tuple(total / count for total in totals)


def seconds_to_tick(seconds, ticks_per_beat, tempos):
    """把秒换算成 tick，走和运行时同样的分段线性 tempo 图。"""
    tick = 0.0
    remaining = seconds
    current = tempos[0]
    for index, tempo in enumerate(tempos):
        start_seconds = tick / ticks_per_beat * (60.0 / current["bpm"])
        if seconds < start_seconds:
            break
        tick = float(tempo["tick"])
        current = tempo
        remaining = seconds - start_seconds
    return int(round(tick + remaining * current["bpm"] / 60.0 * ticks_per_beat))


def unity_color(rgb):
    """Unity 的 JsonUtility 把 Color 写成 {r,g,b,a} 对象，读也只认这个形状。"""
    return {"r": round(rgb[0], 6), "g": round(rgb[1], 6), "b": round(rgb[2], 6), "a": 1.0}


def main():
    parser = argparse.ArgumentParser(description="离线烘焙 BGA 色板")
    parser.add_argument("--video", required=True, help="BGA 视频文件")
    parser.add_argument("--chart", help="谱面 JSON，用来取 ticksPerBeat/tempos（并配合 --patch-chart）")
    parser.add_argument("--out", help="把 paletteKeys 数组写到这个文件")
    parser.add_argument("--patch-chart", action="store_true", help="把 paletteKeys 写回 --chart 指定的谱面")
    parser.add_argument("--segment-seconds", type=float, default=8.0)
    parser.add_argument("--tap-luma", type=float, default=0.42,
                        help="tap 的目标亮度上限（对照运行时浅色世界，默认 0.42）")
    parser.add_argument("--fps", type=int, default=1, help="每秒采样几帧（默认 1）")
    parser.add_argument("--ffmpeg", default="ffmpeg")
    args = parser.parse_args()

    samples = read_average_colors(args.video, args.ffmpeg, args.fps)
    duration = samples[-1][0] if samples else 0.0

    ticks_per_beat, tempos = 480, [{"tick": 0, "bpm": 120}]
    if args.chart:
        with open(args.chart, "r", encoding="utf-8") as handle:
            chart = json.load(handle)
        ticks_per_beat = chart.get("ticksPerBeat", ticks_per_beat)
        if chart.get("tempos"):
            tempos = chart["tempos"]

    keys = []
    start = 0.0
    while start <= duration:
        end = start + args.segment_seconds
        bucket = [rgb for second, rgb in samples if start <= second < end]
        if bucket:
            average = mean_rgb(bucket)
            tap, drag, glow = derive_colors(average, args.tap_luma)
            keys.append({
                "tick": seconds_to_tick(start, ticks_per_beat, tempos),
                "tap": unity_color(tap),
                "drag": unity_color(drag),
                "glow": unity_color(glow),
            })
        start = end

    document = {"paletteKeys": keys}
    text = json.dumps(document, indent=2, ensure_ascii=False)
    if args.out:
        with open(args.out, "w", encoding="utf-8") as handle:
            handle.write(text + "\n")
        print("写了 %d 个色板键到 %s（覆盖 %.1f 秒，每段 %.1f 秒）"
              % (len(keys), args.out, duration, args.segment_seconds))

    if args.patch_chart:
        if not args.chart:
            raise SystemExit("--patch-chart 需要 --chart")
        with open(args.chart, "r", encoding="utf-8") as handle:
            chart = json.load(handle)
        chart["paletteKeys"] = keys
        with open(args.chart, "w", encoding="utf-8") as handle:
            json.dump(chart, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
        print("paletteKeys 已写回 %s" % args.chart)

    for key in keys[:4]:
        print("  tick=%-7d tap=(%.2f,%.2f,%.2f) glow=(%.2f,%.2f,%.2f)"
              % (key["tick"], key["tap"]["r"], key["tap"]["g"], key["tap"]["b"],
                 key["glow"]["r"], key["glow"]["g"], key["glow"]["b"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
