# world speace

Windows 单机背词应用，包含离线词库、FSRS-6 复习、拼写、收藏、永不复习和液态玻璃外观。npm 包直接携带 Windows 程序，不在安装后运行额外下载脚本，无第三方 Node.js 依赖。

需要 **Windows 10/11、.NET Framework 4.8、Node.js 20 或更新版本**。

## 安装和启动

无需 npm 账号，直接从本项目 GitHub 发行包安装：

```powershell
npm install -g https://github.com/yhc904444-eng/world-speace/releases/download/v1.4.0/world-speace-1.4.0.tgz
world-speace
```

也可以临时运行：

```powershell
npx --yes --package=https://github.com/yhc904444-eng/world-speace/releases/download/v1.4.0/world-speace-1.4.0.tgz world-speace
```

安装需要联网，安装完成后启动与学习均可离线。若 PowerShell 阻止执行 npm 生成的 `.ps1`，使用 `npm.cmd`、`npx.cmd`、`world-speace.cmd`，无需降低系统执行策略。

## 学习进度

默认保存到 `%LOCALAPPDATA%\world-speace\UserData`，与 npm 全局安装目录、npx 缓存分开。更新或卸载 npm 包不会删除这份学习记录。

从便携版继续学习，可以直接指定原来的 `UserData` 文件夹：

```powershell
world-speace --data-dir "D:\你的软件目录\UserData"
```

不指定旧数据目录时会使用独立的新进度。不要同时用不同版本打开同一个数据目录。

```powershell
world-speace --version
world-speace --check
world-speace --help
```

`--check` 校验程序 SHA256 并显示路径，不启动应用，不改写学习记录。

## 升级和来源

升级前关闭正在运行的应用，再安装新版本发行包。源码与版本记录：https://github.com/yhc904444-eng/world-speace 。程序完整说明、ECDICT 与 FSRS 的 MIT 许可均包含在 `app` 文件夹中。项目未另外授予原创代码的开源许可证；公共下载不改变第三方组件许可。

这不是“不背单词”官方客户端，不包含原应用账号、云端数据、付费词典或影视原声。
