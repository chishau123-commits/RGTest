using System;

namespace GeometryRhythm.Thart
{
    /// <summary>
    /// 录制时钟：把「预备拍」和「录制」折进同一条时间轴，电脑端与平板端共用这一份算法。
    ///
    /// <see cref="origin"/> = 音频起点 = 录制时间 0。原点之前是预备拍：录制时间恒为 0、
    /// 不采触控，音频只是排期还没出声。数到 0 的瞬间音频与录入同时开始。
    ///
    /// 于是「倒计时」不会吃掉歌曲开头：以前倒计时和音频同时起算，录制时间从 0 就开始走，
    /// 数数的这 3 秒等于把整首歌（以及时间轴播放头）往后推了 3 秒。
    /// </summary>
    public struct ThartRecordingClock
    {
        /// <summary>
        /// 排期缓冲（秒）：收到开始指令到原点之间的固定提前量，
        /// 让 <c>PlayScheduled</c> 拿到一个确实在未来的 dsp 时刻。
        /// 它不属于倒计时，所以不参与预备拍数字。
        /// </summary>
        public const double LeadSeconds = 0.08;

        /// <summary>录制时间原点（dsp 时刻）。</summary>
        public double origin;

        /// <summary>以「现在」起算一个原点：先数 <paramref name="countdownSeconds"/> 秒预备拍，再开始播放与录入。</summary>
        public static ThartRecordingClock Start(double now, float countdownSeconds)
        {
            return new ThartRecordingClock
            {
                origin = now + LeadSeconds + Math.Max(0.0, countdownSeconds)
            };
        }

        /// <summary>还在预备拍里（没到原点）。</summary>
        public bool InPreRoll(double now)
        {
            return now < origin;
        }

        /// <summary>预备拍剩余秒数；到原点之后为 0。</summary>
        public double PreRollRemaining(double now)
        {
            double remaining = origin - now;
            return remaining > 0 ? remaining : 0;
        }

        /// <summary>录制时间（也就是歌曲时间）：原点之前恒为 0。</summary>
        public double Elapsed(double now)
        {
            double elapsed = now - origin;
            return elapsed > 0 ? elapsed : 0;
        }

        /// <summary>
        /// 预备拍数字：从 <c>countdown</c> 数到 1，绝不显示 0。
        /// 排期缓冲不算进数字，所以第一个数字不会比设置的秒数多 1。
        /// </summary>
        public int CountdownLabel(double now)
        {
            double remaining = origin - now - LeadSeconds;
            if (remaining <= 0.0001) return 1;
            return (int)Math.Ceiling(remaining - 0.0001);
        }
    }
}
