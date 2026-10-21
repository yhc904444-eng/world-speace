import {packager} from '@electron/packager';
import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
import {extractFile,listPackage} from '@electron/asar';
import {root,version,prepare} from './prepare.mjs';
const platform=process.platform;
if(!['darwin','linux'].includes(platform))throw new Error('Build desktop packages on the matching macOS/Linux host.');
const out=path.join(root,'release');fs.mkdirSync(out,{recursive:true});
prepare(path.join(root,'desktop/app/www'),'${globalThis.WorldSpeacePlatform || "桌面"}');
let macIcon;
if(platform==='darwin'){
  const iconset=path.join(root,'build/AppIcon.iconset');fs.mkdirSync(iconset,{recursive:true});
  const source=path.join(root,'ios/WorldSpeace/Assets.xcassets/AppIcon.appiconset/AppIcon.png');
  for(const size of [16,32,128,256,512])for(const scale of [1,2])execFileSync('sips',['-z',String(size*scale),String(size*scale),source,'--out',path.join(iconset,`icon_${size}x${size}${scale===2?'@2x':''}.png`)]);
  macIcon=path.join(root,'build/AppIcon.icns');execFileSync('iconutil',['-c','icns',iconset,'-o',macIcon]);
}
for(const arch of ['x64','arm64']){
  const [directory]=await packager({dir:path.join(root,'desktop/app'),out:path.join(root,'build/desktop'),platform,arch,name:'world speace',executableName:'world-speace',icon:macIcon,appBundleId:'eng.yhc904444.worldspeace',appVersion:version,buildVersion:version,electronVersion:'45.0.0',asar:true,prune:true,overwrite:true,ignore:[/\.DS_Store$/]});
  const archive=path.join(directory,platform==='darwin'?'world speace.app/Contents/Resources/app.asar':'resources/app.asar');
  for(const name of ['data/words.tsv','domain.mjs','repository.mjs']){if(!extractFile(archive,'www/'+name).equals(fs.readFileSync(path.join(root,'desktop/app/www',name))))throw new Error('Packaged asset differs: '+name);}
  if(listPackage(archive).some(name=>name.includes('\\')||/\.signing|UserData|progress\.json|node_modules/.test(name)))throw new Error('Unexpected desktop archive content');
  if(platform==='darwin'){
    const app=path.join(directory,'world speace.app');
    execFileSync('codesign',['--force','--deep','--sign','-','--entitlements',path.join(root,'desktop/entitlements.plist'),app],{stdio:'inherit'});
    execFileSync('codesign',['--verify','--deep','--strict',app],{stdio:'inherit'});
    const zip=path.join(out,`world-speace_macOS_${arch}_v${version}.zip`);
    execFileSync('ditto',['-c','-k','--sequesterRsrc','--keepParent',app,zip]);
    const dmgRoot=path.join(root,'build/dmg-'+arch);fs.mkdirSync(dmgRoot,{recursive:true});fs.cpSync(app,path.join(dmgRoot,'world speace.app'),{recursive:true,verbatimSymlinks:true});
    if(!fs.existsSync(path.join(dmgRoot,'Applications')))fs.symlinkSync('/Applications',path.join(dmgRoot,'Applications'));
    execFileSync('hdiutil',['create','-volname','world speace','-srcfolder',dmgRoot,'-ov','-format','UDZO',path.join(out,`world-speace_macOS_${arch}_v${version}.dmg`)],{stdio:'inherit'});
  }else{
    const binary=path.join(directory,'world-speace');fs.chmodSync(binary,0o755);
    execFileSync('tar',['-czf',path.join(out,`world-speace_Linux_${arch}_v${version}.tar.gz`),'-C',path.dirname(directory),path.basename(directory)]);
    const deb=path.join(root,'build/deb-'+arch),control=path.join(deb,'DEBIAN');fs.mkdirSync(control,{recursive:true});
    const appRoot=path.join(deb,'opt/world-speace');fs.mkdirSync(path.dirname(appRoot),{recursive:true});fs.cpSync(directory,appRoot,{recursive:true,verbatimSymlinks:true});
    const sandbox=path.join(appRoot,'chrome-sandbox');if(fs.existsSync(sandbox))fs.chmodSync(sandbox,0o4755);
    fs.writeFileSync(path.join(control,'control'),`Package: world-speace\nVersion: ${version}\nSection: education\nPriority: optional\nArchitecture: ${arch==='x64'?'amd64':'arm64'}\nMaintainer: yhc904444-eng <yhc904444-eng@users.noreply.github.com>\nDepends: libgtk-3-0, libnss3, libasound2 | libasound2t64, libgbm1, libxss1, libatk-bridge2.0-0, libdrm2\nDescription: Offline vocabulary learning with FSRS-6 and ECDICT\n`);
    fs.mkdirSync(path.join(deb,'usr/bin'),{recursive:true});fs.symlinkSync('/opt/world-speace/world-speace',path.join(deb,'usr/bin/world-speace'));
    fs.mkdirSync(path.join(deb,'usr/share/applications'),{recursive:true});fs.writeFileSync(path.join(deb,'usr/share/applications/world-speace.desktop'),'[Desktop Entry]\nType=Application\nName=world speace\nExec=/opt/world-speace/world-speace\nIcon=world-speace\nTerminal=false\nCategories=Education;Languages;\n');
    fs.mkdirSync(path.join(deb,'usr/share/icons/hicolor/256x256/apps'),{recursive:true});fs.copyFileSync(path.join(root,'desktop/app/www/icon.png'),path.join(deb,'usr/share/icons/hicolor/256x256/apps/world-speace.png'));
    execFileSync('dpkg-deb',['--build','--root-owner-group',deb,path.join(out,`world-speace_Linux_${arch}_v${version}.deb`)],{stdio:'inherit'});
  }
  console.log('Packaged '+platform+' '+arch+': '+directory);
}
