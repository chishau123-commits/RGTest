# 独立制谱器验证与交付证据

日期：2026-10-03。此记录区分已执行的自动验证与仍需人工检查的项目。

## 交付边界

- `ChartEditor/` 是独立源码项目，独立锁定 npm 依赖、独立构建和测试；没有修改本次基线 `0bae8dab9fc59aed8561b6d90276ac983122c2f8` 的 Unity 游戏 Assets、Packages、ProjectSettings 或 Android 构建工具。
- 输出 Windows x64 便携 EXE 及完整 `win-unpacked/` 目录，内含桌面运行时；最终使用不要求 Unity/Node 环境。
- 对接现有 `prototype-ring-0` JSON，提供两类音符、直线路径、BPM、相机、编辑命令和原始录入 Take。
- 本分支建立在未合并的游戏 PR #15 之上，本次提交仅增加独立制谱器及其文档/CI。人工评审应先审核 #15，再审查本次独立提交；不由代理合并。

## 已执行

| 验证 | 实际结果与限制 |
|---|---|
| `npm run check` | JavaScript 语法、编辑器封装边界通过；构建文件列表没有 Assets/RingGame 或上级游戏路径 |
| `npm test` | 24/24 通过：BPM 跨段/逆换算、相机求值/逆投影、38 音符样例往返、ID/路径、非法协议、整批撤销、Take 量化 |
| 同步/录入测试 | 四时间戳同步、100 ppm 合成漂移、采集时模型保留、旧代回填、ACK 连续性、去重、超时、17 触点合成批次通过；不是 17 指实机证明 |
| 真正网络集成 | 本机实际 HTTP/WebSocket 服务、配对、ClockPing/Pong、触点持久化后 ACK、断线结束通过；不是无线链路或浏览器真实落指测试 |
| 保存/恢复 | Windows 中文路径原子覆盖、增量 journal、完整 Take 合成、崩溃截断末行恢复通过 |
| npm production audit | 0 个已报告生产依赖漏洞；不构成整体安全认证 |
| Windows 构建 | electron-builder 打包便携 EXE 成功，构建运行时独立于 Unity |
| 原生启动 | 真实桌面 EXE 的隔离 preload、作者界面初始化、36 秒/48 kHz 音频解码、38 音符载入、16:9 舞台初始化、协议零错误通过 |
| 便携版启动 | 实际便携 EXE 自解压启动，通过同样的原生初始化检查 |
| 打包平板资源 | 原生构建从 ASAR 启动 HTTP，平板 HTML/CSS/脚本与预览模块均返回 200 |
| 源码对应性 | 逐一比较当前 main/preload/src/assets 与最终 app.asar，全部一致；EXE 和 ASAR SHA-256 记录在机器证据里 |
| Unity 原项目静态契约 | 原有 `tools/check_project.py` 通过；此操作不是新增 C# 编译、APK 构建或手机验收 |

可复核机器记录：[构建清单](Evidence/chart-editor-build.json)。JUnit、原生与便携启动报告保存在本地 `Builds/ChartEditorEvidence/`，GitHub Actions 会提供独立 Windows 构建下载。

原生/浏览器窗口自动核验工具在本会话出现环境错误，因此没有把菜单点击、拖动、文件对话框或实际屏幕截图标为人工通过。原生启动报告来自程序自己的初始化诊断；它证明打包资源与应用初始化路径可运行，不证明所有交互流程都完成了视觉验收。

## 待人工与实机核验

按 [人工清单](ChartEditorManualVerification.md) 检查 Windows 文件选择、完整制谱/播放/撤销/导出流程、无 Unity 的另一台电脑、Android 浏览器实际多指落下、无线拥塞、持续录入误差分布、真实强运镜显示对齐、断线/后台，以及 iPad Safari。

当前没有提供真实平板录入精度数字，也没有把 RTT、CPU 帧姿态或合成 17 触点测试当成硬件触控验收。默认显示延迟 20 ms 和 review 的 10 ms 同步不确定度阈值均是待校准的工程值。

## 当前协议后续项

外部音乐/封面的完整曲包与 Android 游戏资源导入、独立粒子/装饰/Shader 演出轨道、iPad USB 传输还需后续版本。当前 JSON 导出明确保持游戏可处理的动作白名单，不写入这些未支持字段。
