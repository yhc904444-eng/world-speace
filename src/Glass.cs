using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace LexisDesk {
    // Decorative visuals are siblings of the foreground: no blur, transform or
    // opacity animation is inherited by the startup labels or loading status.
    public sealed class LiquidGlassPanel : Grid {
        sealed class Drift {
            public TranslateTransform Transform;
            public double X,Y,Seconds;
        }
        readonly List<Drift> drifts=new List<Drift>();
        public bool HasActiveAnimations {get{return drifts.Exists(item=>item.Transform.HasAnimatedProperties);}}
        internal static LinearGradientBrush Gradient(Point start,Point end,params string[] colors) {
            var brush=new LinearGradientBrush{StartPoint=start,EndPoint=end};
            for(int i=0;i<colors.Length;i++)brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(colors[i]),(double)i/Math.Max(1,colors.Length-1)));
            brush.Freeze();return brush;
        }
        static RadialGradientBrush Glow(string color) {
            var brush=new RadialGradientBrush{Center=new Point(.44,.42),GradientOrigin=new Point(.44,.42),RadiusX=.56,RadiusY=.56};
            var center=(Color)ColorConverter.ConvertFromString(color);brush.GradientStops.Add(new GradientStop(center,0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(center.A*.52),center.R,center.G,center.B),.45));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0,center.R,center.G,center.B),1));brush.Freeze();return brush;
        }
        internal static void RoundClip(FrameworkElement element,double radius) {
            element.SizeChanged+=delegate{element.Clip=new RectangleGeometry(new Rect(element.RenderSize),radius,radius);};
        }
        void AddDrift(FrameworkElement element,double x,double y,double seconds) {
            var transform=new TranslateTransform();element.RenderTransform=transform;drifts.Add(new Drift{Transform=transform,X=x,Y=y,Seconds=seconds});
        }
        public LiquidGlassPanel() {
            IsHitTestVisible=false;
            Children.Add(new Border{CornerRadius=new CornerRadius(32),Background=MainWindow.Brush("#38EEF7F0"),Effect=new DropShadowEffect{BlurRadius=25,ShadowDepth=8,Opacity=.19,Color=Color.FromRgb(15,57,43)}});
            var lens=new Grid{Background=Gradient(new Point(0,0),new Point(.9,1),"#DDF6FFF9","#B8E6F3EF","#E6F3F8ED")};RoundClip(lens,32);Children.Add(lens);
            var lights=new Canvas();lens.Children.Add(lights);
            var mint=new Ellipse{Width=390,Height=300,Fill=Glow("#995CBFA2")};Canvas.SetLeft(mint,-130);Canvas.SetTop(mint,-75);lights.Children.Add(mint);AddDrift(mint,60,35,2.6);
            var aqua=new Ellipse{Width=370,Height=345,Fill=Glow("#7046A6AB")};Canvas.SetLeft(aqua,330);Canvas.SetTop(aqua,80);lights.Children.Add(aqua);AddDrift(aqua,-48,-30,3.1);
            var pearl=new Ellipse{Width=390,Height=260,Fill=Glow("#9FE6E9A8")};Canvas.SetLeft(pearl,100);Canvas.SetTop(pearl,325);lights.Children.Add(pearl);AddDrift(pearl,-30,-28,2.3);
            // Broad, translucent curved reflections suggest refraction without
            // a live desktop capture, per-frame image processing or a blur pass.
            var ribbon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M -65,315 C 55,223 120,388 289,295 C 410,229 438,204 600,244 L 600,311 C 439,258 388,285 300,338 C 133,440 40,287 -65,364 Z"),Fill=Gradient(new Point(0,0),new Point(.7,1),"#38FFFFFF","#0D629B85","#38FFFFFF"),Stretch=Stretch.None};
            lights.Children.Add(ribbon);AddDrift(ribbon,15,-14,2.8);
            lens.Children.Add(new Border{Background=Glow("#9BFFFFFF"),Margin=new Thickness(64,38,54,30)});
            lens.Children.Add(new Border{CornerRadius=new CornerRadius(32),Background=Gradient(new Point(0,0),new Point(.85,1),"#3AFFFFFF","#02FFFFFF","#22FFFFFF")});
            Children.Add(new Border{CornerRadius=new CornerRadius(32),BorderThickness=new Thickness(1.4),BorderBrush=Gradient(new Point(.1,0),new Point(.8,1),"#FFFFFFFF","#76FFFFFF","#AE719F8B","#F1FFFFFF")});
            Children.Add(new Border{Margin=new Thickness(3),CornerRadius=new CornerRadius(29),BorderThickness=new Thickness(1),BorderBrush=Gradient(new Point(0,0),new Point(1,1),"#B6FFFFFF","#0CFFFFFF","#81FFFFFF")});
            // The upper reflection and lower rim give the transparent edge a
            // little thickness while leaving the content area quiet.
            var rim=new Border{Height=2,Margin=new Thickness(33,1,33,0),VerticalAlignment=VerticalAlignment.Top,Background=Gradient(new Point(0,0),new Point(1,0),"#00FFFFFF","#EEFFFFFF","#00FFFFFF")};Children.Add(rim);
            Loaded+=delegate{Start();};Unloaded+=delegate{Stop();};
        }
        public void Start() {
            Stop();if(!Motion.Enabled||(RenderCapability.Tier>>16)==0)return;
            foreach(var item in drifts) {
                item.Transform.BeginAnimation(TranslateTransform.XProperty,Flow(item.X,item.Seconds));
                item.Transform.BeginAnimation(TranslateTransform.YProperty,Flow(item.Y,item.Seconds));
            }
        }
        static DoubleAnimation Flow(double end,double seconds) {
            return new DoubleAnimation(0,end,TimeSpan.FromSeconds(seconds)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
        }
        public void Stop() {
            foreach(var item in drifts){item.Transform.BeginAnimation(TranslateTransform.XProperty,null);item.Transform.BeginAnimation(TranslateTransform.YProperty,null);}
        }
    }
    public sealed class GlassIconFrame : Grid {
        readonly TranslateTransform reflection=new TranslateTransform(-150,0);
        public bool HasActiveAnimations {get{return reflection.HasAnimatedProperties;}}
        public GlassIconFrame(Image image) {
            Width=140;Height=140;HorizontalAlignment=HorizontalAlignment.Center;
            var lens=new Grid();LiquidGlassPanel.RoundClip(lens,41);
            Children.Add(new Border{CornerRadius=new CornerRadius(41),Background=MainWindow.Brush("#60D3EADB"),Effect=new DropShadowEffect{BlurRadius=17,ShadowDepth=7,Opacity=.14,Color=Color.FromRgb(34,83,62)}});
            lens.Children.Add(new Border{CornerRadius=new CornerRadius(41),Background=LiquidGlassPanel.Gradient(new Point(0,0),new Point(1,1),"#D9FFFFFF","#45FFFFFF","#80C1E4CD"),BorderThickness=new Thickness(1.5),BorderBrush=LiquidGlassPanel.Gradient(new Point(0,0),new Point(1,1),"#FFFFFFFF","#72FFFFFF","#A283B69B")});
            lens.Children.Add(new Border{CornerRadius=new CornerRadius(36),Margin=new Thickness(4),BorderThickness=new Thickness(1),BorderBrush=LiquidGlassPanel.Gradient(new Point(0,0),new Point(1,1),"#BBFFFFFF","#04FFFFFF","#A3FFFFFF")});
            image.Width=100;image.Height=100;image.VerticalAlignment=VerticalAlignment.Center;image.HorizontalAlignment=HorizontalAlignment.Center;lens.Children.Add(image);
            var glint=new Border{Width=76,Height=220,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false,RenderTransform=reflection,Background=LiquidGlassPanel.Gradient(new Point(0,0),new Point(1,.4),"#00FFFFFF","#53FFFFFF","#00FFFFFF")};lens.Children.Add(glint);
            Children.Add(lens);
            Loaded+=delegate{if(Motion.Enabled&&(RenderCapability.Tier>>16)>0)reflection.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(-150,180,TimeSpan.FromMilliseconds(970)){BeginTime=TimeSpan.FromMilliseconds(120),EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop});};
            Unloaded+=delegate{Stop();};
        }
        public void Stop(){reflection.BeginAnimation(TranslateTransform.XProperty,null);}
    }
}
