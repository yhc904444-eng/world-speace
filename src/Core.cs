using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Reflection;

namespace LexisDesk {
    [DataContract] public class Word {
        [DataMember] public string Id;
        [DataMember] public string Text;
        [DataMember] public string Phonetic;
        [DataMember] public string Translation;
        [DataMember] public string Definition;
        [DataMember] public string Tags;
        [DataMember] public int Rank;
        [DataMember] public string Exchange;
        [DataMember] public string Example;
        [DataMember] public string ExampleTranslation;
    }
    [DataContract] public class CardState {
        [DataMember] public int Level;
        [DataMember] public string Due;
        [DataMember] public int Reviews;
        [DataMember] public int Lapses;
        [DataMember(EmitDefaultValue=false)] public int SchedulerVersion;
        [DataMember(EmitDefaultValue=false)] public string Stage;
        [DataMember(EmitDefaultValue=false)] public double Stability;
        [DataMember(EmitDefaultValue=false)] public double Difficulty;
        [DataMember(EmitDefaultValue=false)] public double IntervalDays;
        [DataMember(EmitDefaultValue=false)] public string LastReview;
        [DataMember(EmitDefaultValue=false)] public string ScheduleReason;
    }
    [DataContract] public class AnswerLog {
        [DataMember] public string Day;
        [DataMember] public string Id;
        [DataMember] public int Grade;
        [DataMember] public bool IsNew;
        [DataMember] public bool Spelling;
        [DataMember(EmitDefaultValue=false)] public string At;
        [DataMember(EmitDefaultValue=false)] public string StageBefore;
        [DataMember(EmitDefaultValue=false)] public string StageAfter;
        [DataMember(EmitDefaultValue=false)] public bool Reinforcement;
        [DataMember(EmitDefaultValue=false)] public bool ScheduleUpdated;
        [DataMember(EmitDefaultValue=false)] public string ScheduledDue;
        [DataMember(EmitDefaultValue=false)] public double ElapsedDays;
    }
    [DataContract] public class Book {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember] public List<string> WordIds = new List<string>();
    }
    [DataContract] public class UserData {
        [DataMember] public int Version = 1;
        [DataMember] public string SelectedBook = "cet4";
        [DataMember] public int DailyTarget = 20;
        [DataMember] public bool AutoSpeak;
        [DataMember] public string Voice;
        [DataMember] public Dictionary<string, CardState> Cards = new Dictionary<string, CardState>();
        [DataMember] public List<string> Favorites = new List<string>();
        [DataMember] public List<AnswerLog> History = new List<AnswerLog>();
        [DataMember] public List<Word> CustomWords = new List<Word>();
        [DataMember] public List<Book> CustomBooks = new List<Book>();
        [DataMember] public List<string> ExcludedWords = new List<string>();
        [DataMember] public string PagePalette = "bamboo";
        [DataMember] public string PageStyle = "glass";
        [DataMember] public string GlassDensity = "low";
        [DataMember] public string LuminousIntensity = "soft";
        [DataMember] public bool LuminousMotion = true;
        [OnDeserializing] void PrepareAppearance(StreamingContext context) { LuminousMotion=true; }
        [OnDeserialized] void ReadLegacyData(StreamingContext context) {
            if(ExcludedWords==null)ExcludedWords=new List<string>();
            Appearance.Normalize(this);
        }
    }
    public static class JsonIO {
        public static T Read<T>(string path) {
            using(var f = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(f);
        }
        public static void Write<T>(string path, T obj) {
            var dir=Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(dir);
            var temp=path+".tmp";
            using(var f=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
                new DataContractJsonSerializer(typeof(T)).WriteObject(f,obj); f.Flush(true);
            }
            if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
        }
    }
    public class Store {
        public static readonly string[] Tags = {"zk","gk","cet4","cet6","ky","ielts","toefl","gre","common"};
        public static readonly string[] BookNames = {"中考词汇","高考词汇","大学英语四级","大学英语六级","考研英语","雅思词汇","托福词汇","GRE 词汇","常用高频词"};
        public Dictionary<string,Word> Words = new Dictionary<string,Word>(StringComparer.OrdinalIgnoreCase);
        public UserData Data;
        public string DirectoryPath;
        public string LoadNotice;
        HashSet<string> exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<Book> standardBooks;
        public string StatePath { get { return Path.Combine(DirectoryPath,"progress.json"); } }
        public Store(string directory,Action<double,string> progress=null) {
            Action<double,string> report=progress??((value,message)=>{});
            report(5,"正在打开离线词库…");
            DirectoryPath=directory;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("words.tsv")) {
                if(stream==null) throw new InvalidDataException("缺少内置离线词库。");
                using(var reader=new StreamReader(stream,Encoding.UTF8)) {
                    string line;int loaded=0; while((line=reader.ReadLine())!=null) {
                        var p=line.Split('\t'); if(p.Length<8) continue;
                        int rank; int.TryParse(p[6],out rank);
                        var w=new Word{Id=p[0],Text=p[1],Phonetic=p[2],Translation=Unescape(p[3]),Definition=Unescape(p[4]),Tags=p[5],Rank=rank,Exchange=p[7]};
                        Words[w.Id]=w;
                        if(++loaded%512==0)report(10+66.0*stream.Position/stream.Length,"正在载入词条…");
                    }
                }
            }
            report(80,"正在读取学习记录…");
            if(File.Exists(StatePath)) {
                try { Data=JsonIO.Read<UserData>(StatePath); Validate(Data); }
                catch(Exception e) {
                    try { Data=JsonIO.Read<UserData>(StatePath+".bak"); Validate(Data); LoadNotice="主进度文件无法读取，已从上次备份恢复。"; }
                    catch { throw new InvalidDataException("学习记录损坏，原文件已保留。请将 progress.json 和 .bak 文件备份后再处理。\n"+e.Message); }
                }
            } else Data=new UserData();
            report(89,"正在整理词书与复习计划…");MergeCustom();BuildStandardBooks();RebuildExclusions();report(93,"词库与学习记录已就绪");
        }
        public static string Unescape(string s) { return (s??"").Replace("\\n","\n"); }
        public static void Validate(UserData d) {
            if(d==null || d.Version!=1 || d.Cards==null || d.Favorites==null || d.History==null || d.CustomWords==null || d.CustomBooks==null || d.ExcludedWords==null) throw new InvalidDataException("备份格式不正确或版本不受支持。");
            Appearance.Normalize(d);
            if(d.ExcludedWords.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("永不复习列表中存在无效词条。");
            if(d.DailyTarget<5 || d.DailyTarget>200) throw new InvalidDataException("每日目标应在 5 到 200 之间。");
            if(d.Cards.Any(x=>x.Value==null || x.Value.Level<0 || x.Value.Level>6 || x.Value.Reviews<0 || x.Value.Lapses<0 || !ValidDate(x.Value.Due)||!ReviewScheduler.Valid(x.Value))) throw new InvalidDataException("学习卡片记录无效。");
            if(d.CustomWords.Any(w=>w==null || string.IsNullOrWhiteSpace(w.Id) || string.IsNullOrWhiteSpace(w.Text) || string.IsNullOrWhiteSpace(w.Translation))) throw new InvalidDataException("自定义词条无效。");
            if(d.CustomBooks.Any(b=>b==null || string.IsNullOrWhiteSpace(b.Id) || string.IsNullOrWhiteSpace(b.Name) || b.WordIds==null) || d.History.Any(h=>h==null || string.IsNullOrEmpty(h.Id) || !ValidDay(h.Day) || h.Grade<0 || h.Grade>2)) throw new InvalidDataException("词书或历史记录无效。");
            if(d.History.Any(h=>(h.At!=null&&!ValidDate(h.At))||(h.ScheduledDue!=null&&!ValidDate(h.ScheduledDue))||double.IsNaN(h.ElapsedDays)||double.IsInfinity(h.ElapsedDays)||h.ElapsedDays<0))throw new InvalidDataException("作答时间记录无效。");
        }
        static bool ValidDate(string s) { DateTime t; return DateTime.TryParseExact(s,"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out t); }
        static bool ValidDay(string s) { DateTime t; return DateTime.TryParseExact(s,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out t); }
        public void MergeCustom() { foreach(var w in Data.CustomWords) Words[w.Id]=w; standardBooks=null; }
        public void Save() { Validate(Data); JsonIO.Write(StatePath,Data); }
        public void SetAppearance(string palette,string style) {
            SetAppearance(palette,style,Data.GlassDensity,Data.LuminousIntensity,Data.LuminousMotion);
        }
        public void SetAppearance(string palette,string style,string density,string intensity,bool motion) {
            string beforePalette=Data.PagePalette,beforeStyle=Data.PageStyle,beforeDensity=Data.GlassDensity,beforeIntensity=Data.LuminousIntensity;bool beforeMotion=Data.LuminousMotion;
            Data.PagePalette=palette;Data.PageStyle=style;Data.GlassDensity=density;Data.LuminousIntensity=intensity;Data.LuminousMotion=motion;Appearance.Normalize(Data);
            if(beforePalette==Data.PagePalette&&beforeStyle==Data.PageStyle&&beforeDensity==Data.GlassDensity&&beforeIntensity==Data.LuminousIntensity&&beforeMotion==Data.LuminousMotion)return;
            try{Save();}catch{Data.PagePalette=beforePalette;Data.PageStyle=beforeStyle;Data.GlassDensity=beforeDensity;Data.LuminousIntensity=beforeIntensity;Data.LuminousMotion=beforeMotion;throw;}
        }
        static string WordKey(string text) {return text.Trim().ToLowerInvariant();}
        void RebuildExclusions() {
            exclusions=new HashSet<string>(Data.ExcludedWords.Select(WordKey),StringComparer.OrdinalIgnoreCase);
            Data.ExcludedWords=exclusions.OrderBy(x=>x).ToList();
        }
        public bool IsExcluded(string id) {Word w;return Words.TryGetValue(id,out w)&&exclusions.Contains(WordKey(w.Text));}
        public void SetExcluded(string id,bool excluded) {
            Word w;if(!Words.TryGetValue(id,out w))throw new InvalidOperationException("未找到这个单词。");
            string key=WordKey(w.Text);bool previous=exclusions.Contains(key);
            if(previous==excluded)return;
            if(excluded)exclusions.Add(key);else exclusions.Remove(key);
            Data.ExcludedWords=exclusions.OrderBy(x=>x).ToList();
            try{Save();}catch{if(previous)exclusions.Add(key);else exclusions.Remove(key);Data.ExcludedWords=exclusions.OrderBy(x=>x).ToList();throw;}
        }
        void BuildStandardBooks() {
            // The embedded dictionary is stable: classify it once while loading,
            // rather than sorting all nine collections for each page/card.
            var sorted=Words.Values.Where(w=>!(w.Id??"").StartsWith("custom:")).OrderBy(w=>w.Rank).ThenBy(w=>w.Text).ToList();
            standardBooks=new List<Book>();
            for(int i=0;i<Tags.Length;i++) {
                string tag=Tags[i];
                var query=sorted.Where(w=>tag=="common" ? w.Rank>0 && w.Rank<=3000 : (" "+w.Tags+" ").Contains(" "+tag+" "));
                standardBooks.Add(new Book{Id=tag,Name=BookNames[i],WordIds=query.Select(w=>w.Id).ToList()});
            }
        }
        public List<Book> Books() {
            if(standardBooks==null)BuildStandardBooks();
            var all=new List<Book>(standardBooks);
            all.AddRange(Data.CustomBooks); return all;
        }
        public Book Selected() {
            if(standardBooks==null)BuildStandardBooks();
            return standardBooks.FirstOrDefault(b=>b.Id==Data.SelectedBook)??Data.CustomBooks.FirstOrDefault(b=>b.Id==Data.SelectedBook)??standardBooks[2];
        }
        public static string Day(DateTime now) { return now.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture); }
        public int NewToday(DateTime now) { return Data.History.Where(h=>h.Day==Day(now)&&h.IsNew&&!h.Spelling).Select(h=>h.Id).Distinct().Count(); }
        public int ReviewedToday(DateTime now) { return Data.History.Where(h=>h.Day==Day(now)&&!h.IsNew&&!h.Spelling&&!h.Reinforcement).Select(h=>h.Id).Distinct().Count(); }
        public bool IsDue(string id,DateTime now) { CardState c; return !IsExcluded(id)&&Data.Cards.TryGetValue(id,out c) && ReviewScheduler.Parse(c.Due).ToUniversalTime()<=now.ToUniversalTime(); }
        public int DueCount(DateTime now){return Data.Cards.Keys.Count(id=>Words.ContainsKey(id)&&IsDue(id,now));}
        public DateTime? NextDue(){var dates=Data.Cards.Where(x=>Words.ContainsKey(x.Key)&&!IsExcluded(x.Key)).Select(x=>ReviewScheduler.Parse(x.Value.Due));return dates.Any()?(DateTime?)dates.Min():null;}
        public List<Word> Queue(bool review,bool spell,DateTime now) {
            var ids=Selected().WordIds.Where(id=>Words.ContainsKey(id)&&!IsExcluded(id));
            if(spell) {
                var tested=Data.History.Where(h=>h.Spelling).GroupBy(h=>h.Id).ToDictionary(g=>g.Key,g=>g.Count());
                var learned=ids.Where(id=>Data.Cards.ContainsKey(id)).OrderBy(id=>tested.ContainsKey(id)?tested[id]:0).ThenBy(id=>Words[id].Rank).Take(20).ToList();
                if(learned.Count==0) learned=ids.Take(10).ToList();
                return learned.Select(id=>Words[id]).ToList();
            }
            if(review) return Data.Cards.Keys.Where(id=>Words.ContainsKey(id)&&IsDue(id,now)).OrderBy(id=>ReviewScheduler.Parse(Data.Cards[id].Due).ToUniversalTime()).ThenBy(id=>Words[id].Rank).ThenBy(id=>id).Take(100).Select(id=>Words[id]).ToList();
            return ids.Where(id=>!Data.Cards.ContainsKey(id)).Take(Math.Max(0,Data.DailyTarget-NewToday(now))).Select(id=>Words[id]).ToList();
        }
        public AnswerLog Grade(Word w,int grade,bool spelling,DateTime now,bool reinforcement=false) {
            if(grade<0 || grade>2) throw new ArgumentOutOfRangeException("grade");
            if(IsExcluded(w.Id))return null;
            bool isNew=!Data.Cards.ContainsKey(w.Id);
            CardState previous;Data.Cards.TryGetValue(w.Id,out previous);
            bool practice=!spelling&&!isNew&&(reinforcement||ReviewScheduler.Parse(previous.Due).ToUniversalTime()>now.ToUniversalTime());
            var log=new AnswerLog{Id=w.Id,Day=Day(now),At=now.ToString("o"),Grade=grade,IsNew=isNew&&!spelling,Spelling=spelling,Reinforcement=practice,StageBefore=previous==null?"new":previous.SchedulerVersion==0?"legacy":previous.Stage,ElapsedDays=previous==null||string.IsNullOrEmpty(previous.LastReview)?0:ReviewScheduler.Elapsed(ReviewScheduler.Parse(previous.LastReview),now)};
            if(!spelling) {
                CardState c; if(!Data.Cards.TryGetValue(w.Id,out c)) { c=new CardState(); Data.Cards[w.Id]=c; }
                c.Reviews++;
                if(!practice){ReviewScheduler.Schedule(c,grade,now,isNew);log.ScheduleUpdated=true;}
                log.ScheduledDue=c.Due;log.StageAfter=c.SchedulerVersion==0?"legacy":c.Stage;
            }
            if(grade==0 && !Data.Favorites.Contains(w.Id)) Data.Favorites.Add(w.Id);
            Data.History.Add(log);Save();return log;
        }
        public void ToggleFavorite(string id) { if(!Data.Favorites.Remove(id)) Data.Favorites.Add(id); Save(); }
        public List<Word> Search(string query,bool favorites,bool excludedOnly=false) {
            query=(query??"").Trim();
            IEnumerable<Word> ws=Words.Values;
            if(favorites) { var set=new HashSet<string>(Data.Favorites); ws=ws.Where(w=>set.Contains(w.Id)); }
            if(excludedOnly)ws=ws.Where(w=>IsExcluded(w.Id)).GroupBy(w=>WordKey(w.Text)).Select(g=>g.OrderBy(w=>w.Id.StartsWith("custom:")?1:0).First());
            if(query.Length>0) ws=ws.Where(w=>w.Text.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0 || w.Translation.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
            return ws.OrderBy(w=>w.Text.Equals(query,StringComparison.OrdinalIgnoreCase)?0:w.Text.StartsWith(query,StringComparison.OrdinalIgnoreCase)?1:2).ThenBy(w=>w.Rank).ThenBy(w=>w.Text).Take(150).ToList();
        }
        public int Import(string path,string name) {
            if(new FileInfo(path).Length>20*1024*1024) throw new InvalidDataException("词书文件不能超过 20 MB。");
            var text=File.ReadAllText(path, new UTF8Encoding(false,true));
            var first=text.Split('\n')[0]; char sep=first.Contains("\t")?'\t':',';
            var rows=Csv.Parse(text,sep);
            if(rows.Count<2) throw new InvalidDataException("需要一行表头和至少一个词条。");
            var heads=rows[0].Select(s=>s.Trim().Trim('\uFEFF').ToLowerInvariant()).ToList();
            int wi=heads.IndexOf("word"), ti=heads.IndexOf("translation");
            if(wi<0 || ti<0) throw new InvalidDataException("表头必须包含 word 和 translation。可先使用软件提供的 CSV 模板。");
            var imported=new Dictionary<string,Word>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in rows.Skip(1)) {
                if(row.All(string.IsNullOrWhiteSpace)) continue;
                if(row.Count!=heads.Count) throw new InvalidDataException("第 "+(rows.IndexOf(row)+1)+" 行列数与表头不一致，请检查逗号和引号。");
                string word=row[wi].Trim(), trans=row[ti].Trim();
                if(word.Length==0 || trans.Length==0 || word.Length>150 || trans.Length>10000) throw new InvalidDataException("存在空词条、空释义或过长字段，导入已取消。");
                var id="custom:"+word.ToLowerInvariant();
                Func<string,string> field=h=>{int n=heads.IndexOf(h);return n<0?"":row[n].Trim();};
                imported[id]=new Word{Id=id,Text=word,Translation=trans,Phonetic=field("phonetic"),Definition=field("definition"),Example=field("example"),ExampleTranslation=field("example_translation"),Tags="custom",Rank=999999,Exchange=""};
                if(imported.Count>50000) throw new InvalidDataException("每次最多导入 50000 个词条。");
            }
            if(imported.Count==0) throw new InvalidDataException("文件内没有有效词条。");
            var book=new Book{Id="book:"+Guid.NewGuid().ToString("N"),Name=name,WordIds=imported.Keys.ToList()};
            foreach(var w in imported.Values) { Data.CustomWords.RemoveAll(x=>x.Id==w.Id);Data.CustomWords.Add(w);Words[w.Id]=w; }
            Data.CustomBooks.Add(book); Data.SelectedBook=book.Id;Save(); return imported.Count;
        }
        public void Restore(string path) {
            var restored=JsonIO.Read<UserData>(path); Validate(restored);
            var known=new HashSet<string>(Words.Values.Where(w=>!w.Id.StartsWith("custom:")).Select(w=>w.Id));
            foreach(var w in restored.CustomWords) known.Add(w.Id);
            if(restored.CustomBooks.Any(b=>b.WordIds.Any(id=>!known.Contains(id)))) throw new InvalidDataException("备份中存在缺失词条的词书。");
            Directory.CreateDirectory(DirectoryPath);
            if(File.Exists(StatePath)) File.Copy(StatePath,Path.Combine(DirectoryPath,"before-restore-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".json"),false);
            foreach(var id in Words.Keys.Where(id=>id.StartsWith("custom:")).ToList()) Words.Remove(id);
            Data=restored;MergeCustom();RebuildExclusions();Save();
        }
        public static string ExchangeText(string s) {
            var map=new Dictionary<string,string>{{"p","过去式"},{"d","过去分词"},{"i","现在分词"},{"3","第三人称单数"},{"r","比较级"},{"t","最高级"},{"s","复数"},{"0","原形"}};
            var parts=new List<string>(); foreach(var item in (s??"").Split('/')) {var p=item.Split(':');if(p.Length==2&&map.ContainsKey(p[0]))parts.Add(map[p[0]]+" "+p[1]);} return string.Join("  ·  ",parts);
        }
    }
    public static class Csv {
        public static List<List<string>> Parse(string text,char separator) {
            var rows=new List<List<string>>();var row=new List<string>();var field=new StringBuilder();bool quoted=false;
            for(int i=0;i<text.Length;i++) {
                char c=text[i];
                if(c=='"') { if(quoted && i+1<text.Length && text[i+1]=='"') {field.Append('"');i++;} else if(quoted || field.Length==0) quoted=!quoted; else throw new InvalidDataException("CSV 引号位置不正确。"); }
                else if(c==separator&&!quoted) {row.Add(field.ToString());field.Clear();}
                else if((c=='\n'||c=='\r')&&!quoted) {if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(field.ToString());rows.Add(row);row=new List<string>();field.Clear();}
                else field.Append(c);
            }
            if(quoted) throw new InvalidDataException("CSV 存在未闭合的引号。");
            if(field.Length>0||row.Count>0) {row.Add(field.ToString());rows.Add(row);} return rows;
        }
        public static string Quote(string s) { return "\""+(s??"").Replace("\"","\"\"")+"\""; }
    }
}
