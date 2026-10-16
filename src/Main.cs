using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Speech.Synthesis;
using Microsoft.Win32;

namespace LexisDesk {
    public class MainWindow : Window {
        public readonly Store Store;
        Grid shell;
        StackPanel nav;
        ContentControl body;
        TextBlock status;
        string page="today";
        string findQuery="";
        ListBox results;
        ContentControl detail;
        DispatcherTimer searchTimer;
        SpeechSynthesizer speaker;
        bool speechReady;
        List<Word> session;
        int cursor,correct,sessionStartCount,excludedInSession,sessionAnswers;
        bool spelling,revealed,spellingChecked;
        Dictionary<string,int> attempts;
        TextBox spellBox;
        string spellFeedback;
        int cardSequence;
        public bool ThrowOnError;
        public MainWindow(Store store) {
            Store=store;Title="world speace · 单机词汇学习";Icon=AppAssets.Icon;Width=1240;Height=840;MinWidth=1040;MinHeight=720;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
            FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI");FontSize=14;
            UseLayoutRounding=true;SnapsToDevicePixels=true;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.ClearType);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml")) Resources=(ResourceDictionary)XamlReader.Load(stream);
            Appearance.Apply(Resources,Store.Data);Paint(this,BackgroundProperty,"PageFill");
            BuildShell();Navigate("today");
            PreviewKeyDown+=KeyHandler;
            Closed+=delegate {ReleaseSpeech();};
        }
        public static SolidColorBrush Brush(string hex) {return (SolidColorBrush)new BrushConverter().ConvertFromString(hex);}
        static void Paint(FrameworkElement element,DependencyProperty property,string color) {
            if(color.StartsWith("#"))element.SetValue(property,Brush(color));else element.SetResourceReference(property,color);
        }
        static Thickness Gap(double top,double bottom) {return new Thickness(0,top,0,bottom);}
        public TextBlock Text(string s,double size=14,string color=null,bool bold=false) {
            var text=new TextBlock{Text=s,FontSize=size,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};Paint(text,TextBlock.ForegroundProperty,color??"Ink");return text;
        }
        public Button Button(string title,Action action,bool primary=false) {
            var b=new Button{Content=title};if(primary){Paint(b,Control.BackgroundProperty,"PrimaryFill");Paint(b,Control.ForegroundProperty,"OnAccent");}
            Motion.AttachInteraction(b,1.03,.965);
            b.Click+=delegate {Safe(action);};return b;
        }
        public void Safe(Action action) {try {action();}catch(Exception e){if(ThrowOnError)throw;HandleError(e);}}
        void RecordError(Exception error,string context) {
            try {Directory.CreateDirectory(Store.DirectoryPath);File.AppendAllText(Path.Combine(Store.DirectoryPath,"errors.log"),DateTime.Now.ToString("o")+" ["+context+"]\n"+error+"\n\n",new UTF8Encoding(false));}catch{}
        }
        public void HandleError(Exception error) {
            RecordError(error,"page="+page);
            MessageBox.Show(this,error is NullReferenceException?"界面操作遇到问题，错误详情已记录。已保存的学习进度仍然保留。":error.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Warning);
        }
        void ReleaseSpeech() {
            var previous=speaker;speaker=null;speechReady=false;
            if(previous!=null)try{previous.Dispose();}catch(Exception e){RecordError(e,"speech dispose");}
        }
        void StopSpeech() {
            // A failed SAPI initializer leaves a non-null synthesizer. Calling
            // CancelAll on it re-runs that initializer and must not block navigation.
            if(speaker==null||!speechReady)return;
            try{speaker.SpeakAsyncCancelAll();}catch(Exception e){RecordError(e,"speech stop");ReleaseSpeech();}
        }
        public MotionCard Card(UIElement child,string background="CardFill",double pad=24) {
            var card=new MotionCard(child,Brushes.White,pad);Paint(card.Surface,Border.BackgroundProperty,background);
            Paint(card.Surface,Border.BorderBrushProperty,"CardEdge");card.Surface.SetResourceReference(Border.BorderThicknessProperty,"CardStroke");Paint(card.Shine,Border.BorderBrushProperty,"GlassShine");
            Motion.AttachInteraction(card,1.012,.987);Motion.RevealSurfaceOnFirstLoad(card,Math.Min(4,cardSequence++)*16);return card;
        }
        public StackPanel Stack(params UIElement[] items) {var s=new StackPanel();foreach(var i in items)s.Children.Add(i);return s;}
        public StackPanel Row(params UIElement[] items) {var s=new StackPanel{Orientation=Orientation.Horizontal};foreach(var i in items)s.Children.Add(i);return s;}
        public WrapPanel Wrap(params UIElement[] items) {var s=new WrapPanel();foreach(var i in items){var f=i as FrameworkElement;if(f!=null)f.Margin=new Thickness(0,0,10,8);s.Children.Add(i);}return s;}
        public ScrollViewer Scroll(UIElement child) {return new ScrollViewer{Content=child,Padding=new Thickness(6,5,6,5)};}
        public StackPanel Page(string title,string subtitle) {
            var p=new StackPanel();p.Children.Add(Text(title,30,null,true));var sub=Text(subtitle,13,"Muted");sub.Margin=Gap(8,26);p.Children.Add(sub);return p;
        }
        void BuildShell() {
            shell=new Grid();Paint(shell,Panel.BackgroundProperty,"PageFill");shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(205)});shell.ColumnDefinitions.Add(new ColumnDefinition());
            var backdrop=new PageGlassBackdrop();Grid.SetColumnSpan(backdrop,2);shell.Children.Add(backdrop);
            var sidebar=new Grid();sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});sidebar.RowDefinitions.Add(new RowDefinition());sidebar.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var sidebarSurface=new Border{CornerRadius=new CornerRadius(0,22,22,0)};Paint(sidebarSurface,Border.BackgroundProperty,"SidebarFill");Paint(sidebarSurface,Border.BorderBrushProperty,"CardEdge");sidebarSurface.SetResourceReference(Border.BorderThicknessProperty,"CardStroke");Grid.SetRowSpan(sidebarSurface,3);sidebar.Children.Add(sidebarSurface);
            var brand=Stack(Text("world speace",22,"OnAccent",true),Text("OFFLINE VOCABULARY",10,"NavMuted"));brand.Margin=new Thickness(28,32,20,34);sidebar.Children.Add(brand);
            nav=new StackPanel{Margin=new Thickness(14,0,12,0)};Grid.SetRow(nav,1);sidebar.Children.Add(nav);
            var foot=Stack(Text("每次记住一点点。",14,"NavMuted"),Text("独立单机版  v1.3.1\n学习记录只保存在本机",11,"NavMuted"));foot.Margin=new Thickness(26,24,16,28);Grid.SetRow(foot,2);sidebar.Children.Add(foot);
            shell.Children.Add(sidebar);
            var main=new Grid{Margin=new Thickness(36,27,32,15)};main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var top=new DockPanel{Margin=Gap(0,24)};var date=Text(DateTime.Now.ToString("yyyy年M月d日  dddd",new CultureInfo("zh-CN")),12,"Muted");DockPanel.SetDock(date,Dock.Right);top.Children.Add(date);top.Children.Add(Text("给记忆留一点空间",12,"Muted"));main.Children.Add(top);
            body=new ContentControl{HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Stretch};Grid.SetRow(body,1);main.Children.Add(body);
            status=Text("离线就绪  ·  "+Store.Words.Count.ToString("N0")+" 个内置词条",11,"Muted");status.Margin=Gap(12,0);Grid.SetRow(status,2);main.Children.Add(status);
            Grid.SetColumn(main,1);shell.Children.Add(main);Content=shell;
        }
        public void Navigate(string target) {
            if(searchTimer!=null)searchTimer.Stop();
            StopSpeech();
            page=target;cardSequence=0;nav.Children.Clear();status.Text="离线就绪  ·  "+Store.Words.Count.ToString("N0")+" 个词条  ·  记录保存在本机";
            var keys=new[]{"today","books","dictionary","notebook","excluded","stats","settings"};
            var labels=new[]{"◷   今日学习","▤   我的词书","⌕   离线词典","☆   生词收藏","⊘   不再复习","▥   学习记录","⚙   设置与备份"};
            for(int i=0;i<keys.Length;i++) {string key=keys[i];var b=Button(labels[i],()=>Navigate(key));b.HorizontalContentAlignment=HorizontalAlignment.Left;b.Margin=Gap(0,7);Paint(b,Control.BackgroundProperty,key==target?"NavActive":"NavInactive");Paint(b,Control.ForegroundProperty,key==target?"OnAccent":"NavMuted");nav.Children.Add(b);}
            if(target=="today")ShowToday();else if(target=="books")ShowBooks();else if(target=="dictionary"||target=="notebook"||target=="excluded")ShowDictionary();else if(target=="stats")ShowStats();else if(target=="settings")ShowSettings();
        }
        void ShowToday() {
            var now=DateTime.Now;var book=Store.Selected();var p=Page("今天，也向前一点","先回顾，再认识新词。让每一次练习，都成为下一次想起的线索。");
            var learn=Store.NewToday(now);var review=Store.ReviewedToday(now);var due=book.WordIds.Count(id=>Store.IsDue(id,now));
            var metrics=new UniformGridCompat(3);metrics.Add(Metric("今日新学",learn.ToString("00"),"每日目标 "+Store.Data.DailyTarget+" 词"));metrics.Add(Metric("今日复习",review.ToString("00"),"已回顾的不同单词"));metrics.Add(Metric("到期复习",due.ToString("00"),"当前词书中待巩固的单词"));metrics.Margin=Gap(0,22);p.Children.Add(metrics);
            var hero=new Grid();hero.ColumnDefinitions.Add(new ColumnDefinition());hero.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(210)});
            var left=Stack(Text("TODAY'S FOCUS",11,"HeroMuted",true),Text(book.Name,28,"OnAccent",true),Text("把陌生，变成下一次的熟悉。",15,"HeroMuted"));((TextBlock)left.Children[1]).Margin=Gap(15,10);
            var actions=Row(Button(due>0?"开始复习  →":"暂无到期复习",()=>StartSession(true,false),true),Button("学习新词",()=>StartSession(false,false)));
            Paint((Button)actions.Children[0],Control.BackgroundProperty,"HeroButton");Paint((Button)actions.Children[0],Control.ForegroundProperty,"HeroButtonInk");actions.Margin=Gap(27,2);left.Children.Add(actions);hero.Children.Add(left);
            int excluded=book.WordIds.Count(Store.IsExcluded),active=book.WordIds.Count-excluded;
            int learned=book.WordIds.Count(id=>Store.Data.Cards.ContainsKey(id)&&!Store.IsExcluded(id));
            var right=Stack(Text("词书进度",12,"HeroMuted"),Text(learned.ToString("N0")+" / "+active.ToString("N0"),20,"OnAccent",true),Text("已学 / 参与学习的词\n永不复习 "+excluded+" 词",12,"HeroMuted"));right.Margin=new Thickness(20,25,0,0);((TextBlock)right.Children[1]).Margin=Gap(12,8);Grid.SetColumn(right,1);hero.Children.Add(right);
            p.Children.Add(Card(hero,"HeroFill",30));
            var foot=GridPair(Card(Stack(Text("拼写，让记忆更扎实",18,null,true),WithMargin(Text("用中文释义回想英文，练习真正写出来。",13,"Muted"),10,20),Button("开始拼写测试",()=>StartSession(false,true)))),Card(Stack(Text("你的学习节奏",18,null,true),WithMargin(Text("认识：1 / 3 / 7 / 14 / 30 / 60 天后复习\n模糊：明天再见；忘记：10 分钟后再练",13,"Muted"),10,18),Button("调整每日目标",()=>Navigate("settings")))));foot.Margin=Gap(22,0);p.Children.Add(foot);
            body.Content=Scroll(p);
            if(!string.IsNullOrEmpty(Store.LoadNotice))status.Text=Store.LoadNotice;
        }
        UIElement WithMargin(FrameworkElement item,double top,double bottom){item.Margin=Gap(top,bottom);return item;}
        MotionCard Metric(string label,string number,string caption){var s=Stack(Text(label,12,"Muted"),WithMargin(Text(number,34,null,true),8,3),Text(caption,11,"Muted"));return Card(s,"CardFill",21);}
        Grid GridPair(UIElement left,UIElement right) {var g=new Grid();g.ColumnDefinitions.Add(new ColumnDefinition());g.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});g.ColumnDefinitions.Add(new ColumnDefinition());g.Children.Add(left);Grid.SetColumn(right,2);g.Children.Add(right);return g;}
        void ShowBooks() {
            var p=Page("选一本，慢慢记住","内置词书按 ECDICT 考试标签整理，词书之间会有重叠；同一词条共用学习进度。");
            var tools=Row(Button("导入 CSV / TSV 词书",ImportBook,true),Button("保存导入模板",SaveTemplate));tools.Margin=Gap(0,22);p.Children.Add(tools);
            var grid=new Grid();for(int i=0;i<3;i++)grid.ColumnDefinitions.Add(new ColumnDefinition());
            var books=Store.Books();string selectedId=Store.Selected().Id;for(int i=0;i<books.Count;i++) {
                if(i%3==0)grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var b=books[i];
                int excluded=0,learned=0;foreach(string id in b.WordIds){if(Store.IsExcluded(id))excluded++;else if(Store.Data.Cards.ContainsKey(id))learned++;}bool selected=b.Id==selectedId;
                var panel=Stack(Text(b.Id.StartsWith("book:")?"IMPORTED":"WORD COLLECTION",10,"Muted",true),WithMargin(Text(b.Name,19,null,true),14,10),Text(b.WordIds.Count.ToString("N0")+" 词  ·  已学 "+learned+"  ·  不再复习 "+excluded,12,"Muted"));
                var pb=new ProgressBar{Minimum=0,Maximum=Math.Max(1,b.WordIds.Count-excluded),Value=learned,Height=4,BorderThickness=new Thickness(0),Margin=Gap(16,18)};Paint(pb,Control.BackgroundProperty,"Line");Paint(pb,Control.ForegroundProperty,"Accent");panel.Children.Add(pb);
                var choose=Button(selected?"正在学习  ✓":"选择这本",()=>{Store.Data.SelectedBook=b.Id;Store.Save();ShowBooks();});choose.IsEnabled=!selected;panel.Children.Add(choose);
                var card=Card(panel,selected?"SelectedFill":"CardFill",20);card.Margin=new Thickness(0,0,14,14);Grid.SetRow(card,i/3);Grid.SetColumn(card,i%3);grid.Children.Add(card);
            }
            p.Children.Add(grid);p.Children.Add(Text("词表用于学习参考，不代表最新考试大纲。自制词书可包含音标、英文释义及双语例句。",12,"Muted"));body.Content=Scroll(p);
        }
        void ImportBook() {
            var dialog=new OpenFileDialog{Title="导入 UTF-8 CSV / TSV 词书",Filter="词书文件|*.csv;*.tsv|所有文件|*.*"};if(dialog.ShowDialog(this)!=true)return;
            int count=Store.Import(dialog.FileName,Path.GetFileNameWithoutExtension(dialog.FileName));ShowBooks();status.Text="已导入 "+count+" 个词条，并切换到新词书。";
        }
        public static string TemplateText="word,translation,phonetic,definition,example,example_translation\r\nserendipity,意外发现美好事物的运气,ˌserənˈdɪpəti,A happy discovery made by chance.,Finding this quiet cafe was pure serendipity.,发现这家安静的咖啡馆完全是意外之喜。\r\nresilient,有韧性的；能迅速恢复的,rɪˈzɪliənt,Able to recover after difficulty.,A resilient learner keeps trying after a mistake.,有韧性的学习者会在犯错后继续尝试。\r\n";
        void SaveTemplate(){var d=new SaveFileDialog{FileName="自制词书模板.csv",Filter="CSV 文件|*.csv"};if(d.ShowDialog(this)==true){File.WriteAllText(d.FileName,TemplateText,new UTF8Encoding(true));status.Text="词书模板已保存。";}}
        void ShowDictionary() {
            bool fav=page=="notebook",excluded=page=="excluded";
            var outer=new Grid();outer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});outer.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});outer.RowDefinitions.Add(new RowDefinition());
            outer.Children.Add(Page(excluded?"这些词，不再打扰你":fav?"把生词留在身边":"每个词，都能离线查",excluded?"已从新词、复习和拼写中排除。选中单词后可点“恢复复习”，撤销后保留原有进度。":fav?"忘记的单词会自动加入收藏。收藏与学习进度分别保存。":"输入英文或中文释义搜索。词典不需要网络。"));
            var searchRow=new DockPanel{Margin=Gap(0,20)};if(!excluded){var export=Button("导出收藏 CSV",ExportFavorites);DockPanel.SetDock(export,Dock.Right);searchRow.Children.Add(export);}
            var input=new TextBox{Text=findQuery,ToolTip="输入英文或中文释义搜索",Margin=new Thickness(0,0,15,0)};searchRow.Children.Add(input);Grid.SetRow(searchRow,1);outer.Children.Add(searchRow);
            var panes=new Grid();panes.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(310)});panes.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});panes.ColumnDefinitions.Add(new ColumnDefinition());
            results=new ListBox();results.SelectionChanged+=delegate {if(results.SelectedItem!=null){var item=(ListBoxItem)results.SelectedItem;detail.Content=WordDetails((Word)item.Tag);}};panes.Children.Add(Card(results,"CardFill",8));
            detail=new ContentControl();Grid.SetColumn(detail,2);panes.Children.Add(detail);Grid.SetRow(panes,2);outer.Children.Add(panes);
            searchTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(220)};searchTimer.Tick+=delegate{searchTimer.Stop();findQuery=input.Text;RefreshSearch(fav);};input.TextChanged+=delegate{searchTimer.Stop();searchTimer.Start();};
            body.Content=outer;RefreshSearch(fav);
        }
        void RefreshSearch(bool favorites) {
            bool excluded=page=="excluded";results.Items.Clear();var ws=Store.Search(findQuery,favorites,excluded);
            foreach(var w in ws){var label=Stack(Text(w.Text+(Store.IsExcluded(w.Id)?"  ·  永不复习":""),16,null,true),WithMargin(Text(Short(w.Translation,58),11,"Muted"),5,0));var item=new ListBoxItem{Content=label,Tag=w};results.Items.Add(item);}
            status.Text=(excluded?"不再复习（共 "+Store.Data.ExcludedWords.Count+" 词）":favorites?"生词收藏":"离线词典")+"  ·  显示 "+ws.Count+" 条"+(ws.Count==150?"，请缩小搜索范围":"");
            if(ws.Count>0)results.SelectedIndex=0;else detail.Content=Card(Stack(Text("暂时没有词条",22,null,true),WithMargin(Text(excluded?"可在学习卡片或词典详情中点“永不复习”；已排除的单词会显示在这里。":favorites?"可在词典中点“收藏”，或在练习中标记“忘记”。":"换一个关键词试试；也可以导入包含这个词的自制词书。",14,"Muted"),15,0)));
        }
        static string Short(string value,int max) {value=(value??"").Replace('\n',' ');return value.Length<=max?value:value.Substring(0,max)+"…";}
        UIElement WordDetails(Word w) {
            var p=new StackPanel();p.Children.Add(Text(w.Text,32,null,true));if(!string.IsNullOrWhiteSpace(w.Phonetic))p.Children.Add(WithMargin(Text("/ "+w.Phonetic+" /",15,"Muted"),10,0));
            p.Children.Add(WithMargin(Wrap(Button("▶  发音",()=>Speak(w.Text)),Button(Store.Data.Favorites.Contains(w.Id)?"★  已收藏":"☆  收藏",()=>{Store.ToggleFavorite(w.Id);detail.Content=WordDetails(w);if(page=="notebook")RefreshSearch(true);}),Button(Store.IsExcluded(w.Id)?"恢复复习":"永不复习",()=>{bool excluded=!Store.IsExcluded(w.Id);Store.SetExcluded(w.Id,excluded);detail.Content=WordDetails(w);if(page=="excluded")RefreshSearch(false);status.Text=w.Text+(excluded?" 已设为永不复习，可在“不再复习”中恢复。":" 已恢复，继续使用原有学习进度。");})),18,22));
            p.Children.Add(Text(w.Translation,18));
            if(!string.IsNullOrWhiteSpace(w.Definition)){p.Children.Add(WithMargin(Text("ENGLISH DEFINITION",10,"Muted",true),26,9));p.Children.Add(Text(w.Definition,14,"Muted"));}
            string forms=Store.ExchangeText(w.Exchange);if(forms.Length>0){p.Children.Add(WithMargin(Text("词形变化",12,"Muted",true),22,9));p.Children.Add(Text(forms,13,"Muted"));}
            if(!string.IsNullOrWhiteSpace(w.Example)){p.Children.Add(WithMargin(Text("语境例句 · 自制词书",12,"Muted",true),22,9));p.Children.Add(Text(w.Example,16));p.Children.Add(WithMargin(Text(w.ExampleTranslation,14,"Muted"),9,12));p.Children.Add(Button("▶  朗读例句",()=>Speak(w.Example)));}
            var state=Store.Data.Cards.ContainsKey(w.Id)?Store.Data.Cards[w.Id]:null;
            p.Children.Add(WithMargin(Text(Store.IsExcluded(w.Id)?"已设为永不复习 · 不参加学习、到期复习和拼写":state==null?"尚未学习":("记忆等级 "+state.Level+" / 6  ·  下次复习 "+DateTime.Parse(state.Due,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToString("M月d日 HH:mm")),12,"Muted"),27,0));
            return Card(Scroll(p));
        }
        void ExportFavorites() {
            var d=new SaveFileDialog{FileName="我的生词.csv",Filter="CSV 文件|*.csv"};if(d.ShowDialog(this)!=true)return;
            var text=new StringBuilder("word,translation,phonetic,definition,example,example_translation\r\n");foreach(var id in Store.Data.Favorites){Word w;if(!Store.Words.TryGetValue(id,out w))continue;text.AppendLine(string.Join(",",new[]{w.Text,w.Translation,w.Phonetic,w.Definition,w.Example,w.ExampleTranslation}.Select(Csv.Quote)));}File.WriteAllText(d.FileName,text.ToString(),new UTF8Encoding(true));status.Text="收藏已导出，可重新导入为自制词书。";
        }
        public void StartSession(bool review,bool spell) {
            session=Store.Queue(review,spell,DateTime.Now);cursor=0;correct=0;excludedInSession=0;sessionAnswers=0;sessionStartCount=session.Count;spelling=spell;attempts=new Dictionary<string,int>();
            if(session.Count==0){MessageBox.Show(this,review?"当前词书没有到期复习。忘记的词会在 10 分钟后到期；永不复习的词不会出现在这里。":"今天的新词目标已完成，或当前词书已无可练习的词。可更换词书、调整目标，或在“不再复习”中恢复单词。","可以歇一会儿",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            page="study";revealed=false;spellingChecked=false;spellFeedback="";status.Text="当前词书："+Store.Selected().Name+"  ·  每次作答自动保存";ShowStudy();
        }
        void ShowStudy() {
            cardSequence=0;
            while(cursor<session.Count&&Store.IsExcluded(session[cursor].Id))session.RemoveAt(cursor);
            if(cursor>=session.Count){ShowCompleted();return;}
            Word w=session[cursor];var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new DockPanel{Margin=Gap(0,18)};var exit=Button("返回今日",()=>Navigate("today"));DockPanel.SetDock(exit,Dock.Right);header.Children.Add(exit);header.Children.Add(Text((spelling?"拼写练习":"记忆练习")+"  /  "+(cursor+1)+" / "+session.Count,19,null,true));root.Children.Add(header);
            var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            if(spelling) {
                content.Children.Add(Centered("根据释义，写出英文",12,"HeroMuted"));content.Children.Add(WithMargin(Centered(w.Translation,22,"OnAccent",true),25,26));
                spellBox=new TextBox{FontSize=28,HorizontalAlignment=HorizontalAlignment.Center,MinWidth=320,MaxWidth=580,TextAlignment=TextAlignment.Center,IsReadOnly=spellingChecked,Text=spellingChecked?w.Text:""};spellBox.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;if(!spellingChecked)CheckSpelling();else NextCard();}};content.Children.Add(spellBox);
                var feedback=Centered(spellFeedback,16,spellFeedback.StartsWith("正确")?"HeroMuted":"#F2C9AB");feedback.Margin=Gap(20,10);content.Children.Add(feedback);
                content.Children.Add(WithMargin(Centered(spellingChecked?"正确拼写  "+w.Text:"按 Enter 检查答案",14,"HeroMuted"),0,12));
            } else {
                content.Children.Add(Centered("看见单词，先在心里回想释义",12,"HeroMuted"));
                content.Children.Add(WithMargin(Centered(w.Text,46,"OnAccent",true),24,8));
                content.Children.Add(Centered(string.IsNullOrWhiteSpace(w.Phonetic)?"":"/ "+w.Phonetic+" /",17,"HeroMuted"));
                var audio=Button("▶  朗读   [P]",()=>Speak(w.Text));audio.HorizontalAlignment=HorizontalAlignment.Center;audio.Margin=Gap(22,16);Paint(audio,Control.BackgroundProperty,"HeroSubtle");Paint(audio,Control.ForegroundProperty,"HeroMuted");content.Children.Add(audio);
                if(revealed){content.Children.Add(WithMargin(Centered(w.Translation,23,"OnAccent"),8,16));if(!string.IsNullOrWhiteSpace(w.Example)){content.Children.Add(Centered(w.Example,17,"HeroMuted"));content.Children.Add(WithMargin(Centered(w.ExampleTranslation,14,"HeroMuted"),8,0));}}
                else content.Children.Add(WithMargin(Centered("想起来了吗？",15,"HeroMuted"),20,20));
            }
            var container=Card(Scroll(content),"HeroFill",35);Grid.SetRow(container,1);root.Children.Add(container);
            var bottom=new StackPanel{Margin=Gap(20,0)};var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            if(spelling) actions.Children.Add(Button(spellingChecked?"下一个  →":"检查拼写  [Enter]",()=>{if(spellingChecked)NextCard();else CheckSpelling();},true));
            else if(!revealed)actions.Children.Add(Button("显示释义  [空格]",Reveal,true));
            else {actions.Children.Add(Button("1  忘记",()=>Answer(0)));actions.Children.Add(Button("2  模糊",()=>Answer(1)));actions.Children.Add(Button("3  认识",()=>Answer(2),true));}
            if(!spelling)actions.Children.Add(Button(Store.Data.Favorites.Contains(w.Id)?"★  已收藏":"☆  收藏",()=>{Store.ToggleFavorite(w.Id);ShowStudy();}));
            actions.Children.Add(Button("永不复习",ExcludeCurrentWord));
            bottom.Children.Add(actions);bottom.Children.Add(WithMargin(Centered(spelling?"拼写测试单独记录，不改变复习计划。答错的词自动收藏。":"诚实判断记忆程度。忘记的词会在本轮重现，并在 10 分钟后进入复习。",11,"Muted"),14,0));Grid.SetRow(bottom,2);root.Children.Add(bottom);body.Content=root;
            if(spelling&&!spellingChecked)Dispatcher.BeginInvoke(new Action(()=>spellBox.Focus()),DispatcherPriority.Input);
            if(Store.Data.AutoSpeak&&!spelling&&!revealed)Speak(w.Text);
        }
        TextBlock Centered(string s,double size,string color,bool bold=false){var t=Text(s,size,color,bold);t.TextAlignment=TextAlignment.Center;t.HorizontalAlignment=HorizontalAlignment.Center;return t;}
        public void Reveal(){if(page!="study"||spelling||revealed)return;revealed=true;ShowStudy();}
        public void ExcludeCurrentWord() {
            if(page!="study"||cursor>=session.Count)return;
            var word=session[cursor];Store.SetExcluded(word.Id,true);StopSpeech();excludedInSession++;
            session=session.Take(cursor).Concat(session.Skip(cursor).Where(w=>!Store.IsExcluded(w.Id))).ToList();
            revealed=false;spellingChecked=false;spellFeedback="";ShowStudy();
            status.Text=word.Text+" 已设为永不复习，已移出本轮和后续练习。可在“不再复习”中恢复。";
        }
        public void Answer(int grade){if(page!="study"||spelling||!revealed)return;var w=session[cursor];Store.Grade(w,grade,false,DateTime.Now);sessionAnswers++;if(grade==2)correct++;if(grade==0){int n;attempts.TryGetValue(w.Id,out n);attempts[w.Id]=n+1;if(n<2)session.Add(w);}NextCard();}
        void CheckSpelling(){if(spellingChecked)return;bool ok=string.Equals(spellBox.Text.Trim(),session[cursor].Text,StringComparison.OrdinalIgnoreCase);Store.Grade(session[cursor],ok?2:0,true,DateTime.Now);sessionAnswers++;if(ok)correct++;spellFeedback=ok?"正确！记得很牢。":"还差一点，看看正确拼写。";spellingChecked=true;ShowStudy();}
        void NextCard(){cursor++;revealed=false;spellingChecked=false;spellFeedback="";ShowStudy();}
        void ShowCompleted(){page="completed";var p=Page("这一轮，完成了。","记忆来自一次次相遇。做得很好，留一点时间给消化。");p.Children.Add(Card(Stack(Centered("✓",64,"Accent",true),WithMargin(Centered(sessionStartCount+" 个单词  ·  "+sessionAnswers+" 次练习",27,"Ink",true),20,10),Centered((spelling?"拼写正确":"判断为认识")+" "+correct+" 次"+(excludedInSession>0?"  ·  永不复习 "+excludedInSession+" 词":""),16,"Muted"),WithMargin(Row(Button("回到今日",()=>Navigate("today"),true),Button("查看学习记录",()=>Navigate("stats"))),30,10)),"SelectedFill",45));body.Content=Scroll(p);status.Text="本轮结果已保存在本机。";}
        void KeyHandler(object sender,KeyEventArgs e){if(page!="study")return;if(spelling)return;if(Keyboard.FocusedElement is TextBox)return;if(e.Key==Key.Space){Reveal();e.Handled=true;}else if(e.Key==Key.D1||e.Key==Key.NumPad1){Answer(0);e.Handled=true;}else if(e.Key==Key.D2||e.Key==Key.NumPad2){Answer(1);e.Handled=true;}else if(e.Key==Key.D3||e.Key==Key.NumPad3){Answer(2);e.Handled=true;}else if(e.Key==Key.P){Speak(session[cursor].Text);e.Handled=true;}else if(e.Key==Key.Escape){Navigate("today");e.Handled=true;}}
        void Speak(string text) {
            try {
                if(speaker==null)speaker=new SpeechSynthesizer();
                var voices=speaker.GetInstalledVoices().Where(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en").ToList();
                if(voices.Count==0){status.Text="未检测到离线英语语音。请在 Windows 设置的语言/语音中安装英语语音包。";return;}
                string voice=voices.Any(v=>v.VoiceInfo.Name==Store.Data.Voice)?Store.Data.Voice:voices[0].VoiceInfo.Name;
                if(speechReady)speaker.SpeakAsyncCancelAll();speaker.SelectVoice(voice);speaker.Rate=-1;speaker.SpeakAsync(text);speechReady=true;
            }catch(Exception e){RecordError(e,"speech start");ReleaseSpeech();status.Text="朗读暂不可用，请检查 Windows 默认音频输出和英语语音包。背词与页面切换仍可正常使用。";}
        }
        void ShowStats(){
            var now=DateTime.Now;var p=Page("每一次练习，都有迹可循","按本机日期记录新学、复习和拼写。重复作答保留为练习次数，不重复计入新学单词数。");
            var metrics=new UniformGridCompat(3);metrics.Add(Metric("累计已学",Store.Data.Cards.Count.ToString("N0"),"不同单词"));metrics.Add(Metric("记忆稳定",Store.Data.Cards.Count(x=>x.Value.Level>=4).ToString("N0"),"达到 14 天及以上复习间隔"));metrics.Add(Metric("练习天数",Store.Data.History.Select(h=>h.Day).Distinct().Count().ToString("N0"),"有学习或拼写记录的日期"));p.Children.Add(metrics);
            var chart=new Grid{Height=190,Margin=Gap(22,10)};for(int i=0;i<14;i++)chart.ColumnDefinitions.Add(new ColumnDefinition());var counts=new List<int>();for(int i=13;i>=0;i--){var day=Store.Day(now.Date.AddDays(-i));counts.Add(Store.Data.History.Count(h=>h.Day==day));}int max=Math.Max(5,counts.Max());
            for(int i=0;i<14;i++){var col=new StackPanel{VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Center};col.Children.Add(Text(counts[i].ToString(),11,"Muted"));var bar=new Border{Height=Math.Max(3,135.0*counts[i]/max),Width=27,CornerRadius=new CornerRadius(5,5,0,0),Margin=Gap(8,10)};Paint(bar,Border.BackgroundProperty,i==13?"Accent":"ChartPast");col.Children.Add(bar);col.Children.Add(Text(now.Date.AddDays(i-13).ToString("M/d"),10,"Muted"));Grid.SetColumn(col,i);chart.Children.Add(col);}
            var chartCard=Card(Stack(Text("最近 14 天 · 练习次数",17,null,true),chart));chartCard.Margin=Gap(22,22);p.Children.Add(chartCard);
            int spell=Store.Data.History.Count(h=>h.Spelling);int spellOk=Store.Data.History.Count(h=>h.Spelling&&h.Grade==2);p.Children.Add(Text("拼写累计 "+spell+" 次  ·  正确 "+spellOk+" 次"+(spell>0?"  ·  正确率 "+(100.0*spellOk/spell).ToString("0")+"%":""),15,null,true));
            var days=Store.Data.History.Select(h=>h.Day).Distinct().OrderByDescending(d=>d).Take(30);foreach(var day in days){var hs=Store.Data.History.Where(h=>h.Day==day).ToList();var line=Row(Text(day,14),Text("     新学 "+hs.Where(h=>h.IsNew&&!h.Spelling).Select(h=>h.Id).Distinct().Count()+"  ·  复习 "+hs.Where(h=>!h.IsNew&&!h.Spelling).Select(h=>h.Id).Distinct().Count()+"  ·  拼写 "+hs.Count(h=>h.Spelling)+"  ·  作答 "+hs.Count,13,"Muted"));line.Margin=Gap(16,3);p.Children.Add(line);}body.Content=Scroll(p);
        }
        void ShowSettings(){
            var p=Page("让学习，合你的节奏","不用注册，不连接学习服务。词库、收藏、词书和复习计划全部保存在本地。");
            p.Children.Add(AppearanceOptions());
            var target=new ComboBox{Name="DailyTargetChoice",Width=180,HorizontalAlignment=HorizontalAlignment.Left};foreach(int n in new[]{5,10,15,20,30,40,50,80,100,150,200})target.Items.Add(n);if(!target.Items.Contains(Store.Data.DailyTarget))target.Items.Add(Store.Data.DailyTarget);target.SelectedItem=Store.Data.DailyTarget;
            var auto=new CheckBox{Content="打开学习卡片时自动朗读英文",IsChecked=Store.Data.AutoSpeak,Margin=Gap(20,18)};
            var voice=new ComboBox{Width=340,HorizontalAlignment=HorizontalAlignment.Left};voice.Items.Add("自动选择英语语音");try{using(var ss=new SpeechSynthesizer()){foreach(var v in ss.GetInstalledVoices().Where(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en"))voice.Items.Add(v.VoiceInfo.Name);}}catch{}voice.SelectedIndex=0;if(!string.IsNullOrEmpty(Store.Data.Voice)&&voice.Items.Contains(Store.Data.Voice))voice.SelectedItem=Store.Data.Voice;
            var prefs=Stack(Text("每日新词目标",16,null,true),WithMargin(target,14,0),auto,Text("离线英语语音",13,"Muted"),WithMargin(voice,10,20));prefs.Children.Add(Row(Button("保存设置",()=>{Store.Data.DailyTarget=(int)target.SelectedItem;Store.Data.AutoSpeak=auto.IsChecked==true;Store.Data.Voice=voice.SelectedIndex>0?(string)voice.SelectedItem:null;Store.Save();status.Text="设置已保存。";},true),Button("试听",()=>{Store.Data.Voice=voice.SelectedIndex>0?(string)voice.SelectedItem:null;Speak("A little progress every day.");})));p.Children.Add(Card(prefs));
            var backup=Stack(Text("给进度留一份备份",18,null,true),WithMargin(Text("导出的 JSON 包含学习记录、收藏、设置和自制词书。恢复会替换当前数据，软件会先保留恢复前的副本。",13,"Muted"),10,20),Row(Button("导出完整备份",Backup,true),Button("恢复备份",Restore)));var backupCard=Card(backup);backupCard.Margin=Gap(20,20);p.Children.Add(backupCard);
            p.Children.Add(Text("数据文件夹",12,"Muted"));p.Children.Add(WithMargin(new TextBox{Text=Store.DirectoryPath,IsReadOnly=true,FontSize=12},8,18));
            p.Children.Add(Text("关于这个版本",16,null,true));p.Children.Add(WithMargin(Text("world speace 1.3.1 · 根据单机学习需求独立制作，并非不背单词官方客户端。\n内置词库：ECDICT（MIT 许可）。不包含原应用账号、云端学习记录、影视原声和付费词典。\n内置词条提供释义与词形；双语例句可随自制词书导入。发音使用 Windows 本地英语语音。\n词表来源、筛选规则和许可证随发行包提供。",12,"Muted"),10,18));body.Content=Scroll(p);
        }
        public void RefreshAppearance(){Appearance.Apply(Resources,Store.Data);}
        UIElement AppearanceOptions() {
            var palette=new ComboBox{Name="PagePaletteChoice",Width=240,ItemsSource=Appearance.Palettes,DisplayMemberPath="Name",SelectedItem=Appearance.Palette(Store.Data.PagePalette),HorizontalAlignment=HorizontalAlignment.Left};
            var style=new ComboBox{Name="PageStyleChoice",Width=240,ItemsSource=new[]{"液态玻璃","简洁纯色"},SelectedIndex=Store.Data.PageStyle=="solid"?1:0,HorizontalAlignment=HorizontalAlignment.Left};
            System.Windows.Automation.AutomationProperties.SetName(palette,"页面颜色");System.Windows.Automation.AutomationProperties.SetName(style,"页面风格");
            var choices=Wrap(Stack(Text("页面颜色",13,"Muted"),WithMargin(palette,9,0)),Stack(Text("页面风格",13,"Muted"),WithMargin(style,9,0)));choices.Margin=Gap(18,12);
            bool updating=false;
            Action apply=()=>Safe(()=>{
                if(updating||palette.SelectedItem==null||style.SelectedIndex<0)return;
                try{Store.SetAppearance(((PagePalette)palette.SelectedItem).Id,style.SelectedIndex==1?"solid":"glass");RefreshAppearance();status.Text="外观已保存，重启后仍会使用当前颜色与页面风格。";}
                catch{updating=true;try{palette.SelectedItem=Appearance.Palette(Store.Data.PagePalette);style.SelectedIndex=Store.Data.PageStyle=="solid"?1:0;}finally{updating=false;}throw;}
            });
            palette.SelectionChanged+=delegate{apply();};style.SelectionChanged+=delegate{apply();};
            var card=Card(Stack(Text("外观与配色",18,null,true),WithMargin(Text("喜欢透亮的玻璃质感，或更安静的纯色页面，都由你选择。",13,"Muted"),9,0),choices,Text("选择后立即生效并自动保存，无需重启。",12,"Muted")));card.Margin=Gap(0,20);return card;
        }
        void Backup(){var d=new SaveFileDialog{FileName="world speace 备份-"+DateTime.Now.ToString("yyyyMMdd")+".json",Filter="JSON 备份|*.json"};if(d.ShowDialog(this)==true){JsonIO.Write(d.FileName,Store.Data);status.Text="完整备份已导出。";}}
        void Restore(){var d=new OpenFileDialog{Filter="JSON 备份|*.json",Title="选择world speace备份"};if(d.ShowDialog(this)!=true)return;Store.Validate(JsonIO.Read<UserData>(d.FileName));if(MessageBox.Show(this,"将用备份替换当前学习记录、收藏和自制词书。恢复前的记录会自动另存副本。继续吗？","恢复学习记录",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;Store.Restore(d.FileName);RefreshAppearance();ShowSettings();status.Text="备份已恢复。";}
        public void RenderTo(string path){UpdateLayout();if(Motion.Enabled)Motion.Pump(365);var element=(FrameworkElement)Content;int width=Math.Max(1,(int)Math.Ceiling(element.ActualWidth)),height=Math.Max(1,(int)Math.Ceiling(element.ActualHeight));var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);image.Render(element);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));using(var f=File.Create(path))png.Save(f);}
        public void TestSpelling(string answer){spellBox.Text=answer;CheckSpelling();}
    }
    public class UniformGridCompat : Grid {
        int count;public UniformGridCompat(int cols){for(int i=0;i<cols;i++)ColumnDefinitions.Add(new ColumnDefinition());}
        public void Add(FrameworkElement child){Grid.SetColumn(child,count);child.Margin=new Thickness(0,0,count==ColumnDefinitions.Count-1?0:16,0);Children.Add(child);count++;}
    }
    public static class Program {
        [STAThread] public static int Main(string[] args) {
            try {
                string root=AppDomain.CurrentDomain.BaseDirectory;string data=Path.Combine(root,"UserData");int di=Array.IndexOf(args,"--data-dir");if(di>=0&&di+1<args.Length)data=Path.GetFullPath(args[di+1]);
                bool startupProbe=args.Contains("--startup-probe");
                Action<string> trace=message=>{if(startupProbe)File.AppendAllText(Path.Combine(root,"startup-probe.txt"),DateTime.Now.ToString("o")+" "+message+"\n",new UTF8Encoding(false));};
                if(startupProbe){File.WriteAllText(Path.Combine(root,"startup-probe.txt"),"");trace("entry");}
                if(args.Contains("--self-test"))return Tests.Run(root);
                var app=new Application();
                if(args.Contains("--render-tests")){Tests.Render(root,app);return 0;}
                if(args.Contains("--navigation-tests")){Tests.Navigation(root,app);return 0;}
                if(args.Contains("--exclusion-tests")){Tests.Exclusions(root,app);return 0;}
                if(args.Contains("--motion-tests")){MotionTests.Run(root,app);return 0;}
                if(args.Contains("--appearance-tests")){AppearanceTests.Run(root,app);return 0;}
                bool created;using(var mutex=new System.Threading.Mutex(true,"Local\\LexisDesk_"+StableHash(data),out created)){
                    if(!created){trace("same data directory is already open");if(startupProbe)return 2;MessageBox.Show("这个数据文件夹已有 world speace 正在运行，请使用已打开的窗口。","world speace");return 0;}
                    MainWindow window=null;var splash=new SplashWindow();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
                    if(startupProbe)splash.ProgressReported+=value=>trace("progress "+value.ToString("0"));
                    app.DispatcherUnhandledException+=delegate(object s,DispatcherUnhandledExceptionEventArgs e){var active=app.MainWindow as MainWindow??window;if(active!=null)active.HandleError(e.Exception);else MessageBox.Show(e.Exception.Message,"world speace无法启动");e.Handled=true;};
                    bool started=false;splash.Loaded+=async delegate {
                        if(started)return;started=true;trace("splash loaded");
                        try{window=await StartupFlow.OpenAsync(app,data,splash);trace("ready: "+window.Title+" visible="+window.IsVisible);if(startupProbe){await System.Threading.Tasks.Task.Delay(400);window.RenderTo(Path.Combine(root,"startup-probe.png"));app.Shutdown();}}
                        catch(OperationCanceledException){app.Shutdown();}
                        catch(Exception error){trace(error.ToString());if(!startupProbe)MessageBox.Show(error.Message,"world speace无法启动",MessageBoxButton.OK,MessageBoxImage.Error);app.Shutdown(1);}
                    };
                    splash.Closed+=delegate{if(app.MainWindow==splash)app.Shutdown();};
                    int exit=app.Run(splash);mutex.ReleaseMutex();return exit;
                }
            }catch(Exception e){if(args.Contains("--self-test")||args.Contains("--render-tests")||args.Contains("--navigation-tests")||args.Contains("--exclusion-tests")||args.Contains("--motion-tests")||args.Contains("--startup-probe")||args.Contains("--appearance-tests")){File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"test-error.txt"),e.ToString());return 1;}MessageBox.Show(e.Message,"world speace无法启动",MessageBoxButton.OK,MessageBoxImage.Error);return 1;}
        }
        static string StableHash(string s){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(s.ToLowerInvariant()))).Replace("-","").Substring(0,24);}
    }
}
