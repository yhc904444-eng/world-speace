using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LexisDesk {
    public static class MotionTests {
        static readonly List<string> passed=new List<string>();
        static void Check(bool value,string message){if(!value)throw new Exception("FAILED: "+message);passed.Add("PASS: "+message);}
        static void Near(double actual,double expected,string message){Check(Math.Abs(actual-expected)<.002,message);}
        static void Capture(Window window,string path,Brush background=null) {
            window.UpdateLayout();var element=(FrameworkElement)window.Content;
            int width=Math.Max(1,(int)Math.Ceiling(window.ActualWidth)),height=Math.Max(1,(int)Math.Ceiling(window.ActualHeight));
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            if(background!=null) {
                var scene=new DrawingVisual();using(var context=scene.RenderOpen()){context.DrawRectangle(background,null,new Rect(0,0,width,height));context.DrawImage(bitmap,new Rect(0,0,width,height));}
                var composite=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);composite.Render(scene);bitmap=composite;
            }
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(path))encoder.Save(file);
        }
        static void Enter(FrameworkElement element){element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseEnterEvent});}
        static void Leave(FrameworkElement element){element.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=UIElement.MouseLeaveEvent});}
        static void Press(FrameworkElement element,bool down){element.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,Environment.TickCount,MouseButton.Left){RoutedEvent=down?UIElement.PreviewMouseLeftButtonDownEvent:UIElement.PreviewMouseLeftButtonUpEvent,Handled=true});}
        static bool SharpAncestors(FrameworkElement element) {
            for(DependencyObject node=element;node!=null;node=VisualTreeHelper.GetParent(node)) {
                var visual=node as UIElement;if(visual!=null&&(!visual.RenderTransform.Value.IsIdentity||visual.Opacity!=1||visual.Effect!=null||visual.CacheMode!=null))return false;
                var framework=node as FrameworkElement;if(framework!=null&&!framework.LayoutTransform.Value.IsIdentity)return false;
            }
            return true;
        }
        static byte[] TextPixels(Window window,FrameworkElement text) {
            var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth),(int)Math.Ceiling(window.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(window);
            var bounds=text.TransformToAncestor(window).TransformBounds(new Rect(text.RenderSize));
            var rectangle=new Int32Rect((int)Math.Floor(bounds.Left),(int)Math.Floor(bounds.Top),(int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height));
            var pixels=new byte[rectangle.Width*rectangle.Height*4];bitmap.CopyPixels(rectangle,pixels,rectangle.Width*4,0);return pixels;
        }
        static IEnumerable<TextBlock> Labels(DependencyObject root) {
            var label=root as TextBlock;if(label!=null)yield return label;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Labels(VisualTreeHelper.GetChild(root,i)))yield return child;
        }
        static SplashWindow GlassPreview() {
            return new SplashWindow{ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-16000,Top=-16000};
        }
        static void GlassChecks(string directory) {
            var preview=GlassPreview();preview.Show();
            try {
                Motion.Pump(100);var bar=Labels(preview).First(label=>label.Text=="0%");
                Check(Labels(preview).All(SharpAncestors),"glass startup labels have no animated or blurred ancestors");
                var progress=FindProgress(preview);progress.ApplyTemplate();preview.UpdateLayout();
                var track=(FrameworkElement)progress.Template.FindName("PART_Track",progress);var indicator=(FrameworkElement)progress.Template.FindName("PART_Indicator",progress);
                Near(indicator.ActualWidth,0,"glass progress indicator is empty at zero");
                preview.Report(67,"正在载入词条…");Motion.Pump(260);preview.UpdateLayout();
                Check(Math.Abs(indicator.ActualWidth/track.ActualWidth-.67)<.01&&bar.Text=="67%","glass indicator width and label follow actual reported progress");
                preview.Report(42,"过期状态");Check(preview.ReportedProgress==67&&preview.LoadingCaption=="正在载入词条…","stale loading messages cannot move glass progress backward");
                Capture(preview,Path.Combine(directory,"glass-on-light.png"),MainWindow.Brush("#E9F0E8"));
                Capture(preview,Path.Combine(directory,"glass-on-dark.png"),MainWindow.Brush("#152D27"));
                bool accelerated=(RenderCapability.Tier>>16)>0;
                Check(preview.Glass.HasActiveAnimations==accelerated,"ambient glass motion follows hardware rendering capability");
                var before=TextPixels(preview,preview.Glass);Motion.Pump(140);var after=TextPixels(preview,preview.Glass);
                Check(!accelerated||!before.SequenceEqual(after),"glass highlights visibly flow while labels keep their layout");
                Check(Labels(preview).All(SharpAncestors),"flowing glass leaves foreground typography sharp");
                preview.Report(100,"准备就绪");Motion.Pump(230);preview.UpdateLayout();Near(indicator.ActualWidth,track.ActualWidth,"glass progress fills its track at completion");
            }finally{preview.Close();}
            Check(!preview.Glass.HasActiveAnimations&&!preview.IconGlass.HasActiveAnimations,"closing startup stops all decorative glass clocks");
            Motion.EnabledOverride=false;var reduced=GlassPreview();reduced.Show();
            try {
                reduced.Report(100,"准备就绪");Motion.Pump(40);
                Check(!reduced.Glass.HasActiveAnimations&&!reduced.IconGlass.HasActiveAnimations&&Labels(reduced).All(SharpAncestors),"reduced motion keeps static glass styling with sharp text");
                Capture(reduced,Path.Combine(directory,"glass-reduced-motion.png"),MainWindow.Brush("#E9F0E8"));
            }finally{reduced.Close();Motion.EnabledOverride=true;}
        }
        static ProgressBar FindProgress(DependencyObject root) {
            var bar=root as ProgressBar;if(bar!=null)return bar;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindProgress(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}
            return null;
        }
        public static void Run(string root,Application app) {
            var directory=Path.Combine(root,"motion-tests",DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(directory);
            Motion.EnabledOverride=true;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            var splash=new SplashWindow{ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-16000,Top=-16000};
            var progress=new List<double>();splash.ProgressReported+=value=>progress.Add(value);app.MainWindow=splash;splash.Show();
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};int ticks=0;timer.Tick+=delegate{ticks++;};timer.Start();
            var stopwatch=Stopwatch.StartNew();
            var pending=StartupFlow.OpenAsync(app,Path.Combine(directory,"state"),splash,window=>{window.ShowInTaskbar=false;window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=-16000;window.Top=-16000;});
            int frame=0;
            while(!pending.IsCompleted&&stopwatch.ElapsedMilliseconds<10000){Motion.Pump(90);if(!splash.WasClosed)Capture(splash,Path.Combine(directory,"startup-"+(frame++).ToString("00")+".png"));}
            timer.Stop();Check(pending.IsCompleted,"startup completes without blocking dispatcher");
            var main=pending.GetAwaiter().GetResult();app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
            try {
                Check(main.IsVisible&&splash.WasClosed&&app.MainWindow==main,"splash hands off to one visible main window");
                Check(progress.Count>10&&progress.Last()==100&&progress.SequenceEqual(progress.OrderBy(x=>x)),"real loading progress is monotonic and reaches ready only at completion");
                Check(ticks>=8&&stopwatch.ElapsedMilliseconds>=1000,"logo entrance remains visible while UI stays responsive");
                Check(!File.Exists(main.Store.StatePath)&&main.Store.Data.Cards.Count==0,"animated startup does not modify learning records");
                Check(!splash.Glass.HasActiveAnimations&&!splash.IconGlass.HasActiveAnimations,"real startup releases glass animations before entering the app");
                GlassChecks(directory);
                main.RenderTo(Path.Combine(directory,"01-main.png"));
                var title=main.Text("轻一点，也很清晰",24,null,true);var buttonText=main.Text("悬停与点击",16);
                var demoButton=main.Button("悬停与点击",()=>{});demoButton.Content=buttonText;demoButton.Background=Brushes.White;demoButton.Width=180;demoButton.HorizontalAlignment=HorizontalAlignment.Center;demoButton.Margin=new Thickness(0,25,0,0);
                // Keep the test backdrop constant so a pixel comparison measures
                // glyph stability, independently of the chosen glass gradient.
                var demo=new MotionCard(main.Stack(title,main.Text("卡片悬停 / 按钮反馈",14,"#6D7F83"),demoButton),Brushes.White,24);Motion.AttachInteraction(demo,1.012,.987);Motion.RevealSurfaceOnFirstLoad(demo);demo.Width=450;demo.Height=200;demo.HorizontalAlignment=HorizontalAlignment.Center;demo.VerticalAlignment=VerticalAlignment.Center;
                var test=new Window{Width=620,Height=360,Content=demo,Resources=main.Resources,Background=Brushes.White,UseLayoutRounding=true,SnapsToDevicePixels=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-16000,Top=-16000};TextOptions.SetTextFormattingMode(test,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(test,TextRenderingMode.ClearType);test.Show();Motion.Pump(400);
                try {
                    var buttonSurface=Motion.Surface(demoButton);var titlePixels=TextPixels(test,title);var buttonPixels=TextPixels(test,buttonText);
                    Motion.RevealSurface(demo);Motion.Pump(45);Check(Motion.Scale(demo.Surface).ScaleX<.9999,"entrance still animates the card background");
                    Check(SharpAncestors(title)&&SharpAncestors(buttonText),"entrance keeps all text ancestors unscaled and fully opaque");
                    Check(titlePixels.SequenceEqual(TextPixels(test,title))&&buttonPixels.SequenceEqual(TextPixels(test,buttonText)),"text pixels stay identical during entrance animation");Capture(test,Path.Combine(directory,"02-sharp-entrance.png"));Motion.Pump(200);
                    double width=demoButton.ActualWidth;Enter(demoButton);Motion.Pump(55);Check(SharpAncestors(buttonText)&&buttonPixels.SequenceEqual(TextPixels(test,buttonText)),"hover keeps text pixels and text ancestors stable");Motion.Pump(150);Near(Motion.Scale(buttonSurface).ScaleX,1.03,"button background grows slightly on hover");Near(demoButton.ActualWidth,width,"hover does not change layout or hit target size");Capture(test,Path.Combine(directory,"03-hover.png"));
                    Press(demoButton,true);Motion.Pump(30);Check(SharpAncestors(buttonText)&&buttonPixels.SequenceEqual(TextPixels(test,buttonText)),"pointer press keeps button text pixels stable");Motion.Pump(100);Near(Motion.Scale(buttonSurface).ScaleX,.965,"pointer press shrinks button background");Capture(test,Path.Combine(directory,"04-pressed.png"));
                    Press(demoButton,false);Motion.Pump(200);Near(Motion.Scale(buttonSurface).ScaleX,1.03,"release returns button background to hovered size");
                    Press(demoButton,true);Leave(demoButton);Press(demoButton,false);Motion.Pump(200);Near(Motion.Scale(buttonSurface).ScaleX,1,"dragging pointer away never leaves pressed scale stuck");
                    Enter(demo);Motion.Pump(200);Near(Motion.Scale(demo.Surface).ScaleX,1.012,"section backgrounds have restrained hover feedback");Check(SharpAncestors(title)&&titlePixels.SequenceEqual(TextPixels(test,title)),"card hover keeps title pixels stable");Leave(demo);Motion.Pump(200);Near(Motion.Scale(demo.Surface).ScaleX,1,"section background returns to original scale");
                    var source=PresentationSource.FromVisual(test);demoButton.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Space){RoutedEvent=Keyboard.PreviewKeyDownEvent});Motion.Pump(120);Near(Motion.Scale(buttonSurface).ScaleX,.965,"keyboard press gets the same feedback");demoButton.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,Key.Space){RoutedEvent=Keyboard.PreviewKeyUpEvent});Motion.Pump(200);Near(Motion.Scale(buttonSurface).ScaleX,1,"keyboard release restores original scale");
                    demoButton.IsEnabled=false;Enter(demoButton);Motion.Pump(200);Near(Motion.Scale(buttonSurface).ScaleX,1,"disabled controls do not animate as active buttons");demoButton.IsEnabled=true;Leave(demoButton);
                    Motion.EnabledOverride=false;Enter(demoButton);Motion.RevealSurface(demo);Near(Motion.Scale(buttonSurface).ScaleX,1,"reduced motion keeps hover scale unchanged");Near(demo.Surface.Opacity,1,"reduced motion reveals background immediately");Check(SharpAncestors(title)&&SharpAncestors(buttonText),"reduced motion keeps text sharp");Motion.EnabledOverride=true;
                }finally{test.Close();}
                foreach(var page in new[]{"books","stats","dictionary","excluded","settings","today","books","today"}){main.Navigate(page);main.UpdateLayout();}
                Motion.Pump(400);Check(main.Store.Data.History.Count==0,"rapid animated page navigation preserves data");main.RenderTo(Path.Combine(directory,"04-after-navigation.png"));
                File.WriteAllLines(Path.Combine(root,"motion-test-results.txt"),passed,new UTF8Encoding(false));
            }finally{main.Close();if(!splash.WasClosed)splash.Close();Motion.EnabledOverride=null;}
        }
    }
}
