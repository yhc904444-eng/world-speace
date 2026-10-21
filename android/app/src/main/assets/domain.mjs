// Pure learning logic, ported from Windows v1.5.0. No Android or UI dependencies.
export const VERSION = '1.5.1';
export const TAGS = ['zk','gk','cet4','cet6','ky','ielts','toefl','gre','common'];
export const BOOK_NAMES = ['中考词汇','高考词汇','大学英语四级','大学英语六级','考研英语','雅思词汇','托福词汇','GRE 词汇','常用高频词'];
export const PALETTES = [
  ['bamboo','青竹绿','#F1F6F2','#2D6A55','#173E32','#19372E','#5F7469','#E0EDE4','#86C9AC'],
  ['ocean','雾海蓝','#F1F5FB','#35638D','#203D5A','#21394F','#60758B','#E0EAF6','#87BCE5'],
  ['lilac','柔雾紫','#F7F3FB','#75558F','#49325F','#3C2E4E','#786887','#ECE2F4','#B6A0D4'],
  ['apricot','暖杏色','#FBF5EE','#90603D','#613F2C','#4D382A','#816D5C','#F3E5D5','#DFB38B'],
  ['slate','简约灰','#F3F5F7','#536776','#303F4B','#2D3A45','#667581','#E4EAEE','#A7BEC9']
];
export const clone = value => {const result=JSON.parse(JSON.stringify(value));if(result?.Cards&&typeof result.Cards==='object'&&!Array.isArray(result.Cards))Object.setPrototypeOf(result.Cards,null);return result;};
export const key = text => String(text).trim().toLowerCase();
export const iso = value => new Date(value).toISOString().replace(/\.(\d{3})Z$/, (_,ms) => '.'+ms+'0000Z');
export function day(value = new Date()) { const d=new Date(value); return [d.getFullYear(),String(d.getMonth()+1).padStart(2,'0'),String(d.getDate()).padStart(2,'0')].join('-'); }
const validDate = value => typeof value==='string' && /^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d/.test(value) && Number.isFinite(Date.parse(value));
const assert = (ok,message) => { if(!ok)throw new Error(message); };
const hasOwn = (object,property) => Object.prototype.hasOwnProperty.call(object,property);
const clamp = (value,lo,hi) => Math.max(lo,Math.min(hi,value));
const W=[.212,1.2931,2.3065,8.2956,6.4133,.8334,3.0194,.001,1.8722,.1666,.796,1.4835,.0614,.2629,1.6483,.6014,1.8729,.5425,.0912,.0658,.1542];
const initialDifficulty = rating => W[4]-Math.exp(W[5]*(rating-1))+1;
export const recall = (stability,days) => Math.pow(1+(Math.pow(.9,-1/W[20])-1)*Math.max(0,days)/clamp(stability,.001,36500),-W[20]);
export function updateMemory(card,rating,elapsed) {
  assert(Number.isInteger(rating)&&rating>=1&&rating<=4,'回忆评分无效。');
  let s=card.Stability||0,d=card.Difficulty||0;
  if(s<=0){s=W[rating-1];d=clamp(initialDifficulty(rating),1,10);}
  else {
    const nextD=clamp(W[7]*initialDifficulty(4)+(1-W[7])*(d+(10-d)*(-W[6]*(rating-3))/9),1,10);
    if(elapsed<1){let growth=Math.exp(W[17]*(rating-3+W[18]))*Math.pow(s,-W[19]);if(rating>=2)growth=Math.max(1,growth);s*=growth;}
    else {const r=recall(s,Math.floor(elapsed));if(rating===1)s=Math.min(W[11]*Math.pow(d,-W[12])*(Math.pow(s+1,W[13])-1)*Math.exp((1-r)*W[14]),s/Math.exp(W[17]*W[18]));else s*=1+Math.exp(W[8])*(11-d)*Math.pow(s,-W[9])*(Math.exp((1-r)*W[10])-1)*(rating===2?W[15]:1)*(rating===4?W[16]:1);}
    d=nextD;
  }
  card.Stability=clamp(s,.001,36500);card.Difficulty=d;
}
export function schedule(card,grade,now,isNew) {
  const date=new Date(now),legacy=!isNew&&!card.SchedulerVersion;
  if(legacy){const interval=card.Level===0?10/1440:[1,1,3,7,14,30,60][card.Level];Object.assign(card,{SchedulerVersion:1,Stability:Math.max(.1,interval),Difficulty:clamp(initialDifficulty(3),1,10),Stage:card.Level===0?'relearning':'review',IntervalDays:interval,LastReview:iso(Date.parse(card.Due)-interval*86400000)});}
  const previous=card.Stage,elapsed=isNew?0:Math.max(0,(date-Date.parse(card.LastReview))/86400000);
  updateMemory(card,grade+1,elapsed);card.SchedulerVersion=1;card.LastReview=iso(date);if(grade===0)card.Lapses++;
  if(isNew||grade===0||(['learning','relearning'].includes(previous)&&grade===1)){
    card.Stage=isNew?'learning':previous==='learning'?'learning':'relearning';card.Level=0;card.IntervalDays=10/1440;card.Due=iso(+date+600000);
    card.ScheduleReason=isNew?'初次接触：10 分钟后检验回忆。':grade===0?'未能独立想起：10 分钟后重新检验。':'短时巩固仍吃力：10 分钟后再检验。';
  } else {const days=clamp(Math.floor(card.Stability+.5),1,365);card.IntervalDays=days;card.Stage='review';card.Due=iso(+date+days*86400000);card.Level=days>=60?6:days>=30?5:days>=14?4:days>=7?3:days>=3?2:1;card.ScheduleReason=(grade===1?'想起但吃力':'独立想起')+'：根据本词回忆记录安排 '+days+' 天后复习。';}
  if(legacy)card.ScheduleReason+=' 旧记录以原间隔近似衔接。';
}
export const defaults = () => ({Version:1,SelectedBook:'cet4',DailyTarget:20,AutoSpeak:false,Voice:null,Cards:Object.create(null),Favorites:[],History:[],CustomWords:[],CustomBooks:[],ExcludedWords:[],PagePalette:'bamboo',PageStyle:'glass',GlassDensity:'low',LuminousIntensity:'soft',LuminousMotion:true});
export function normalize(input) {
  assert(input&&input.Version===1,'备份格式不正确或版本不受支持。');
  const d=Object.assign(Object.create(null),defaults(),clone(input));
  if(Array.isArray(d.Cards)){const cards=Object.create(null);for(const item of d.Cards){assert(item&&typeof item.Key==='string'&&item.Value&&!hasOwn(cards,item.Key),'卡片记录格式错误或存在重复单词。');cards[item.Key]=item.Value;}d.Cards=cards;}
  assert(d.Cards&&typeof d.Cards==='object'&&!Array.isArray(d.Cards),'学习卡片格式不正确。');
  assert(Number.isInteger(d.DailyTarget)&&d.DailyTarget>=5&&d.DailyTarget<=200,'每日目标应在 5 到 200 之间。');
  assert(typeof d.SelectedBook==='string'&&typeof d.AutoSpeak==='boolean'&&typeof d.LuminousMotion==='boolean'&&(d.Voice===null||typeof d.Voice==='string'),'学习设置格式不正确。');
  for(const field of ['Favorites','History','CustomWords','CustomBooks','ExcludedWords'])assert(Array.isArray(d[field]),'备份缺少 '+field+'。');
  assert(d.Favorites.every(x=>typeof x==='string')&&d.ExcludedWords.every(x=>typeof x==='string'&&x.trim()),'收藏或排除列表无效。');
  for(const [id,c] of Object.entries(d.Cards)){
    assert(id&&c&&Number.isInteger(c.Level)&&c.Level>=0&&c.Level<=6&&Number.isInteger(c.Reviews)&&c.Reviews>=0&&Number.isInteger(c.Lapses)&&c.Lapses>=0&&validDate(c.Due),'学习卡片记录无效。');
    if(c.SchedulerVersion)assert(c.SchedulerVersion===1&&['learning','relearning','review'].includes(c.Stage)&&Number.isFinite(c.Stability)&&c.Stability>=.001&&c.Stability<=36500&&Number.isFinite(c.Difficulty)&&c.Difficulty>=1&&c.Difficulty<=10&&Number.isFinite(c.IntervalDays)&&c.IntervalDays>0&&c.IntervalDays<=365&&validDate(c.LastReview),'复习模型记录无效。');
  }
  for(const w of d.CustomWords)assert(w&&typeof w.Id==='string'&&w.Id.startsWith('custom:')&&typeof w.Text==='string'&&w.Text.trim()&&w.Text.length<=150&&typeof w.Translation==='string'&&w.Translation.trim()&&w.Translation.length<=10000,'自定义词条无效。');
  for(const w of d.CustomWords){for(const field of ['Phonetic','Definition','Tags','Exchange','Example','ExampleTranslation'])assert(w[field]==null||typeof w[field]==='string','自定义词条字段格式无效。');assert(w.Rank==null||(Number.isInteger(w.Rank)&&w.Rank>=0),'自定义词条排序字段无效。');}
  assert(new Set(d.CustomWords.map(w=>w.Id)).size===d.CustomWords.length,'自定义词条 ID 重复。');
  for(const b of d.CustomBooks)assert(b&&typeof b.Id==='string'&&b.Id.startsWith('book:')&&typeof b.Name==='string'&&b.Name.trim()&&Array.isArray(b.WordIds)&&b.WordIds.every(id=>typeof id==='string'),'自制词书记录无效。');
  for(const h of d.History){assert(h&&typeof h.Id==='string'&&/^\d{4}-\d\d-\d\d$/.test(h.Day)&&Number.isInteger(h.Grade)&&h.Grade>=0&&h.Grade<=2&&typeof h.IsNew==='boolean'&&typeof h.Spelling==='boolean','学习历史无效。');for(const t of ['At','ScheduledDue'])assert(!h[t]||validDate(h[t]),'学习时间记录无效。');assert(!h.ElapsedDays||(Number.isFinite(h.ElapsedDays)&&h.ElapsedDays>=0),'作答间隔无效。');}
  if(!PALETTES.some(p=>p[0]===d.PagePalette))d.PagePalette='bamboo';
  if(!['glass','solid','luminous'].includes(d.PageStyle))d.PageStyle='glass';
  if(!['dense','low','medium','clear'].includes(d.GlassDensity))d.GlassDensity='low';
  if(!['soft','balanced','vivid'].includes(d.LuminousIntensity))d.LuminousIntensity='soft';
  d.ExcludedWords=[...new Set(d.ExcludedWords.map(key))].sort();return d;
}
export function serialize(data) { const d=normalize(data);d.Cards=Object.entries(d.Cards).map(([Key,Value])=>({Key,Value}));return JSON.stringify(d); }
export function parseDictionary(text) {
  return text.replace(/(?:\r?\n)+$/,'').split(/\r?\n/).map(line=>{const p=line.split('\t');assert(p.length>=8,'内置词库不完整。');return {Id:p[0],Text:p[1],Phonetic:p[2],Translation:p[3].replace(/\\n/g,'\n'),Definition:p[4].replace(/\\n/g,'\n'),Tags:p[5],Rank:+p[6]||0,Exchange:p[7],Example:'',ExampleTranslation:''};});
}
export function parseCsv(text,separator) {
  const rows=[],row=[];let field='',quoted=false;
  for(let i=0;i<text.length;i++){const c=text[i];if(c==='"'){if(quoted&&text[i+1]==='"'){field+='"';i++;}else if(quoted||!field)quoted=!quoted;else throw new Error('CSV 引号位置不正确。');}else if(c===separator&&!quoted){row.push(field);field='';}else if((c==='\n'||c==='\r')&&!quoted){if(c==='\r'&&text[i+1]==='\n')i++;row.push(field);rows.push([...row]);row.length=0;field='';}else field+=c;}
  assert(!quoted,'CSV 存在未闭合的引号。');if(field||row.length){row.push(field);rows.push(row);}return rows;
}
export function importBook(text,name,id='book:'+Date.now().toString(36)) {
  assert(new TextEncoder().encode(text).length<=20*1024*1024,'词书文件不能超过 20 MB。');
  const sep=text.split('\n')[0].includes('\t')?'\t':',',rows=parseCsv(text,sep);
  assert(rows.length>=2,'需要表头和至少一个词条。');const heads=rows[0].map(x=>x.trim().replace(/^\uFEFF/,'').toLowerCase());
  assert(heads.includes('word')&&heads.includes('translation'),'表头必须包含 word 和 translation。');const words=new Map();
  for(let i=1;i<rows.length;i++){const row=rows[i];if(row.every(x=>!x.trim()))continue;assert(row.length===heads.length,'第 '+(i+1)+' 行列数与表头不一致。');const field=h=>heads.includes(h)?row[heads.indexOf(h)].trim():'';const word=field('word'),translation=field('translation');assert(word&&translation&&word.length<=150&&translation.length<=10000,'存在空词条、空释义或过长字段，导入已取消。');const wordId='custom:'+key(word);words.set(wordId,{Id:wordId,Text:word,Translation:translation,Phonetic:field('phonetic'),Definition:field('definition'),Example:field('example'),ExampleTranslation:field('example_translation'),Tags:'custom',Rank:999999,Exchange:''});assert(words.size<=50000,'每次最多导入 50000 个词条。');}
  assert(words.size,'文件内没有有效词条。');return {words:[...words.values()],book:{Id:id,Name:name.trim().slice(0,100)||'我的词书',WordIds:[...words.keys()]}};
}
export const exportFavorites = store => '\uFEFFword,translation,phonetic,definition,example,example_translation\r\n'+store.data.Favorites.map(id=>store.words.get(id)).filter(Boolean).map(w=>[w.Text,w.Translation,w.Phonetic,w.Definition,w.Example,w.ExampleTranslation].map(s=>'"'+String(s||'').replaceAll('"','""')+'"').join(',')).join('\r\n');
export function exchangeText(value='') {const names={p:'过去式',d:'过去分词',i:'现在分词','3':'第三人称单数',r:'比较级',t:'最高级',s:'复数','0':'原形'};return value.split('/').map(x=>x.split(':')).filter(p=>names[p[0]]&&p[1]).map(p=>names[p[0]]+' '+p[1]).join(' · ');}
export class Store {
  constructor(words,data=defaults(),save=()=>{}) {
    this.base=words;this.persist=save;this.data=normalize(data);this.rebuild();
  }
  rebuild(){this.words=new Map(this.base.map(w=>[w.Id,w]));for(const w of this.data.CustomWords)this.words.set(w.Id,w);const sorted=[...this.base].sort((a,b)=>a.Rank-b.Rank||a.Text.localeCompare(b.Text));this.standard=TAGS.map((tag,i)=>({Id:tag,Name:BOOK_NAMES[i],WordIds:sorted.filter(w=>tag==='common'?w.Rank>0&&w.Rank<=3000:w.Tags.split(' ').includes(tag)).map(w=>w.Id)}));this.exclusions=new Set(this.data.ExcludedWords);this.validateKnown(this.data);}
  validateKnown(data){const known=new Set(this.base.map(w=>w.Id));for(const w of data.CustomWords)known.add(w.Id);assert(data.CustomBooks.every(b=>b.WordIds.every(id=>known.has(id))),'备份中存在缺失词条的词书。');}
  transact(change,rebuild=false){const next=clone(this.data);change(next);const valid=normalize(next);if(rebuild)this.validateKnown(valid);this.persist(valid);this.data=valid;this.exclusions=new Set(valid.ExcludedWords);if(rebuild)this.rebuild();}
  books(){return [...this.standard,...this.data.CustomBooks];}
  selected(){return this.books().find(b=>b.Id===this.data.SelectedBook)||this.standard[2];}
  isExcluded(id){const w=this.words.get(id);return !!w&&this.exclusions.has(key(w.Text));}
  isDue(id,now=new Date()){return !this.isExcluded(id)&&!!this.data.Cards[id]&&Date.parse(this.data.Cards[id].Due)<=+new Date(now);}
  due(now=new Date()){return Object.keys(this.data.Cards).filter(id=>this.words.has(id)&&this.isDue(id,now));}
  nextDue(){const times=Object.entries(this.data.Cards).filter(([id])=>this.words.has(id)&&!this.isExcluded(id)).map(([,c])=>Date.parse(c.Due));return times.length?new Date(Math.min(...times)):null;}
  newToday(now=new Date()){return new Set(this.data.History.filter(h=>h.Day===day(now)&&h.IsNew&&!h.Spelling).map(h=>h.Id)).size;}
  reviewedToday(now=new Date()){return new Set(this.data.History.filter(h=>h.Day===day(now)&&!h.IsNew&&!h.Spelling&&!h.Reinforcement).map(h=>h.Id)).size;}
  queue(review=false,spelling=false,now=new Date()) {
    const ids=this.selected().WordIds.filter(id=>this.words.has(id)&&!this.isExcluded(id));
    if(spelling){const counts=new Map();for(const h of this.data.History)if(h.Spelling)counts.set(h.Id,(counts.get(h.Id)||0)+1);const learned=ids.filter(id=>this.data.Cards[id]).sort((a,b)=>(counts.get(a)||0)-(counts.get(b)||0)||this.words.get(a).Rank-this.words.get(b).Rank).slice(0,20);return (learned.length?learned:ids.slice(0,10)).map(id=>this.words.get(id));}
    if(review)return this.due(now).sort((a,b)=>Date.parse(this.data.Cards[a].Due)-Date.parse(this.data.Cards[b].Due)||this.words.get(a).Rank-this.words.get(b).Rank||a.localeCompare(b)).slice(0,100).map(id=>this.words.get(id));
    return ids.filter(id=>!this.data.Cards[id]).slice(0,Math.max(0,this.data.DailyTarget-this.newToday(now))).map(id=>this.words.get(id));
  }
  grade(id,grade,spelling=false,now=new Date(),reinforcement=false) {
    assert(this.words.has(id)&&Number.isInteger(grade)&&grade>=0&&grade<=2,'单词或评分无效。');if(this.isExcluded(id))return null;
    const previous=this.data.Cards[id],isNew=!previous,practice=!spelling&&!isNew&&(reinforcement||Date.parse(previous.Due)>+new Date(now));
    const log={Id:id,Day:day(now),At:iso(now),Grade:grade,IsNew:isNew&&!spelling,Spelling:spelling,Reinforcement:practice,StageBefore:!previous?'new':!previous.SchedulerVersion?'legacy':previous.Stage,ElapsedDays:!previous?.LastReview?0:Math.max(0,(+new Date(now)-Date.parse(previous.LastReview))/86400000)};
    this.transact(d=>{if(!spelling){const c=d.Cards[id]||(d.Cards[id]={Level:0,Reviews:0,Lapses:0,Due:iso(now)});c.Reviews++;if(!practice){schedule(c,grade,now,isNew);log.ScheduleUpdated=true;}log.ScheduledDue=c.Due;log.StageAfter=c.SchedulerVersion?c.Stage:'legacy';}if(grade===0&&!d.Favorites.includes(id))d.Favorites.push(id);d.History.push(log);});return log;
  }
  toggleFavorite(id){assert(this.words.has(id),'没有找到这个单词。');this.transact(d=>{const i=d.Favorites.indexOf(id);if(i>=0)d.Favorites.splice(i,1);else d.Favorites.push(id);});}
  exclude(id,value=true){assert(this.words.has(id),'没有找到这个单词。');const text=key(this.words.get(id).Text);this.transact(d=>{d.ExcludedWords=d.ExcludedWords.filter(x=>x!==text);if(value)d.ExcludedWords.push(text);});}
  settings(values){this.transact(d=>{for(const field of ['SelectedBook','DailyTarget','AutoSpeak','Voice','PagePalette','PageStyle','GlassDensity','LuminousIntensity','LuminousMotion'])if(hasOwn(values,field))d[field]=values[field];});}
  search(query='',mode='all'){const q=key(query),seen=new Set();return [...this.words.values()].filter(w=>{if(mode==='favorites'&&!this.data.Favorites.includes(w.Id))return false;if(mode==='excluded'){const text=key(w.Text);if(!this.isExcluded(w.Id)||seen.has(text))return false;seen.add(text);}return !q||key(w.Text).includes(q)||w.Translation.includes(q);}).sort((a,b)=>{const rank=w=>key(w.Text)===q?0:key(w.Text).startsWith(q)?1:2;return rank(a)-rank(b)||a.Rank-b.Rank||a.Text.localeCompare(b.Text);}).slice(0,150);}
  import(text,name){const result=importBook(text,name,'book:'+Date.now().toString(36)+'-'+Math.random().toString(36).slice(2,8));this.transact(d=>{const replacements=new Set(result.words.map(w=>w.Id));d.CustomWords=d.CustomWords.filter(w=>!replacements.has(w.Id)).concat(result.words);d.CustomBooks.push(result.book);d.SelectedBook=result.book.Id;},true);return result.words.length;}
  restore(input){const next=normalize(input);this.validateKnown(next);this.persist(next);this.data=next;this.rebuild();}
}
