import path from 'node:path';
import {execFileSync} from 'node:child_process';
export function zipFolder(folder,output){
  if(process.platform==='win32')execFileSync('tar.exe',['-a','-cf',output,'-C',path.dirname(folder),path.basename(folder)],{stdio:'inherit'});
  else execFileSync('zip',['-q','-r',output,path.basename(folder)],{cwd:path.dirname(folder),stdio:'inherit'});
}
