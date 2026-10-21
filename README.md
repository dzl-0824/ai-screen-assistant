# AI截图助手

Windows 截图、文字识别、翻译、贴图与屏幕录制工具。使用 WinForms + WPF，调用 OpenAI 兼容的视觉模型接口。

## 功能

- 框选截图、矩形/圆形/箭头/画笔/文字/打码、撤销重做。
- 截图自动保存在本地，可修改目录；历史切换和悬浮贴图。
- 向上/向下自动滚动截图并拼接；遇到大段空白继续寻找正文，自动压缩多余留白。
- 文字识别、翻译、名词解释、总结、表格提取、二维码识别。
- 全屏截图发送邮件，使用自行配置的 SMTP 邮箱和收件人。
- 全屏或区域录屏，系统声音和麦克风可单独开关，支持暂停、继续，保存 MP4。
- 可修改的全局快捷键、托盘运行、可选开机自启。

## 默认快捷键

| 快捷键 | 功能 |
| --- | --- |
| Alt + ~ | 屏幕录制 |
| Alt + 1 | 截图与贴图 |
| Alt + 2 | 智能识别 |
| Alt + 3 | 文字识别 |
| Alt + 4 | 截图翻译 |
| Alt + 5 | 名词解释 |
| Alt + 6 | 截图总结 |
| Alt + 7 | 表格提取 |
| Alt + 8 | 二维码识别 |
| Alt + 9 | 全屏截图发邮件 |
| Ctrl + Alt + F9 | 录屏暂停/继续 |
| Ctrl + Alt + F10 | 结束录屏并保存 |

「截图与贴图」页可开启滚动截图极速模式：连续采集，匹配不稳时自动降为稳定模式，默认开启。修改后保存生效。

快捷键可在主界面修改，保存后生效。截图编辑中滚轮调整线条粗细或字号；Ctrl+C 复制，Ctrl+P 贴图；Esc 取消或关闭。

## 构建与启动

需要 Windows 10/11、.NET Framework 4.x（推荐 4.8）和 PowerShell 5.1 或更新版本。

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\dist\ScreenText.exe --settings
```

构建脚本从 NuGet 获取 ZXing.Net 0.16.11 和 NAudio 1.10.0，从 Gyan 获取 FFmpeg。依赖保存在项目下 `.deps`，应用输出在 `dist`，不需要安装到系统目录。

默认截图目录为 `D:\ScreenText\Screenshots`，录屏目录为 `D:\ScreenText\Recordings`，均可在应用中调整。

首次启动请通过托盘打开设置，填写自己的 API 地址、视觉模型名称和 API Key。默认接口为百炼 OpenAI 兼容接口，默认模型名称为 `qwen3.8-flash`，请根据实际服务权限调整。

## 数据与配置

本仓库不包含任何用户配置、API Key、Token、邮箱授权码、联系人、截图或录屏。配置在运行后生成于应用旁的 `config/settings.json`，API Key 和 SMTP 授权码使用 Windows DPAPI 当前用户加密；不要分享整个配置目录。调用模型会将所选图片发送到你配置的服务；二维码识别在本地执行。

使用邮件功能前，请自行配置 SMTP，快捷键触发后会直接发送全屏截图。录屏由独立 FFmpeg 进程完成。

## 第三方组件

- ZXing.Net：Apache-2.0。
- NAudio：MIT。
- FFmpeg：本构建使用 Gyan essentials GPLv3 版本，作为独立可执行程序运行。

相关许可证见 `licenses/`。仓库仅提交应用源码与必要图标、提示音；依赖由构建脚本下载。
