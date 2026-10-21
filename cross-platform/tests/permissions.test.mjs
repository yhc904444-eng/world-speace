import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {publicPermissions} from '../tools/public-permissions.mjs';
test('Linux installed app remains traversable after root ownership replaces the builder', {skip:process.platform==='win32'},()=>{
  const root=fs.mkdtempSync(path.join(os.tmpdir(),'world-speace-mode-'));
  const directory=path.join(root,'private-staging'),binary=path.join(directory,'world-speace'),data=path.join(directory,'dictionary');
  fs.mkdirSync(directory,{mode:0o700});fs.writeFileSync(binary,'binary',{mode:0o700});fs.writeFileSync(data,'data',{mode:0o600});
  publicPermissions(directory);
  assert.equal(fs.statSync(directory).mode&0o777,0o755);assert.equal(fs.statSync(binary).mode&0o777,0o755);assert.equal(fs.statSync(data).mode&0o777,0o644);
});
