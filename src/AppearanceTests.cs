using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LexisDesk {
    public static class AppearanceTests {
        static readonly List<string> passed=new List<string>();
        static void Check(bool ok,string message){if(!ok)throw new Exception("FAILED: "+message);passed.Add("PASS: "+message);}
        static IEnumerable<DependencyObject> Tree(DependencyObject root) {
            yield return root;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in Tree(VisualTreeHelper.GetChild(root,i)))yield return item;
        }
        static ComboBox Choice(MainWindow window,string name){return Tree(window).OfType<ComboBox>().Single(item=>item.Name==name);}
        static bool Sharp(TextBlock text) {
            for(DependencyObject node=text;node!=null;node=VisualTreeHelper.GetParent(node)){
                var element=node as UIElement;if(element!=null&&(!element.RenderTransform.Value.IsIdentity||element.Opacity!=1||element.Effect!=null||element.CacheMode!=null))return false;
            }
            return true;
        }
        public static void Run(string root,Application app) {
            app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var directory=Path.Combine(root,"appearance-tests",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(directory);
            var store=new Store(Path.Combine(directory,"state"));var now=DateTime.Now;var word=store.Queue(false,false,now)[0];store.Grade(word,2,false,now);store.SetExcluded(store.Queue(false,false,now)[0].Id,true);
            string due=store.Data.Cards[word.Id].Due;int history=store.Data.History.Count;var queue=store.Queue(false,false,now).Select(item=>item.Id).ToArray();
            store.SetAppearance("ocean","solid");var reload=new Store(store.DirectoryPath);
            Check(reload.Data.PagePalette=="ocean"&&reload.Data.PageStyle=="solid"&&reload.Data.Cards[word.Id].Due==due,"appearance persists across restart with SRS intact");
            var backup=Path.Combine(directory,"appearance-backup.json");JsonIO.Write(backup,store.Data);store.SetAppearance("lilac","glass");store.Restore(backup);
            Check(store.Data.PagePalette=="ocean"&&store.Data.PageStyle=="solid"&&store.Data.ExcludedWords.Count==1,"full backup restores appearance and exclusions");
            var legacyDirectory=Path.Combine(directory,"legacy");Directory.CreateDirectory(legacyDirectory);
            var legacy=Regex.Replace(File.ReadAllText(store.StatePath),"\"Page(?:Palette|Style)\":\"[^\"]*\",","");
            Check(!legacy.Contains("PagePalette")&&!legacy.Contains("PageStyle"),"legacy fixture has no appearance fields");
            File.WriteAllText(Path.Combine(legacyDirectory,"progress.json"),legacy,new UTF8Encoding(false));var oldStore=new Store(legacyDirectory);
            Check(oldStore.Data.PagePalette=="bamboo"&&oldStore.Data.PageStyle=="glass"&&oldStore.Data.History.Count==history&&oldStore.Data.Cards[word.Id].Due==due,"older learning records gain defaults without losing progress");
            oldStore.Data.PagePalette="future-palette";oldStore.Data.PageStyle="future-style";JsonIO.Write(oldStore.StatePath,oldStore.Data);var unknown=new Store(legacyDirectory);
            Check(unknown.Data.PagePalette=="bamboo"&&unknown.Data.PageStyle=="glass"&&unknown.Data.History.Count==history,"unknown appearance values fall back without rejecting learning data");
            var window=new MainWindow(store){ThrowOnError=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-16000,Top=-16000};window.Show();window.Navigate("settings");window.UpdateLayout();
            try {
                var palette=Choice(window,"PagePaletteChoice");var style=Choice(window,"PageStyleChoice");var target=Choice(window,"DailyTargetChoice");target.SelectedItem=30;
                foreach(var color in Appearance.Palettes)foreach(var mode in new[]{"glass","solid"}) {
                    palette.SelectedItem=color;style.SelectedIndex=mode=="solid"?1:0;window.UpdateLayout();
                    Check(store.Data.PagePalette==color.Id&&store.Data.PageStyle==mode&&ReferenceEquals(Choice(window,"DailyTargetChoice"),target)&&(int)target.SelectedItem==30&&store.Data.DailyTarget==20,color.Id+" / "+mode+": live selection preserves unsaved learning settings");
                    var heading=Tree(window).OfType<TextBlock>().Single(item=>item.Text=="让学习，合你的节奏");var ink=(SolidColorBrush)heading.Foreground;
                    Check(ink.Color==(Color)ColorConverter.ConvertFromString(color.Ink)&&Tree(window).OfType<MotionCard>().All(card=>mode=="glass"?card.Surface.Background is LinearGradientBrush:card.Surface.Background is SolidColorBrush)&&Tree(window).OfType<PageGlassBackdrop>().Single().Visibility==(mode=="glass"?Visibility.Visible:Visibility.Collapsed),color.Id+" / "+mode+": current text, surfaces and backdrop update together");
                    Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+" / "+mode+": visible text keeps untransformed opaque ancestors");
                    window.RenderTo(Path.Combine(directory,color.Id+"-"+mode+"-settings.png"));
                }
                Check(store.Data.History.Count==history&&store.Data.Cards[word.Id].Due==due&&store.Queue(false,false,now).Select(item=>item.Id).SequenceEqual(queue),"changing every appearance leaves history, due dates and queue untouched");
                var saved=new Store(store.DirectoryPath);Check(saved.Data.PagePalette=="slate"&&saved.Data.PageStyle=="solid","the last UI choice survives a fresh store load");
                string originalDirectory=store.DirectoryPath;var blockedDirectory=Path.Combine(directory,"blocked-save");Directory.CreateDirectory(Path.Combine(blockedDirectory,"progress.json"));store.DirectoryPath=blockedDirectory;bool rejected=false;
                try{style.SelectedIndex=0;}catch{rejected=true;}finally{store.DirectoryPath=originalDirectory;}
                Check(rejected&&store.Data.PageStyle=="solid"&&style.SelectedIndex==1&&window.Resources["CardFill"] is SolidColorBrush,"failed save rolls back the selected style and keeps the displayed theme");
                foreach(var color in new[]{Appearance.Palettes[1],Appearance.Palettes[2]}) {
                    store.SetAppearance(color.Id,color.Id=="ocean"?"glass":"solid");window.RefreshAppearance();
                    foreach(string page in new[]{"today","books","dictionary","notebook","excluded","stats"}){window.Navigate(page);window.UpdateLayout();Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+": "+page+" keeps text independent of glass visuals");}
                    window.Navigate("today");window.RenderTo(Path.Combine(directory,color.Id+"-today.png"));
                    window.StartSession(false,false);window.UpdateLayout();Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+": learning card keeps text sharp");window.RenderTo(Path.Combine(directory,color.Id+"-study.png"));
                }
                store.SetAppearance("bamboo","glass");window.RefreshAppearance();window.Width=1040;window.Height=720;window.Navigate("settings");window.UpdateLayout();
                var controls=Tree(window).OfType<ComboBox>().Where(item=>item.Name=="PagePaletteChoice"||item.Name=="PageStyleChoice").ToList();
                Check(controls.Count==2&&controls.All(item=>item.ActualWidth>=180&&item.TransformToAncestor(window).Transform(new Point()).Y+item.ActualHeight<window.ActualHeight),"both appearance choices fit the minimum window size");window.RenderTo(Path.Combine(directory,"minimum-settings.png"));
                File.WriteAllLines(Path.Combine(root,"appearance-test-results.txt"),passed,new UTF8Encoding(false));
            }finally{window.Close();}
        }
    }
}
