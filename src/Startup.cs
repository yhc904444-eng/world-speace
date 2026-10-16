using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LexisDesk {
    public static class AppAssets {
        static BitmapFrame icon;
        public static BitmapFrame Icon {
            get {
                if(icon==null)using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico")){
                    if(stream==null)throw new InvalidDataException("缺少应用图标。");
                    icon=new IconBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames.OrderByDescending(frame=>frame.PixelWidth).First();icon.Freeze();
                }
                return icon;
            }
        }
    }
    public class SplashWindow : Window {
        readonly ProgressBar progress;
        readonly TextBlock caption,percentage;
        readonly Image icon;
        internal readonly LiquidGlassPanel Glass;
        internal readonly GlassIconFrame IconGlass;
        public double ReportedProgress {get;private set;}
        public string LoadingCaption {get{return caption.Text;}}
        public bool WasClosed {get;private set;}
        public event Action<double> ProgressReported;
        public SplashWindow() {
            Title="词间 · 正在启动";Icon=AppAssets.Icon;Width=560;Height=480;ResizeMode=ResizeMode.NoResize;WindowStyle=WindowStyle.None;
            AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=true;
            FontFamily=new FontFamily("Microsoft YaHei UI, Segoe UI");
            UseLayoutRounding=true;SnapsToDevicePixels=true;TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml"))Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream);
            var panel=new Grid{Margin=new Thickness(18)};
            Glass=new LiquidGlassPanel();panel.Children.Add(Glass);
            var brand=Label("L E X I S  /  O F F L I N E",9,"#496F60",new Thickness(0,25,0,0));brand.VerticalAlignment=VerticalAlignment.Top;panel.Children.Add(brand);
            var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,Width=350,Margin=new Thickness(0,18,0,0)};
            var image=new Image{Source=AppAssets.Icon};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);icon=image;IconGlass=new GlassIconFrame(image);content.Children.Add(IconGlass);
            content.Children.Add(Label("词 间",29,"#163B31",new Thickness(0,21,0,7),true));
            content.Children.Add(Label("每次记住一点点。",13,"#4C6A5F",new Thickness(0,0,0,29)));
            progress=new ProgressBar{Minimum=0,Maximum=100,Value=0,Height=10,Width=284,IsIndeterminate=false,Style=(Style)FindResource("GlassProgress")};
            content.Children.Add(progress);
            var status=new Grid{Width=284,Margin=new Thickness(0,13,0,0)};status.ColumnDefinitions.Add(new ColumnDefinition());status.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            caption=Label("正在打开离线词库…",11,"#3E6352",new Thickness(0));caption.TextAlignment=TextAlignment.Left;status.Children.Add(caption);
            percentage=Label("0%",11,"#3E6352",new Thickness(8,0,0,0));Grid.SetColumn(percentage,1);status.Children.Add(percentage);content.Children.Add(status);
            progress.ValueChanged+=delegate(object sender,RoutedPropertyChangedEventArgs<double> e){percentage.Text=((int)Math.Round(e.NewValue)).ToString()+"%";};
            panel.Children.Add(content);Content=panel;
            Loaded+=delegate{Motion.RevealIcon(icon,30,15,520);};
            MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left)try{DragMove();}catch(InvalidOperationException){}};
            Closed+=delegate{WasClosed=true;Glass.Stop();IconGlass.Stop();};
        }
        static TextBlock Label(string value,double size,string color,Thickness margin,bool bold=false) {
            return new TextBlock{Text=value,FontSize=size,Foreground=MainWindow.Brush(color),TextAlignment=TextAlignment.Center,Margin=margin,FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal};
        }
        public void Report(double value,string message) {
            if(WasClosed||Dispatcher.HasShutdownStarted||Dispatcher.HasShutdownFinished)return;
            if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Report(value,message)),DispatcherPriority.Normal);return;}
            if(value<ReportedProgress)return;
            value=Math.Max(ReportedProgress,Math.Min(100,Math.Max(0,value)));double previous=progress.Value;
            ReportedProgress=value;caption.Text=message;
            progress.BeginAnimation(RangeBase.ValueProperty,null);progress.Value=value;
            if(Motion.Enabled)progress.BeginAnimation(RangeBase.ValueProperty,new DoubleAnimation(previous,value,TimeSpan.FromMilliseconds(190)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop},HandoffBehavior.SnapshotAndReplace);
            var callback=ProgressReported;if(callback!=null)callback(value);
        }
        public async Task FadeAndCloseAsync() {
            bool animated=await Dispatcher.InvokeAsync(()=>{
                if(WasClosed)return false;
                if(Motion.Enabled){BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(1,0,TimeSpan.FromMilliseconds(170)));return true;}
                return false;
            }).Task;
            if(animated)await Task.Delay(175);
            await Dispatcher.InvokeAsync(()=>{if(!WasClosed)Close();}).Task;
        }
    }
    public static class StartupFlow {
        public static async Task<MainWindow> PrepareAsync(string directory,SplashWindow splash) {
            var stopwatch=Stopwatch.StartNew();
            var store=await Task.Run(()=>new Store(directory,splash.Report));
            var window=await splash.Dispatcher.InvokeAsync(()=>{
                if(splash.WasClosed)throw new OperationCanceledException();
                splash.Report(95,"正在准备学习界面…");var prepared=new MainWindow(store);splash.Report(100,"准备就绪");return prepared;
            }).Task;
            // Let the requested logo entrance finish; never invent loading work.
            int remaining=Motion.Enabled?1050-(int)stopwatch.ElapsedMilliseconds:0;
            if(remaining>0)await Task.Delay(remaining);
            return await splash.Dispatcher.InvokeAsync(()=>{if(splash.WasClosed){window.Close();throw new OperationCanceledException();}return window;}).Task;
        }
        public static async Task<MainWindow> OpenAsync(Application app,string directory,SplashWindow splash,Action<MainWindow> beforeShow=null) {
            var window=await PrepareAsync(directory,splash);
            await app.Dispatcher.InvokeAsync(()=>{
                if(splash.WasClosed){window.Close();throw new OperationCanceledException();}
                if(beforeShow!=null)beforeShow(window);
                app.MainWindow=window;app.ShutdownMode=ShutdownMode.OnMainWindowClose;window.Show();
            }).Task;
            await splash.FadeAndCloseAsync();return window;
        }
    }
}
