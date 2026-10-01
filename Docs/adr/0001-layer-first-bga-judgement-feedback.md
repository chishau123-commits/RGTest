# 判定反馈走图层模型，不做文件级换流

Status: accepted

制谱器与游玩运行时都要让判定结果改变画面。我们决定：**默认路径是图层模型**——保持一条连续视频流，逐 Note 的反馈由引擎侧程序化图层与调色承载，段落走向的切换靠**同一条文件内 seek 到另一个区段**；**不**把"切换到另一个视频解码来源"（换流）作为默认实现。换流只作为 Windows 上的可选增强保留。

## 为什么

- Unity 的 `VideoPlayer` 没有 playlist：一个实例同时只有一个来源，`clip` 与 `url` 是"最后设置的生效"。`Stop()` 会**销毁内部资源（纹理、缓冲内容）**并把 `isPrepared` 置回 false，所以换 clip 必然重新 `Prepare()`。官方 Issue Tracker 现存 "The video flickers to black when changing Video Clip"；社区一致的无缝做法是双 player 全部预 prepare。
- 判定最密可达每秒十几次（BPM 200 的 1/16），而本仓库现有两条视频路径的同步容差已经是 **200–250 ms**（`VideoBgaRuntime.cs:131-159`、`VisualAuthoringRuntime.cs:401,405`），seek 又会串行排队。文件级换流不可能承载逐 Note 反馈——这条路上限不是画质，是物理。
- Android 的硬解实例数是**设备上报的能力项**（`CodecCapabilities.getMaxSupportedInstances()`），只有声明 Media Performance Class 的设备被强制"6 路 1080p"；中间件另有播放句柄数与**分辨率总面积**两个独立上限。旁证：BMS 圈的 BGA 编码指南建议 480p、单文件 <40MB，720p 只推荐给 beatoraja——**1080p 双流不是这个题材的常态**。
- 这个领域已有的规范就是这个形态：BMS 的 `#xxx06 BGA-POOR`（玩家漏掉音符时显示的图像序列）与 `#xxx07 BGA-LAYER`（叠加在 BGA 之上的图像对象），bmson 里即 `poor_events` / `layer_events`。影视侧的 seamless branching / multi-angle（一条流最多 9 个 angle、同一时刻只有一个生效）才是"换流"，且只在章节末的 branching 指令处切。

## 后果

- 谱面格式必须**同时**能表达两种臂内容：`派生变体` 与 `区段引用`。区段引用让"将来换成真素材"不需要改格式。
- 同一谱面在 Windows（可用双流增强）与 Android（单流）上观感可能不同。这是已知代价，不是缺陷。
- `BlenderBgaRuntime` **冻结不删**：它是全仓库唯一能提供真深度与遮挡的路径，是"风格一致 → 空间一致"升级的唯一现成入口。
- 素材的体积代价是**故意的**：关键帧密集的规格（固定 60fps、无 B 帧、每 15 帧一个关键帧）换来 seek 到区段头的响应速度，不能当作赘肉顺手优化掉。
- 判定反馈的"同帧"只能由反馈层提供。任何把逐 Note 反馈接到 BGA 源上的实现都是错的。

## 考虑过的替代

- **蓝光 seamless branching / multi-angle 作为默认**：效果上限更高（真的是不同内容），但要求多条流共享时间轴、分辨率与编码参数，且依赖双解码器，Android 不保证。
- **每个判定换一条视频片段**：物理不可行（见上）。

## 依据

- [VideoPlayer.Stop](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.Stop.html)、[VideoPlayer.Prepare](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer.Prepare.html)、[VideoPlayer.clip](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-clip.html)、[seekCompleted](https://docs.unity3d.com/ScriptReference/Video.VideoPlayer-seekCompleted.html)
- Issue Tracker：[changing Video Clip 时黑帧](https://issuetracker.unity3d.com/issues/the-video-flickers-to-black-when-changing-video-clip)、[反向 seek 明显更慢](https://issuetracker.unity3d.com/issues/seeking-backwards-in-videoplayer-video-takes-considerably-more-time-than-seeking-forwards)
- Android：[Media Performance Class](https://source.android.google.cn/docs/compatibility/17/mpc?hl=en)；[CRIWARE Android H.264 多路限制](https://game.criware.jp/manual/unity_plugin_zh/latest/contents/usr_android_specified_tips_h264_050.html)
- 领域规范：[bmson spec — BGA](https://bmson-spec.readthedocs.io/en/master/doc/index.html)、[BMS 命令表](https://hitkey.nekokan.dyndns.info/cmds.htm)、[multi-angle（MSDN）](https://learn.microsoft.com/en-us/previous-versions//ms783422(v=vs.85))
- 交付与体积：[团结引擎手册 — Asset packs in Unity](https://docs.unity.cn/cn/tuanjiemanual/1.7/Manual/android-asset-packs-in-unity.html)
