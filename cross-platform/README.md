# world speace 跨平台 v1.5.1

Android v1.5.1 的离线学习逻辑和界面复用于 macOS、Linux、iOS/iPadOS 和网页版。Windows 继续交付原 v1.5.0 WPF 程序。各端支持相同 JSON 完整备份，进度各自本地保存，迁移需要手动导入，不会自动合并。

| 平台 | 交付内容 | 安装或最后一步 |
| --- | --- | --- |
| Windows 10/11 | 原 v1.5.0 便携 ZIP | 解压运行，升级保留 UserData |
| Android 8+ | v1.5.1 原签名 APK | 覆盖安装，保留手机进度 |
| macOS | Intel x64 / Apple Silicon arm64 的 DMG 和 app ZIP | 拖到 Applications；当前为 ad hoc 签名，尚无 Developer ID 签名或公证 |
| Linux | x64 / arm64 的 DEB 和 tar.gz | 推荐在 Ubuntu 24.04 / 兼容 Debian 系统安装 DEB，系统包管理器解决依赖 |
| iOS/iPadOS 16.4+ | Xcode 工程、真机 arm64 未签名 IPA | 自己的 Apple 开发者 Team、证书和 provisioning profile 才能完成真机分发 |
| 网页 / PWA | 完整静态离线网站 ZIP | HTTPS 部署后可安装到主屏幕；本机可用自带 Node 预览服务 |

macOS 构建不代表获得 Apple 商店或公证认可。首次打开被系统拦截时，只对已核对来源与 SHA256 的本应用使用系统“隐私与安全性 → 仍要打开”，不要关闭整个系统的安全功能。也可在具备 Developer ID 身份的 macOS 上自行签名、公证。

Linux DEB：`sudo apt install ./world-speace_Linux_x64_v1.5.1.deb`，arm64 机器换对应文件。便携 tar.gz 解压后运行 world-speace；某些发行版限制 Chromium 沙箱，建议使用 DEB。程序不会为运行而关闭沙箱。

桌面学习记录在 Electron 标准用户数据目录下：macOS 的 `~/Library/Application Support/world-speace/`；Linux 通常是 `~/.config/world-speace/`（遵循 XDG 配置）。核心文件为 progress.json 和 before-restore.json。卸载程序包不会主动删除这些文件。iOS 数据路径和签名方法见 [iOS 说明](ios/README.md)；网页存储边界见 [网页版说明](web/README.md)。

## 构建

使用 Node.js 24，在本目录运行 `npm ci`、`npm test`、`npm run prepare:assets`。所有运行资源从 Android 工程的显式清单复制，保留文件内容、词库与许可。

- macOS / Linux：在相应系统执行 `npm run package:desktop`，分别打包 x64 和 arm64。开发依赖固定在 package-lock.json 中，无需原生 Node 插件。
- iOS：macOS + Xcode，执行 `bash ios/build.sh`。`bash ios/smoke-simulator.sh` 实际运行模拟器，使用隔离测试数据检查词库、作答保存、JSON 导出和重开。
- 网页：`npm run package:web`，输出 ZIP 在 release/。本机测试 `node build/web/serve.mjs`。

桌面程序提供 `--smoke-test --smoke-report=/绝对路径/报告.json`，在随机临时数据目录验证真实渲染进程、词库、原生存储、完整备份与重开；不会触碰正常进度。iOS 模拟器测试入口仅在 Debug 构建中存在。

GitHub Actions 的 Cross-platform packages 工作流完成原生构建和验证并上传构建附件。工作流不包含发布密钥或签名身份；iOS Release 明确禁用签名，macOS 仅作本机 ad hoc 签名。发布者检查结果后再将完成的包上传 GitHub Release。

## 范围

这次覆盖常用的 Windows、Android、macOS、Linux、iOS/iPadOS 和支持 PWA 的浏览器。HarmonyOS NEXT、电视系统、手表和商店专用渠道没有独立原生工程。支持 Android APK 的鸿蒙设备可尝试 Android 包，完全不支持 APK 的设备可使用支持的浏览器网页版；不把这些兼容方式称为原生鸿蒙版。

ECDICT 和 FSRS 的 MIT 许可随每个资源包保留。Electron 的许可证与 Chromium 第三方许可随桌面运行时分发。
