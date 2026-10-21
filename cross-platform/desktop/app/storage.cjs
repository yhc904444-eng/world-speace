const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const LIMIT=20*1024*1024;
function atomicWrite(target,text){
  const temporary=target+'.'+crypto.randomUUID()+'.tmp';let descriptor;
  try{descriptor=fs.openSync(temporary,'wx',0o600);fs.writeFileSync(descriptor,text,'utf8');fs.fsyncSync(descriptor);fs.closeSync(descriptor);descriptor=undefined;fs.renameSync(temporary,target);}
  finally{if(descriptor!==undefined)fs.closeSync(descriptor);if(fs.existsSync(temporary))fs.unlinkSync(temporary);}
}
function validate(text){
  if(typeof text!=='string'||Buffer.byteLength(text,'utf8')>LIMIT)throw new Error('文件不能超过 20 MB。');
  const data=JSON.parse(text);if(data?.Version!==1||!data.Cards||typeof data.Cards!=='object')throw new Error('学习进度格式无效。');return text;
}
class FileStore{
  constructor(directory){this.directory=directory;fs.mkdirSync(directory,{recursive:true,mode:0o700});this.file=path.join(directory,'progress.json');this.previousFile=path.join(directory,'before-restore.json');}
  read(file=this.file){try{if(fs.statSync(file).size>LIMIT)throw new Error('进度文件超过 20 MB，请保留原文件。');return new TextDecoder('utf-8',{fatal:true}).decode(fs.readFileSync(file));}catch(error){if(error.code==='ENOENT')return '';throw error;}}
  load(){try{return JSON.stringify({ok:true,state:this.read()});}catch(error){return JSON.stringify({ok:false,error:'无法读取学习进度：'+error.message});}}
  save(text){try{atomicWrite(this.file,validate(text));return '';}catch(error){return '未保存学习进度：'+error.message;}}
  preserve(){try{const original=this.read();if(original)atomicWrite(this.previousFile,original);return '';}catch(error){return '无法保留恢复前进度：'+error.message;}}
}
module.exports={FileStore,atomicWrite,validate,LIMIT};
