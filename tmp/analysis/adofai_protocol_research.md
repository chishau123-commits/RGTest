# ADOFAI 谱面数据格式核验与本项目借鉴

研究日期：2026-10-03（Asia/Shanghai）。用途：修订圆环触控音游计划书的数据协议，不实现 ADOFAI 兼容层。

## 1. 证据边界

本次已经访问官方公告与官方机制文档、公开上传的 `.adofai` 文件、社区作者自己编写的解析器和事件生成器。**没有找到官方发布的完整、版本化 JSON Schema**，因此以下“文件格式字段”须称为“公开文件与社区工具交叉核验”，不能称为“ADOFAI 官方完整协议”。没有使用反编译游戏源代码或疑似泄漏代码。

来源分级：A＝官方一手说明；B＝公开文件样本，仅证明该样本的实际字段；C＝社区工具自己的源码，证明其读写模型，不保证完整模拟游戏行为。

## 2. 已核验结论

| 结论 | 证据与局限 |
|---|---|
| 编辑器保存为 `.adofai`，音频是另一个同目录文件 | A：[2019 官方编辑器公告](https://store.steampowered.com/news/posts/?appids=977950&enddate=1568400706&feed=steam_community_announcements)，其中 2019-05-03 段落说明文件分享与音频搭配；不由此推导所有现代资源打包细节。 |
| 常见文件结构由关卡设置、路径、事件及装饰组成 | C：[ADOFAI-JS 的 LevelOptions](https://github.com/adofaiex/ADOFAI-JS/blob/1f66bfa8c4146853d239c80c47ed2168d9208d02/src/structure/interfaces.ts)，声明 `settings`、`actions`、`decorations`、可选 `pathData`/`angleData`。这是社区接口，不是官方 schema。 |
| 路径可用字符序列，也可用角度数组表达 | C：[ADOFAI-JS 路径转换实现](https://github.com/adofaiex/ADOFAI-JS/blob/1f66bfa8c4146853d239c80c47ed2168d9208d02/src/pathdata/index.ts)；B：[公开上传的旧版样本](https://gitee.com/Suimg/adofai-helper/blob/bceefa5f199c5c47619e56489241500122a59323/level.adofai) 实际有字符路径。不要把路径方向数组当成固定间隔音符时间数组。 |
| 事件采用类型区分与位置锚点 | C：[事件接口](https://github.com/adofaiex/ADOFAI-JS/blob/1f66bfa8c4146853d239c80c47ed2168d9208d02/src/structure/interfaces.ts) 存在 `floor`/`eventType`；B：同一公开样本在多个不同 floor 上设置速度和镜头。floor 是砖块定位，不能无依据解释为固定 beat 序号。 |
| 运镜具有平移、旋转和缩放 | A：[2019-08-01 官方公告](https://store.steampowered.com/news/posts/?appids=977950&enddate=1568400706&feed=steam_community_announcements) 明确说明 Move Camera 三种能力。C：[旧版社区 MoveCamera 类](https://github.com/CrackThrough/ADOFAI-WebModule/blob/ec4018c7775d2f97b4d0783f342979330c9848e1/src/actions/MoveCamera.ts) 展示位置、旋转、缩放、时长、相对参照及缓动字段。 |
| 事件支持角偏移而非只在砖块命中时瞬发 | A：同一官方公告说明角偏移可使事件在命中 tile 之后触发。C：上述 MoveCamera 类实际含 `angleOffset`、`duration`、`ease` 和 `eventTag`。 |
| 调速事件可记录绝对 BPM 或倍率 | B：[公开样本](https://gitee.com/Suimg/adofai-helper/blob/bceefa5f199c5c47619e56489241500122a59323/level.adofai) 使用 SetSpeed；C：[旧版社区 SetSpeed 类](https://github.com/CrackThrough/ADOFAI-WebModule/blob/ec4018c7775d2f97b4d0783f342979330c9848e1/src/actions/SetSpeed.ts) 含 `speedType`、`beatsPerMinute`、`bpmMultiplier`。不能称当前所有版本字段均一致。 |
| 装饰对象与影响装饰的事件可分开建模 | C：[CLiF 的事件生成器](https://github.com/CLiF-1593/ADOFAI_DynamicDecoration/blob/12b8f846f46cdd3d5eed67a12ca2c6f691e4ba4b/ADOFAI_DynamicDecoration/EventJson.cpp) 分别生成 AddDecoration 与 MoveDecorations，后者含目标 tag 和事件 eventTag；前者含图像、初始位置与外观等属性。 |
| 对象标签与事件标签用途不同；重复事件可引用事件标签 | C：同一 CLiF 生成器把装饰目标参数写入 MoveDecorations.tag，把事件参数写入 MoveDecorations.eventTag；RepeatEvents 的目标参数来自 event_tag，并写到其 tag 字段，同时输出重复次数和间隔。这里是作者代码的直接参数流证据；完整跨 floor、递归及冲突执行语义未证实。 |
| 版本迁移和特殊砖块会影响时间含义 | A：[官方 v2.9.7 更新](https://7thbeat.notion.site/ADOFAI-Changelog-v2-9-7-24e90365a16180b7ad6decc4168954b4) 提及 360° 砖块 beat 数修正、FreeRoam duration 一致性以及新旧版本迁移可能失配。协议借鉴必须保留版本、单位和迁移策略。 |

公开样本的仓库上传者为 Suimg，文件里的作者字段为 oxmengruhu；没有独立确认二者关系或该文件导出流程。它用于观察字段，不作为官方分发谱面、不作为已获授权的音乐或美术内容。本地仅保存文本以便核验，未下载音频或图片。

## 3. 时间定位：可以说到哪里

官方 [Timing Window Calculations](https://7thbeat.notion.site/Timing-Window-Calculations-a80645e4f14f487b9696a244e1727c57) 的基础换算是：常规基础关系下，180° 对应 1 beat；角度量换为时间量为 `angle / BPM × 1000 / 3` 毫秒。该页面是判定窗口说明，**不是完整事件调度协议**。

结合官方 angle offset 说明，可给出受限推导：在固定 BPM、常规两球、无暂停及其他特殊事件的片段，若某 tile 基准时间为 T，那么 90° 的偏移相当于半拍；在 120 BPM 下半拍是 250 ms。这个例子属于基础关系推导，不是对所有 ADOFAI 事件的官方等式承诺。

不能把 `floor × 60/BPM` 作为通用触发时间。路径角度、变速、暂停、多球及特殊砖块会改变砖块时间；要实现完整导入必须另建版本对应的路径节奏计算器并实测，而本项目本期不做导入。

`duration` 在已访问源码中是数值，但源码类型声明未注明时间单位。社区文档将多类持续特效解释为 beat，本次没有获得覆盖全部事件、版本与变速边界的官方单位规范。因此**不得把 ADOFAI 的所有 duration 一概写为秒，也不得声称所有事件均已官方确认以拍计时**。本项目采用自行明确的 `durationTicks`，解决单位歧义，无需依赖未核实语义。

## 4. 可借鉴与必须重设的内容

以下为本项目的设计建议，不是 ADOFAI 已存在字段或对其兼容性的承诺。

| ADOFAI 可观察思路 | 本项目作者数据设计 |
|---|---|
| settings 与其他内容分区 | `settings` 只保存关卡元数据和默认外观；已有 `timebase` 明确 PPQ、起点偏移与分段 BPM。 |
| 路径方向暗含轨道节奏 | 触控圆环音游保留独立 `notes` 和 `paths`；音符必须写明确的命中 tick，路径只描述移动圆的几何与插值，不由路径计算音乐时间。 |
| floor＋angleOffset | 改为绝对整数 `tick`；需要锚定音符时，编辑器将 noteId＋offsetTicks 解算成 tick。不要同时让作者维护两份时刻。 |
| eventType 分类事件 | 作者数据使用 `actions`，动作由 `eventType` 分派；本期白名单 MoveCamera、MoveDecoration、ParticleBurst、LightPulse。`MoveDecoration` 是本项目命名，ADOFAI 的现代生成器字段为复数 `MoveDecorations`。 |
| AddDecoration 对象与 MoveDecorations 控制分离 | `decorations` 保存稳定 id、assetId、初始 transform、layer 与 tags；动作使用 targetId，标签批选在编译时展开成确定目标 id。对象初态只保留一份。 |
| ease／eventTag | `ease` 使用有限枚举；事件可带 id 供编辑器引用，事件标签只服务批处理。不要把对象标签和事件标签共用成一个含混字段。 |
| RepeatEvents | 本期把“按 N 拍重复”设计成编辑器预设，导出前展开普通动作，不加入运行时递归或可执行脚本。 |
| 事件播放器的参数动画 | 编译器将 actions 转为内部轨道/事件表。`tracks` 不再要求作者手写，避免同属性初态、目标态与动画轨道多处重复。 |
| 版本与旧文件迁移 | 顶层 `schemaVersion` 必填；编译版本与内容 hash 写入产物。未知游戏动作拒绝加载，编辑器可以保留未知视觉扩展并告知降级。 |

本项目具体时间规则建议：1 beat＝PPQ tick，quarter-note beat；tick 0 对应音频起点加明确的 `audioOffsetMs`。正偏移的含义写为“tick 0 在音频开始后多少毫秒”。`durationTicks` 为非负整数，0 是瞬时赋值。跨 BPM 变化的持续时间通过 beat→seconds 分段积分，禁止按动作起点 BPM 一次乘算。固定时长视觉效果若后续需要毫秒，应另设命名不同的 durationMs 并互斥，首版不加入双单位复杂度。

冲突规则也应自行定义：同目标同属性的动作按 `(tick, order, id)` 排序；后一动作在自己起点取上一状态作为 from 并接管该属性。随机特效使用声明式参数与固定种子。预览跳转从初态/检查点纯求值到目标 tick，不通过反向播放历史补回状态。

## 5. 尚未核验与明确不承诺

- 未找到当前完整官方 schema；未验证所有事件的单位、默认值及字段版本。
- 未核验所有特殊轨道、暂停、三球/多球、FreeRoam 的 floor→时间规则。
- 未核验 RepeatEvents 的跨砖块、递归、同标签重复顺序及最新变体；本期编辑器展开可避免依赖。
- 没有宣称 `.adofai` 导入、导出、数据兼容或玩法兼容。协议设计借鉴结构和工作流，不复制轨道玩法。
- ADOFAI-JS 新版类型中 MoveCamera 用 easing、SetSpeed 用 speed，和旧版样本/旧工具字段不同；不能把任一社区类型声明当作全部真实导出字段。生产代码应以我们自己的 schema 为唯一规范。

## 6. 稳定来源索引

1. A：[官方 Wiki 入口](https://7thbeat.notion.site/ADOFAI-Wiki-95a4e44fcc0340f6ae55160d97eddee7)，可追到机制说明。
2. A：[官方判定时间换算](https://7thbeat.notion.site/Timing-Window-Calculations-a80645e4f14f487b9696a244e1727c57)。
3. A：[官方 2019 编辑器公告集合](https://store.steampowered.com/news/posts/?appids=977950&enddate=1568400706&feed=steam_community_announcements)。
4. A：[官方 v2.9.7 迁移/时长公告](https://7thbeat.notion.site/ADOFAI-Changelog-v2-9-7-24e90365a16180b7ad6decc4168954b4)。
5. B：[公开文件样本](https://gitee.com/Suimg/adofai-helper/blob/bceefa5f199c5c47619e56489241500122a59323/level.adofai)，master 当次 SHA 为 `bceefa5f199c5c47619e56489241500122a59323`；下载的 raw 内容在 `tmp/analysis/gitee_level_sample.adofai`，含 pathData，旧 settings.version＝2，未发现顶层 decorations。
6. C：[ADOFAI-JS 仓库固定版本](https://github.com/adofaiex/ADOFAI-JS/tree/1f66bfa8c4146853d239c80c47ed2168d9208d02)，当次 SHA 为 `1f66bfa8c4146853d239c80c47ed2168d9208d02`。
7. C：[ADOFAI-WebModule 固定版本](https://github.com/CrackThrough/ADOFAI-WebModule/tree/ec4018c7775d2f97b4d0783f342979330c9848e1)，SHA 为 `ec4018c7775d2f97b4d0783f342979330c9848e1`，2021 已归档，适合作旧字段交叉核验，不证明最新兼容。
8. C：[CLiF 事件生成器固定版本](https://github.com/CLiF-1593/ADOFAI_DynamicDecoration/blob/12b8f846f46cdd3d5eed67a12ca2c6f691e4ba4b/ADOFAI_DynamicDecoration/EventJson.cpp)，SHA 为 `12b8f846f46cdd3d5eed67a12ca2c6f691e4ba4b`，代码自己构造 JSON 事件；此研究未移植代码。

