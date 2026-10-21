import fs from 'node:fs';
import path from 'node:path';
import {spawnSync} from 'node:child_process';
import {root,version,prepareWeb} from './prepare.mjs';
const target=prepareWeb(),out=path.join(root,'release');fs.mkdirSync(out,{recursive:true});
const zip=path.join(out,`world-speace_Web_v${version}.zip`);
const result=process.platform==='win32'
  ?spawnSync('powershell.exe',['-NoProfile','-Command',`Compress-Archive -LiteralPath '${target.replaceAll("'","''")}' -DestinationPath '${zip.replaceAll("'","''")}' -Force`],{stdio:'inherit'})
  :spawnSync('zip',['-q','-r',zip,'web'],{cwd:path.dirname(target),stdio:'inherit'});
if(result.status!==0)throw new Error('Web archive failed');console.log(zip);
