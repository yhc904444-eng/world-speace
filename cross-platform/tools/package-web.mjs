import fs from 'node:fs';
import path from 'node:path';
import {root,version,prepareWeb} from './prepare.mjs';
import {zipFolder} from './archive.mjs';
const target=prepareWeb(),out=path.join(root,'release');fs.mkdirSync(out,{recursive:true});
const zip=path.join(out,`world-speace_Web_v${version}.zip`);
zipFolder(target,zip);console.log(zip);
