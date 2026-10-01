# 2D 音游玩法与 Groove Coaster 机制：网络调研记录

> 起因：用户指出方向性错误——"视频里有 3D 场景才要做成 3D 的游戏，2D 的视频就做成 2D 游戏；2D 游戏的玩法参照 osu、节奏过山车这类"。
> 本仓库当前的游玩运行时 `GeometryRhythmDemo` 是 3D 的（音符沿 spline 飞来、相机骑路线前进、有雾与光照），而 `BV12K4y1P7ps`（EBIMAYO - GOODTEK）这条 BGA 是**平面图形动画** → 按该规则应当做成 2D。
> 本文只记录**外部调研**结论；本仓库现状（哪些是 3D 专有、2D 数据在哪一步被压掉）见 §8 的实测摘要。

## 0. 证据分级与抓取限制

- 【有据可查】= 抓到原文并引用；【二手】= 抓到了，但来源是社区 wiki / 百科而非官方；【推断】= 基于已抓原文的推理；【无一手来源】= 只见标题或商店页，正文未取得。
- **本环境抓不到**（因此相关论断一律没有写死）：`store.steampowered.com`、`github.com` / `raw.githubusercontent.com`、`hitkey.bms.ms`、`namu.wiki`、`readonly.wiki`、Fandom、`osu.ppy.sh` 正文（Cloudflare）、`web.archive.org`、`gamedeveloper.com`（403）、`steamcommunity.com`。
- 未完成的一路：**osu! 一手来源**（判定面坐标域、打击物件判定几何、判定窗公式、Background Dim 机制）——见 §7。

## 1. 结论速览

1. **「节奏过山车」= Taito 的 Groove Coaster**（§2）。
2. **Groove Coaster 的官方机制是"一条轨道 + 唯一判定点 = avatar"，读法是"avatar 与 target 重合的瞬间"**，不是"音符飞到判定线"（§3.1）——即**伪 3D 呈现、1D 判定**。
3. **Groove Coaster 新作 FUTURE PERFORMERS 就是"官方 MV 当背景、轨道音符叠在上面"的在售商业案例**，并用"系列首次横置全屏"给背景让位（§3.5）。
4. **"BGA/视频 + 音符"在业界被明文分成两派**：BGA 当背景叠音符（**DJMAX / DDR / Pump It Up**）与 BGA 放专用区域（**beatmaniaIIDX / LunaticRave2**）——见 bmson 规范脚注 4 原文（§4）。
5. **可读性的规范答案不是"调暗"，而是"高对比 + 描边/阴影"**：Game Accessibility Guidelines 给的可测阈值是 **4.5:1**；另有闪烁/重复图案的硬阈值与 Rhythm Doctor 的"减少闪烁"落地案例（§6）。
6. **没有检索到以"视频背景音游"为专题的一手设计文献**——这一点我没有找到，不替它编（§6.4）。

## 2. 「节奏过山车」的身份

【有据可查】百度百科条目名即《节奏过山车》并带 `fromtitle=Groove Coaster`，正文写「《节奏过山车》（Groove Coaster）是由日本 TAITO CORP. 开发的一款音乐节奏类游戏」——[百度百科](https://wapbaike.baidu.com/item/%E8%8A%82%E5%A5%8F%E8%BF%87%E5%B1%B1%E8%BD%A6/10703652?fromtitle=Groove%20Coaster&fromid=60326076)。
【有据可查】Taito 官方新闻页确认系列 2011 年自手机端起家——[Taito 官方新闻](https://www.taito.co.jp/mob/topics/26716)。
另有 Steam 版《节奏过山车2》条目（[cr173](https://www.cr173.com/soft/1670897.html)）、[游侠网下载页](https://3g.ali213.net/down/groovecoaster.html)、[英维基条目](https://www.wikiwand.com/en/Groove_Coaster)。**Steam 商店页正文抓不到**，所以商店页原文无一手来源。

## 3. Groove Coaster 的玩法机制

### 3.1 轨道与判定：判定点唯一，就是 avatar 自己

【有据可查】街机官方 ABOUT 原文：

> 「画面上のレールの上を走る「アバター」がターゲットを通過する時に、曲のリズムにあわせてタイミングよく専用演奏コントローラー「ブースター」を操作しよう！ ジェットコースターのような疾走感と、楽器を演奏しているかのようなグルーヴ感を体感できます!」
> — [groovecoaster.jp/about](https://groovecoaster.jp/about/)

【有据可查】官方「遊び方」三步原文：

> 「1：音楽に合わせて、画面上のレールを「アバター」が走っていきます。2：アバターとターゲットが重なった瞬間にブースターを操作！ ターゲットの種類によってブースターの操作方法が変わります。3：曲が終わった時、スコアがゲージの70%を越えていればクリア！」
> — [groovecoaster.jp/about/howtoplay](https://groovecoaster.jp/about/howtoplay/)

→ **轨道是唯一玩法载体；判定点唯一，就是 avatar**（avatar 即"播放头"）；target 是预先布置在轨道上的固定物，玩家读的是 **avatar 何时压上它**。这是官方明写，不是推断。

### 3.2 音符怎么读时机

【推断】读法是**空间重合**而不是"投影到判定线"：轨道弯曲、相机运动时，屏幕上的重合点位置会变。
【有据可查】操作方式随 target 类型变化（见 §3.4 的 Basic 4 种 / Advanced 9 种）。

### 3.3 相机与画面：伪 3D 呈现、1D 判定

【有据可查】官方英文页大标题「Spinning?! Accelerating?! Run through the ever-changing sheet music!」，同页宣传「Unique to Groove Coaster! New! First in the series Full screen horizontal display」——[FP 官方英文页](https://groovecoaster.com/fp/en/lp01/index.html)；日文页同段写「シリーズ初！横置きフル画面表示」「画面いっぱいに広がる音楽の世界」——[FP 官方日文页](https://groovecoaster.com/fp/)。
【二手】百度百科：「游戏画面模拟过山车轨道视角，场景随音乐节奏 360 度旋转变化」。
【推断】视觉是**伪 3D 透视 / 沿轨道推进的运镜**（会旋转、加速），但**判定是一维轨道参数（avatar 前进距离）**——即「3D 画面、1D 判定」。手机版官方口号「音と光のジェットコースター」——[App 官方 ABOUT](https://groovecoaster.com/apps/about.html)。

### 3.4 输入方式（官方）

- **街机**：双「ブースター」，官方原文「「ブースター」はどちらか片方だけでも操作できます！」【有据可查，howtoplay】。
- **手机（GC2 ORIGINAL STYLE）**：触屏点按；另有「オリジナルスタイル機能」——唱歌、拍手、敲桌子、拿真乐器都能玩，"不必用触摸屏"【有据可查，App ABOUT】。
- **Switch FP**：Basic 4 种 —— Circle「Press any button」/ Line「Hold any button」/ Arrow「Press a button that matches the arrow direction」/ Square「Press any button (ZL, L, R, ZR)」，另加 Combination（一手按住 + 另一手点）与 Special Note；Advanced 在此基础上再加 Dual Circle / Dual Line / Dual Arrow / Left・Right Circle / Left・Right Arrow【有据可查，FP EN】。
- **判定档位**：FP 为 PERFECT+ / PERFECT / GREAT / GOOD / MISS，SP NOTE 只出 PERFECT+ 或 MISS，曲末槽 ≥70% 算 CLEAR，等级 FAILED / CLEAR / FULL CHAIN / ALL PERFECT，LINE 音符按晚或松早会 MISS【二手，[wikihouse SwitchFP](https://www.wikihouse.com/groove/index.php?Groove%20Coaster%A1%CASwitchFP%C8%C7%A1%CB%2F%A5%B2%A1%BC%A5%E0%B3%B5%CD%D7)】。
  **判定窗口的毫秒数：无一手来源**（没找到官方数值表）。

### 3.5 背景：程序化图形 → 新作改用 MV

【有据可查】官方对系列背景的表述是随曲定制的背景图形：「曲のイメージに合わせて作りこまれた背景グラフィックの再現は多くのプレイヤーの皆様より高い評価をいただいています」（[Taito 官方新闻](https://www.taito.co.jp/mob/topics/26716)）——即程序化/预渲染图形，**不是通用 BGA 视频格式**。
【有据可查】新作 FP 明确改成 MV：「Uses music videos of popular songs, along with original expressions unique to Groove Coaster!」/ 日文「まるでミュージックビデオのような世界でお気に入りの曲を楽しむことができる」（FP 官方页）。
【推断】层级上轨道 + target 叠在背景之上；**官方没有任何"层级"声明，layer order 无一手来源**。
→ 对本项目的价值：**GC FP 是"MV 压在 2D 轨道音符之下"的在售商业案例**，且它用"全屏横向显示"给背景让位。

### 3.6 版本差异（Steam 是不是同一套玩法）

同一套核心（avatar 沿轨道撞 target、按类型操作），差异在输入与谱面【二手，[機種ごとの違い](https://www.wikihouse.com/groove/index.php?%B5%A1%BC%EF%A4%B4%A4%C8%A4%CE%B0%E3%A4%A4)】：

| 版本 | 输入 | 备注 |
|---|---|---|
| 街机（4MAX DIAMOND GALAXY） | 双 booster | 含店内对战、活动、解锁曲 |
| App（GC2 ORIGINAL STYLE） | 触屏 + 拟乐器 | 官方称移植了街机舞台（"アーケードモード追加"）【有据可查，App ABOUT】 |
| Steam | 键盘 / 手柄 | 「AC版が基になっている」；**不支持触摸屏**；无 BASIC 谱面；含 EXTRA 模式；皮肤/音效不可改 |
| Switch FP（2025-07-31） | 手柄 | 从零重做的完全新作：4/9 种 note、横置全屏、MV 背景、首次剧情模式【有据可查，Taito 新闻 + FP 官方页】 |

## 4. "BGA / 视频 + 音符"的业界分流（bmson 规范，本次最硬的一手材料）

【有据可查】bmson 规范 BGA 一节脚注 4 原文：

> 「Some game may choose to display the BGA as the background, and overlay notes on top of it. Example commercial games that use this approach are DJ MAX series, DDR, and Pump It Up. Other games may display the BGA in a dedicated space. Examples are beatmaniaIIDX and LunaticRave2.」
> — [bmson spec · BGA](https://bmson-spec.readthedocs.io/en/master/doc/index.html#bga-bga)

同节其他可直接引用的规则：

- `layer_events` = 叠在 BGA 之上的层；**`poor_events` = MISS 时切换的层**（本项目阶段 1 实现的 `effectClips.target=="judgement"` + `result=="miss"` 与此同源）。
- 透明规则：「Unlike BMS Layer Channel #xxx07, black pixels will not be made transparent. If you want transparency, use a file format that support transparency, such as PNG」——**透明靠文件格式，不能黑底抠**。
- 尺寸与裁切：「Recommended picture size is 1280x720. 1920x1080 is also acceptable. In game with different aspect ratio, the background image may be cropped in the center. Therefore, make sure that the key elements are near the center of the image.」
- 播放器侧格式：图片 PNG、视频 WebM（可忽略音轨）。规范自称「Currently, BGA specification is just compatible with BMS」，命令级细节指向 hitkey 的 BMS command memo——**该 memo 抓不到，所以 `#BGA` 显示区域等命令级定义无一手来源**。

【有据可查】素材制作侧的公开实践：BMS 生产 checklist 的 BGA 编码指南写「many skins have square BGA windows and many people use the setting to stretch BGAs」，并给出方窗黑边填充的做法与「LR2SD can only render up to 256x256 for BGAs」——[wcko87 · BGA Encoding Guide](https://wcko87.github.io/bms-checklist/video-encoding)。

## 5. 逐案例：2D 音符压在视频/动画背景上还读得出来

| 案例 | 玩法维度 | 背景 | 音符与背景的层级 | 公开可查的可读性手段 | 来源等级 |
|---|---|---|---|---|---|
| osu! | 2D 面（固定 512×384） | 图或**可选视频** | 维度分离：判定面是固定 2D 域，视频是可选背景层（video 层在 storyboard 之下、所有 storyboard 元素在打击物件之下） | Background dim 默认 **0.7**（含视频，break 时降 30%）+ **RC 硬规则：combo 颜色不得与背景/视频混同** | 【有据可查】见 §6.5（经 `ppy/osu-wiki` 源 Markdown 取证，正文被 Cloudflare 拦） |
| Just Shapes & Beats | 2D 面，玩法是"躲" | 程序化（几何体即攻击） | **背景与玩法同一层**，没有独立音符层 | 机制上只需辨识"粉色有害物"；官方访谈强调"不用音乐耳朵也能玩" | 【有据可查】「players incarnate one of four simple shapes and they must survive the tracks by avoiding the pink shapes that the music creates.」[Amazon Luna 访谈](https://www.amazongamestudios.com/en-us/news/articles/amazon-luna-just-shapes-and-beats-interview) |
| A Dance of Fire and Ice | 1–2 轨 2D | 程序化（非视频） | 轨道/行星在背景之上 | 未找到官方设计文章；只有"因可读性问题删掉某段减速"的二手记录 | **无一手来源** |
| Rhythm Doctor | 1D 单轨（7 拍） | 程序化 + 故意遮挡 | 玩法层与视觉层**被刻意解耦** | ①「画面を見なくても遊べる設計は，目の見えない人への配慮であると同時に，音そのものに集中させるための極めて合理的な仕組み」；②「時にはウインドウそのものがデスクトップ上を飛び回り視界を遮る」；③ 1.0.4 在辅助功能里加「减少闪烁」并说明"有些特效与关卡设计理念深度绑定，仍会保留" | 【有据可查】[4Gamer](https://www.4gamer.net/games/950/G095079/20251217016/)、[补丁文本转载](https://news.17173.com/content/03102026/123605899.shtml) |
| Thumper | 单轨高速推进，伪 3D | 程序化 | 障碍在轨道上，轨道即画面主体 | GDC 2017 postmortem 主题是"不用高级 shader / 粒子 / 光照也做出这套视觉"；PS Blog 讲 VR 里把甲虫调到约 40cm 才"感觉对了" | 【有据可查（摘要级）】[GoNintendo 摘要](https://gonintendo.com/stories/284016-seven-years-in-alpha-the-thumper-postmortem)、[PS Blog](https://blog.playstation.com/2017/02/27/gdc-17-creating-thumpers-virtual-unreality/)；逐项手段**无一手来源**（原文 gamedeveloper 403） |
| BMS / beatmania IIDX | 固定 lane（1D 判定、2D 呈现） | **BGA = 图片/视频** | 规范明写**两种流派** | 见 §4 | 【有据可查】bmson 规范 |
| **Groove Coaster FP** | 1D 轨道判定 + 伪 3D 呈现 | **官方 MV 视频** | 轨道 + target 叠在 MV 之上（层级为推断） | 用"系列首次横置全屏"给 MV 留面 | 【有据可查】[FP 官方英文页](https://groovecoaster.com/fp/en/lp01/index.html) |
| itch.io / 粉丝 VJ 项目 | — | — | — | — | **无一手来源**（搜索只返回论坛帖与 jam 评论，不是项目说明） |

## 6. 通用无障碍规范（"音符压背景"的标准答案）

【有据可查】Game Accessibility Guidelines（2012 起、业界联盟成果）Vision/Basic 条原文：

> 「Ideally place your text and UI elements on a plain high contrast background, or where that is not possible, use prominent outlines and shadows to separate them from the background.」

并给出可测阈值 **4.5:1**——[GAG 高对比条](https://gameaccessibilityguidelines.com/provide-high-contrast-between-text-ui-and-background/)。这就是"描边/阴影/纯色底板"这一类手段的规范出处。

【有据可查】相邻的硬规范是**闪烁与重复图案**：GAG 列出具体阈值（>5 秒的闪烁序列；1 秒内 >3 次且覆盖 ≥25% 屏；移动重复图案 ≥25%；静态重复图案 ≥40%），并明确 **"epilepsy safe" 这种字样绝对不能使用**，应写 "screen flash effects / effects intensity"——[GAG 防闪烁条](https://gameaccessibilityguidelines.com/avoid-flickering-images-and-repetitive-patterns/)。落地案例：Rhythm Doctor 1.0.4 的「减少闪烁」（§5）。

### 6.5 osu!(standard) 的运行机制（一手来源已补齐）

完整取证与全部原文引用见 [research/osu-playfield-and-background-video-mechanics.md](../research/osu-playfield-and-background-video-mechanics.md)（wiki 引文取自 `ppy/osu-wiki` 仓库的 `wiki/<页面>/en.md` —— 该仓库就是线上 wiki 的权威源；源码为 `ppy/osu` / `ppy/osu-framework` 的 `master` 快照）。

**判定面**：固定 **512×384 game pixels** 的 2D 坐标域，原点左上，中心 (256,192)，整体比窗口中心低 8 game pixels。映射宽高比是 **`FillMode.Fit` 等比 + 居中、绝不拉伸**，缩放系数恒 1.6（512/640 的 "magic ratio"）。16:9 时多出来的**是横向坐标空间**——storyboard 域由 640×480 变宽到 854×480，判定面中心仍是屏幕中心。判定面不裁剪（`UpdateSubTreeMasking() => false`），边框存在但默认 None。

**打击物件**：命中区是**真圆**，判别式化简为「光标到圆心距离 ≤ 显示半径」，无额外容差。CS→半径 `r = (54.4 − 4.48·CS)·1.00041`（CS 0/5/10 → 54.4/32/9.6）。判定窗（表值是**半宽**）：GREAT `80−6·OD`、OK `140−8·OD`、MEH `200−10·OD`、MISS 固定 `400`。hit circle 在**按下瞬间**判定；slider 需按住跟随（跟随圈扩展态 = 半径 ×2.4）。notelock = 「两物件时间窗重叠」且「前者未判定」时后者输入被忽略、圆圈抖动。

**approach circle**：`Scale 4 → 1`、透明度 →0.9，在 `TimePreempt` 内收缩；AR 0/5/10 → preempt 1800/1200/450 ms。

**层序（自下而上）**：背景图 → **Video** → Background → (Fail｜Pass) → Foreground → **打击物件** → Overlay。即视频紧贴背景图之上，且**所有 storyboard 元素都在打击物件之下**（Overlay 除外）。

**视频规格（Ranking Criteria 硬规则）**：分辨率 **≤1280×720**、必须 **H.264 / `.mp4`**、必须**删掉音轨**、offset 一致；**没有给视频规定码率**（码率只约束音频）；整包上限 5 MB + 每分钟 10 MB、封顶 100 MB；**不要求 16:9**。官方给的编码配方：`-c:v libx264 -crf 20 -preset veryslow -vf scale=-1:720 -an -sn -map_metadata -1 -map_chapters -1`。

**可读性规则（这是本领域最直接的一手约束）**：RC 明文要求 combo 颜色「must not blend with the beatmap's background/storyboard/video in any case」，slider body 与 border 同样不得与背景/视频混同——**即"音符颜色必须与视频分得开"是被写进平台规则的**，而 dim 是默认 0.7。**两者不是二选一，是同时用。**

### 6.6 BMS 侧的默认：BGA 与音符本来就是两个区域

【有据可查】hitkey 的 BMS command memo（作者自述「This is not what translated specifications. This is only my memo.」）：常规 BGA 画布就是 **256×256**，「Specification has not defined the method of processing oversized images」；措辞上区分「the space for a musical score display」与「the space for an image display」，且 LR2 / IIDX 即使接受大于 256×256 的图，**图也不会溢出到音符区**；只有 spread canvas 实现（BM98、nanasi、fgt++、uBMplay 1.5.0+、IIDXv、HDX、Sonorous、BGAEncAdv…）会让大图铺到音符显示区上。
`#BMPxx` 无位置参数，`#BGAxx` 才带 `x1 y1 x2 y2 dx dy`（裁剪 + 落点）；另有 0B–0E 不透明度、`#ARGB`、`#SWBGA` 按键动画。bmson 里 BGA 事件只有 pulse + 图片 id，**无位置/尺寸字段**。

→ **对本项目最关键的一层含义**：用户这条 BGA 出自 **BMS 传统**，而 BMS 传统默认是"BGA 专用区域、不压音符"；但用户要的"完全融入视频、毫无违和感"恰恰是**另一派**（DJMAX / DDR / PIU 与 Groove Coaster FP 的全屏派）。**"毫无违和感"与"BGA 专用窗口"（黑边/边框）本质上不相容**——这是一个必须由用户拍板的方向选择。

### 6.7 净结论

公开资料里，"音符压在视频上还读得出"**没有单一规范**，而是三件事的组合：

- **(a) 结构性解法**：要么把背景与音符分到不同区域（IIDX / LR2 专用 BGA 窗口），要么干脆承认背景即玩法层（JSAB）。
- **(b) 显示/素材解法**：调暗、全屏让位、方窗黑边、中心安全区（GC FP 与 wcko87 实践属同一思路）。
- **(c) 通用无障碍解法**：高对比 + 描边/阴影（GAG 4.5:1）与限制闪烁（GAG 阈值 + Rhythm Doctor 的开关）。

并且**没有检索到以"视频背景音游"为专题的一手设计文献**——我没有找到，不替它编。

## 7. 未完成 / 无一手来源（诚实清单）

**原"未完成"的 osu! 一路已补齐**：`osu.ppy.sh` 正文确实被 Cloudflare 拦，但改从 `ppy/osu-wiki`（线上 wiki 的权威源）与 `ppy/osu` 源码取证成功，结论见 §6.5 与 [research/osu-playfield-and-background-video-mechanics.md](../research/osu-playfield-and-background-video-mechanics.md)。

该报告显式标注了**两处存疑**（没有为了叙事完整而缝合）：① approach circle 完全显形的时机，wiki 说 preempt 的 2/3，lazer 代码是固定 400 ms（AR≥5），两者不一致且未找到能判定权威行为的来源；② 16:9 时"背景铺满非判定面区域"属推断（wiki 只明确写了 storyboard 坐标域变宽到 854×480）。

另有两份同源的深度调研（上一轮会话入库）：[research/osu-storyboard.md](../research/osu-storyboard.md)（storyboard 系统）、[research/unity-videoplayer-extra.md](../research/unity-videoplayer-extra.md)（Unity VideoPlayer，按 Unity 6.6 文档取证）。原始抓取留在 `research/_raw/`。

**无一手来源**：`#BGA` 显示区域/裁剪的命令级定义（hitkey 抓不到）；IIDX 机台内是否有 BGA 显示设置（关闭、尺寸等）——什么都没查到；Groove Coaster 的判定窗口毫秒数；Steam 商店页原文；ADOFAI 的官方设计文章；Thumper 的逐项可读性手段。

## 8. 对本仓库的直接含义（实测事实，供决策用）

外部结论要落到这个仓库上，因此把最吃紧的实测事实一并记下（详细侦察见会话记录；行号已核）：

| 事实 | 位置 |
|---|---|
| `NoteData` **零坐标字段**（只有 `id/tick/pathId/action/protectedNote`） | `Assets/RhythmDemo/Runtime/ChartData.cs:145-152` |
| 现有 3 张 VSRG 谱面是 4 轨、`placement.y=0`、`stagePath` 退化为 +Z 直线 | `Assets/RhythmDemo/Resources/Charts/joker-reg5-4k.json:64-91` 等 |
| `.osu` **转换源本身**就是 mania 4K（`y` 恒 384，`x` 仅 64/192/320/448 四个值） | `Charts/Sources/only-my-railgun-jack-4k.osu`（1886 个打击物件实测） |
| Thart 触控是 16:9 内 0–100 百分比，但出口把 (x,y) 压成列下标 | `Assets/RhythmDemo/Thart/Core/ThartChartBuilder.cs:469`、`:563` |
| `.thr` 容器**已经**在存 2D 百分比（`SixColumn.thr` 内 `xPercent`/`yPercent` 各 726 次） | `Builds/ThartEditor/Charts/SixColumn.thr`（实测解包） |
| 判定谓词结构已是 2D 点按式，3D 只活在"投影出那个多边形" | `Assets/RhythmDemo/Runtime/JudgementEngine.cs:111-140` + `RhythmDemoController.cs:270-304` |
| 3D 专有：`SpatialDirector`（332 行）、`StageSpline`（131 行）、`StageVisuals`、雾/环境光/平行光 | `Assets/RhythmDemo/Runtime/` |
| 已经是 2D/无关：HUD（UGUI 屏幕空间）、`GameViewport` 的 16:9 框、视频 BGA 远平面通路、色板 | `DemoHud.cs:39-42`、`GameViewport.cs`、`RhythmDemoBga.cs`、`RhythmDemoPalette.cs` |

**与本仓库既有文档的冲突**（按 `Docs/agents/domain.md` 的约定显式上报，未自行覆盖）：`Docs/VideoBgaBranchingPlan.md` 的非目标（"不把 Note 改成 2D 屏幕空间"）、决定 12（"Note 保留 3D 世界空间位姿"）、结尾重申（"不把 Note 2D 化"）三条与用户新要求相反；决定 20 把 Note 可读性锚在运行时雾色上，而 `RhythmDemoBga.cs:209-212` 的断言正以雾色为基准——**转 2D 后该基准会变成视频画面本身**，烘焙规则要跟着改。`Docs/adr/0002-thart-is-the-chart-editor.md:24` 明确否决过"两套并存各管一条线"。
