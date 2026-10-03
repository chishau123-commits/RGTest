# 圆环音游 / Ring Game Prototype

Unity **2022.3.62f3c1**，Built-in Render Pipeline。工程根目录就是本仓库；用 Unity Hub 打开本目录。此分支向 `ReStart` 提交 PR，由非作者审核后合并。

Android 游戏本体原型已包含横屏触屏、缩圈/移动圆两种点击、三指演示段、临判定仍运动的玩法镜头、输入/视觉/显示延迟估计参数、暂停/恢复/重开及 JSONL 诊断。

[独立电脑制谱器](ChartEditor/README.md) 位于 `ChartEditor/`：独立 Windows 程序、波形/拍线、两类音符与路径、BPM 分段、相机动作、撤销/恢复和局域网平板实时触控录入。制谱器有自己的源码、依赖、测试和构建流程，不进入 Unity 的 Assets，也不依赖游戏程序集；两者通过现有谱面 JSON 互通。完整外部音频曲包与更多特效事件后续以新协议对接。

```text
Assets/RingGame/
├── Core/        协议加载、节拍换算、几何求值、判定
├── Runtime/     DSP、原始触点、历史画面姿态、呈现和诊断
├── Editor/      开发构建入口（不进入手机程序）
├── Resources/   自制 36 秒节拍音频和 38 个音符测试谱
├── Scenes/      Game.unity 入口
└── Tests/       EditMode 回归
```

首次运行依次执行：

```powershell
./tools/unity.ps1 -Task Prepare
./tools/unity.ps1 -Task Test
./tools/unity.ps1 -Task Android
./tools/android.ps1 -Task Install
./tools/android.ps1 -Task Launch
```

构建脚本与 ADB 参数见 [Android 构建调试](Docs/AndroidBuild.md)。[人工核验清单](Docs/ManualVerification.md) 给出操作、预期和证据字段；[原型协议](Docs/PrototypeProtocol.md) 明确支持边界；[本次验证记录](Docs/ImplementationEvidence.md) 区分执行结果与待人工验收。

只看编辑器画面：打开本目录后选择 **Ring Game > Open Gameplay Preview**，自动演奏内置样谱；可在 Game 视图暂停、恢复、重开。自动预览不计作实机触控验收。

构建产物位于 `Builds/`，不进入源码 Git 历史。规划 PDF 保留在 `output/pdf/`。`tools/generate_sample.py` 可从 Python 标准库重新生成原创测试音频和谱面，不使用参考视频的音乐/美术。
