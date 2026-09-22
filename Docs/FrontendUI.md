# 赛博朋克 UI：标题、歌曲列表、结算与游玩 HUD

全部玩家界面已经作为可交互的 Unity UGUI 接入原来的 `GeometryRhythmDemo` 场景，不是图片或视频。2026-09-18 版本采用深色赛博朋克视觉；现有游玩背景、Note 皮肤、四种判定规则、JSON 格式和不限帧设置保留。

## 1. 如何运行

打开 `Assets/RhythmDemo/Scenes/GeometryRhythmDemo.unity`，等待编译后按 Play。默认先显示标题，而不是立即播放歌曲。也可以运行 `Builds/RhythmHacker/RhythmHacker.exe`；分发时携带整个目录。

完整流程：标题 → 主界面 → 模式选择 → 剧情关卡／自由选歌 → 剧情播放／手动游玩／自动演示 → 结算 → 再来一次／返回选歌／继续剧情。MainUI 的剧情进度与剧情关卡页读取同一数值。

| 页面 | 操作 | 结果 |
| --- | --- | --- |
| 标题 | ENTER GAME／Enter | 标题淡出，主界面缓慢浮现，同时 BGM 渐入 |
| 主界面 | ENTER!／Enter | MainUI 向上滑出，模式选择从底部上推进入 |
| 主界面 | 右下角 BG | 在主界面背景 `stage-01` 至 `stage-03` 之间循环切换 |
| 主界面 | 左下角报纸图标／NEWS | 从图标位置弹出桌面式窗口气泡，进入原创赛博社交流 |
| 主界面 | 右下角手机图标 | 从图标位置展开虚拟好友窗口，查看好友并进入聊天 |
| 主界面 | 顶栏 `<` | 从左上角按钮位置弹出返回标题确认框；可取消或确认返回 |
| 主界面 | 顶栏 `SET` | 从右上角按钮位置弹出操作说明窗口 |
| News | 标题栏 `X`／Esc | 将窗口缩回左下角图标并返回 MainUI |
| 主界面 | Esc | 返回标题 |
| 选曲 | 点击曲目 | 更新右侧详情 |
| 选曲 | PLAY CHART／Enter | 从头开始手动游玩 |
| 选曲 | AUTOPLAY / PREVIEW | 自动演示，结算明确标记为预览 |
| 模式选择 | STORY MODE | 进入剧情章节选择 UI |
| 模式选择 | FREE MODE | 进入自由选歌界面 |
| 模式选择 | MAIN MENU／Esc | 反向下滑返回 MainUI |
| 剧情章节 | SELECT MODE／Esc | 返回模式选择 |
| 剧情章节 0-1 | 点击当前章节卡片 | 进入 `0-1` 剧情播放页；分支只影响后续对白文本 |
| 剧情播放 | CONTINUE／Enter／Space | 推进当前对白或背景步骤 |
| 剧情播放 | 选项按钮 | 设置剧情变量，继续当前线性步骤 |
| 剧情播放 | ENTER STAGE | 播放该步骤绑定的谱面，结算后返回剧情 |
| 选曲 | SELECT MODE／Esc | 返回模式选择 |
| 游玩 | 左上暂停／Space／Esc | 暂停；可 RESUME SIGNAL 或 MUSIC ARCHIVE |
| 游玩 | 歌曲时间结束 | 自动结算，不再停留在旧 HUD 的 COMPLETE 提示 |
| 结算 | PLAY AGAIN／Enter | 同曲、同模式重试；分数与 Combo 重置 |
| 结算 | MUSIC ARCHIVE／Esc | 返回歌曲列表并保留所选曲目 |

游玩中仍支持 R 重开、A 切换模式并重开、M 静音、左右方向键跳转练习。切换到菜单时停止音频和谱面时间，隐藏游玩 HUD、路径、Note；深色菜单完整覆盖底层环境。开始／恢复游玩时等待菜单触点松开，防止按钮点击穿透到全屏判定 Note。

## 2. 页面与视觉

以用户提供的 Cytus II 与杂音均衡器截图为视觉方向，使用原创网格／电路边框／信号核心插画，没有复制商业游戏 Logo、角色、封面、字体或 Note 皮肤。

- 标题：独立于 MainUI 的全屏视频界面，使用电影式上下黑边。上方黑边内居中显示白色未来风格游戏标题 `RHYTHM HACKER`；下方黑边同时作为唯一入口，显示白色“开始游戏”与英文注释 `Game Start`。视频从 `Assets/StreamingAssets/TitleVideos/` 自动扫描，每次启动随机选择一个；当前 `raputa-maimai.mp4` 已转为 1080p H.264 + AAC，避免依赖部分机器缺失的 HEVC 解码器。没有可用视频或解码失败时，回退到 `CyberTitleGraphic` 绘制的斜切块面和破碎圆环。标题页不读取角色大厅背景、不创建角色槽或关卡胶囊。
- 主界面：独立的大厅界面，使用顶部窄状态栏、全屏角色舞台和底部横贯操作条。顶部中央显示 `0-1` 至 `0-3` 的关卡进度，底部中央是 `ENTER!` 入口，右下圆形 `BG` 按钮循环切换背景图。`Resources/Backgrounds/stage-01.png` 至 `stage-03.png` 加载成功后会自动隐藏占位剪影；资源缺失时才显示空白人物剪影作为回退。
- News：左下角报纸图标进入原创社交流。News 作为 MainUI 上方的桌面式窗口弹出，关闭时缩回图标位置，因此不会销毁或重载底层 MainUI。首屏每行至少可排列 4 个纵向小窗卡片，显示图片、标题、发布者和正文摘要；初始两条帖子默认是未读状态，MainUI 报纸图标和对应帖子卡片都会显示红点。点击卡片进入详情后，该帖子的红点立即清除，返回预览时不会重新出现。详情窗口从卡片位置展开，隐藏标题栏 `X`，显示完整正文、已有评论、标签和爱心点赞按钮，并提供 `< BACK TO FEED` 缩回卡片流。评论采用普通帖子模式，每篇帖子根据自身正文提供对应的预设评论，不进入对话分支。详情正文、评论及投稿者 ID 使用更大字号。帖子与基础点赞数集中在 `RhythmFrontendView` 的 `newsPosts` 列表，内容始终留在本地，不接入网络。
- 好友：MainUI 右下角手机图标展开精简的 Friends 窗口。当前只有好友 `Tnelis` 和接受申请后的 `Baka`，聊天按真实消息节奏逐条出现，好友输入时显示闪烁的 `···`，输入时长按文本长度计算；双击聊天区域可跳过当前好友剩余的消息等待，直接显示待发送内容。Tnelis 对话包含两次分支选择，选择会写入临时聊天记录并影响后续回复，关闭窗口后记录仍保留到本次运行结束为止。未读好友消息会在 MainUI 手机图标和对应好友卡片同时显示红点；打开聊天后才清除。
- 好友申请：Tnelis 对话结束后关闭 FriendUI 才生成 News03；阅读 News03 并退出详情页才生成 Baka 的 News04，配图为 `baka-post.png`。在 Baka 帖子的评论中输入与帖子正文对应的评论后，首次回到 MainUI 才会收到带有 `baka.jpg` 头像的好友申请。FriendUI 标题栏右上方有独立的联系人申请图标，带红点时点击打开申请面板；申请面板只允许接受，接受后 Baka 才会加入好友列表并自动打开预设聊天。
- 全局通知：新 News、好友申请和新好友消息会在界面左上方生成通知卡片；多个通知按事件发生顺序从上到下排列。通知使用从左侧滑入、缩放、淡入以及退场淡出动画，可点击快速跳转到对应帖子、好友申请面板或聊天。通知会在 8 秒后自动消失，点击卡片或阅读对应内容时也会立即消失；每次新通知播放提高音量后的 `Resources/Audio/notification.wav`。
- 顶部弹窗：左上角 `<` 的返回确认和右上角 `SET` 的操作说明同样从各自按钮位置展开；关闭时缩回原按钮。弹窗都使用全屏暗幕、切角面板和青／粉／紫强调色，底下的 MainUI 保持存在，切换时不会重新构建。
- BG 入口：MainUI 右下角 `BG` 图标带持续呼吸式脉冲轮廓，背景切换期间脉冲频率和亮度增强。背景图使用前后两层 `RawImage` 交叉渐变，切换完成后才替换当前层，不会出现硬切。
- 音频：标题页进入时画面与视频音轨共同在 3 秒内淡入，不启动 MainUI 的菜单 BGM。点击“开始游戏”后会立即同时播放约 2.1 秒的未来机械提示音 `Now let's begin` 和开始标题 3 秒淡出；随后 MainUI 在 1.8 秒内浮现，`Resources/Music/main bgm` 同步在 1.8 秒内从 0 渐入。进入选曲后继续播放，开始游玩时淡出，返回主界面或选曲时恢复。
- 模式选择：分为 `STORY MODE` 与 `FREE MODE` 两张大型路线卡片。剧情路线显示 MainUI 同步的当前 `0-1` 进度和后续锁定节点；自由模式显示“可游玩全部谱面”的路线提示。
- 剧情章节：横向展示 `0-1`、`0-2`、`0-3` 三个章节节点，当前进度高亮，后续节点锁定。`0-1` 已接入 `Resources/Story/main-story.json`，点击当前章节卡片进入剧情播放。
- 剧情播放：沿用 MainUI 的深色叠层、切角面板、青色强调轨和 Mono 标头；使用全屏背景、透明角色立绘、底部对白面板和章节选择返回入口。选项仅设置变量，后续对白按变量替换文本。进入关卡步骤复用正式谱面播放，结算后由“RETURN TO STORY”回到剧情。
- 自由选曲：左侧滚动曲库，中间独立封面套，右侧曲名／作者及 BPM、时长、音符数、路径数仪表。底部区分手动开始与自动预览。
- 结算：大号总分区、独立荧光黄评级、青色准确率、四色判定统计卡片与再次游玩入口。
- 加载、缺失资源错误、空曲库：同一套边框、字体和状态语言，不加入虚假的百分比、网络状态或不可玩的曲目。
- 游玩 HUD：深色顶部仪表、荧光黄 Combo、青色准确率与进度条、底部模式／声音／重开控制。中间仍是原有三维舞台，没有加全屏滤镜或修改 Note。
- 暂停：半透明遮罩和切角控制台，保留恢复／返回选曲功能。遮罩只在暂停时接收点击；装饰网格、边框、文案不拦截游玩触点。

界面框架文案沿用 Demo 的英文风格；剧情对白和选项使用运行时中文字体回退。字体为随工程打包的 **Rajdhani Bold / Medium** 与 **Share Tech Mono**，均为 SIL OFL 1.1 开源字体，不依赖电脑安装。字体位于 `Resources/Fonts`，同目录保留完整许可证；分发包另附 `Docs/ThirdParty`。

菜单没有大面积 Bloom 或模糊后处理。所有页面切换统一由最上层全屏黑幕控制，因此 MainUI 的背景和 UI 始终同步淡入，切换时也不会短暂露出旧页面。标题优先用 `VideoPlayer` 输出到 `RenderTexture`，再显示在 `RawImage`；视频本身不参与谱面或主游戏音频时钟。视频使用 Direct Audio，因此标题黑幕淡入进度会同步写入视频音量。没有视频时，`CyberTitleGraphic` 作为原创矢量回退。MainUI 使用 `RawImage` 展示 `Resources/Backgrounds/stage-01` 至 `stage-03` 的图片，并按画布比例做居中裁切。普通页面入场淡入为 0.18 秒，标题进入和离开均为 3 秒，MainUI 进入为 1.8 秒。

MainUI 的 `ENTER!` 和选歌页返回按钮使用同一套双向页面位移：进入时旧 MainUI 向上滑出、歌曲列表从底部同步上推；返回时歌曲列表向下滑出、MainUI 从顶部同步落回。转场完成后才销毁旧页面，不会把两个页面叠成静态弹窗。

共享主题在 `CyberTheme.cs`：深底 `Background`、分层面板 `Panel/PanelLight`，强调色 `Cyan/Purple/Lime/Pink`。优先从这里调整颜色与字体。布局数字集中在 `RhythmFrontendView`，游玩读数集中在 `DemoHud`；不要为了修改 UI 去改 `SceneVisuals` 或判定引擎。

布局采用 1600×900 设计尺寸，在实际 Canvas 的安全区域内等比容纳。它不是渲染分辨率：独立程序默认 1920×1080，Game 视图也可选 Full HD。非 16:9 窗口仍保持主要控件完整，不通过裁掉两侧内容强行铺满。

## 3. 改游戏名字

游戏标题为 **Rhythm Hacker**，默认两行 `RHYTHM`、`HACKER`。`RhythmDemoController` 的 **Game Title** 可在非 Play 状态修改；页眉品牌名自动合并为一行，品牌标记统一使用 `R/H`。

Windows 产品名由 `ProjectSettings.productName` 和批处理构建入口 `DemoBuildTools.ValidateAndBuild` 统一设置为 `Rhythm Hacker`，构建输出为 `Builds/RhythmHacker/RhythmHacker.exe`。

`RhythmDemoController.Show Frontend` 默认开启。开发时关闭可跳过菜单直达旧的游玩 Demo；`-demoSmoke` 始终跳过菜单，用于原有玩法验证。

## 4. 新增歌曲与 JSON 约定

歌曲列表扫描 `Assets/RhythmDemo/Resources/Charts/` 中的 TextAsset JSON。每一份合法谱面就是一个可选条目，不另造与谱面不一致的假数据。当前只有真实可玩的 `Geometry / First Light`，没有虚构的其他歌曲或难度。

1. 把制谱器导出的有效 JSON 放到上述目录，文件名保持唯一。
2. 使用谱面已有的 `title`、`author` 元数据。标题含 ` / ` 时，详情主标题取最后一段，完整名称仍保留在数据中。
3. 配置 `audioResource` 指向 Resources 下的音频，不带扩展名；为空仍使用原有合成演示音乐。
4. 重新启动场景／重新构建，即可收录。运行中的列表不会监听磁盘热更新。

BPM 范围、时长、Note 数、同时路径上限均从 JSON 计算。无效 JSON 会跳过并在 Console 发出 Warning；若某个有效谱面引用了缺失音频，启动时给出可读错误并允许返回选曲。空列表也有空状态，不提供不能使用的 Play 按钮。

`Chart Override` 和 `-demoChart <path>` 保留，显式指定的谱面排在首位。添加新曲时不需要修改选曲 UI、计分逻辑或 JSON schema。不同难度当前表现为不同 JSON 条目，尚未引入“歌曲—多个难度”的目录层级。

## 5. 结算的真实性

`PlayResult` 在结束时从 `JudgementEngine` 复制成绩，之后重试不会把已显示的结果改成零。显示的是本次真实判定数据，不是固定的满分样例。

- 自动演示显示 `AUTOPLAY / PREVIEW` 与“不是玩家记录”的提示。
- 用左右方向键跳转后显示 `PRACTICE SESSION`，分母使用练习区间可判定 Note 数。
- 手动全曲显示 `MANUAL PLAY`。
- 目前不写入历史成绩、最高分、排行榜或云端记录，界面明确说明尚未开启保存。
- 歌曲结束会统一处理尚未判定的末尾音符，避免谱面时间被夹到歌曲总时长后留下 Pending。

评级是当前 Demo 的展示规则：100% 为 SSS，≥99% 为 SS，≥98% 为 S，≥95% 为 A，≥90% 为 B，≥80% 为 C，其余为 D。评级不改变原有计分算法。

## 6. 代码职责

| 文件 | 职责 |
| --- | --- |
| `Runtime/RhythmFrontend.cs` | 页面状态、真实曲库、活动游玩实例、开始／返回／结算、可重复的 UI 自检 |
| `Runtime/RhythmFrontendView.cs` | 标题／主界面／模式选择／剧情章节／自由选曲／结算／加载／错误／空状态、按钮、滚动、安全区域与转场动画 |
| `Runtime/CyberTheme.cs` | 共享色板、字体加载、轻量网格绘制辅助 |
| `Runtime/CyberTitleGraphic.cs` | 标题页无视频或解码失败时专用的黑场、斜切块面和破碎圆环，不读取 MainUI 背景 |
| `Runtime/CyberHomeBackdropGraphic.cs` | 主界面原创城市、舞台光环、透视网格与低对比背景层 |
| `Runtime/CyberIdolGraphic.cs` | 主界面空白人物剪影占位与替换区域边框，不依赖外部图片 |
| `Runtime/CyberNewsArtwork.cs` | News 帖子中的原创雨水街区／信号灯牌矢量图 |
| `Runtime/NewsIconGraphic.cs` | MainUI 左下角的报纸入口图标 |
| `Runtime/PhoneIconGraphic.cs` | MainUI 右下角的手机／好友入口图标 |
| `Runtime/FriendRequestIconGraphic.cs` | FriendUI 标题栏右上角的独立好友申请图标 |
| `Runtime/HeartIconGraphic.cs` | News 详情中的爱心点赞图标 |
| `Resources/News/news-01.jpg` 至 `news-02.jpg` | News 帖子的本地图片，按卡片比例自由中心裁剪 |
| `Resources/Friends/tnelis.jpg` | 好友 Tnelis 的头像资源 |
| `Resources/Friends/baka.jpg` | Baka 的好友头像与申请头像 |
| `Resources/News/baka-post.png` | Baka 的 News04 帖子配图 |
| `Resources/Audio/notification.wav` | 新 News、好友申请和新消息的提示音 |
| `Resources/Backgrounds/stage-01.png` 至 `stage-03.png` | 主界面可循环切换的三张全屏背景图 |
| `StreamingAssets/TitleVideos/` | 标题视频目录；启动时扫描 `.mp4/.mov/.webm/.m4v` 并随机选择一个 |
| `Resources/Audio/now-lets-begin.wav` | 进入 MainUI 前播放的未来机械提示音 |
| `Runtime/CyberPanelGraphic.cs` | 切角面板及按钮底图 |
| `Runtime/CyberFrameGraphic.cs` | 渐变边饰、电路轨迹、低对比背景网格 |
| `Runtime/MenuArtwork.cs` | 原创信号核心封面插画，非游玩 Note |
| `Runtime/RhythmDemoController.cs` | 启动菜单入口、游玩会话、完成标记、菜单／游玩隔离 |
| `Runtime/DemoHud.cs` | 赛博仪表 HUD、判定提示、暂停控制台，接口与回调保持原样 |
| `Editor/DemoValidation.cs` | 原有规则检查及曲目元数据／结算快照验证 |

只保持一个活动游玩实例。换曲时先停用旧实例，再销毁并创建新实例，防止双相机、双 AudioListener 或多条音乐同时播放。同曲重试复用实例与合成音轨；页面切换不重新加载整个场景。

制谱器文件与自定义舞台路径实现未在本次 UI 工作中改写，继续使用同一份 ChartData 和 SpatialDirector。

## 7. 验证与截图

批处理 `GeometryRhythm.Editor.DemoBuildTools.ValidateAndBuild` 现在包含 239 项断言，新增了曲库元数据、评级、自动预览标记、结算快照不受重置影响等检查。

玩法自检仍使用 `-demoSmoke`。UI 自检另用：

```powershell
.\RhythmHacker.exe -frontendSmoke -demoCapture "D:\Captures\UI" -screen-width 1600 -screen-height 900 -screen-fullscreen 0 -logFile "D:\Captures\ui.log"
```

自检触发实际 Button 回调，检查标题只有单一入口、进入后主界面与 BGM 渐显状态、背景切换菜单、操作说明开关、选曲、开始、暂停返回、手动 Miss 结果、重试重置、自动满分预览、结算返回与主界面返回，导出实际 Unity 渲染截图和 `frontend-smoke.txt`。QA 还会覆盖加载、错误、空状态和暂停画面；错误与空曲库截图使用仅存在于 QA 流程的状态，不伪造正式库内容。截图有效性检查使用整页明暗对比，兼容深色 UI；重要文字是否可见仍需逐张视觉验收。它还会在内存里临时创建一份 QA 谱面，切换过去再切回，检查活动相机／AudioListener 唯一且成绩重置；该谱面不会加入正式曲库或写入文件。该快速自检直接推进部分判定状态，不冒充真人打完一首歌。

原版 UI 验证记录保留在 `.validation/UI-final`。赛博版本构建与回归记录见 [赛博 UI 验收](CyberUIVerification.md)，包括最终日志路径、截图、八个玩法核心文件的 SHA-256 对照和验证边界。

![标题（旧版截图，下一次 Player 自检时替换）](Screenshots/ui-title.png)

![歌曲列表](Screenshots/ui-songs.png)

![结算](Screenshots/ui-results.png)

![暂停](Screenshots/ui-pause.png)

当前仍是可继续迭代的 Demo：没有联网、账号、存档、排行榜、难度分组、搜索、手柄焦点导航、移动端真机字体／安全区验收。
