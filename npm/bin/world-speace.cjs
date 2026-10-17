#!/usr/bin/env node
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const os = require('node:os');
const crypto = require('node:crypto');
const { spawn } = require('node:child_process');
const pkg = require('../package.json');

function parseArgs(args) {
  const options = {};
  for (let i = 0; i < args.length; i++) {
    const arg = args[i];
    if (arg === '--help' || arg === '-h') options.help = true;
    else if (arg === '--version' || arg === '-v') options.version = true;
    else if (arg === '--check') options.check = true;
    else if (arg === '--data-dir') {
      if (!args[i + 1] || args[i + 1].startsWith('--')) throw new Error('--data-dir 后需要数据文件夹路径。');
      options.dataDir = args[++i];
    } else throw new Error(`未知参数：${arg}。使用 world-speace --help 查看用法。`);
  }
  return options;
}
function dataDirectory(custom, env = process.env, home = os.homedir()) {
  return path.resolve(custom || path.join(env.LOCALAPPDATA || path.join(home, 'AppData', 'Local'), 'world-speace', 'UserData'));
}
function verifyBundle(root = path.join(__dirname, '..', 'app')) {
  const exe = path.join(root, 'world speace.exe');
  if (!fs.existsSync(exe) || !fs.existsSync(`${exe}.config`)) throw new Error('程序文件不完整，请关闭应用后重新安装 npm 包。');
  const hash = crypto.createHash('sha256').update(fs.readFileSync(exe)).digest('hex');
  if (hash !== pkg.worldSpeace.exeSha256) throw new Error('程序 SHA256 校验失败，请从官方发行包重新安装。');
  return exe;
}
function launchPlan(options, root, env) {
  return { executable: verifyBundle(root), args: ['--data-dir', dataDirectory(options.dataDir, env)] };
}
async function main(args = process.argv.slice(2)) {
  const options = parseArgs(args);
  if (options.help) {
    console.log('world speace · Windows 单机词汇学习\n\nworld-speace                     启动应用\nworld-speace --check             检查程序与数据路径，不启动\nworld-speace --data-dir "路径"    使用指定的学习数据文件夹\nworld-speace --version           查看版本\n\n需要 Windows 10/11、.NET Framework 4.8 与 Node.js 20+。\n默认数据：%LOCALAPPDATA%\\world-speace\\UserData\n升级或卸载 npm 包不会删除独立的数据文件夹。');
    return;
  }
  if (options.version) { console.log(pkg.version); return; }
  if (process.platform !== 'win32') throw new Error('world speace 是 Windows 桌面应用，需要 Windows 10/11。');
  const plan = launchPlan(options);
  if (options.check) {
    console.log(`world speace ${pkg.worldSpeace.appVersion}\n程序校验通过：${plan.executable}\n学习数据目录：${plan.args[1]}`);
    return;
  }
  fs.mkdirSync(plan.args[1], { recursive: true });
  await new Promise((resolve, reject) => {
    const child = spawn(plan.executable, plan.args, { detached: true, stdio: 'ignore', windowsHide: false, shell: false });
    child.once('error', reject);
    child.once('spawn', () => {
      console.log(`已启动 world speace ${pkg.worldSpeace.appVersion}（PID ${child.pid}）\n学习数据：${plan.args[1]}`);
      child.unref(); resolve();
    });
  });
}
if (require.main === module) main().catch(error => { console.error(`world-speace: ${error.message}`); process.exitCode = 1; });
module.exports = { parseArgs, dataDirectory, verifyBundle, launchPlan };
