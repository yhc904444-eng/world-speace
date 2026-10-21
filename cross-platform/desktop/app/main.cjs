const {app,BrowserWindow,ipcMain,protocol,session,dialog,Menu}=require('electron');
const fs=require('node:fs'),path=require('node:path'),os=require('node:os');
const {FileStore,LIMIT,atomicWrite}=require('./storage.cjs');
const ORIGIN='worldspeace://app',START=ORIGIN+'/assets/index.html';
const smoke=process.argv.includes('--smoke-test');
const reportArgument=process.argv.find(arg=>arg.startsWith('--smoke-report='));
let win,store,picked=null,picking=false,lastExport=null;
app.setName('world-speace');
if(smoke){app.disableHardwareAcceleration();app.setPath('userData',fs.mkdtempSync(path.join(os.tmpdir(),'world-speace-smoke-')));}
protocol.registerSchemesAsPrivileged([{scheme:'worldspeace',privileges:{standard:true,secure:true,supportFetchAPI:true,corsEnabled:true}}]);
const authorized=event=>event.sender===win?.webContents&&event.senderFrame?.url===START;
const emit=detail=>{if(win&&!win.isDestroyed())win.webContents.send('world-speace:result',detail);};
const showError=error=>dialog.showMessageBox(win,{type:'error',title:'world speace',message:error.message||String(error)});
function bindBridge(){
  ipcMain.on('world-speace:state',(event,operation,text)=>{
    if(!authorized(event)){event.returnValue='访问被拒绝。';return;}
    try{event.returnValue=operation==='load'?store.load():operation==='save'?store.save(text):operation==='raw'?store.read():operation==='previous'?store.read(store.previousFile):operation==='preserve'?store.preserve():operation==='release'?(picked=null,''):'无效的操作。';}
    catch(error){event.returnValue='无法读取进度：'+error.message;}
  });
  ipcMain.on('world-speace:pick',async(event,operation)=>{
    if(!authorized(event)||picking||!['backup','book'].includes(operation))return;
    picking=true;
    try{
      const result=await dialog.showOpenDialog(win,{title:operation==='backup'?'导入完整进度备份':'导入自制词书',properties:['openFile'],filters:[{name:operation==='backup'?'JSON 备份':'CSV / TSV 词书',extensions:operation==='backup'?['json']:['csv','tsv','txt']}]});
      if(result.canceled||!result.filePaths[0])return;
      const source=result.filePaths[0];if(fs.statSync(source).size>LIMIT)throw new Error('文件不能超过 20 MB。');
      const bytes=fs.readFileSync(source);new TextDecoder('utf-8',{fatal:true}).decode(bytes);picked=bytes;
      emit({kind:'picked',operation,name:path.basename(source)});
    }catch(error){await showError(error);}finally{picking=false;}
  });
  ipcMain.on('world-speace:export',async(event,name,mime,text)=>{
    if(!authorized(event))return;
    try{
      if(typeof text!=='string'||Buffer.byteLength(text,'utf8')>LIMIT)throw new Error('导出文件不能超过 20 MB。');
      if(smoke){lastExport={name,mime,text};emit({kind:'exported'});return;}
      const safeName=path.basename(String(name)).replaceAll('\\','_');
      const result=await dialog.showSaveDialog(win,{title:'导出文件',defaultPath:path.join(app.getPath('documents'),safeName)});
      if(result.canceled||!result.filePath)return;
      atomicWrite(result.filePath,text);emit({kind:'exported'});
    }catch(error){await showError(error);}
  });
  ipcMain.on('world-speace:close',event=>{if(authorized(event))win.close();});
}
async function runSmoke(){
  const passed=[];
  const check=(ok,name)=>{if(!ok)throw new Error(name);passed.push(name);};
  await win.webContents.executeJavaScript(`new Promise((resolve,reject)=>{let attempts=0;const poll=()=>{if(document.documentElement.dataset.ready==='true')return resolve(true);if(document.documentElement.dataset.error)return reject(new Error(document.documentElement.dataset.error));if(++attempts>150)return reject(new Error('Startup timed out'));setTimeout(poll,100);};poll();})`);
  check(await win.webContents.executeJavaScript('typeof require==="undefined" && typeof process==="undefined"'),'renderer has no Node access');
  check(win.webContents.getLastWebPreferences().sandbox===true,'renderer sandbox enabled');
  check(await win.webContents.executeJavaScript(`fetch('data/words.tsv').then(r=>r.text()).then(t=>t.trimEnd().split('\\n').length===18147)`),'packaged offline dictionary loads');
  check(await win.webContents.executeJavaScript(`document.fonts.load('400 16px "World Speace CJK"','今天中文词库').then(()=>document.fonts.check('400 16px "World Speace CJK"','今天中文词库'))`),'bundled Chinese font loads without system CJK fonts');
  await win.webContents.executeJavaScript(`(()=>{const click=s=>document.querySelector(s).click();click('[data-action="start-new"]');click('[data-action="reveal"]');click('[data-grade="2"]');click('[data-action="today"]');click('[data-action="settings"]');const target=document.getElementById('DailyTarget');target.value='30';target.dispatchEvent(new Event('change',{bubbles:true}));click('[data-action="export-backup"]');})()`);
  await new Promise(resolve=>setTimeout(resolve,100));
  const saved=store.read(),data=JSON.parse(saved);
  check(data.Cards.length===1&&data.History.length===1&&data.DailyTarget===30,'learning and preferences reach atomic native storage');
  check(lastExport?.text===saved,'native JSON backup equals persisted progress');
  check((await session.defaultSession.fetch('https://example.com').then(()=>false,()=>true)),'external network blocked');
  await win.loadURL(START);
  await win.webContents.executeJavaScript(`new Promise((resolve,reject)=>{let n=0;const poll=()=>document.documentElement.dataset.ready==='true'?resolve(true):++n>100?reject(new Error('Reopen timeout')):setTimeout(poll,100);poll();})`);
  check(store.read()===saved,'reopening preserves exact saved progress');
  return {passed,count:passed.length,platform:process.platform,arch:process.arch,electron:process.versions.electron,userDataIsolated:true};
}
app.whenReady().then(async()=>{
  store=new FileStore(app.getPath('userData'));
  const assets=path.join(__dirname,'www');
  protocol.handle('worldspeace',request=>{
    const url=new URL(request.url);
    if(request.method!=='GET'||url.host!=='app')return new Response('',{status:403});
    if(url.pathname==='/import.txt')return new Response(picked,{status:picked?200:404,headers:{'Content-Type':'text/plain; charset=utf-8'}});
    let relative;try{relative=decodeURIComponent(url.pathname).slice('/assets/'.length);}catch{return new Response('',{status:400});}
    if(!url.pathname.startsWith('/assets/')||relative.includes('\\')||relative.split('/').includes('..'))return new Response('',{status:403});
    const file=path.resolve(assets,relative);
    if(!file.startsWith(assets+path.sep))return new Response('',{status:403});
    try{const mime={'.html':'text/html','.mjs':'text/javascript','.css':'text/css','.png':'image/png','.ttf':'font/ttf','.json':'application/json','.tsv':'text/plain','.txt':'text/plain'}[path.extname(file)]||'application/octet-stream';return new Response(fs.readFileSync(file),{headers:{'Content-Type':mime+(['image/png','font/ttf'].includes(mime)?'':'; charset=utf-8'),'Cache-Control':'no-store'}});}catch{return new Response('',{status:404});}
  });
  session.defaultSession.setPermissionRequestHandler((_contents,_permission,callback)=>callback(false));
  session.defaultSession.setPermissionCheckHandler(()=>false);
  session.defaultSession.webRequest.onBeforeRequest((details,callback)=>callback({cancel:!details.url.startsWith(ORIGIN+'/')&&!details.url.startsWith('blob:')&&!details.url.startsWith('data:')}));
  win=new BrowserWindow({width:1050,height:850,minWidth:360,minHeight:600,show:!smoke,backgroundColor:'#F1F6F2',title:'world speace',icon:path.join(assets,'icon.png'),webPreferences:{preload:path.join(__dirname,'preload.cjs'),sandbox:true,contextIsolation:true,nodeIntegration:false,webSecurity:true,spellcheck:false}});
  win.webContents.setWindowOpenHandler(()=>({action:'deny'}));
  win.webContents.on('will-navigate',(event,url)=>{if(url!==START)event.preventDefault();});
  win.webContents.on('will-attach-webview',event=>event.preventDefault());
  Menu.setApplicationMenu(Menu.buildFromTemplate(process.platform==='darwin'?[{label:'world speace',submenu:[{role:'about'},{role:'hide'},{role:'quit'}]},{role:'editMenu'},{role:'windowMenu'}]:[{role:'editMenu'},{role:'windowMenu'}]));
  bindBridge();await win.loadURL(START);
  if(smoke){win.show();try{const result=await runSmoke();const output=reportArgument?.slice('--smoke-report='.length);if(output){fs.mkdirSync(path.dirname(output),{recursive:true});await win.webContents.executeJavaScript('document.fonts.ready.then(()=>new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve))))');await new Promise(resolve=>setTimeout(resolve,250));try{fs.writeFileSync(output.replace(/\.json$/,'.png'),(await win.webContents.capturePage()).toPNG());}catch(error){result.screenshotWarning=error.message;console.warn('Screenshot unavailable:',error.message);}fs.writeFileSync(output,JSON.stringify(result,null,2));}console.log(JSON.stringify(result));app.exit(0);}catch(error){console.error(error);app.exit(1);}}
}).catch(error=>{console.error(error);app.exit(1);});
app.on('window-all-closed',()=>app.quit());
