# prototype-ring-0 协议与运行时契约

本子集沿用计划书的 `settings / notes / paths / decorations / actions` 组织；参考 ADOFAI 的设置、对象与类型动作分离思路。ADOFAI 资料核验见 [原研究记录](../tmp/analysis/adofai_protocol_research.md)；本协议是自行定义的触控谱面，不能直接读取 `.adofai`，也不宣称完整 v1 曲包兼容。

作者 JSON 只有 tick，一律经同一个核心编译器推导秒。`schemaVersion` 必须为 `prototype-ring-0`；`timebase.ppq=960`。1 beat 是四分音符。`offsetUs` 的正值表示 tick0 在音轨开始之后；`tempos` 从 tick0 起严格递增，持续时间跨 BPM 分段积分。

| 区域 | 必填字段和支持范围 |
|---|---|
| 顶层 | schemaVersion, timebase, settings, notes, paths, actions, decorations |
| timebase | ppq, offsetUs, tempos；每项 tick 整数、bpm 正有限数 |
| settings | title 非空、requiredTouches 正整数，不限制为 2/4 |
| notes | id, tick, spawnTick, motion, target{x,y}, radius；arrival 必须 pathId；shrink 的 pathId 可省略或空 |
| paths | id, type=`linear`, start{x,y}, end{x,y}；start 与 end 不同，end 必须等于引用音符 target |
| actions | id, eventType=`MoveCamera`, tick, durationTicks, position{x,y}, rotation, scale, ease |
| decorations | 必须空数组；首版尚未实现作者装饰 |

谱面坐标原点在中央，域高 100，宽 `100×16/9`，x 向右、y 向上。半径使用同一单位，不是百分比 x/y 混合单位。预告从 spawnTick 到 tick；缩圈从 3r 到 r；移动圆半径恒为 r，沿直线到接收环。同一音符仅点击接收目标圆盘，移动圆本身不能提前代替它。

运镜是对整个玩法层的二维相似变换：`p' = translation + rotation × (scale × p)`。scale 必须正有限；rotation 为度，正值逆时针；动作 position/rotation/scale 是绝对目标值，从上一动作终态插值，初态为 identity。支持 `linear`、`smooth`（3u²−2u³）两种缓动，动作按 tick 排序且不能重叠，durationTicks=0 是瞬时赋值。HUD 固定在安全区。任意绝对歌曲时间可直接求值，无 deltaTime 累计状态。

`ChartJson.Parse` 在 DTO 转换前验证 JSON 必填键、类型和未知键；拒绝重复键、非标准 JSON、未知动作/表现/缓动、空对象引用等。限制 UTF-8 文本 1 MiB、深度 64、notes/paths 各 10000、actions 2000。编译器继续校验 ID 全局唯一、顺序、数值、路径终点和谱面声明触点数。此限制针对原型，未来版本须显式迁移。

评分为 `rawInputSong + inputCalibration − hitSeconds`，Perfect≤35ms，Great≤70ms，Good≤100ms；超出尾窗 Miss。边界由精度容差 1ns 保持包含性。超时扫描使用 `songNow+inputCalibration`，且在同批输入处理后进行。一条接触只能消费一次；一次落指匹配一个音符。多押先最大化命中数，再按时间误差、空间误差和稳定 ID 做确定匹配。窗口内待判音符仍保持接收轮廓。

原始落指采用 Input System 1.7 EnhancedTouch 的 startTime/startScreenPosition；持指、滑动、抬指不生成评分输入。音乐采用 AudioSource.PlayScheduled 的 DSP 时钟，输入桥接于每个播放 epoch 重建。UI 同样接收 Began，但以资格标记与命中区域隔离评分。暂停恢复期间清除队列与姿态，恢复预约前的输入只能操作 UI。

空间定位先计算 `estimatedSubmit = touchTime − configuredDisplayLatency`，选择不晚于这个时刻的历史帧，再逆变换触点。历史已经包含视觉校准，输入校准只改时间评分。历史约 0.5 秒；缺历史拒绝评分。**LateUpdate 记录是 CPU 渲染提交前的姿态，不能等同物理显示时刻。默认 20ms 只是可调估计，必须实机核验。** 延迟处理不能改用处理帧的新姿态。

`requiredTouches` 至少覆盖同 tick 的音符数量。Android 手动游玩前要求观测到所需同时接触数，避免把 Input System 固定槽位数当成手机物理上限。日志分别记录槽位与观察峰值；更高触点谱可通过开发 JSON 加载。诊断预览不作为人工触控评分验收。

开发加载只读取 app persistentDataPath 下 `prototype-chart.json`，否则读取内置 Resources。暂时共用预置 metronome.wav，所有命中时间必须在音频 `[0,length)` 内；spawn 可位于音轨开始前。加载输出“载入文本重新编码为无 BOM UTF-8”的 SHA-256，保留文本空白，不一定等于带 BOM 原文件的字节 hash；File.ReadAllText 自动识别的其他 BOM 编码也统一重新编码。Windows 核对可用 `SHA256(Encoding.UTF8.GetBytes(File.ReadAllText(path)))`。协议错误显示在画面/日志中，重开后可读取修复文件。正式曲包、资源清单、玩家文件选择器留待后续。
