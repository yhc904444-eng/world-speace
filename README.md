# 词间 · Windows 单机词汇学习

一个无需账号和安卓模拟器的 Windows 桌面背词程序。词库、学习记录、收藏和复习安排均保存在本机，日常学习无需联网。

当前版本：**v1.3.0**。程序基于 C# / WPF / .NET Framework 4.8 独立实现，并非“不背单词”官方客户端，也不包含原应用的账号、付费词典或影视原声。

## 下载与运行

前往 [Releases 下载页面](https://github.com/yhc904444-eng/cijian-windows/releases/latest)，下载 `词间_Windows单机版_v1.3.0.zip`，完整解压到有写入权限的文件夹，然后双击 `词间.exe`。

支持安装有 .NET Framework 4.8 的 Windows 10 / 11。运行发行包不需要安装 Python、Node.js、Java 或开发工具。

升级时先关闭旧程序，用新版程序文件覆盖原目录，**保留 `UserData` 文件夹**即可继续使用已有进度。

## 功能

- **18,147 个离线词条**：音标、英汉释义及可用词形变化。
- **9 个分类词书**：中考、高考、四六级、考研、雅思、托福、GRE 和常用高频词。
- **记忆卡片与到期复习**：根据“忘记 / 模糊 / 认识”安排后续复习。
- **拼写练习**：根据中文释义回想英文，单独记录拼写结果。
- **永不复习**：看到已掌握的词可立即排除，退出后续新词、复习和拼写队列；支持在“不再复习”列表恢复。
- **离线查词与收藏**：按英文或中文释义搜索，导出收藏 CSV。
- **自制词书**：导入 UTF-8 CSV / TSV，支持双语例句。
- **离线发音**：使用 Windows 已安装的英语语音。
- **本地进度与备份**：每日目标、练习记录、完整 JSON 导出与恢复。
- **页面外观可选**：五种配色，支持液态玻璃和简洁纯色，切换后立即生效并自动保存。
- **启动与界面动效**：液态玻璃启动面板、流动光晕、图标反光和实际加载进度；板块背景有入场、悬停放大和按下缩小反馈，文字始终保持原尺寸。

![学习卡片](docs/images/learning.png)

![不再复习管理](docs/images/exclusions.png)

启动页采用薄荷绿液态玻璃风格：半透明面板、圆润透光边缘、流动光晕、图标反光及玻璃进度条。图标浮现时文字保持静止，进度来自实际加载；光影动画在启动页关闭时停止。Windows 关闭界面动画或使用软件渲染时，保留静态玻璃外观。

进入应用后，动效仅作用于卡片、按钮的背景，文字不参与缩放和移动，避免动画时字形发虚。词书分类在后台启动加载时整理，切换页面直接使用已有词表。

## 切换页面外观

打开 **设置与备份 → 外观与配色**，可选择 **青竹绿、雾海蓝、柔雾紫、暖杏色、简约灰**。页面风格可选 **液态玻璃** 或 **简洁纯色**，不喜欢玻璃质感时切换到纯色即可。

颜色与风格选中后立即应用并自动保存，重启后保留；不会丢失当前页面中尚未保存的每日目标、朗读等设置。外观也包含在完整 JSON 备份中，旧版进度会自动补齐默认外观。

![外观设置](docs/images/appearance.png)

![启动页](docs/images/startup.png)

## 永不复习如何生效

在学习卡片或词典详情点击“永不复习”，当前卡片会跳过，这个英文单词在不同词书中的同名条目也会排除。设置忽略英文大小写，重启后仍有效。

历史记录和收藏保留，词典中仍可查询。误点后可在“不再复习”页面点击“恢复复习”，沿用原来的学习进度和复习日期。不同词形分别处理，例如 `learn` 与 `learned`。

## 数据与隐私

发行程序的学习数据位于程序同目录下的 `UserData` 文件夹。请不要直接在压缩包内运行，也不要把程序放入只读目录。

本仓库和发行包不包含个人学习记录、备份、错误日志或原 Android APK。复制整个软件目录或导出完整 JSON 备份，都可以保存自己的学习数据。

## 从源码构建

在有 .NET Framework 4.8 的 Windows 上，用 PowerShell 执行：

```powershell
.\build.ps1
```

程序输出到 `dist/词间.exe`。内置词库与图标已经包含在源码中，普通构建不需要下载数据或安装第三方包。

开发验证入口：

```powershell
$app = Join-Path $PWD 'dist\词间.exe'
Start-Process -FilePath $app -ArgumentList '--self-test' -Wait
Start-Process -FilePath $app -ArgumentList '--exclusion-tests' -Wait
Start-Process -FilePath $app -ArgumentList '--navigation-tests' -Wait
Start-Process -FilePath $app -ArgumentList '--render-tests' -Wait
Start-Process -FilePath $app -ArgumentList '--motion-tests' -Wait
Start-Process -FilePath $app -ArgumentList '--appearance-tests' -Wait
.\package.ps1
```

测试会在 `dist` 内生成独立的测试目录，不修改正常使用的 `UserData`。界面测试需要可运行 WPF 的 Windows 桌面环境。`package.ps1` 在验证输出存在后生成发行 ZIP，并使用明确的文件清单排除学习数据。

如需重新筛选上游词库，使用 Python 3.11 或更新版本运行 `tools/prepare_dictionary.py`。该步骤需要联网并会更新词库和来源记录。重新生成图标可运行 `tools/make_icon.py`，需要 Pillow；这些工具不是应用运行依赖。

## 词库来源与限制

词库来自 [ECDICT](https://github.com/skywind3000/ECDICT)，按考试标签和词频筛选。上游许可证保留在 [data/ECDICT-LICENSE.txt](data/ECDICT-LICENSE.txt)，筛选规则和 SHA256 在 [data/source.json](data/source.json)。

分类词表存在重叠，用于学习参考，不代表最新官方考试大纲。内置数据没有完整的例句与影视音频；可通过自制词书补充例句。发音取决于 Windows 本地英语语音和可用音频输出，发音失败不会阻断学习或页面切换。

完整操作说明见 [使用说明](使用说明.md)。
