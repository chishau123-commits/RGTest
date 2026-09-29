# Thart 制谱器 · 坐标域与录入规范

Thart 是「平板跟着歌点，电脑端生成铺面」的制谱流程：平板端 `ThartTouchRecorder`
录触控 → `ThartChartBuilder` 转成音符 → 电脑端 `ThartEditorController` 编辑/预览 →
打成 `.thr` 谱面包给游玩端用。

本文记录三件容易再次踩坑的事：**16:9 坐标域**、**二维分列**、**时间轴拖动窗口**。

## 1. 一切坐标都在 16:9 里

铺面形状不能随屏幕比例变化，所以从录入到游玩统一固定 16:9：

| 环节 | 代码 | 规则 |
| --- | --- | --- |
| 平板录入 | `ThartTouchRecorder.CaptureFrameScreen()` | 顶部应用栏（`TopBarHeight`）与底部状态条（`BottomBarHeight`）之间取最大的 16:9 矩形，按 16×9 整数块取整；框外画黑边**且不录入** |
| 触控归一化 | `ThartTouchRecorder.FramePoint()` | 屏幕点（左下角原点）→ 录入框内像素与百分比；`xPercent/yPercent` 相对**录入框**，不是整屏；`frameWidth/frameHeight` 随数据一起上报（`schemaVersion = 3`） |
| 世界坐标域 | `TouchToNoteConfig.fieldHeight` + `FieldWidth` | 纵向跨度是世界单位主参数，横向恒等于 `fieldHeight × 16/9`，**不能单独拉某一轴** |
| 电脑端 2D 视图 | `ThartEditorController.AspectFieldRect()` / `WorldToField()` | 主视图里画的就是 16:9 铺面平面图，列标记与音符都落在真实 (x, y) 上 |
| 3D 预览 | `ThartEditorPreview.EnsurePreviewTarget()` | 渲染到 16:9 贴图（同样按 16×9 整数块），再贴进主视图，窗口比例不影响取景 |
| 游玩端 | `GameViewport.Apply()` + `MobileUiLayout.SafeArea` | 相机 viewport 锁到 16:9（`camera.pixelRect`），非 16:9 屏幕加黑边；UGUI 板子限制在 `GameViewport.Content()`（16:9 ∩ 安全区）内 |

要点：

* 触控与判定的坐标仍然走 `Camera.WorldToScreenPoint`，它给出的是**含 viewport 偏移的全屏像素**，
  所以锁 viewport 不会让点击判定错位。
* 离屏 QA 渲染（`-demoSmoke` / `-captureHud`）自己有 render target，`GameViewport.Apply()`
  在 `camera.targetTexture != null` 时保持整块贴图，否则 HUD 用的屏幕空间会算错。
* 老录制数据（`schemaVersion = 2`，没有 `frameWidth`）按整屏百分比解释，重新录一次即可对齐。

### 两个已经踩过的坑

1. **上下原点不能混用**。`Input.touch.position.y`、`frame.y`、`frame.yMax` 都是**左下角**原点，
   所以「离录入框顶部多远」只能是 `frame.yMax - screenPosition.y`。
   写成 `Screen.height - screenPosition.y - frame.y` 会把整段录制的 Y 整体推下去
   `Screen.height - frame.height - 2×frame.y`（3048×2032 平板上是 50px，四角变成
   6%/8.95%）。自检里 `frame top-left maps to 0%,0%` 就是钉这条的。
2. **抬指帧会被采样漏掉**。60Hz 采样下快速点按的 Ended 常丢，于是「同一个 fingerId 的下一次
   按压」会以 Moved/Ended 出现在**另一个角**上，被当成同一根手指移动、位置取平均 ——
   四个角会变成三列（中间多一个 49% 的假列）。`ThartChartBuilder.IsJump()` 按
   `maxJumpPercent`（默认占录入框宽度 20%）把这种物理上不可能的瞬移拆成新轨迹。
   真机复现：`ThartFieldValidation` 的 `a missed lift-off does not average two presses into one`。

## 2. 分列是二维的

`ThartChartBuilder.BuildFreeLayout()` 按 **(x, y) 的世界距离**聚簇，不是只按 X：

* 只按 X 聚簇时，「左上角一个点 + 左下角一个点」会被并成一列，纵向位置被平均掉，
  表现就是「我明明按了四个角，制谱器里只有两条竖线」。
* 合并阈值 = `columnMergePercent`（占录入框宽度百分比）换算成世界单位的**半径**；
  超过 `maxColumns` 时反复合并距离最近的两列，最后按 X 从左到右编号 P1..Pn。
* `AssignColumns()` 同样按二维最近列匹配；`MergeCloseNotes()` 同时看两个轴，
  所以「同一时刻按两个角」不会合成一个音符。

## 3. 时间轴拖动：拖动期间锁住可见窗口

`GetTimelineView()` 平时让窗口以播放头为中心（缩放越大看得越细）。但拖动播放头时
窗口必须钉死（`LockTimelineView()` / `timelineViewLocked`，松手解锁），否则是正反馈：

```
鼠标右移 → songTime 变大 → 窗口跟着右移 → 同一个鼠标位置又对应更晚的时间 → …
```

缩放 8× 时按 0.9 的位置按住不动，20 帧就能从 60s 跑到 190s，也就是「指针一拖就飞出去」。
锁窗口后 `TimelineTimeAtLocalX()` 是纯函数：同一个鼠标位置永远给出同一个时间。

## 4. 自检与工具

| 用途 | 命令 |
| --- | --- |
| 坐标域/分列/拖动窗口/视口数学回归（28 项） | `Unity.exe -batchmode -quit -projectPath <项目> -executeMethod GeometryRhythm.Thart.EditorTools.ThartFieldValidation.RunBatch` |
| 用一份合成四角录制核对编辑器画的位置 | `ThartEditor.exe -thartRecording Builds\verify\thart-corners.json` |
| 自动截图（可带按键，例如 `-Keys 3` 切预览） | `Tools\Thart\thart_corner_shot.ps1` |

`ThartFieldValidation` 覆盖：16:9 比例与四角世界坐标、四角录入 → 四条不同列、
同位置重复按复用同列、同时按下时「同点合并 / 异角不合并」、抬指帧丢失时不把两次按压平均、
锁窗口拖动漂移为 0（并断言旧算法确实会 runaway）、`GameViewport.Frame()` 在 1920×1080 /
2400×1080 / 1280×1024 下都精确 16:9 且居中、平板录入框在 16:10 / 20:9 / 16:9 / 4:3 下
都是精确 16:9 且不压到上下两条栏、录入框四角正好映射到 0%/100% 且框外按压被拒绝。

真机（Xiaomi Pad，3048×2032）实测：录入框量出来是 `3040×1710 @ (4,186)`，
比例 `1.77778`；四个角按下去记下来的是 `5.99%/6.02%`、`94.01%/6.02%`、
`5.99%/93.98%`、`94.01%/93.98%`，框外那一次被丢弃，编辑器得到 4 列 4 音符。
复现命令（平板已 adb 连接时）：

```powershell
adb shell input motionevent DOWN 186 289 ; adb shell input motionevent UP 186 289   # 左上角
# … 其余三角同理，坐标 = 录入框角 + 6% 内缩
```
