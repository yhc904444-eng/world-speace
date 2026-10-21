using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
            store.SetAppearance("ocean","solid","medium","vivid",false);var reload=new Store(store.DirectoryPath);
            Check(reload.Data.PagePalette=="ocean"&&reload.Data.PageStyle=="solid"&&reload.Data.GlassDensity=="medium"&&reload.Data.LuminousIntensity=="vivid"&&!reload.Data.LuminousMotion&&reload.Data.Cards[word.Id].Due==due,"all appearance preferences persist across restart with SRS intact");
            var backup=Path.Combine(directory,"appearance-backup.json");JsonIO.Write(backup,store.Data);store.SetAppearance("lilac","luminous","clear","soft",true);store.Restore(backup);
            Check(store.Data.PagePalette=="ocean"&&store.Data.PageStyle=="solid"&&store.Data.GlassDensity=="medium"&&store.Data.LuminousIntensity=="vivid"&&!store.Data.LuminousMotion&&store.Data.ExcludedWords.Count==1,"full backup restores light, transparency and motion preferences with exclusions");
            var legacyDirectory=Path.Combine(directory,"legacy");Directory.CreateDirectory(legacyDirectory);
            var legacy=Regex.Replace(File.ReadAllText(store.StatePath),"\"(?:PagePalette|PageStyle|GlassDensity|LuminousIntensity)\":\"[^\"]*\",?","");legacy=Regex.Replace(legacy,"\"LuminousMotion\":(?:true|false),?","");legacy=legacy.Replace(",}","}");
            Check(!legacy.Contains("PagePalette")&&!legacy.Contains("PageStyle"),"legacy fixture has no appearance fields");
            File.WriteAllText(Path.Combine(legacyDirectory,"progress.json"),legacy,new UTF8Encoding(false));var oldStore=new Store(legacyDirectory);
            Check(oldStore.Data.PagePalette=="bamboo"&&oldStore.Data.PageStyle=="glass"&&oldStore.Data.GlassDensity=="low"&&oldStore.Data.LuminousIntensity=="soft"&&oldStore.Data.LuminousMotion&&oldStore.Data.History.Count==history&&oldStore.Data.Cards[word.Id].Due==due,"older learning records gain low-transparency defaults without losing progress");
            oldStore.Data.PagePalette="future-palette";oldStore.Data.PageStyle="future-style";oldStore.Data.GlassDensity="future-density";oldStore.Data.LuminousIntensity="future-light";JsonIO.Write(oldStore.StatePath,oldStore.Data);var unknown=new Store(legacyDirectory);
            Check(unknown.Data.PagePalette=="bamboo"&&unknown.Data.PageStyle=="glass"&&unknown.Data.GlassDensity=="low"&&unknown.Data.LuminousIntensity=="soft"&&unknown.Data.History.Count==history,"unknown appearance values fall back without rejecting learning data");
            // Specifically preserve the existing 1.4 palette/style when only new fields are absent.
            var previous=JsonIO.Read<UserData>(backup);previous.PageStyle="solid";JsonIO.Write(Path.Combine(legacyDirectory,"progress.json"),previous);
            var previousJson=Regex.Replace(File.ReadAllText(Path.Combine(legacyDirectory,"progress.json")),"\"(?:GlassDensity|LuminousIntensity)\":\"[^\"]*\",?","");previousJson=Regex.Replace(previousJson,"\"LuminousMotion\":(?:true|false),?","").Replace(",}","}");
            File.WriteAllText(Path.Combine(legacyDirectory,"progress.json"),previousJson,new UTF8Encoding(false));var previousStore=new Store(legacyDirectory);
            Check(previousStore.Data.PageStyle=="solid"&&previousStore.Data.PagePalette=="ocean"&&previousStore.Data.GlassDensity=="low"&&previousStore.Data.LuminousMotion,"1.4 appearance keeps its palette and style while receiving new defaults");
            var window=new MainWindow(store){ThrowOnError=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-16000,Top=-16000};window.Show();window.Navigate("settings");window.UpdateLayout();
            try {
                var palette=Choice(window,"PagePaletteChoice");var style=Choice(window,"PageStyleChoice");var target=Choice(window,"DailyTargetChoice");target.SelectedItem=30;
                foreach(var color in Appearance.Palettes)foreach(var mode in Appearance.StyleIds) {
                    palette.SelectedItem=color;style.SelectedIndex=Array.IndexOf(Appearance.StyleIds,mode);window.UpdateLayout();
                    Check(store.Data.PagePalette==color.Id&&store.Data.PageStyle==mode&&ReferenceEquals(Choice(window,"DailyTargetChoice"),target)&&(int)target.SelectedItem==30&&store.Data.DailyTarget==20,color.Id+" / "+mode+": live selection preserves unsaved learning settings");
                    var heading=Tree(window).OfType<TextBlock>().Single(item=>item.Text=="让学习，合你的节奏");var ink=(SolidColorBrush)heading.Foreground;
                    Check(ink.Color==(Color)ColorConverter.ConvertFromString(color.Ink)&&Tree(window).OfType<MotionCard>().All(card=>mode!="solid"?card.Surface.Background is LinearGradientBrush:card.Surface.Background is SolidColorBrush)&&Tree(window).OfType<PageGlassBackdrop>().Single().Visibility==(mode!="solid"?Visibility.Visible:Visibility.Collapsed),color.Id+" / "+mode+": current text, surfaces and backdrop update together");
                    Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+" / "+mode+": visible text keeps untransformed opaque ancestors");
                    window.RenderTo(Path.Combine(directory,color.Id+"-"+mode+"-settings.png"));
                }
                Check(store.Data.History.Count==history&&store.Data.Cards[word.Id].Due==due&&store.Queue(false,false,now).Select(item=>item.Id).SequenceEqual(queue),"changing every appearance leaves history, due dates and queue untouched");
                var density=Choice(window,"GlassDensityChoice");var intensity=Choice(window,"LuminousIntensityChoice");var motion=Tree(window).OfType<CheckBox>().Single(item=>item.Name=="LuminousMotionChoice");
                foreach(string mode in new[]{"glass","luminous"}) {
                    style.SelectedIndex=Array.IndexOf(Appearance.StyleIds,mode);byte last=255;
                    for(int i=0;i<Appearance.DensityIds.Length;i++) {
                        density.SelectedIndex=i;window.UpdateLayout();var brush=(LinearGradientBrush)window.Resources["CardFill"];byte minimum=brush.GradientStops.Min(stop=>stop.Color.A);
                        Check(minimum==Appearance.SurfaceAlpha(Appearance.DensityIds[i])&&minimum<last&&store.Data.GlassDensity==Appearance.DensityIds[i],mode+": transparency level "+i+" changes surface alpha in order");last=minimum;
                    }
                }
                density.SelectedIndex=1;Check(((LinearGradientBrush)window.Resources["CardFill"]).GradientStops.Min(stop=>stop.Color.A)>=244,"recommended glass is more opaque than the old minimum alpha of 197");
                var backdrop=Tree(window).OfType<PageGlassBackdrop>().Single();Motion.EnabledOverride=true;motion.IsChecked=true;
                for(int i=0;i<Appearance.IntensityIds.Length;i++){intensity.SelectedIndex=i;Check(store.Data.LuminousIntensity==Appearance.IntensityIds[i]&&Math.Abs((double)window.Resources["LuminousStrength"]-new[]{.42,.7,1.0}[i])<.001,"luminous intensity "+i+" updates live");}
                backdrop.UpdateMotion(true,true,false);Check(backdrop.HasActiveAnimations==((RenderCapability.Tier>>16)>0),"luminous flow starts only with hardware rendering");
                Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),"animated luminous background never transforms or fades foreground text");
                window.Navigate("books");Check(ReferenceEquals(backdrop,Tree(window).OfType<PageGlassBackdrop>().Single()),"book navigation reuses the existing backdrop rather than recreating light layers");window.Navigate("settings");window.UpdateLayout();
                palette=Choice(window,"PagePaletteChoice");style=Choice(window,"PageStyleChoice");density=Choice(window,"GlassDensityChoice");intensity=Choice(window,"LuminousIntensityChoice");motion=Tree(window).OfType<CheckBox>().Single(item=>item.Name=="LuminousMotionChoice");
                motion.IsChecked=false;backdrop.UpdateMotion(true,true,false);Check(!backdrop.HasActiveAnimations,"turning flow off stops every page light animation");motion.IsChecked=true;
                backdrop.UpdateMotion(true,true,false);backdrop.UpdateMotion(false,true,false);Check(!backdrop.HasActiveAnimations,"inactive window stops luminous clocks");
                backdrop.UpdateMotion(true,true,false);backdrop.UpdateMotion(true,true,true);Check(!backdrop.HasActiveAnimations,"minimized window stops luminous clocks");
                backdrop.UpdateMotion(true,true,false);window.Hide();Check(!backdrop.HasActiveAnimations,"hiding the real window stops luminous clocks");window.Show();
                Motion.EnabledOverride=false;backdrop.UpdateMotion(true,true,false);Check(!backdrop.HasActiveAnimations,"reduced motion keeps the luminous scene static");Motion.EnabledOverride=null;
                style.SelectedIndex=0;backdrop.UpdateMotion(true,true,false);Check(!backdrop.HasActiveAnimations,"returning to glass stops luminous clocks");
                style.SelectedIndex=1;Check(!density.IsEnabled&&!intensity.IsEnabled&&!motion.IsEnabled,"solid mode disables inapplicable transparency and light controls");
                var saved=new Store(store.DirectoryPath);Check(saved.Data.PagePalette=="slate"&&saved.Data.PageStyle=="solid"&&saved.Data.GlassDensity=="low"&&saved.Data.LuminousIntensity=="vivid"&&saved.Data.LuminousMotion,"the last UI choices survive a fresh store load");
                string originalDirectory=store.DirectoryPath;var blockedDirectory=Path.Combine(directory,"blocked-save");Directory.CreateDirectory(Path.Combine(blockedDirectory,"progress.json"));store.DirectoryPath=blockedDirectory;bool rejected=false;
                try{style.SelectedIndex=0;}catch{rejected=true;}finally{store.DirectoryPath=originalDirectory;}
                Check(rejected&&store.Data.PageStyle=="solid"&&style.SelectedIndex==1&&window.Resources["CardFill"] is SolidColorBrush,"failed save rolls back the selected style and keeps the displayed theme");
                foreach(var field in new[]{"density","intensity","motion"}) {
                    store.DirectoryPath=blockedDirectory;bool failed=false;
                    try{if(field=="density")density.SelectedIndex=0;else if(field=="intensity")intensity.SelectedIndex=0;else motion.IsChecked=false;}catch{failed=true;}finally{store.DirectoryPath=originalDirectory;}
                    Check(failed&&density.SelectedIndex==1&&intensity.SelectedIndex==2&&motion.IsChecked==true&&store.Data.GlassDensity=="low"&&store.Data.LuminousIntensity=="vivid"&&store.Data.LuminousMotion,"failed save rolls back "+field+" without changing other preferences");
                }
                foreach(var color in new[]{Appearance.Palettes[1],Appearance.Palettes[2],Appearance.Palettes[0]}) {
                    store.SetAppearance(color.Id,color.Id=="bamboo"?"luminous":color.Id=="ocean"?"glass":"solid");window.RefreshAppearance();
                    foreach(string page in new[]{"today","books","dictionary","notebook","excluded","stats"}){window.Navigate(page);window.UpdateLayout();Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+": "+page+" keeps text independent of glass visuals");}
                    window.Navigate("today");window.RenderTo(Path.Combine(directory,color.Id+"-today.png"));
                    window.StartSession(false,false);window.UpdateLayout();Check(Tree(window).OfType<TextBlock>().Where(item=>item.IsVisible).All(Sharp),color.Id+": learning card keeps text sharp");window.RenderTo(Path.Combine(directory,color.Id+"-study.png"));
                }
                store.SetAppearance("bamboo","glass");window.RefreshAppearance();window.Width=1040;window.Height=720;window.Navigate("settings");window.UpdateLayout();
                var controls=Tree(window).OfType<ComboBox>().Where(item=>new[]{"PagePaletteChoice","PageStyleChoice","GlassDensityChoice","LuminousIntensityChoice"}.Contains(item.Name)).ToList();
                Check(controls.Count==4&&controls.All(item=>item.ActualWidth>=180&&item.TransformToAncestor(window).Transform(new Point()).Y+item.ActualHeight<window.ActualHeight&&item.TransformToAncestor(window).Transform(new Point()).X+item.ActualWidth<window.ActualWidth),"all four appearance choices fit the minimum window size");window.RenderTo(Path.Combine(directory,"minimum-settings.png"));
                Check(store.Data.History.Count==history&&store.Data.Cards[word.Id].Due==due&&store.Queue(false,false,now).Select(item=>item.Id).SequenceEqual(queue),"all light and transparency edits leave learning history and review queue intact");
                File.WriteAllLines(Path.Combine(root,"appearance-test-results.txt"),passed,new UTF8Encoding(false));
            }finally{File.WriteAllLines(Path.Combine(root,"appearance-test-results.txt"),passed,new UTF8Encoding(false));window.Close();Motion.EnabledOverride=null;}
        }
    }
}
