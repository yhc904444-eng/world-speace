(() => {
  const originalFetch=window.fetch.bind(window),stateKey='world-speace-native-contract.state',previousKey=stateKey+'.previous';
  window.__nativeExports=[];window.__nativePicks=[];window.__nativeReleases=0;window.__pickedText='';
  window.fetch=(input,options)=>String(input)==='/import.txt'?Promise.resolve(new Response(window.__pickedText,{status:200,headers:{'Content-Type':'text/plain; charset=utf-8'}})):originalFetch(input,options);
  window.Android={
    readState(){return JSON.stringify({ok:true,state:localStorage.getItem(stateKey)||''});},
    saveState(value){if(window.__failSave)return '模拟磁盘已满，未保存。';localStorage.setItem(stateKey,value);return '';},
    preserveBeforeRestore(){const text=localStorage.getItem(stateKey);if(text)localStorage.setItem(previousKey,text);return '';},
    previousBackup(){return localStorage.getItem(previousKey)||'';},
    rawState(){return localStorage.getItem(stateKey)||'';},
    voices(){return '[]';},speak(){return '未找到离线英语语音。';},stopSpeech(){},
    pickDocument(kind){window.__nativePicks.push(kind);setTimeout(()=>window.dispatchEvent(new CustomEvent('native-result',{detail:{kind:'picked',operation:kind,name:kind==='backup'?'Windows备份.json':'手机词书.csv'}})),10);},
    exportDocument(name,mime,text){window.__nativeExports.push({name,mime,text});setTimeout(()=>window.dispatchEvent(new CustomEvent('native-result',{detail:{kind:'exported'}})),10);},
    releasePicked(){window.__nativeReleases++;},closeApp(){window.__nativeClosed=true;}
  };
})();
