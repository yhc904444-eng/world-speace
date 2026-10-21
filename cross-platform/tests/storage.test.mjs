import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {FileStore} from '../desktop/app/storage.cjs';
const fixture=fs.readFileSync(new URL('../../android/tests/fixtures/windows-backup.json',import.meta.url),'utf8');
const temporary=()=>fs.mkdtempSync(path.join(os.tmpdir(),'world-speace-unit-'));
test('desktop atomic storage reads, saves and reopens exact Windows-compatible progress',()=>{
  const directory=temporary(),store=new FileStore(directory);assert.deepEqual(JSON.parse(store.load()),{ok:true,state:''});assert.equal(store.save(fixture),'');assert.equal(new FileStore(directory).read(),fixture);assert.equal(store.preserve(),'');assert.equal(store.read(store.previousFile),fixture);
});
test('desktop invalid save and failed rename preserve the previous bytes',()=>{
  const directory=temporary(),store=new FileStore(directory);store.save(fixture);assert.match(store.save('{"Version":99}'),/未保存/);assert.equal(store.read(),fixture);
  const blocked=path.join(directory,'blocked');fs.mkdirSync(blocked);store.file=blocked;assert.match(store.save(fixture),/未保存/);assert.equal(fs.readdirSync(directory).some(name=>name.endsWith('.tmp')),false);assert.equal(new FileStore(directory).read(),fixture);
});
test('corrupt UTF-8 progress reports failure and is never overwritten during load',()=>{
  const directory=temporary(),store=new FileStore(directory),bytes=Buffer.from([0xff,0xfe,0x80]);fs.writeFileSync(store.file,bytes);assert.equal(JSON.parse(store.load()).ok,false);assert.deepEqual(fs.readFileSync(store.file),bytes);
});
