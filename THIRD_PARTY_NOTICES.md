# 第三方数据与组件

## ECDICT

- 上游项目：https://github.com/skywind3000/ECDICT
- 本项目使用：由原始 CSV 筛选得到的 18,147 个学习词条。
- 上游声明许可证：MIT。
- 完整许可原文：`data/ECDICT-LICENSE.txt`，发行包中为 `ECDICT-LICENSE.txt`。
- 数据来源、筛选规则及精确 SHA256：`data/source.json`。

`data/words.tsv` 包含从上游继承的词条内容；请保留上述归属和许可文件。

## FSRS-6

- 上游项目：https://github.com/open-spaced-repetition/go-fsrs
- 参考提交：`61159b9b3891b1279b63206afef4086d7ef92992`。
- 使用内容：FSRS-6 默认权重和记忆状态计算公式，移植为本项目的 C# `ReviewScheduler`。
- 上游版权：Copyright (c) 2022 open-spaced-repetition；许可证 MIT，完整原文在 `licenses/FSRS-MIT.txt`，发行包中为 `FSRS-MIT.txt`。
- 公式验证：使用官方 https://github.com/open-spaced-repetition/ts-fsrs 的 npm `ts-fsrs 5.4.2` 生成数值参考；测试参考包不随本应用分发。
- 本应用另行实现短时巩固步骤、本轮练习保护、旧记录衔接与 365 天上限。这些产品规则不代表 FSRS 上游的完整调度器。

## Windows 系统组件

应用通过 Windows 的 .NET Framework / WPF 及 System.Speech 使用桌面界面和本地语音能力。它们由用户的 Windows 环境提供，不作为本仓库的第三方二进制依赖打包。

## 自制内容

示例词书中的 30 条双语例句、模板示例、界面和其余应用代码为本项目独立制作。项目不分发原 Android APK 或提取其付费内容。
