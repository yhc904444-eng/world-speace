# 第三方数据与组件

## ECDICT

- 上游项目：https://github.com/skywind3000/ECDICT
- 本项目使用：由原始 CSV 筛选得到的 18,147 个学习词条。
- 上游声明许可证：MIT。
- 完整许可原文：`data/ECDICT-LICENSE.txt`，发行包中为 `ECDICT-LICENSE.txt`。
- 数据来源、筛选规则及精确 SHA256：`data/source.json`。

`data/words.tsv` 包含从上游继承的词条内容；请保留上述归属和许可文件。

## Windows 系统组件

应用通过 Windows 的 .NET Framework / WPF 及 System.Speech 使用桌面界面和本地语音能力。它们由用户的 Windows 环境提供，不作为本仓库的第三方二进制依赖打包。

## 自制内容

示例词书中的 30 条双语例句、模板示例、界面和应用代码为本项目独立制作。项目不分发原 Android APK 或提取其付费内容。
