# EdgeTune Windows

Windows 音乐侧边浮窗，基于 [EdgeTune macOS](https://github.com/shitao462/EdgeTune) 的交互与功能独立实现。C# / .NET 10 / WPF，GPL-3.0。

## 下载与运行

1. 打开本仓库的 **Actions → Build Windows**，选择最近一次绿色成功的运行。
2. 在 **Artifacts** 下载 `EdgeTune-Windows-x64`（下载需登录 GitHub）。解压下载文件，再解压里面的软件 ZIP。
3. 将整个文件夹放到固定位置，例如 `C:\Apps\EdgeTune`，运行 **EdgeTune.exe**。无需安装 .NET 或开发环境。
4. 软件驻留任务栏通知区域。默认快捷键 **Ctrl+Alt+M** 呼出 / 收起，托盘菜单可以打开设置或退出。

支持 Windows 10 2004（19041）及更新版本、Windows 11，首版提供 x64 便携包。自动构建的包未做商业代码签名，Windows 可能提示未知发布者，请核对本仓库来源。不要只移动 EXE，需保留整个软件文件夹；启用开机启动后也应保持该位置固定。

## 功能

- 316 × 110 浮窗，鼠标离开一秒后平滑收起，边缘保留 7 × 72 圆角主题条；停留 0.2 秒展开。
- 不抢焦点的置顶窗口、全局快捷键、托盘入口、图钉常驻；拖动歌名区域调整垂直位置，快捷键在鼠标所在显示器呼出。
- 上一首 / 播放暂停 / 下一首，网易云音乐、QQ 音乐、Spotify 和 Apple Music 选择。
- 网易云与 QQ 优先使用各自曲库的原文和已有中文翻译；原文与翻译各一行，没有翻译时原文最多两行，保持与设置按钮的间距。
- 校验歌名、歌手、专辑及时长，匹配歧义或接口失败回退 LRCLIB；明确纯音乐显示“纯音乐，请欣赏”。
- 5 个纯色、3 个渐变，边缘条同色；黑色、白色、玻璃主题。
- 所选播放器 Windows 音频会话的实时峰值驱动边缘条，仅上半部分伸缩、圆角，暂停恢复静态；可关闭。
- 当前用户登录后自动启动，设置保存于 `%LOCALAPPDATA%\EdgeTune`；无需管理员权限。
- 可自定义全局呼出快捷键，并提示快捷键冲突；设置页显示媒体和音频连接状态，提供日志入口。

## LOL 验证

主要目标为 **LOL 无边框窗口模式**。进入游戏后按 Ctrl+Alt+M，查看浮窗是否出现；点击切歌后游戏应保持前台，鼠标移开后收起。可在设置换一个游戏未占用的快捷键。

本程序使用普通置顶窗口与 Windows 全局快捷键，不向游戏注入代码。独占全屏可能遮住普通桌面浮窗，不保证可覆盖。LOL / Vanguard 的实际兼容性、不同缩放和多显示器效果必须在 Windows 实机测试；源码完成、构建成功不代表这些验证已经通过。

## 媒体与音频说明

依赖播放器向 Windows 系统媒体控制（SMTC）发布曲目信息。如播放器版本未提供该会话，窗口会显示等待信息，此时切歌和曲目歌词不可用。可检查 Windows 音量媒体面板是否显示该客户端，更新播放器或启用其系统媒体控制选项。

音频动画使用 NAudio 读取所选进程的 Windows 音频会话峰值，不录音、不截图、不保存或上传声音；不会跟随 LOL 的声音。每三秒重新枚举音频设备和会话以处理耳机切换、播放器重启。玻璃背景使用系统模糊能力，系统或透明窗口组合不支持时回退半透明外观。歌词翻译依赖曲库提供，不自动机器翻译。

## 构建

Windows 上安装 .NET 10 SDK 后：

```powershell
dotnet run --project tests/EdgeTune.Tests.csproj -c Release
dotnet publish src/EdgeTune.Windows/EdgeTune.Windows.csproj -c Release -r win-x64 --self-contained true -o artifacts/EdgeTune-Windows-x64
```

Mac 可进行源码和跨平台歌词测试，并用 `EnableWindowsTargeting` 编译 Windows 目标；运行界面、系统媒体控制、音频会话和游戏覆盖需要 Windows。GitHub Actions 在 Windows 自动运行歌词回归检查并生成便携包。推送 `v*` 标签会创建带下载包的 GitHub Release。

## 首次实机检查

交给 Windows Agent 执行的完整步骤、验收标准和报告模板：[实机测试说明](docs/WINDOWS_AGENT_TEST_PLAN.md)。

- 四种播放器的识别与切歌，暂停 / 恢复和进度同步。
- 外文歌双语歌词、长歌词、纯音乐及无歌词情况。
- LOL 无边框模式快捷键呼出、切歌不抢焦点、移开收起。
- 常驻、8 种配色、3 种背景、重启保留设置、登录自启。
- 边缘仅上半部分跟随所选播放器，暂停与设备切换后恢复。
- 100% / 150% / 200% 缩放、多显示器、快捷键冲突。

诊断日志位于 `%LOCALAPPDATA%\EdgeTune\diagnostics.log`，不包含歌词内容或音频。依赖：NAudio（MIT），Windows SDK / .NET / WPF；歌词接口为曲库公开网络端点，可能随服务更新变化。
