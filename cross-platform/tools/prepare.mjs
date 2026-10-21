import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
export const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
export const version=JSON.parse(fs.readFileSync(path.join(root,'package.json'),'utf8')).version;
const assets=path.resolve(root,'../android/app/src/main/assets');
const names=['index.html','app.css','app.mjs','domain.mjs','repository.mjs','icon.png','data/words.tsv','data/source.json','licenses/ECDICT-MIT.txt','licenses/FSRS-MIT.txt'];
export function prepare(target,label){
  fs.mkdirSync(target,{recursive:true});
  for(const name of names){const dest=path.join(target,name);fs.mkdirSync(path.dirname(dest),{recursive:true});fs.copyFileSync(path.join(assets,name),dest);}
  const appPath=path.join(target,'app.mjs');
  let code=fs.readFileSync(appPath,'utf8');
  code=code.replaceAll('Android ${VERSION}',label+' ${VERSION}');
  fs.writeFileSync(appPath,code);
  const prepared=[...names];
  if(label!=='iOS'){
    fs.mkdirSync(path.join(target,'fonts'),{recursive:true});
    for(const name of ['NotoSansSC.ttf','OFL.txt','source.json']){const relative='fonts/'+name;fs.copyFileSync(path.join(root,'desktop/fonts',name),path.join(target,relative));prepared.push(relative);}
    const css=path.join(target,'app.css');
    const fontFace='@font-face{font-family:"World Speace CJK";src:url("fonts/NotoSansSC.ttf") format("truetype");font-style:normal;font-weight:100 900;font-display:block;}\n';
    fs.writeFileSync(css,fs.readFileSync(css,'utf8').replace('@charset "UTF-8";','@charset "UTF-8";\n'+fontFace).replace('system-ui,-apple-system,"Noto Sans SC"','system-ui,-apple-system,"World Speace CJK","Noto Sans SC"'));
  }
  if(fs.readFileSync(path.join(target,'domain.mjs'),'utf8').includes(`VERSION = '${version}'`)===false)throw new Error('Shared application version differs from cross-platform version');
  console.log('Prepared '+label+': '+target);
  return prepared;
}
export function prepareWeb(){
  const target=path.join(root,'build/web'),prepared=prepare(target,'网页');
  for(const name of ['pwa.mjs','manifest.webmanifest','icon.svg','icon-192.png','icon-512.png','serve.mjs'])fs.copyFileSync(path.join(root,'web',name),path.join(target,name));
  const index=path.join(target,'index.html');
  fs.writeFileSync(index,fs.readFileSync(index,'utf8').replace('</head>','<link rel="manifest" href="manifest.webmanifest"><link rel="apple-touch-icon" href="icon.png"></head>').replace('</body>','<script type="module" src="pwa.mjs"></script></body>'));
  const cached=[...prepared,'./','pwa.mjs','manifest.webmanifest','icon.svg','icon-192.png','icon-512.png'];
  const digest=createHash('sha256');for(const name of cached.filter(n=>n!=='./'))digest.update(fs.readFileSync(path.join(target,name)));
  const template=fs.readFileSync(path.join(root,'web/service-worker.js'),'utf8');
  fs.writeFileSync(path.join(target,'service-worker.js'),template.replace('__VERSION__',version+'-'+digest.digest('hex').slice(0,12)).replace('__FILES__',JSON.stringify(cached)));
  fs.copyFileSync(path.join(root,'web/README.md'),path.join(target,'README.md'));
  return target;
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  prepare(path.join(root,'desktop/app/www'),'${globalThis.WorldSpeacePlatform || "桌面"}');
  prepare(path.join(root,'ios/WorldSpeace/www'),'iOS');prepareWeb();
}
