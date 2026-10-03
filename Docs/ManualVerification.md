# Android 人工核验清单

此文档供非作者在 PR 上复核。先查看同一提交的 [执行证据](ImplementationEvidence.md)，再记录本设备结果；未执行项目保留“待核验”，不要用单测或自动预览替代手指实测。

## 准备与定位

按 [AndroidBuild](AndroidBuild.md) 构建 Development APK，连接 USB 调试并安装启动。记录 APK SHA-256、PR head SHA、Unity 版本、设备型号/Android 版本、刷新率、音频输出方式；建议先使用手机扬声器。游戏固定横屏。上方 PLAY / PAUSE / RESUME、RESTART、PREVIEW，下面校准按钮只在非游玩状态显示。默认预览关闭。

先同时触碰三个位置并抬起，状态栏“touches observed”应达到 3；再 PLAY。所需三指尚未观测到时会给出提示。此验证不是自动宣称设备最大触点数。

```powershell
./tools/android.ps1 -Task DeviceInfo
./tools/android.ps1 -Task CaptureLogs
./tools/android.ps1 -Task PullDiagnostics
./tools/android.ps1 -Task Screenshot
```

截图/日志保存在 `Builds/AndroidEvidence/`；应用 JSONL 在 Android app 文件目录的 diagnostics 下。日志包含 session、ready/play、input、pose、judgement、pause/interrupted/complete。UI 落指应为 ui-or-outside-playfield；评分候选有原始时间、修正歌曲时刻、选中 frameId/selectedVisualSongSeconds 和逆变换 chartPoint。pose 记录使用 visualSongSeconds 字段。Miss 的 spatialDistance=-1 表示无接触距离。

## 逐项操作

| 编号 | 操作 | 预期结果与需保存证据 |
|---|---|---|
| M01 安装启动 | 安装并冷启动，旋转手机 | 无崩溃、横屏、HUD 在安全区；保存启动截图、logcat、应用 session |
| M02 缩圈 | 4.000s 的第一颗青色圆，在外环与内圆边缘重合时点接收圆 | 圈从 3r 收到 r；到时仍需落指；一次判定和一次计数。保存录屏及 note n00-0 的日志 |
| M03 到位圆 | 5.000s 的橙色圆，先在 4.5s 点接收圆盘外的移动圆，随后在 5s 点接收环 | 明显提前且在接收区外的点击不成功；到位圆与环同心、等半径。±100ms 内点到接收圆盘可以评分，即使移动圆尚未完全到位。note n01-0 |
| M04 生命周期 | 按住目标，等目标到时；滑过目标、抬指；然后快速点按几次 | 持指/滑动/抬指无新增评分；每个 Began 独立 contactId，原始时间与位置保留；不能一根手指多次自动命中 |
| M05 三押 | 10.000s（beat20）、20.800s（beat44）、27.200s（beat60）三指各点一个目标 | 同批匹配各自消费，无 2/4 人工上限；保存三条 input、三个不同 noteId 与 contactId。不以 ADB 单点模拟替代 |
| M06 强运镜 | 正常触控 6–30 秒，尤其运镜中间目标到时 | 目标在判定前继续平移/旋转/缩放，HUD 不移动；看见的位置能点到；记录 frameId、估计显示延迟，录屏核对偏差 |
| M07 校准 | 暂停或 Ready 时 INPUT 调到 -40ms；与零校准对比；VISUAL 独立调整 | 修正时间仅加输入校准；视觉偏移只改呈现；空间反投影用历史帧，不重复加视觉偏移。数学边界由自动测试精确复核，人工误差不冒称精确 1ms |
| M08 暂停恢复 | 任意时间 PAUSE，触碰舞台，RESUME；切后台再返回 | 暂停输入不计分；恢复有预约准备时间、清队列/旧姿态；后台返回为中断状态，显式恢复；音轨不重新从头开始 |
| M09 重开结果 | RESTART 后再次 PLAY，演奏到结束 | 清除判定与分数，首颗仍从 4s 开始；36s 完成显示结果；完整日志 flushed。校准值保留 |
| M10 坏谱/导入 | 复制内置 JSON 改一个目标或 tick，PushChart 后 RESTART；随后测试未知版本/缺 target/offset 导致负命中时刻 | source 指向外部 JSON；按协议说明将载入文本重新编码为无 BOM UTF-8 后核对 SHA-256，修改可见；坏谱明确报错、不静默用内置谱；修复并 RESTART 后可恢复 |
| M11 更多触点 | 在 Ready 实测设备更多手指；用独立自由坐标同 tick 谱声明相同 requiredTouches | 记录实际峰值及系统槽位；声明超过已观测能力阻止手动 PLAY；有几指只声明已实测几指 |
| M12 卡顿与显示延迟 | 捕获明显 >250ms 帧停顿或后台切换；对照录屏与 pose | 明确 Interrupted；不扩大判定窗或自动补分。20ms 默认显示延迟注明未测；调节后保存真实设备条件 |

PREVIEW: ON 会自动演奏，可以先观察圆环/镜头和截图，日志 judgement.preview=true；不能把这种结果写成触控通过。桌面鼠标只是单指开发路径，其时间来源会标记 processing-time。

## 审核记录模板

| 项目 | 填写 |
|---|---|
| 审核者 / 日期 | 待填写 |
| PR head SHA / APK SHA-256 | 待填写 |
| 设备 / OS / 刷新率 / 音频方式 | 待填写 |
| 自动检查/构建 | 引用执行证据及 CI |
| M01–M12 | 每项填写通过/失败/待核验，以及日志/录像文件 |
| 实测同时触点 / 显示延迟估计 | 分别填写，无法确定则明确未测 |
| 未关闭问题 / 合并意见 | 待填写 |

审核由非 PR 作者完成，审批后才合并到 ReStart。GitHub 静态 CI 不含 Unity 许可证构建，也不自动证明设备响应精度；分支保护若未由维护者配置，按人工 PR 门禁执行。
