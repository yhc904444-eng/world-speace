(() => {
  const call=(method,...args)=>window.prompt('__WORLD_SPEACE__'+JSON.stringify({method,args}), '')||'';
  window.Android={
    readState:()=>call('readState'),saveState:text=>call('saveState',text),rawState:()=>call('rawState'),
    preserveBeforeRestore:()=>call('preserveBeforeRestore'),previousBackup:()=>call('previousBackup'),
    voices:()=>call('voices'),speak:(word,voice)=>call('speak',word,voice),stopSpeech:()=>call('stopSpeech'),
    pickDocument:operation=>call('pickDocument',operation),exportDocument:(name,mime,text)=>call('exportDocument',name,mime,text),
    releasePicked:()=>call('releasePicked'),closeApp:()=>''
  };
  const ready=()=>{if(document.documentElement.dataset.ready==='true'){window.webkit.messageHandlers.worldSpeaceStatus.postMessage('ready');return true;}return false;};
  document.addEventListener('DOMContentLoaded',()=>{if(ready())return;const observer=new MutationObserver(()=>{if(ready())observer.disconnect();});observer.observe(document.documentElement,{attributes:true,attributeFilter:['data-ready']});});
})();
