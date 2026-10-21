async () => {
  const passed=[],wait=ms=>new Promise(resolve=>setTimeout(resolve,ms));
  const assert=(value,message)=>{if(!value)throw new Error(message);passed.push(message);};
  const click=selector=>{const el=document.querySelector(selector);if(!el)throw new Error('Missing '+selector);el.click();};
  const read=()=>JSON.parse(localStorage.getItem('world-speace-native-contract.state'));
  assert(document.documentElement.dataset.ready==='true','native-bridge mode loads packaged dictionary');
  click('[data-action="start-new"]');click('[data-action="reveal"]');click('[data-grade="2"]');
  const before=read();assert(before.Cards.length===1&&before.History.length===1,'answers use Android persistence bridge');
  click('[data-action="today"]');click('[data-action="settings"]');
  click('[data-action="export-backup"]');assert(__nativeExports.length===1&&__nativeExports[0].mime==='application/json','backup uses native document export');
  const output=JSON.parse(__nativeExports[0].text);assert(Array.isArray(output.Cards)&&output.Cards[0].Key,'native backup has Windows-compatible dictionary encoding');
  window.__pickedText=JSON.stringify({Version:1,SelectedBook:'cet4',DailyTarget:30,AutoSpeak:false,Voice:null,Cards:[{Key:'state',Value:{Level:0,Reviews:1,Lapses:0,Due:'2026-10-19T15:08:37.2158083+08:00',LastReview:'2026-10-19T14:58:37.2158083+08:00',Stability:2.3065,Difficulty:2.118103970459015,IntervalDays:10/1440,SchedulerVersion:1,Stage:'learning',ScheduleReason:'初次接触：10 分钟后检验回忆。'}}],History:[],Favorites:['state'],ExcludedWords:['might'],CustomWords:[],CustomBooks:[],PageStyle:'solid',PagePalette:'ocean',GlassDensity:'medium',LuminousIntensity:'vivid',LuminousMotion:false});
  click('[data-action="import-backup"]');await wait(80);
  assert(__nativePicks.at(-1)==='backup'&&document.querySelector('#modal-content h2').textContent==='恢复这份备份？','native picker result opens backup confirmation');
  assert(read().DailyTarget===20,'backup does not replace learning state before confirmation');
  click('[data-action="confirm"]');await wait(30);const restored=read();assert(restored.DailyTarget===30&&restored.PageStyle==='solid'&&restored.PagePalette==='ocean','confirmed native backup applies settings');
  assert(restored.Cards[0].Value.Due==='2026-10-19T15:08:37.2158083+08:00'&&restored.ExcludedWords.includes('might'),'Windows due date and exclusions survive native import exactly');
  click('[data-action="export-previous"]');const previous=JSON.parse(__nativeExports.at(-1).text);assert(previous.History.length===1&&previous.DailyTarget===20,'restore preserves original state in exportable native backup');
  const stable=JSON.stringify(read());window.__pickedText='{"Version":99}';click('[data-action="import-backup"]');await wait(70);assert(JSON.stringify(read())===stable&&!document.getElementById('modal').open,'invalid picked backup does not alter native progress');
  assert(__nativeReleases===2,'picked document memory is released for successful and rejected imports');
  window.__pickedText='word,translation\nserendipity,意外发现';click('[data-action="import-book"]');await wait(60);click('[data-action="confirm"]');await wait(30);assert(read().CustomWords[0].Text==='serendipity','native CSV picker imports custom word book');
  click('[data-action="settings"]');window.__failSave=true;const target=document.getElementById('DailyTarget');target.value='40';target.dispatchEvent(new Event('change',{bubbles:true}));window.__failSave=false;
  assert(read().DailyTarget===30&&document.getElementById('DailyTarget').value==='30','failed Android write rolls back visible settings');
  click('[data-action="test-speech"]');await wait(20);assert(document.getElementById('toast').textContent.includes('离线英语语音'),'missing voice pack gives a useful message');
  assert(document.documentElement.scrollWidth<=innerWidth,'native-mode settings fit phone width');
  return {kind:'browser simulation of native bridge contract; not an Android device test',passed,count:passed.length};
}
