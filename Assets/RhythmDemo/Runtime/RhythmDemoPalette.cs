using System.Collections.Generic;
using UnityEngine;

namespace GeometryRhythm
{
    /// <summary>
    /// Note 与反馈层配色的权威来源：谱面里的 <c>paletteKeys</c>（离线从 BGA 视频烘焙，
    /// 见 <c>Tools/Bga/bake_bga_palette.py</c>）。
    ///
    /// 运行时**不做**任何自动取色：方案决定 10 把配色定成「离线烘焙 + 谱师手工兜底」，
    /// 因为自动取色会抖，而且会取到低对比度的脏色，直接伤害 Note 的可读性——那是玩法底线。
    /// 视频画面自己的颜色（glow）只允许驱动氛围量。
    ///
    /// 没有 paletteKeys 的谱面完全走原路：PaletteActive 为 false，SceneVisuals 的硬编码默认色生效。
    /// </summary>
    public sealed partial class RhythmDemoController
    {
        PaletteKeyData[] paletteTable;

        /// <summary>
        /// 相邻两键色相差很远时（这段视频是橙、下一段是青），整段线性 RGB 插值会在中间穿过灰调，
        /// Note 会变成土黄。而视频自己的颜色是硬切的，本段就该是本段的颜色。所以规则是
        /// 「整段保持，只在边界做一小段过渡」——只剩 0.35 秒在混，混不出脏色。
        /// </summary>
        const float PaletteBlendSeconds = .35f;

        public bool PaletteActive { get; private set; }
        public Color PaletteTap { get; private set; }
        public Color PaletteDrag { get; private set; }
        public Color PaletteGlow { get; private set; }

        private void InitializePalette()
        {
            PaletteActive = false;
            paletteTable = null;
            var keys = Chart != null ? Chart.paletteKeys : null;
            if (keys == null || keys.Length == 0) return;

            // 烘焙按 tick 升序写；手工编辑后可能乱序，排序是防御而不是风格。
            var sorted = new List<PaletteKeyData>(keys);
            sorted.Sort((a, b) => a.tick.CompareTo(b.tick));
            paletteTable = sorted.ToArray();

            PaletteTap = paletteTable[0].tap;
            PaletteDrag = paletteTable[0].drag;
            PaletteGlow = paletteTable[0].glow;
            PaletteActive = true;
        }

        /// <summary>每个可视帧调用一次：按当前歌曲时间插值色板，推给共享材质。</summary>
        private void ApplyPalette(double time)
        {
            if (!PaletteActive || paletteTable == null || library == null) return;

            double tick = tempo.BeatAtSeconds(time) * Chart.ticksPerBeat;
            int index = 0;
            while (index + 1 < paletteTable.Length && paletteTable[index + 1].tick <= tick) index++;
            PaletteKeyData a = paletteTable[index];
            PaletteKeyData b = index + 1 < paletteTable.Length ? paletteTable[index + 1] : a;

            float amount = 0f;
            if (!ReferenceEquals(a, b))
            {
                double secondsB = tempo.SecondsAtBeat(b.tick / (double)Chart.ticksPerBeat);
                double blendStart = secondsB - PaletteBlendSeconds;
                if (time >= blendStart)
                    amount = Mathf.Clamp01((float)((time - blendStart) / PaletteBlendSeconds));
            }

            PaletteTap = Color.Lerp(a.tap, b.tap, amount);
            PaletteDrag = Color.Lerp(a.drag, b.drag, amount);
            PaletteGlow = Color.Lerp(a.glow, b.glow, amount);
            library.ApplyPalette(PaletteTap, PaletteDrag, PaletteGlow);
        }
    }
}
