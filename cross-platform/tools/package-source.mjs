import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {root,version,prepare} from './prepare.mjs';
const repository=path.dirname(root),release=path.join(root,'release');
fs.mkdirSync(path.join(root,'build'),{recursive:true});fs.mkdirSync(release,{recursive:true});
const packageRoot=fs.mkdtempSync(path.join(root,'build/source-'));
const allSource=path.join(packageRoot,'world-speace');fs.mkdirSync(allSource);
const names=execFileSync('git',['ls-files','-z'],{cwd:repository}).toString('utf8').split('\0').filter(Boolean);
for(const name of names){
  if(/(^|\/)(\.git|\.signing|UserData|node_modules|verification|build)(\/|$)|\.(jks|p12|mobileprovision)$|keystore-password|(^|\/)progress\.json$/.test(name))throw new Error('Private or generated tracked path: '+name);
  const target=path.join(allSource,name);fs.mkdirSync(path.dirname(target),{recursive:true});fs.copyFileSync(path.join(repository,name),target);
}
prepare(path.join(allSource,'cross-platform/ios/WorldSpeace/www'),'iOS');
prepare(path.join(allSource,'cross-platform/desktop/app/www'),'${globalThis.WorldSpeacePlatform || "桌面"}');
const iosSource=path.join(packageRoot,'world-speace-ios');fs.cpSync(path.join(allSource,'cross-platform/ios'),iosSource,{recursive:true});
const commit=execFileSync('git',['rev-parse','HEAD'],{cwd:repository}).toString().trim();
fs.writeFileSync(path.join(allSource,'RELEASE.json'),JSON.stringify({version,commit,windows:'1.5.0',android:version,otherPlatforms:version,privateDataIncluded:false},null,2));
for(const [folder,filename] of [[allSource,`world-speace_All-Source_v${version}.zip`],[iosSource,`world-speace_iOS_v${version}-Source.zip`]]){
  const output=path.join(release,filename);
  if(process.platform==='win32'){
    execFileSync('powershell.exe',['-NoProfile','-Command',`Compress-Archive -LiteralPath '${folder.replaceAll("'","''")}' -DestinationPath '${output.replaceAll("'","''")}' -Force`]);
    execFileSync('powershell.exe',['-NoProfile','-File',path.join(repository,'android/tools/normalize-zip-paths.ps1'),'-Path',output],{stdio:'inherit'});
  }else execFileSync('zip',['-q','-r',output,path.basename(folder)],{cwd:packageRoot});
  console.log(output);
}
