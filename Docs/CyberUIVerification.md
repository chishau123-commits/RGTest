# 赛博 UI 验收记录

日期：2026-09-17。Unity 2022.3.62f3c1，Windows x64，Built-in 渲染管线。

## 变更边界

本次重做标题、选曲、结算、加载、错误、空曲库、游玩 HUD 和暂停控制台。仅增加 UI 绘制组件与字体资源；没有改变 Note 网格、Tap / Drag 定义、保护套规则、输入判定时窗、音频同步、谱面结构或镜头／路径。菜单中的同心环是封面插画，不是 Note 皮肤。制谱器继续使用原来的接口。

`RhythmFrontend.cs` 本次只扩展 opt-in 的 `-frontendSmoke` QA 截图与测试；正常页面状态转换和会话管理方法保持原样。

## 最终自动验证

- 构建日志：`.validation/build-cyber-final.log`。
- 结果：`GEOMETRY_VALIDATION_PASS 239 checks`；`GEOMETRY_BUILD_SUCCESS 73863116 bytes`。
- UI 自检：`.validation/Cyber-final/frontend-smoke.txt`，`PASS=True`。使用实际 Button 回调测试标题、选曲、手动／自动开始、暂停返回、手动全 Miss、重试重置、自动满分结算、返回标题、临时 QA 曲目切换和相机／AudioListener 唯一性。
- 玩法自检：`.validation/Gameplay-cyber-final/player-smoke.txt`，结果如下。
- 最终两份 Player 日志：`.validation/player-cyber-final.log` 与 `.validation/player-gameplay-cyber-final.log`，未发现 Exception、Shader error、MissingReference 或 Assertion failed。
- 独立程序均以 1920×1080 窗口测试；QA RenderTexture 截图固定为 1600×900，不代表游戏被限到该分辨率。

```text
PASS=True
Images=True
FourRules=True
DspClock=True
RenderSettings=True
TargetFrameRate=-1
VSync=0
Notes=204
Score=1000000
Misses=0
```

保持桌面不封顶帧率、VSync 关闭、每帧渲染、4× MSAA、动态分辨率关闭。该测试验证配置和功能，不是整曲帧率基准，不承诺所有硬件达到某个固定 FPS。

## 逐屏视觉检查

已查看实际 Player 导出的标题、选曲、结算、暂停、加载、缺失资源错误和空曲库截图，以及 8 秒游玩 HUD。首轮发现 Rajdhani 字体行高造成主读数裁切，已增加标题／分数／评级／Combo 的文字框高度并重跑构建。背景网格已降低亮度，标题色差层与主标题使用相同字号适配与行距。

截图不是生成式设计稿，均来自真正运行的 Unity 三维相机和 UGUI：

- [标题](Screenshots/ui-title.png)
- [歌曲列表](Screenshots/ui-songs.png)
- [结算](Screenshots/ui-results.png)
- [游玩 HUD](Screenshots/ui-gameplay.png)
- [暂停](Screenshots/ui-pause.png)
- [加载](Screenshots/ui-loading.png)
- [错误状态（QA 注入）](Screenshots/ui-error.png)
- [空曲库（QA 注入）](Screenshots/ui-empty.png)

## Unity 编辑器实际点击验收

通过 computer-use 操作用户打开的 Unity 编辑器，而非只调用测试函数：选择 Full HD (1920×1080)，确认 Low Resolution Aspect Ratios 与 Game view only VSync 都未开启，预览 Scale 为 1x；启动标题，点击进入曲库，点击自动预览，在 12 秒左右点击暂停并使用控制台按钮恢复。64 秒歌曲自然结束后自动进入结算，显示 1,000,000 分、204 Perfect、0 Good、0 Miss、100.00%、SSS。之后点击 MUSIC ARCHIVE 返回歌曲列表。验收后退出 Play。

启动检查时曾遇到旧运行会话在脚本热重载后的空引用；退出 Play 并重新启动后，错误未在新会话或最终独立 Player 中复现，因此未改动玩法代码。

## 玩法文件完整性

在开始 UI 改动前后逐项比较 SHA-256，以下八个文件全部相同。它们分别覆盖 JSON 数据、判定、空间导演、舞台曲线、Note／环境视觉、DSP 时钟、主控制器与演示谱面。

| 文件 | SHA-256（修改前后相同） |
| --- | --- |
| `ChartData.cs` | `FEC733A7E6CD0660F262E122706CCAC7BE6C942D23AC45D5C825D7CE3EA2A2B3` |
| `JudgementEngine.cs` | `88974CC50401D2C75E69EAD10AFD9E432827DFE77843D6DE6546926FDA389121` |
| `SpatialDirector.cs` | `C8FE403FBA591804679574C174D774EAFE79137DC6521F4705F97D2219966CE8` |
| `StageSpline.cs` | `BCA92E7F59D9705137938DB662512FDBEB07F7B96BF023976A08B896CC16858E` |
| `SceneVisuals.cs` | `9A97A9412677E9D06F352B03D002E42F2C0274ACD547A0E6EFE8C9CF2C84C42C` |
| `SongClock.cs` | `059C19E5D089F7AB81986990500999EFEC113157D6D5FD035C4664DCDBAC6496` |
| `RhythmDemoController.cs` | `292E16DBF8052C99A39490F03FE7D3DF3D9B520382EF348FD602161190444D45` |
| `geometry-demo.json` | `8326B6561DB6C12A7141BB0D1951BCF93BF3D0FC660B85ADD350C4D59DC5B3D9` |

## 验证边界

- UI 快速自检会程序化推进判定状态，不能称为真人完成整曲；加载失败与空列表是受控 QA 画面。
- 目前仅有一首真实 Demo。不会为填满曲库而增加假歌、假难度、排行榜或最高分。
- 主菜单使用安全区域等比布局；本次重点验收 Windows Full HD。尚未完成移动端刘海屏、多指输入、中文字体、全部宽高比和长标题的真机验收。
- Play 模式中的 C# 热重载可能清空既有非序列化会话引用；编辑脚本前应退出 Play，再刷新资源并重新运行。本次不为该编辑器开发场景修改玩法核心。
- 菜单仍用英文，字体授权和分发要求见 [字体许可说明](ThirdParty/Fonts.md)。
