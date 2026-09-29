# 制谱器相机机位轨道（2026-09-25）

## 问题

视频谱面的相机被写死成 `(0, 0, Z(t))` + 单位旋转：`VideoChartSpace.ApplyCamera` 只允许沿一条轴对齐直线推进，`Unproject` 也是不含旋转的反投影。因此 `tick / x% / y% / z` 四个数只能描述一台**不会转头**的相机，谱面永远是直着飞过来的，无法拐弯，也无法贴合 BGA 里平移、推拉、摇镜的运镜。

## 实现

新增**可选**的机位轨道 `videoSpace.cameraPoseKeys`，每帧记录：

```json
{ "tick": 9600, "dx": 4, "dy": 1, "dz": 0, "yaw": 12, "pitch": -4, "roll": 0, "fov": 53, "easing": "smoother" }
```

两条轨道职责分开，互不干扰：

- `cameraZKeys` → **音符里程**：音符多快飞过来、判定面在第几米。仍然是递增的标量，判定时刻、出生远端、流速公式一个字未改。
- `cameraPoseKeys` → **画面构图**：相机在哪、朝哪、多大焦距。

运行时：

- 位姿 `Pose(t) = (0, 0, 里程(t)) + offset(t)`，朝向由 `yaw/pitch/roll` 按该段 `easing` 插值，`fov` 一并插值（省略读回 0 时按 53°）。
- 每个锚点在**它自己的 tick** 用当时的位姿反投影：`AnchorWorld = P(T) + R(T) · LocalPoint(x%, y%, 7)`。因此在锚点自己的时刻，画面百分比精确不变；其他时刻才随相机运动产生透视。
- 锚点之间按**里程**而不是世界 Z 分段线性插值；超出首末锚点则沿该锚点自身的轨道继续延伸。
- 判定面中心与屏幕 up 轴同样跟随机位，相机 roll 时 Note 和判定圈跟着画面一起倾斜。
- `Initialize` 只为没有机位轨道的谱面补一条**全 0** 轨道；全 0 与旧版本逐位相同，旧谱面零迁移、零重写。

编辑器：Paths 页新增第三个页签 **Camera pose** 与时间轴 `CAMERA POSE` 行，支持添加 / 拖动改时间 / 右键删除 / 逐段缓动 / `Reset this key to the straight rail`。时间轴状态栏实时显示 `camera ON RAIL` 或 `camera 12.3m off rail · yaw -8.5° · pitch 2.1° · roll 0° · FOV 47°`。

## 修改的文件

- `Assets/RhythmDemo/Runtime/VideoChartSpace.cs`：`VideoCameraPoseKey` DTO、`CameraPose`、`Pose/PoseAtDistance/RailAtDistance/RailRotationAtDistance`、位姿化的 `Unproject/Project/AnchorWorld`、按里程插值 + 末端延伸、校验。
- `Assets/RhythmDemo/Runtime/SpatialDirector.cs`：`RouteAt/RouteRotationAt` 走机位、新增 `JudgementCenter` / `CameraUp`、Note 的 up 轴跟随机位。
- `Assets/RhythmDemo/ChartEditor/ChartEditorInteraction.cs`：视口投影矩阵改用时序 FOV（见下）。
- `Assets/RhythmDemo/ChartEditor/ChartEditorVideoSpace.cs`：机位面板、`EnsureCameraPoseKey`、位姿化的百分比换算、三页签。
- `Assets/RhythmDemo/ChartEditor/ChartEditorTimeline.cs`：`CAMERA POSE` 轨道行与操作。
- `Assets/RhythmDemo/ChartEditor/ChartEditorPlayback.cs`、`ChartEditorWorkspaceUI.cs`：播放头与判定圈改用 `JudgementCenter` / `CameraUp`。
- `Assets/RhythmDemo/ChartEditor/ChartEditorVideoSpaceChecks.cs`：新增 10 条回归检查。
- `Docs/GeometryChartStudio.md`：相机 rail / pose 用法与格式说明。

## 验证

Unity 2022.3.62f3c1 批处理构建 `Builds/CameraPoseQA`，`CHART_STUDIO_BUILD_PASS`，退出码 0。命令：

```
GeometryChartStudio.exe -chart BGA\ChartPackages\Summer20_H3_20s.grchart -chartEditorSmoke <套件> -chartEditorCapture <目录>
```

| 套件 | 报告 | 改动前基线 | 本轮结果 |
|---|---|---|---|
| 视频坐标 | `video-space-checks.txt` | PASS 62（`BGA/tests/note-fast-read-video`，04:35） | **PASS 72** |
| 音符流速 / 远端出生 | `note-speed-checks.txt` | PASS 71（`BGA/tests/note-fast-read-final`，04:35） | **PASS 71** |
| 节拍编辑 | `beat-editing-checks.txt` | PASS 55（`BGA/tests/note-fast-read-beat`，04:35） | **PASS 55** |

把改动前的 62 条与本轮的 72 条逐条比对，差异**恰好只有新增的 10 条**（见下），原有 62 条名称与结果一字未改。后两个套件的检查条数与改动前完全相同。

新增检查（10 条）：

- 无机位轨道、或机位轨道全 0，逐位复现轴对齐相机。
- 带 offset、转头、缓动、变焦的机位，仍让每个百分比锚点精确落在自己时刻的判定面上（误差 6.08e-05 百分比 / 3.81e-06 世界单位）。
- 锚点按里程插值，相机转头 160° 后轨道不折叠（误差 0）。
- 机位确实把轨道远端在世界里弯开（δ = 10.97 单位）。
- 判定面中心与屏幕 up 跟随机位。
- 手写关键帧省略 `fov` 字段时回退 53°。
- 校验拒绝乱序 tick、非有限角度、越界 FOV、超出歌曲的 tick。
- 轨道在末锚点之外继续延伸到远端出生门（z = 137 = 期望值）；带机位时沿自身轨道延伸 30 单位而不是塌缩。
- 真实播放器里，带 offset / 转头 / fov 62° 的机位仍把锚点投到授权百分比上（误差 7.6e-06）。

旧谱面不变的证据（同一次运行内）：

- `No XY motion or rotation at 0/2/6/12/19s` 五条。
- `25 percent/world/camera round trips < 0.001 percent; error=7.629395E-06`。
- `V1 Scene is folded into V2 independent Paths`、`Migration preserves complete curves across all Paths; error=9.83E-07`、`Path-only migration is idempotent`。
- `Beat editing keeps fixed video-camera pose and FOV`。

## 本轮发现并修复的两个真实缺陷

1. **视口矩阵写死 53°。** `UpdateViewportRect()` 在每次刷新时把 `sceneCamera.projectionMatrix` 重设为 `Perspective(53, …)`。机位关键帧一旦改变 FOV，反投影用的焦距（62°）和实际渲染的焦距（53°）就不一致，所有锚点整体错位。实测误差 3.6697%，与按 62°/53° 焦距差推算的 3.676% 吻合。修复后误差 7.629395E-06。
2. **轨道末端被钳制。** 远端出生门在最后一个锚点之外（出生深度 100，而末锚点里程只有 107）。原实现在每帧 query 距离上都强制 `result.z = 距离`，这个赋值实际承担了「沿轨道继续向前延伸」的职责；改成插值后丢掉了它，音符首帧深度从 100 掉到 58.595（恰好等于 107 − 48.405，与实测值完全一致），`note-speed` 套件的 3x/8x/16x 远端出生检查因此失败。修复为沿该锚点自身轨道延伸，对轴对齐相机与旧行为逐位相同。

两个缺陷都只在「旧检查没覆盖到的组合」里出现：前者要机位改 FOV 才会暴露，后者要查询距离超出末锚点才会暴露。现均已加入回归检查。

## 边界与已知限制

- **平面 BGA 不会随相机旋转。** 平移、推拉、变焦与小角度转头能和视频画面大致对上；大角度 yaw/pitch 会让 3D 前景与平面视频的透视分家。这是素材形态的限制，不是制谱器可以消除的。
- 机位只影响画面。判定时刻、出生远端、判定位置、BPM、音频偏移与谱面数据都不随它改变（`note-speed` 套件覆盖）。
- 本轮所有运行都没有生成 PNG 截图（`ScreenCapture.CaptureScreenshot` 未落盘），因此不把截图内容当作证据。默认 `-chartEditorSmoke` 的最终 `PASS=False` 来自它最后一条 `ScreenshotHasContent(chart-studio.png)` 断言，与代码路径无关；`-chartEditorInteractionSmoke` / `-chartEditorTimelineSmoke` 等子套件需要各自的 flag，本轮未逐一执行。
- **`-chartEditorLockedVideoCameraSmoke` 是已被取代的旧套件，本轮 21 条 FAIL 不代表回归。** 它断言相机在任何歌曲时间都**完全不动**（对固定世界点 `(3,1,16)` 的投影必须逐位不变），而视频坐标谱面的契约正相反：相机必须随里程推进。两者不可能在同一张谱面上同时成立。报告时间线也证实了这点——它最后一次 PASS 是 02:31（34 条），而 02:52 起 `video-space` 套件接管并持续到 04:35（40 → 62 条全 PASS），此后这个旧套件再未运行。它针对的是 `ApplyLockedVideoCamera` 里那条「无 videoSpace 时的固定前视」回退分支，该分支在视频坐标谱面上已经不可达。本轮错误地拿视频坐标谱面去跑它，因此不作为证据。
- 未做移动端真机、长时间播放与极端角度（>90°）的实机观感验证；数学上已覆盖转头 160° 不折叠。
