# world speace iOS v1.5.1

原生 UIKit / WKWebView 工程，iOS / iPadOS 16.4 及以上。包含完整离线词库、原子进度文件、系统文档导入导出和英语语音。没有第三方 iOS 依赖。源码 ZIP 已包含 www；在仓库中先在 cross-platform 执行 `npm run prepare:assets`。

macOS 上用 Xcode 打开 WorldSpeace.xcodeproj，选择 WorldSpeace。先选择 Simulator 可免开发者签名调试。`bash smoke-simulator.sh` 会在独立测试目录验证词库加载、作答保存、JSON 备份及重开，并生成模拟器截图。Release 包不包含该测试入口。

`bash build.sh` 会构建真机 arm64 的未签名 IPA。这不是可直接点击安装的安装包。真机安装的最后一步需要你自己的 Apple 开发者身份、证书及匹配的 provisioning profile。在 Xcode 的 Signing & Capabilities 选择自己的 Team；根据证书权限调整 Bundle Identifier 后运行到设备，或使用 Product → Archive / Distribute App 生成 Development、Ad Hoc、TestFlight 或 App Store 分发包。

本项目不包含 Apple 签名身份，也不会代为注册或购买开发者计划。CI 生成的 unsigned.ipa 仅用于签名交接。App Store 上传还需要你提供发布资料、隐私声明及适配的应用图标；未宣称通过商店审核。

进度保存在应用私有的 Application Support/world-speace/progress.json，恢复前副本为 before-restore.json。备份文件与 Windows v1.5.0 和 Android v1.5.1 兼容。删除应用会删除进度；请先导出 JSON。
