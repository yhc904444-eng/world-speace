# world speace · Android 单机版 v1.5.1

将现有 Windows v1.5.0 的离线学习功能移植到 Android，保留青竹绿等五种配色与液态玻璃、简洁纯色、沉浸流光三种风格。手机界面采用底部导航、触屏学习卡片和系统文件选择器。

## 安装与使用

1. 把 `world-speace_Android_v1.5.1.apk` 传到手机，点击安装。如果系统提示，允许当前文件管理器安装此 APK。已安装 v1.5.0 时直接覆盖安装，保留本机进度；不要先卸载或清除应用数据。
2. 打开 world speace，在“词书”选择词书，再回到“今日”学习。
3. 在“我的 → 设置与备份”调整每日目标、外观与语音，或导入、导出进度。

最低 Android 8.0（API 26）。界面使用系统 Android WebView，请保持 WebView 可用并更新。程序没有联网权限，词库、界面与学习算法全部包含在 APK 内，不需要账号、服务器或首次下载词库。英语朗读需手机已安装离线英语语音包；没有语音包时仍可背词、复习与查词。

学习数据写入应用私有目录，替换安装同一签名的新版本会继续使用原数据。**卸载或清除应用数据会删除本机进度，请先导出完整备份。** 不包含云端同步、原应用账号或付费内容。

## v1.5.1 启动修复

修复 v1.5.0 打开后提示“离线词库未能加载。”、停留在“导出原始进度”页面的问题。旧 APK 的嵌套资源条目包含 Windows 反斜杠（`assets/data\words.tsv`），Android 按 `assets/data/words.tsv` 查找时找不到词库。构建现在先统一 ZIP 内路径，再对齐、签名，并检查最终 APK 的每个资源路径与内容哈希。

此版继续使用原包名和原签名，Android versionCode 从 15001 升为 15002；学习数据格式与私有文件路径保持一致。也修复了缺少 `Object.hasOwn` 的系统 WebView 无法重新加载已保存卡片或修改设置的问题。Windows 版保持 v1.5.0。

## 已包含功能

- 18,147 个 ECDICT 离线词条、9 类词书、音标、释义与可用词形。
- 新词卡片、全部已学词书的到期复习、FSRS-6 默认模型、10 分钟短时巩固；本轮重现不取消原定到期复习。
- 拼写练习、自动收藏遗忘词、手动收藏、“永不复习”与恢复。
- 英文或中文离线搜索、自制 UTF-8 CSV / TSV 词书与双语例句。
- 每日目标、学习与拼写记录、最近 14 天统计、离线英语朗读。
- 五种配色、三种页面风格、玻璃透明度、流光强度和动态开关；设置自动保存。
- 完整 JSON 备份恢复、收藏 CSV 导出、CSV 模板导出、恢复前备份保留。

## 与电脑版迁移进度

Windows v1.5.0：在“设置与备份”导出完整 JSON 备份，把文件传到手机，然后在安卓版的“我的 → 设置与备份 → 导入 / 恢复备份”选择它。确认后会替换手机当前的记录，并保留恢复前副本。

安卓版导出的完整备份也可在 Windows v1.5.0 恢复。已对照 Windows 原版程序实际执行读取、校验、恢复、再次导出，验证了复习日期、模型参数、自制词书、例句、收藏、排除项和外观信息。两端各自离线保存，**不会自动合并或同步**；恢复前请核对哪份备份更新。

词书导入支持最多 20 MB 的 UTF-8 文件，必须包含 `word` 与 `translation` 表头；可选 `phonetic,definition,example,example_translation`。完整备份同样限 20 MB。不要把收藏 CSV 当作完整学习备份。

## 验证范围

- 41 项核心测试通过，包含 13 组官方 FSRS-6 参考数值、Windows 格式兼容、排期保护、CSV、损坏输入、存储失败回滚和旧 WebView 的卡片重开等。
- 直接从签名 APK 的原始 ZIP 条目读取页面，旧包精确复现词库 404 和相同错误文字；修复包读取 18,147 个词条并进入首页。
- 390×844 手机尺寸的 29 项页面操作检查、15 项 Android 桥接接口模拟检查通过。使用合成进度，在相同浏览器存储中将旧 APK 切换为新 APK，验证原始进度保留、可保存设置并再次打开。
- Android APK 编译、版本及权限检查、ZIP 对齐、v2/v3 签名、10 个资源的精确路径和内容哈希校验通过；签名证书与旧包一致。
- Android → Windows 原程序 → Android 的 JSON 迁移验证通过。
- **未完成 Android 真机或模拟器安装测试**。页面检查在浏览器手机尺寸下进行，桥接检查使用模拟接口，不替代设备测试。安装器、软键盘、系统文件选择器和具体机型的英语语音需在设备上确认。

`docs/` 内图片为手机尺寸的浏览器页面截图。

## 工程与构建

`app/src/main/java/`：Android Activity、受限的本地资源加载、私有文件原子写入、系统文件选择与离线 TTS。`app/src/main/assets/domain.mjs`：不依赖 Android 的学习算法与数据校验。`repository.mjs`：原生存储与导入导出适配。`app.mjs` / `app.css`：手机界面。测试仅使用 Node 内置测试框架，无 npm 运行依赖。

在 Windows PowerShell 中，安装 JDK 21、Android SDK platform 36 和 build-tools 36.0.0 后运行：

```powershell
.\build.ps1 -AndroidSdk "C:\Android\Sdk" -JavaHome "C:\Java\jdk-21"
node --test tests/core.test.mjs
.\tools\test-zip-paths.ps1
.\tools\verify-apk.ps1 -Path "..\release\world-speace_Android_v1.5.1.apk"
```

也可以设置 `ANDROID_HOME`、`JAVA_HOME` 后直接执行 `build.ps1`。在本仓库中会复用已有工具链；独立源码包携带完整静态资源，可以使用指定的外部 SDK 构建，不需要电脑版目录。APK 在仓库的 `release/` 中；独立解压时输出到工程自身的 `release/`。

首次构建生成项目专用签名，保存在被 Git 忽略的 `.signing/` 中。后续可安装更新必须继续使用这份签名；请单独保留它，不要放进 APK、源码交付包或公共仓库。交付的源码 ZIP 不包含签名私钥、用户进度与验证产生的测试数据。

本地页面预览：`node tools/preview.mjs`，访问输出的 `127.0.0.1` 地址。预览仅绑定本机；`native-contract.html` 是预览服务专用的接口测试页面，不会打进 APK。

检查真实 APK 的页面资源（需要 Python 3）：`python tools/apk-preview.py "..\release\world-speace_Android_v1.5.1.apk"`，访问输出的地址。该服务使用 ZIP 中的原始条目名查找资源，避免 Windows 解压或 Python 默认路径转换掩盖资源错误；它仍是浏览器验证，不代替 Android 安装测试。详细修复记录见 [v1.5.1 验证记录](docs/v1.5.1-verification.md)。

## 数据与参考

- ECDICT：随包提供 MIT 许可与 `data/source.json`，词库与 Windows 版文件 SHA256 一致。
- FSRS-6：公式移植自本仓库 Windows `ReviewScheduler.cs`，保留上游 MIT 许可；参考向量来自已有官方实现校验资料。
- [Android 本地 WebView 内容](https://developer.android.com/develop/ui/views/layout/webapps/load-local-content)、[原生桥接接口边界](https://developer.android.com/privacy-and-security/risks/insecure-webview-native-bridges)、[系统文件选择与保存](https://developer.android.com/training/data-storage/shared/documents-files)、[离线语音判断](https://developer.android.com/reference/android/speech/tts/Voice#isNetworkConnectionRequired())。

本应用不是不背单词官方客户端。
