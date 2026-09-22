# 赛博 UI 验收记录

日期：2026-09-18。Unity 2022.3.62f3c1，Windows x64，Built-in 渲染管线。

## 标题与主界面分层

标题页现在与主大厅完全分开。标题屏播放 `StreamingAssets/TitleVideos/` 中随机选中的视频，使用电影式上下黑边；顶部黑边显示白色未来风格游戏标题，下方黑边是唯一入口，显示“开始游戏 / Game Start”。标题页不加载 MainUI 的角色舞台图片、不创建角色槽或关卡胶囊，也不启动菜单 BGM；进入标题时画面与视频音量同步用 3 秒淡入。点击后会同时播放约 2.1 秒的 `Now let's begin` 机械提示音并开始标题 3 秒淡出，视频在淡出过程结束后停止；MainUI 在 1.8 秒内浮现，同时 `Resources/Music/main bgm` 在 1.8 秒内由静音渐入。进入 MainUI 后可继续使用顶部 `0-1` 至 `0-3` 关卡进度、三张可循环背景图、左下角 News 报纸入口、右下角手机好友入口、右下角 `ENTER!` 操作条、左上角返回确认和右上角操作说明。News、好友、返回确认、操作说明都不替换 MainUI，而是作为上层窗口从对应按钮位置展开，关闭时缩回按钮。News 初始两条帖子带未读红点，卡片列表也显示对应未读标记；打开详情后单条未读标记清除。News 详情使用爱心图标点赞和与正文对应的普通评论。好友窗口当前显示 Tnelis，聊天按文本长度延迟逐条出现，好友输入时显示闪烁 `···`，并包含带两次选择的预设分支。未读好友消息会在 MainUI 手机图标和 Tnelis 卡片同时显示红点，打开聊天后清除。Tnelis 对话结束后会生成 News03；打开 News03 会生成 Baka 的 News04。对 Baka 帖子评论后，FriendUI 右上方申请图标亮红点；打开申请面板并接受后 Baka 才加入好友列表并自动打开预设聊天。新新闻、好友申请和好友消息还会在左上方生成可点击通知栈，播放提示音，并在 8 秒、点击或阅读对应内容后消失。News 卡片流每行至少可放 4 个纵向小窗，显示图片、标题、发布者和正文摘要；详情窗口从点击卡片位置展开，隐藏标题栏 `X`，改用 `< BACK TO FEED`，返回时缩回原卡片。`ENTER!` 先上拉进入剧情／自由模式选择；剧情章节页与 MainUI 的 `0-1` 当前进度同步，自由模式复用完整曲库界面。无视频时会回退到 `CyberTitleGraphic`。

`CyberHomeBackdropGraphic.cs` 与 `CyberIdolGraphic.cs` 继续作为 MainUI 背景资源缺失时的 UGUI MeshGraphic 回退；角色透明立绘仍可从 `Resources/Characters/hero` 自动加载并覆盖前景剪影。静态编译已用 Unity 2022.3.62f3c1 自带的 Roslyn 编译 `Assembly-CSharp` 与 `Assembly-CSharp-Editor` 响应文件，均无诊断。标题单入口、进入 MainUI 和 BGM 状态检查已加入 `-frontendSmoke`。由于验收时本机已有 Unity 编辑器占用同一工程，本轮没有并行启动第二次 Unity Player 构建；最终 Player 截图仍需在下一次独占 Unity 会话时重跑并替换 `ui-title.png`。

## 变更边界

本次重做标题、选曲、结算、加载、错误、空曲库、游玩 HUD 和暂停控制台。仅增加 UI 绘制组件与字体资源；没有改变 Note 网格、Tap / Drag 定义、保护套规则、输入判定时窗、音频同步、谱面结构或镜头／路径。菜单中的同心环是封面插画，不是 Note 皮肤。制谱器继续使用原来的接口。

`RhythmFrontend.cs` 本次只扩展 opt-in 的 `-frontendSmoke` QA 截图与测试；正常页面状态转换和会话管理方法保持原样。

## 最终自动验证

- 构建日志：`.validation/build-cyber-final.log`。
- 结果：`GEOMETRY_VALIDATION_PASS 239 checks`；`GEOMETRY_BUILD_SUCCESS 73863116 bytes`。
- UI 自检：`.validation/Cyber-final/frontend-smoke.txt`，上一轮 `PASS=True`。当前代码增加了标题单入口、进入主界面和 BGM 渐显状态检查；最终 Player 结果待下一次独占 Unity 会话重跑。
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
