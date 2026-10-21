import fs from 'node:fs';
import path from 'node:path';
// Packager staging folders can retain mkdtemp's 0700 mode. Installed app
// directories must be traversable after the package manager changes the owner.
export function publicPermissions(target){
  const info=fs.lstatSync(target);if(info.isSymbolicLink())return;
  if(info.isDirectory()){fs.chmodSync(target,0o755);for(const name of fs.readdirSync(target))publicPermissions(path.join(target,name));}
  else fs.chmodSync(target,info.mode&0o111?0o755:0o644);
}
