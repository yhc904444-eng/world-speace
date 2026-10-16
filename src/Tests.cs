using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Speech.Synthesis;

namespace LexisDesk {
    public static class Tests {
        static List<string> passed=new List<string>();
        static void Check(bool ok,string name){if(!ok)throw new Exception("FAILED: "+name);passed.Add("PASS: "+name);}
        public static int Run(string root){
            var dir=Path.Combine(root,"test-runs",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(dir);
            var s=new Store(Path.Combine(dir,"state"));var now=new DateTime(2026,10,15,12,0,0,DateTimeKind.Local);
            Check(s.Words.Count>15000,"real embedded offline dictionary loaded");
            Check(s.Books().Count==9&&s.Books().All(b=>b.WordIds.Count>500),"nine nonempty book collections");
            Check(s.Books().All(b=>b.WordIds.SequenceEqual(s.Words.Values.Where(item=>!item.Id.StartsWith("custom:")&&(b.Id=="common"?item.Rank>0&&item.Rank<=3000:(" "+item.Tags+" ").Contains(" "+b.Id+" "))).OrderBy(item=>item.Rank).ThenBy(item=>item.Text).Select(item=>item.Id))),"prebuilt book membership and ordering match dictionary tags and frequency");
            string selected=s.Data.SelectedBook;s.Data.SelectedBook="missing-book";Check(s.Selected().Id=="cet4","missing book selection falls back to CET4");s.Data.SelectedBook=selected;
            Check(s.Words["computer"].Translation.Length>0&&s.Search("computer",false).First().Id=="computer","exact dictionary match comes first");
            Check(s.Search("计算机",false).Count>0,"Chinese definition search");
            var q=s.Queue(false,false,now);Check(q.Count==20&&q.Select(item=>item.Id).Distinct().Count()==20,"daily unseen word queue");
            var w=q[0];s.Grade(w,2,false,now);
            Check(s.NewToday(now)==1&&s.Data.Cards[w.Id].Stage=="learning"&&!s.IsDue(w.Id,now.AddMinutes(9)),"new word counted once and enters short-term learning");
            Check(s.IsDue(w.Id,now.AddMinutes(10)),"new learning becomes due after ten elapsed minutes");
            var state=s.Data.Cards[w.Id];Check(DateTime.Parse(state.Due)==now.AddMinutes(10),"first remembered answer preserves a real short learning step");
            s.Grade(w,2,false,now.AddMinutes(10));Check(state.Stage=="review"&&DateTime.Parse(state.Due)>now.AddDays(1),"successful delayed learning graduates to model-based review");
            var due=ReviewScheduler.Parse(state.Due);double difficulty=state.Difficulty;s.Grade(w,1,false,due);Check(state.Stage=="review"&&state.Difficulty>difficulty&&DateTime.Parse(state.Due)>due,"effortful recall updates difficulty and schedules a model-based interval");
            s.Grade(q[1],0,false,now);Check(s.Data.Favorites.Contains(q[1].Id)&&!s.IsDue(q[1].Id,now.AddMinutes(9))&&s.IsDue(q[1].Id,now.AddMinutes(10)),"forgotten word favorited and due in ten minutes");
            Check(s.Queue(true,false,now.AddMinutes(11)).Any(x=>x.Id==q[1].Id),"review queue contains due card");
            s.Grade(q[2],0,true,now);Check(!s.Data.Cards.ContainsKey(q[2].Id)&&s.Data.Favorites.Contains(q[2].Id)&&s.NewToday(now)==2,"spelling logged without changing SRS or new-word count");
            var reloaded=new Store(s.DirectoryPath);Check(reloaded.Data.History.Count==5&&reloaded.Data.Favorites.Count==2,"progress and favorites survive restart");
            for(int i=2;i<20;i++)s.Grade(q[i],2,false,now);Check(s.Queue(false,false,now).Count==0&&s.NewToday(now)==20,"daily limit enforced across session restarts");
            s.Data.DailyTarget=30;s.Save();Check(s.Queue(false,false,now).Count==10,"daily target change applies immediately");
            var parsed=Csv.Parse("word,translation\r\n\"hello, world\",\"第一行\r\n第二行\"\r\n",',');Check(parsed.Count==2&&parsed[1][0]=="hello, world"&&parsed[1][1].Contains("第二行"),"CSV quoted commas and multiline fields");
            bool rejected=false;try{Csv.Parse("a,\"unclosed",',');}catch(InvalidDataException){rejected=true;}Check(rejected,"broken CSV rejected");
            var template=Path.Combine(dir,"template.csv");File.WriteAllText(template,MainWindow.TemplateText,new UTF8Encoding(true));int count=s.Import(template,"测试词书");Check(count==2&&s.Selected().Name=="测试词书"&&s.Words["custom:resilient"].Example.Length>0,"CSV import with bilingual examples");
            Check(s.Books().Count==10&&s.Books().Last().Id==s.Selected().Id&&s.Books().Take(9).All(b=>!b.WordIds.Any(id=>id.StartsWith("custom:"))),"import appears immediately without contaminating built-in books");
            var bad=Path.Combine(dir,"invalid.csv");File.WriteAllText(bad,"word,translation\nvalid,释义\nempty,\n",new UTF8Encoding(false));int before=s.Data.CustomWords.Count;rejected=false;try{s.Import(bad,"不应导入");}catch(InvalidDataException){rejected=true;}Check(rejected&&s.Data.CustomWords.Count==before,"invalid import leaves existing data unchanged");
            var tsv=Path.Combine(dir,"book.tsv");File.WriteAllText(tsv,"word\ttranslation\ntranquil\t宁静的\n",new UTF8Encoding(false));Check(s.Import(tsv,"TSV词书")==1,"TSV import");
            var back=Path.Combine(dir,"backup.json");JsonIO.Write(back,s.Data);s.Data.Favorites.Clear();s.Data.CustomBooks.Clear();s.Save();s.Restore(back);Check(s.Data.Favorites.Count>0&&s.Data.CustomBooks.Count==2,"full backup restores custom books and favorites");
            Check(s.Books().Count==11&&s.Selected().Name=="TSV词书"&&s.Selected().WordIds.Single()=="custom:tranquil","restored books and selection refresh after cached books were used");
            Check(Directory.GetFiles(s.DirectoryPath,"before-restore-*.json").Length==1,"pre-restore safety copy preserved");
            var badJson=Path.Combine(dir,"bad.json");File.WriteAllText(badJson,"{}");rejected=false;try{s.Restore(badJson);}catch{rejected=true;}Check(rejected&&s.Data.CustomBooks.Count==2,"invalid backup rejected without replacing current data");
            var recover=new Store(Path.Combine(dir,"recovery"));recover.Save();recover.Data.DailyTarget=30;recover.Save();File.WriteAllText(recover.StatePath,"broken json");var restored=new Store(recover.DirectoryPath);Check(restored.LoadNotice!=null&&restored.Data.DailyTarget==20,"corrupted primary progress recovers from atomic backup");
            Check(Store.ExchangeText("p:went/d:gone/i:going/3:goes").Contains("过去式 went"),"word forms decoded");
            File.WriteAllLines(Path.Combine(root,"self-test-results.txt"),passed,new UTF8Encoding(false));return 0;
        }
        static void Click(MainWindow w,string title){
            var b=FindButton((DependencyObject)w.Content,title);if(b==null)throw new Exception("Button not found: "+title);
            var peer=new ButtonAutomationPeer(b);((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            w.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background,new Action(()=>{}));
        }
        static Button FindButton(DependencyObject obj,string title){var b=obj as Button;if(b!=null&&Convert.ToString(b.Content)==title)return b;int n=System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj);for(int i=0;i<n;i++){var found=FindButton(System.Windows.Media.VisualTreeHelper.GetChild(obj,i),title);if(found!=null)return found;}return null;}
        public static void Exclusions(string root,Application app) {
            var dir=Path.Combine(root,"exclusion-tests",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(dir);
            var s=new Store(Path.Combine(dir,"core"));var now=DateTime.Now;var initial=s.Queue(false,false,now);var unseen=initial[0];var due=initial[1];
            s.SetExcluded(unseen.Id,true);Check(!s.Queue(false,false,now).Any(x=>x.Id==unseen.Id)&&s.NewToday(now)==0,"exclude unseen word without spending daily target");
            s.Grade(due,0,false,now);int history=s.Data.History.Count;s.SetExcluded(due.Id,true);
            Check(!s.IsDue(due.Id,now.AddDays(10))&&!s.Queue(true,false,now.AddDays(10)).Any(x=>x.Id==due.Id),"excluded overdue word absent from due count and review queue");
            Check(!s.Queue(false,true,now).Any(x=>s.IsExcluded(x.Id)),"both learned and fallback spelling queues omit excluded words");
            s.Grade(due,2,false,now);Check(s.Data.History.Count==history&&s.Data.Cards[due.Id].Level==0,"stale grading cannot reschedule excluded word or erase history");
            Check(s.Search(unseen.Text,false).Any(x=>x.Id==unseen.Id)&&s.Search("",false,true).Count==2,"excluded words remain searchable and available in management list");
            var reload=new Store(s.DirectoryPath);Check(reload.IsExcluded(unseen.Id)&&reload.IsExcluded(due.Id),"exclusions survive restart");
            var backup=Path.Combine(dir,"backup.json");JsonIO.Write(backup,s.Data);s.SetExcluded(unseen.Id,false);s.Restore(backup);Check(s.IsExcluded(unseen.Id),"full backup restores exclusion list");
            s.SetExcluded(due.Id,false);Check(s.IsDue(due.Id,now.AddMinutes(11))&&s.Data.Cards[due.Id].Level==0,"restore word retains prior review schedule and history");
            var file=Path.Combine(dir,"import.csv");File.WriteAllText(file,"word,translation\r\n"+unseen.Text.ToUpperInvariant()+",同名单词\r\n",new UTF8Encoding(true));s.Import(file,"同名测试");var imported=s.Selected().WordIds[0];
            Check(s.IsExcluded(imported)&&s.Queue(false,false,now).Count==0&&s.Queue(false,true,now).Count==0,"same word excluded across built-in books and uppercase imports");
            s.SetExcluded(imported,false);Check(!s.IsExcluded(unseen.Id)&&s.Queue(false,false,now).Count==1,"restore imported equivalent restores same English word globally");
            var noExclusions=new UserData();var legacyPath=Path.Combine(dir,"oldformat","progress.json");JsonIO.Write(legacyPath,noExclusions);var oldText=File.ReadAllText(legacyPath).Replace("\"ExcludedWords\":[],","");File.WriteAllText(legacyPath,oldText);
            Check(!oldText.Contains("ExcludedWords"),"legacy fixture has no exclusion field");var oldStore=new Store(Path.GetDirectoryName(legacyPath));Check(oldStore.Data.ExcludedWords.Count==0,"legacy progress files load with an empty exclusion list");
            s.SetExcluded(unseen.Id,true);s.Restore(legacyPath);Check(s.Data.ExcludedWords.Count==0&&!s.IsExcluded(unseen.Id),"restoring legacy backup clears cached exclusions consistently");
            var uiStore=new Store(Path.Combine(dir,"ui"));uiStore.Data.DailyTarget=5;
            var first=uiStore.Queue(false,false,now)[0];uiStore.Grade(first,0,false,now);uiStore.Data.Cards[first.Id].Due=now.AddMinutes(-1).ToString("o");uiStore.Save();
            var w=new MainWindow(uiStore){ThrowOnError=true,ShowInTaskbar=false,Left=-16000,Top=-16000,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();
            try {
                w.StartSession(true,false);w.RenderTo(Path.Combine(dir,"01-review-button.png"));Click(w,"永不复习");Check(uiStore.IsExcluded(first.Id)&&FindButton((DependencyObject)w.Content,"回到今日")!=null,"exclude last review card immediately completes current session");
                w.Navigate("excluded");w.RenderTo(Path.Combine(dir,"02-management.png"));Click(w,"恢复复习");Check(!uiStore.IsExcluded(first.Id)&&uiStore.IsDue(first.Id,now),"management restore button reactivates existing schedule");
                w.Navigate("dictionary");w.UpdateLayout();Click(w,"永不复习");Check(uiStore.Data.ExcludedWords.Count==1,"dictionary detail exclusion button works");
                w.Navigate("today");var pending=uiStore.Queue(false,false,now);var skipped=pending[0];w.StartSession(false,false);w.UpdateLayout();Click(w,"永不复习");Check(uiStore.IsExcluded(skipped.Id)&&!uiStore.Data.Cards.ContainsKey(skipped.Id),"study exclusion advances without creating an answer");
                for(int i=1;i<pending.Count;i++){w.UpdateLayout();Click(w,"显示释义  [空格]");Click(w,"3  认识");}Check(FindButton((DependencyObject)w.Content,"回到今日")!=null,"rest of session completes normally after exclusion");
                w.Navigate("today");w.StartSession(false,true);w.UpdateLayout();int spellingHistory=uiStore.Data.History.Count;Click(w,"永不复习");Check(uiStore.Data.History.Count==spellingHistory,"spelling exclusion advances without logging a failed answer");
                w.Navigate("excluded");w.RenderTo(Path.Combine(dir,"03-excluded-list.png"));w.Navigate("today");w.RenderTo(Path.Combine(dir,"04-today.png"));
                w.Width=1040;w.Height=720;w.Navigate("dictionary");w.RenderTo(Path.Combine(dir,"05-narrow-detail.png"));
                uiStore.SetExcluded(first.Id,false);uiStore.Data.CustomBooks.Add(new Book{Id="test-repeat",Name="单词重现测试",WordIds=new List<string>{first.Id}});uiStore.Data.SelectedBook="test-repeat";uiStore.Data.Cards[first.Id].Due=DateTime.Now.AddMinutes(-1).ToString("o");uiStore.Save();
                w.Navigate("today");w.StartSession(true,false);w.UpdateLayout();Click(w,"显示释义  [空格]");Click(w,"1  忘记");w.UpdateLayout();Click(w,"永不复习");
                Check(FindButton((DependencyObject)w.Content,"回到今日")!=null&&uiStore.IsExcluded(first.Id),"excluding forgotten repeated card removes all queued repeats and completes session");
            }finally{w.Close();}
            File.WriteAllLines(Path.Combine(root,"exclusion-test-results.txt"),passed,new UTF8Encoding(false));
        }
        public static void Navigation(string root,Application app){
            var dir=Path.Combine(root,"navigation-tests",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(dir);
            var store=new Store(Path.Combine(dir,"state"));store.Data.SelectedBook="cet6";var w=new MainWindow(store){ThrowOnError=true,ShowInTaskbar=false,Left=-16000,Top=-16000,WindowStartupLocation=WindowStartupLocation.Manual};w.Show();
            var trace=new List<string>();Action<string> step=s=>{trace.Add(s);File.WriteAllLines(Path.Combine(root,"navigation-trace.txt"),trace);};
            try {
                step("start learning 20 words");w.StartSession(false,false);w.UpdateLayout();
                for(int i=0;i<21;i++){if(i==0||i==5||i==20)Click(w,"▶  朗读   [P]");Click(w,"显示释义  [空格]");Click(w,i==0?"1  忘记":"3  认识");}
                Check(store.Data.Cards.Count==20,"complete all 20 learning words via UI buttons");
                foreach(var label in new[]{"查看学习记录","◷   今日学习","▤   我的词书","⌕   离线词典","☆   生词收藏","⌕   离线词典","⚙   设置与备份","◷   今日学习"}){step("click "+label);w.UpdateLayout();Click(w,label);w.UpdateLayout();}
                Check(true,"all navigation pages after completing 20 words");
                step("spelling all 20 words");w.StartSession(false,true);w.UpdateLayout();for(int i=0;i<20;i++){w.TestSpelling("wrong");w.UpdateLayout();Click(w,"下一个  →");}
                foreach(var label in new[]{"回到今日","☆   生词收藏","⌕   离线词典","☆   生词收藏","⚙   设置与备份","▥   学习记录"}){step("click "+label);w.UpdateLayout();Click(w,label);w.UpdateLayout();}
                Check(true,"all navigation pages after completing 20 spelling words");
                File.WriteAllLines(Path.Combine(root,"navigation-test-results.txt"),passed);
            }finally{w.Close();}
        }
        public static void Render(string root,Application app){
            var dir=Path.Combine(root,"render-tests");Directory.CreateDirectory(dir);var store=new Store(Path.Combine(dir,"state-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")));var w=new MainWindow(store);
            w.ShowInTaskbar=false;w.Left=-16000;w.Top=-16000;w.WindowStartupLocation=WindowStartupLocation.Manual;w.Show();w.RenderTo(Path.Combine(dir,"01-today.png"));
            w.Navigate("books");w.RenderTo(Path.Combine(dir,"02-books.png"));
            w.Navigate("dictionary");w.RenderTo(Path.Combine(dir,"03-dictionary.png"));
            w.Navigate("today");w.StartSession(false,false);w.RenderTo(Path.Combine(dir,"04-study.png"));
            Click(w,"显示释义  [空格]");w.RenderTo(Path.Combine(dir,"05-revealed.png"));
            Click(w,"3  认识");Check(store.Data.Cards.Count==1,"UI button grades word and saves progress");
            w.Navigate("today");w.StartSession(false,true);w.TestSpelling("wrong answer");w.RenderTo(Path.Combine(dir,"06-spelling.png"));Check(store.Data.History.Any(h=>h.Spelling&&h.Grade==0),"spelling UI logs wrong answer");
            w.Navigate("stats");w.RenderTo(Path.Combine(dir,"07-stats.png"));w.Navigate("settings");w.RenderTo(Path.Combine(dir,"08-settings.png"));
            w.Width=1040;w.Height=720;w.Navigate("today");w.UpdateLayout();var bmp=new RenderTargetBitmap(1040,680,96,96,PixelFormats.Pbgra32);bmp.Render((System.Windows.Media.Visual)w.Content);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using(var file=File.Create(Path.Combine(dir,"09-minimum-size.png")))encoder.Save(file);
            File.WriteAllLines(Path.Combine(dir,"ui-test-results.txt"),passed,new UTF8Encoding(false));w.Close();
        }
    }
}
