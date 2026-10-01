# Thart 视频 BGA 绑定与 .thr 打包：验证记录

> 对应 [VideoBgaBranchingPlan.md](VideoBgaBranchingPlan.md) 的**阶段 0a**（制谱器能播 BGA）。
> 素材：[`BGA/goodtek60/`](../BGA/goodtek60)（用户指定的 BGA，RIFE 插帧到 60fps，gitignore）。

## 0. 结论速览

| 问题 | 结果 |
|---|---|
| Thart 能播视频 BGA 吗 | **能**。主视图里画出来（[截图](Screenshots/thart-bga-main-view.png)），BGA 垫在编辑器 UI 之下 |
| `.thr` 能装 BGA 吗 | **能**。`bga/manifest.json` + `bga/video.mp4` + `bga/audio.wav`，包内视频字节数与源**逐字节一致** |
| 打开 `.thr` 会自动绑上 BGA 吗 | **能**。展开到缓存目录再绑定，卸载后从包内还原仍能解码 |
| 自检结果 | **`RESULT=PASS CHECKS=14`**，退出码 0 |

## 1. 为什么不能沿用旧制谱器的做法

旧制谱器把 `VideoBgaRuntime` 切到 `CameraFarPlane`（`VideoBgaRuntime.Activate()`），视频画到相机远平面。

**Thart 里这条路走不通**：Thart 的屏幕绘制权完全归 `OnGUI`，而它在画任何东西之前先执行 `Styles.DrawBackdrop` 把整屏铺成不透明底色（`ThartEditorController.UI.cs:138-166`）。远平面视频会被那层盖住。

所以新增了一个**追加式**接口：`VideoBgaRuntime.ActivateAsTexture()`——`active = true` 但保持 `APIOnly`，由宿主自己把 `Texture` 画出来。`Activate()` 的语义与调用方不受影响（旧制谱器与未来的游玩端仍用它）。Thart 侧在 `DrawMainView` 里用 `GUI.DrawTexture` 把 BGA 贴进主视图的 16:9 矩形，位置与 3D 预览通路（`ThartEditorPreview.EnsurePreviewTarget` + `PreviewTexture`）一致。

## 2. 落地的东西

| 位置 | 内容 |
|---|---|
| `Runtime/VideoBgaRuntime.cs` | 新增 `ActivateAsTexture()` 与 `IsActive`（追加，不改既有行为） |
| `Thart/Desktop/ThartEditorVideoBga.cs` | 视频通路：绑定 / 卸载 / 每帧定位 / 画进主视图 / `.thr` 槽位读写 / 打包往返自检 |
| `Thart/Desktop/ThartEditorSeekSpike.cs` | seek 量测本体（从旧制谱器搬来，旧文件已删） |
| `Thart/Desktop/ThartEditorController.cs` | `Start` / `Update` / `OnDestroy` 三个挂钩 |
| `Thart/Desktop/ThartEditorController.UI.cs` | `DrawMainView` 里画 BGA |
| `Thart/Desktop/ThartEditorController.Files.cs` | 新建→卸载；打开→优先用包内 BGA；保存→带上 `bga/` 条目 |
| `Thart/Core/ThartPackage.cs` | `BgaDir = "bga/"`、`Meta.hasBga/bgaManifest`、`Content.BgaEntries`、`Save` 的可选 BGA 条目参数 |

命令行（沿用 `CommandLineArgument` 约定）：

```
-thartCapture <dir>            报告/截图目录（默认 persistentDataPath/ThartSmoke）
-thartBga <manifest.json>      挂一份 BGA 包（并写进谱面的 videoBga）
-thartBgaPackSmoke <manifest>  打包往返自检后退出
-thartSeekSpike <manifest>     运行时级 seek 量测后退出
-thartSeekSpikeDecoder <m>     解码器级 seek 量测后退出
```

Thart 原本没有任何"写报告 + 退出码"通路；这套约定是本次建立的（`-thartCapture` → `<套件>-checks.txt` + `Application.Quit(code)`）。

## 3. 自检覆盖了什么

`-thartBgaPackSmoke` 走完整链路：挂载外部 BGA 包 → 存成 `.thr` → 重新读包 → 卸载 → 从包内展开还原 → 再绑定解码，并且**每一步都截图**。

```
PASS 源 BGA 包能加载并解码
PASS 谱面记的是包内相对路径 bga/manifest.json
PASS 挂载后主视图截图有内容（258000 字节）
PASS 收集到 bga/ 条目（manifest + 视频）
PASS 写出 .thr
DETAIL .thr 大小 = 159716397 字节
PASS 重新读回 .thr
PASS meta 标记 hasBga
PASS 包内有 bga/ 条目
PASS 包内视频字节数与源一致（112243494 vs 112243494）
PASS 先卸载，确认后面是从包内还原的
PASS 从包内还原后 BGA 仍能解码
PASS 还原后主视图截图有内容（258000 字节）
RESULT=PASS CHECKS=14
```

视觉证据：[thart-bga-main-view.png](Screenshots/thart-bga-main-view.png)（主视图里是 BGA 画面，编辑器面板与 16:9 录入框叠在它之上）。截图落盘是异步的——第一次跑断言读到 0 字节，改成轮询文件大小后才对；这条坑记在下面。

## 4. 已知问题与后续

- **音频可能被装两遍**：自检产出的 `.thr` 是 152 MB，而视频只有 107 MB——因为包内既有 `bga/audio.wav`（BGA 自带，22.75 MB），又有一份会话里自动载入的歌曲音频（约 22 MB）。旧制谱器的口径是"若歌曲就是视频内的音轨，`chart.json` 直接复用 BGA 目录里的 `audio.wav`，不再复制一份"（[VideoBgaWorkflow.md:56](VideoBgaWorkflow.md)）。**Thart 现在还没有这条复用规则**，是待办。
- **打包把视频整块读进内存**：`ThartZip.Entry` 只接受 `byte[]`，且 `ThartZip` 是 store-only（不压缩），所以 107 MB 视频会整个进内存再写出，`Load` 时同理。1080p60 两分钟的包能跑（本机实测数秒），但增量保存/流式写入是以后的事。
- **BGA 侧的分叉点标注还没做**：`.thr` 能装 BGA、能绑定，但谱面里还没有 `bgaBranching`（阶段 2 的事）。
- **缓存目录不清理**：包内 BGA 会展开到 `persistentDataPath/ThartBgaCache/<内容哈希>/`，同名内容命中同一目录，但旧版本不会自动删。
