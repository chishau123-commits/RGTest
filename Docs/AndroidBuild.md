# Android 构建与调试

本项目根目录就是 Unity 工程，使用 **Unity 2022.3.62f3c1、Built-in Render Pipeline**。本次先交付游玩运行时；Windows smoke player 是供桌面检查玩法画面的同一运行时，独立电脑制谱器仍是后续需求。

## 环境与产物

通过 Unity Hub 为指定 Editor 安装 Android Build Support、Android SDK & NDK Tools、OpenJDK。Editor 需要已经激活的有效许可证。推荐使用这一 Editor 自带的 SDK、NDK、JDK，避免机器 PATH 上的其他版本影响复现；所选 SDK 中必须安装 `platforms/android-35`。脚本默认 Editor 路径是 `C:\Program Files\Unity\Hub\Editor\2022.3.62f3c1\Editor\Unity.exe`，可用 `-UnityPath` 覆盖。

Android 构建参数如下；这是供 USB 实机调试的 APK，不表示已经完成商店发行要求。

| 项目 | 设置 |
| --- | --- |
| 包名 | `com.chishau.ringgame.prototype` |
| 架构 / 后端 | ARM64 / IL2CPP |
| 最低 Android | API 26（Android 8.0），且设备支持 ARM64 |
| Target API | 35，可用 `-AndroidApi` 显式选择其他已安装且 >= 26 的 API |
| 图形 API | OpenGLES3 |
| 方向 | Landscape Left；运行时横屏布局 |
| 调试 | Development Build + Script Debugging |
| 签名 | Unity 开发调试签名；不使用发布密钥 |
| 场景 | `Assets/RingGame/Scenes/Game.unity`；运行时自动创建玩法对象 |

Unity 2022.3 的 ARM64 目标要求 IL2CPP。不要将后端切成 Mono 后继续声称 APK 支持 ARM64。[Android Player settings](https://docs.unity3d.com/2022.3/Documentation/Manual/class-PlayerSettingsAndroid.html)

## 从源代码复现

先关闭使用该工程的 Unity Editor，再从仓库根目录运行以下 PowerShell 命令。首次打开需要恢复 Packages 并导入 Assets，耗时明显长于增量运行。不要同时让两个 Unity 进程打开同一工程。

```powershell
python .\tools\check_project.py
.\tools\unity.ps1 -Task Test
.\tools\unity.ps1 -Task Android
```

首次静态检查如果报告 bootstrap 场景或 `.meta` 缺失，应先执行 `.\tools\unity.ps1 -Task Prepare` 再检查；完整 PR 应已包含场景和资源 `.meta`。`Prepare` 仅在场景不存在时创建空场景，不覆盖已有场景内容。

`Test` 通过 `RingGame.Editor.TestCommands.RunEditMode` 同步执行当前的普通 NUnit EditMode 测试，写出完整 NUnit XML 后才退出 Editor。该入口会拒绝多帧的 `[UnityTest]`、`[UnitySetUp]`、`[UnityTearDown]`，避免这些测试被同步过滤后误报全通过；以后增加此类测试需另建异步运行路径。零测试、失败、跳过或无结果都报失败。测试默认等待 180 秒，其他任务 900 秒，可用 `-TimeoutSeconds` 覆盖；超时只终止本次启动的 Editor，并报错，即便 XML 已显示通过也不能算脚本完整通过。

构建成功后输出：

- `Builds/Android/RingGame-debug.apk`。
- `Builds/Android/build-summary.json`：Unity 版本、平台、结果、错误/警告数、大小、耗时与输出路径。
- `Builds/Validation/android-editor.log`：完整 Editor 构建日志。
- `Builds/Validation/editmode-results.xml`：本次 EditMode 测试结果；旧结果移为 `.previous`，零测试或缺少结果都判失败。

这些生成目录被 Git 忽略，审核人按需要保留到自己的证据目录或 PR 附件中。脚本打印 APK 的 SHA-256，可将 hash 与安装记录关联。非默认安装路径和自定义输出示例：

```powershell
.\tools\unity.ps1 -Task Android -UnityPath 'E:\Unity\2022.3.62f3c1\Editor\Unity.exe' -OutputPath 'Builds\Android\review.apk' -AndroidApi 35
.\tools\unity.ps1 -Task WindowsSmoke
```

Windows smoke 产物在 `Builds/WindowsSmoke/RingGame.exe`，采用 Windows x64 / Mono。桌面启动、鼠标操作与截图只能补充运行时检查，不能代替 Android 真机触控、多指和音频延迟核验。

Editor 菜单也提供 `Ring Game > Prepare Project`、`Build Android Debug APK`、`Build Windows Gameplay Smoke Player`。批处理的静态入口为 `RingGame.Editor.BuildCommands.BuildAndroid`，自定义参数为 `-ringBuildPath`、`-ringAndroidApi`。[Unity command-line arguments](https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html)

## USB 安装、启动与取证

手机开启开发者选项和 USB 调试，用数据线连接电脑，并在手机上确认授权。脚本不会自动改手机调试设置。仅在 ADB 显示 `device` 的授权设备上继续：

```powershell
.\tools\android.ps1 -Task Devices
.\tools\android.ps1 -Task Install
.\tools\android.ps1 -Task Launch
.\tools\android.ps1 -Task DeviceInfo
.\tools\android.ps1 -Task Screenshot
.\tools\android.ps1 -Task CaptureLogs
.\tools\android.ps1 -Task PullDiagnostics
```

多个设备时必须指定 `-Serial '<adb devices 显示的序列号>'`；零设备、`unauthorized` 或 `offline` 会明确报错。可用 `-AdbPath` 指向另一套 Android platform-tools，用 `-ApkPath` 安装其他本项目 APK。

`Install` 使用 `adb install -r -t`，保留已有应用数据。签名冲突、ABI 不支持、Android 版本过低或手机拒绝安装时应记录具体错误，先查原因；脚本不会自动卸载或清空应用来掩盖失败。`Launch` 使用 Unity 2022.3 默认 `UnityPlayerActivity` 并请求等待启动结果。

证据默认保存在 `Builds/AndroidEvidence/`：`device-info.json`、`screen.png`、`logcat.txt`，以及拉回的 `diagnostics/`。Logcat 是当前缓冲区的有界快照，未清空历史日志；记录操作时间和安装 APK hash，避免把其他运行的日志当成本次结果。诊断目录尚未产生时 `PullDiagnostics` 会报错，此时应先实际开始、操作并结束或重启一次样段。

可通过 Android Studio / Unity Profiler 附加到该 Development APK，检查脚本异常、帧耗时和内存。USB 断开、应用切后台与重返前台也需要人工核验，不能以“安装成功”替代游玩通过。[Unity Android debugging](https://docs.unity3d.com/2022.3/Documentation/Manual/android-debugging-on-an-android-device.html)

## 开发用谱面覆盖

运行时优先读取 `Application.persistentDataPath/prototype-chart.json`，文件不存在时使用内置 `Resources/Charts/prototype`。Android 默认路径为 `/sdcard/Android/data/com.chishau.ringgame.prototype/files/prototype-chart.json`。JSON 必须符合当前运行时支持的协议子集；格式错误应被拒绝并产生可见错误，不能悄悄继续使用错误数据。

```powershell
.\tools\android.ps1 -Task PushChart -ChartPath '.\Assets\RingGame\Resources\Charts\prototype.json'
```

推送后重启样段或应用加载。该命令只覆盖本应用这一个明确命名的开发谱面文件。恢复内置谱面需要人工移走/重命名该覆盖文件；脚本不自动清空手机数据。这里是开发用 JSON 传输，完整谱面包、用户导入界面和制谱器仍须按独立需求实现。[Unity persistentDataPath](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/Application-persistentDataPath.html)

## PR 与人工审核证据

需求在 GitHub 按一条可独立验收的行为建 issue，自由认领并在自己的功能分支修改，PR 目标为 `ReStart`。提交源码、配置、资源与 `.meta`；不提交 `Library`、`Temp`、本机许可证、调试日志、APK 或个人设备序列号。

PR 应写明关联需求、结果、人工步骤、实际运行的命令和结果，以及未验证项目。作者提交构建和日志证据后，由非作者复核代码和需求，并在测试设备上完成手动核验；作者不能代替审核人宣称“人工验收通过”。需求模板不固定人员分组。

GitHub `Source contract` 工作流只使用 Python 标准库检查版本锁定、资源 JSON 的语法和基础谱面字段、`.meta` GUID、源码目录边界和差异格式；它不持有 Unity license，也不编译 C# 或构建 APK。绿色 CI 只表示这些静态检查通过。Unity Test Runner、实际 APK 构建、安装启动、两种音符、多指、强运镜和音频触控延迟必须分别提供证据。实际结果写到本 PR 的验证记录中，不预填“通过”。
