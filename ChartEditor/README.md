# Ring Chart Editor / 独立制谱器

这是独立的 **Windows x64 桌面程序**，源码、依赖、测试、构建全部位于 `ChartEditor/`。安装或运行制谱器不需要 Unity、Node.js 或游戏 APK。开发电脑构建时使用 Node.js 24 与 npm。

与游戏的边界是导出的谱面 JSON。制谱器不引用 `Assets/RingGame` 的程序集或源码，游戏不会编译、打包或运行这里的代码。`assets/` 的演示音乐与谱面是既有原创样例的副本，用于独立安装后离线体验。

## 运行与开发

双击 `Builds/ChartEditor/RingChartEditor-0.1.1-win-x64.exe`。便携程序首次启动会把运行时解压到临时目录；工程保存位置由作者选择，恢复资料写到 `%APPDATA%/ring-chart-editor/`。启动自动打开 36 秒原创演示工程。

从源码开发：

```powershell
cd ChartEditor
npm ci --ignore-scripts
# 本机开发启动需要下载 Electron；构建命令本身会下载构建运行时。
node node_modules/electron/install.js
npm start
```

验证与独立构建：

```powershell
npm run check
npm test
npm audit --omit=dev --audit-level=high
npm run build
./tools/native-smoke.ps1
```

`npm run build` 输出 Windows 便携 EXE 与 `win-unpacked/`；整套 `win-unpacked/` 文件夹也可以分发。产物位于仓库 `Builds/ChartEditor/`，不提交到 Git。GitHub 的 `Independent chart editor` 工作流会运行测试、构建 Windows 版并上传可下载构建。

## 编辑流程

1. 打开演示，或新建工程后导入 WAV / MP3 / OGG / M4A。实际格式能否解码由随程序打包的 Chromium 音频支持决定；失败会显示原因。
2. 在右侧设置标题、作者、拍零偏移和 BPM 分段。BPM 修改保持 tick，重新计算音频秒数；可撤销。
3. 点击波形或拍线定位，选择左侧缩圈/到位工具，在舞台点击放置。当前时间为零时放置到第一拍，避免零预读音符。
4. 用选择工具点中目标，拖动接收圆；到位路径终点随目标移动，黄色起点可拖动。右侧可精确修改目标、半径、判定 tick、出现 tick。
5. 时间轴支持拖动音符或运镜的起始时刻，保持音符预读长度；松开只产生一次撤销。属性栏也可输入精确 tick。
6. 在当前拍添加运镜，编辑绝对平移、旋转、正的等比缩放及 linear/smooth 缓动。添加、改起点/时长和时间轴拖动会检查冲突，拒绝重叠或同起点并显示已有动作 ID 与范围；被拒绝的编辑不进入历史。动作可以在上一动作结束 tick 起接续。支持任意定位求值，运镜不会改变音符判定 tick。
7. Space 播放/暂停；开启节拍器、清晰模式或 A/B 循环。布局模式显示当前时间附近和选中目标，播放显示真正的预读与到位过程。
8. 校验列表定位错误对象。修复错误后导出 `prototype-chart.json`，另存 `.ringproject` 工程保留音频、Take、作者和循环范围。

拍线细分支持 1、1/2、1/4、1/8、1/12、1/16、1/24 拍及自由整数 tick。Ctrl+S 保存、Ctrl+Z 撤销、Ctrl+Shift+Z/Ctrl+Y 重做，Delete 删除；V/1/2 切换工具，C 添加运镜。复制会生成新的稳定 ID、独立路径，并移动到下一拍。

## 平板实时触控录入

1. 电脑和平板处于同一可互通的局域网，点击“开启平板连接”。扫码或在平板浏览器输入完整地址；地址的 `#` 后是本轮配对令牌，必须保留。
2. 如有多个网卡，选择平板可访问的地址。二维码对应第一次显示的地址，切换网卡后使用下方完整文本地址。
3. 平板横屏，可点“全屏”。保持采集页面前台，等待电脑显示“已同步”。音乐由电脑播放；平板只显示谱面、触控反馈和同步质量。
4. 电脑定位到补录起点，关闭 A/B 循环，点击“开始录制”。两秒后音频开始，平板在画布中采集所有 pointerdown，支持设备和浏览器提供的多点数量，不锁死为两指或四指。
5. 持指、滑动和抬指不生成音符。留边不录入。强运镜时用平板保留的历史画面姿态反投影，不使用电脑收到样本时的最新姿态。
6. 停止后进入 Take 列表。原始样本已落盘，保留时间、像素/归一化位置、映射位置、姿态、时钟模型和质量。按当前网格、导入校准、缩圈/到位类型显式导入，整批操作可撤销。
7. Take 可单独导出为 `.take.json`。断线会结束本轮，重连后开启新 Take；可从恢复草稿找回已保存样本。

浏览器输入时间和屏幕显示时间存在估计误差。平板默认显示延迟估计 20 ms；它只用于选择历史画面。Take 导入校准改变目标音乐时间，不改原始数据。RTT 和不确定度不是物理输入精度证明；正式录入需实测校准。

局域网服务仅在作者开启连接后监听，关闭后令牌失效；HTTP/WebSocket 首版适用于可信本地网络。若 Windows 防火墙询问是否允许连接，由用户决定授权。工具不会修改系统防火墙、联网设置或绑定公网账户。

Android 的可选开发 USB 路线：

```powershell
# PORT 替换为界面显示的端口；需要已授权的 ADB 设备。
adb reverse tcp:PORT tcp:PORT
# 平板浏览器使用 http://127.0.0.1:PORT/#原配对令牌
adb reverse --remove tcp:PORT
```

这是 Android 的开发转发，不是 iPad USB 支持。iPad 首版采用局域网 Safari；真实 iPad 触控、Safari 全屏行为尚待设备验证。

## 保存与协议范围

`.ringproject` 使用 `ring-editor-1`，JSON 内嵌音乐数据与原始 Take，与给游戏的 JSON 分开。每 15 秒保存有修改的恢复草稿，失去焦点与正常关闭时也保存。原始触控逐批追加到 journal 并 fsync 后才发 ACK；停止时合成完整 Take，异常中断可恢复完整日志前缀。

工程允许保存和重新打开存在可修复时序/几何错误的草稿，错误会显示在校验列表，预览在修复前暂停；给游戏的 JSON 导入与导出保持严格校验。

如果旧草稿显示“谱面存在错误”，舞台会提示第一个错误的对象 ID，下方自动切到校验列表。对运镜重叠，点击“顺延冲突运镜”，先查看每个起点的调整，再确认。修复按起点顺序将冲突动作移到最早空闲 tick，保留全部动作、时长、位置、旋转、缩放、音符、音频和原始 Take；不自动删除重复动作。整批修改可 Ctrl+Z 撤销。顺延改变演出时间，需预览复核；若想删除多余动作，可点错误项选中后按 Delete。非法数字或非唯一 ID 不自动猜测修复。

游戏导出为现有 `prototype-ring-0`：PPQ 960、两种点按、直线路径、MoveCamera、空 decorations。大小、数值、ID、引用、相机重叠、音频尾界与触点声明会校验。整数限于 JavaScript 安全整数，几何值必须能表示为 Unity float32。

当前 Android 原型只读开发覆写 `prototype-chart.json` 并使用预置节拍音频。制谱器可以导入外部音乐做预览，但外部音频曲包、封面和游戏选曲/资源导入不属于本次接口，不能把工程内音乐当成已被手机加载。测试 JSON 对接时请使用随制谱器附带的原创演示音频。

粒子/装饰/Shader 的独立演出轨道、完整曲包协议和手机游戏侧的外部音频导入需在后续协议版本协同实现；此版不会输出当前游戏无法处理的事件。

## 代码入口

| 文件/目录 | 职责 |
|---|---|
| `main.cjs` | 独立桌面窗口、受限 IPC、原生文件选择、原子保存、平板服务生命周期 |
| `preload.cjs` | 在隔离上下文中暴露有限桌面 API，不给页面 Node 或任意路径读写 |
| `src/core/chart.mjs` | 制谱器自己的协议验证、BPM 换算、相机求值、投影、稳定 ID 与导出 |
| `src/core/authoring.mjs` | 运镜编辑时序保护、原子拒绝冲突、保留动作的修复计划 |
| `src/core/project.mjs` | 作者工程、命令历史、撤销/重做、Take 量化导入 |
| `src/core/storage.mjs` | 临时文件、fsync 与同目录原子替换 |
| `src/ui/editor.mjs` | 属性/时间轴/工具操作、录制流程、保存恢复、错误定位 |
| `src/ui/transport.mjs` | Web Audio 解码、预约播放、定位、输出时间估计 |
| `src/ui/render.mjs` | 预告圆、到位圆、路径、相似变换、波形绘制 |
| `src/recording/host.mjs` | 明确资源白名单、令牌配对、真实 WebSocket、ACK 与设备状态 |
| `src/recording/session.mjs` | 四时间戳同步、漂移模型、代次、历史 Take 回填、坐标映射 |
| `src/recording/take-store.mjs` | 原始样本增量落盘、停止合成、崩溃恢复 |
| `src/tablet/` | 平板网页、多指 Began、历史姿态、重传与断线提示 |
| `tests/` | 协议、撤销、时钟、实际网络、保存与恢复回归 |
| `tools/` | 边界检查、原生启动检查、只供开发的本机浏览器预览 |

人工验收见 [制谱器验收单](../Docs/ChartEditorManualVerification.md)，消息协议见 [触控录入协议](../Docs/TouchRecordingProtocol.md)，实际验证结果见 [交付证据](../Docs/ChartEditorEvidence.md)。
