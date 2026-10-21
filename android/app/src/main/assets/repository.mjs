import { defaults, serialize } from './domain.mjs';
export const native = typeof window!=='undefined' && typeof window.Android!=='undefined';
const KEY='world-speace.android.v1';
export function load() {
  if(native){const result=JSON.parse(window.Android.readState());if(!result.ok)throw new Error(result.error);return result.state?JSON.parse(result.state):defaults();}
  const text=localStorage.getItem(KEY);return text?JSON.parse(text):defaults();
}
export function save(data) {
  const text=serialize(data);if(native){const error=window.Android.saveState(text);if(error)throw new Error(error);}else localStorage.setItem(KEY,text);
}
export function beforeRestore() {if(native){const error=window.Android.preserveBeforeRestore();if(error)throw new Error(error);}else{const text=localStorage.getItem(KEY);if(text)localStorage.setItem(KEY+'.previous',text);}}
export function previous() {return native?window.Android.previousBackup():localStorage.getItem(KEY+'.previous');}
export function raw() {return native?window.Android.rawState():localStorage.getItem(KEY);}
export function exportFile(name,mime,text) {
  if(native){window.Android.exportDocument(name,mime,text);return;}
  const url=URL.createObjectURL(new Blob([text],{type:mime}));const a=document.createElement('a');a.href=url;a.download=name;a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
export function speak(word,voice) {
  if(native){const error=window.Android.speak(word,voice||'');if(error)throw new Error(error);return;}
  if(!window.speechSynthesis)throw new Error('当前预览环境没有语音引擎。');const phrase=new SpeechSynthesisUtterance(word);phrase.lang='en-US';const local=speechSynthesis.getVoices().find(v=>v.localService&&v.lang.startsWith('en'));if(!local)throw new Error('未检测到离线英语语音。');phrase.voice=local;phrase.rate=.85;speechSynthesis.cancel();speechSynthesis.speak(phrase);
}
export function stopSpeech(){if(native)window.Android.stopSpeech();else window.speechSynthesis?.cancel();}
