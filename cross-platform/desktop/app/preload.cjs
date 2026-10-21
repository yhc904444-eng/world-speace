const {contextBridge,ipcRenderer}=require('electron');
const invoke=(operation,...args)=>ipcRenderer.sendSync('world-speace:state',operation,...args);
const voices=()=>window.speechSynthesis?.getVoices().filter(v=>v.localService&&/^en([-_]|$)/i.test(v.lang))||[];
contextBridge.exposeInMainWorld('WorldSpeacePlatform',process.platform==='darwin'?'macOS':process.platform==='linux'?'Linux':'桌面');
contextBridge.exposeInMainWorld('Android',{
  readState:()=>invoke('load'),saveState:text=>invoke('save',text),rawState:()=>invoke('raw'),
  preserveBeforeRestore:()=>invoke('preserve'),previousBackup:()=>invoke('previous'),
  voices:()=>JSON.stringify(voices().map(v=>v.name)),
  speak(word,preferred){const available=voices(),voice=available.find(v=>v.name===preferred)||available[0];if(!voice)return '未找到离线英语语音。';const phrase=new SpeechSynthesisUtterance(String(word));phrase.lang=voice.lang;phrase.voice=voice;phrase.rate=.85;window.speechSynthesis.cancel();window.speechSynthesis.speak(phrase);return '';},
  stopSpeech:()=>window.speechSynthesis?.cancel(),
  pickDocument:operation=>ipcRenderer.send('world-speace:pick',operation),
  exportDocument:(name,mime,text)=>ipcRenderer.send('world-speace:export',name,mime,text),
  releasePicked:()=>invoke('release'),closeApp:()=>ipcRenderer.send('world-speace:close')
});
ipcRenderer.on('world-speace:result',(_event,detail)=>window.dispatchEvent(new CustomEvent('native-result',{detail})));
