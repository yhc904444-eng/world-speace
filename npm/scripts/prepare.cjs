'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { verifyBundle } = require('../bin/world-speace.cjs');
const source = path.resolve(__dirname, '../../release/world speace');
const target = path.resolve(__dirname, '../app');
const files = ['world speace.exe', 'world speace.exe.config', 'ECDICT-LICENSE.txt', 'FSRS-MIT.txt', 'source.json', '使用说明.md', '语境示例30词.tsv', '自制词书模板.csv', 'SHA256.txt'];
verifyBundle(source);
for (const name of files) if (!fs.statSync(path.join(source, name)).isFile()) throw new Error(`缺少发行文件：${name}`);
// Refuse unexpected content, including private data, rather than removing it.
if (fs.existsSync(target)) {
  for (const name of fs.readdirSync(target)) {
    if (!files.includes(name) || !fs.lstatSync(path.join(target, name)).isFile()) throw new Error(`npm/app 中含非发行文件：${name}；请检查后在干净目录打包。`);
  }
}
fs.mkdirSync(target, { recursive: true });
for (const name of files) fs.copyFileSync(path.join(source, name), path.join(target, name));
verifyBundle(target);
console.log(`已准备 ${files.length} 个公开发行文件；不复制 UserData、备份或日志。`);
