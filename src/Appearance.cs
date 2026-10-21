using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace LexisDesk {
    public sealed class PagePalette {
        public string Id {get;private set;}
        public string Name {get;private set;}
        public readonly string Canvas,Accent,Deep,Ink,Muted,Soft,Glow;
        public PagePalette(string id,string name,string canvas,string accent,string deep,string ink,string muted,string soft,string glow) {
            Id=id;Name=name;Canvas=canvas;Accent=accent;Deep=deep;Ink=ink;Muted=muted;Soft=soft;Glow=glow;
        }
    }
    public static class Appearance {
        public static readonly string[] StyleIds={"glass","solid","luminous"};
        public static readonly string[] StyleNames={"液态玻璃","简洁纯色","沉浸流光"};
        public static readonly string[] DensityIds={"dense","low","medium","clear"};
        public static readonly string[] DensityNames={"近乎不透","低透明（推荐）","适中透明","通透"};
        public static readonly string[] IntensityIds={"soft","balanced","vivid"};
        public static readonly string[] IntensityNames={"柔和（推荐）","均衡","绚丽"};
        // This is the minimum surface alpha, never the opacity of a text ancestor.
        public static byte SurfaceAlpha(string density){int index=Array.IndexOf(DensityIds,density);return new byte[]{252,244,230,212}[index<0?1:index];}
        public static readonly PagePalette[] Palettes={
            new PagePalette("bamboo","青竹绿","#F1F6F2","#2D6A55","#173E32","#19372E","#5F7469","#E0EDE4","#86C9AC"),
            new PagePalette("ocean","雾海蓝","#F1F5FB","#35638D","#203D5A","#21394F","#60758B","#E0EAF6","#87BCE5"),
            new PagePalette("lilac","柔雾紫","#F7F3FB","#75558F","#49325F","#3C2E4E","#786887","#ECE2F4","#B6A0D4"),
            new PagePalette("apricot","暖杏色","#FBF5EE","#90603D","#613F2C","#4D382A","#816D5C","#F3E5D5","#DFB38B"),
            new PagePalette("slate","简约灰","#F3F5F7","#536776","#303F4B","#2D3A45","#667581","#E4EAEE","#A7BEC9")
        };
        public static PagePalette Palette(string id){return Palettes.FirstOrDefault(p=>p.Id==id)??Palettes[0];}
        public static void Normalize(UserData data) {
            data.PagePalette=Palette(data.PagePalette).Id;
            if(!StyleIds.Contains(data.PageStyle))data.PageStyle="glass";
            if(!DensityIds.Contains(data.GlassDensity))data.GlassDensity="low";
            if(!IntensityIds.Contains(data.LuminousIntensity))data.LuminousIntensity="soft";
        }
        static SolidColorBrush Solid(string hex){var brush=MainWindow.Brush(hex);brush.Freeze();return brush;}
        static string Alpha(string hex,string alpha){return "#"+alpha+hex.TrimStart('#');}
        static LinearGradientBrush Blend(params string[] colors){return LiquidGlassPanel.Gradient(new Point(0,0),new Point(1,1),colors);}
        static RadialGradientBrush Glow(string color) {
            var brush=new RadialGradientBrush{Center=new Point(.5,.5),GradientOrigin=new Point(.5,.5),RadiusX=.5,RadiusY=.5};
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(Alpha(color,"6D")),0));
            brush.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(Alpha(color,"00")),1));brush.Freeze();return brush;
        }
        public static void Apply(ResourceDictionary resources,UserData data) {
            Normalize(data);var p=Palette(data.PagePalette);bool glass=data.PageStyle!="solid",luminous=data.PageStyle=="luminous";
            string alpha=SurfaceAlpha(data.GlassDensity).ToString("X2");
            resources["Ink"]=Solid(p.Ink);resources["Muted"]=Solid(p.Muted);resources["Accent"]=Solid(p.Accent);
            resources["OnAccent"]=Solid("#FFFFFF");resources["HeroMuted"]=Solid("#E0EBE6");resources["NavMuted"]=Solid("#D4E2DF");
            resources["Canvas"]=Solid(p.Canvas);resources["Edge"]=Solid(p.Soft);resources["Line"]=Solid(p.Soft);
            resources["PageFill"]=glass?(Brush)Blend(p.Canvas,"#FBFDFC",p.Soft):Solid(p.Canvas);
            resources["CardFill"]=glass?(Brush)Blend("#FEFFFFFF",Alpha(p.Soft,alpha),"#FCFFFFFF"):Solid("#FFFFFF");
            resources["SelectedFill"]=glass?(Brush)Blend("#FEFFFFFF",Alpha(p.Soft,alpha),Alpha(p.Soft,"FC")):Solid(p.Soft);
            resources["HeroFill"]=glass?(Brush)Blend(p.Deep,p.Accent,p.Deep):Solid(p.Deep);
            resources["SidebarFill"]=glass?(Brush)Blend(p.Deep,p.Accent,p.Deep):Solid(p.Deep);
            resources["NavActive"]=glass?(Brush)Blend("#3DFFFFFF","#20FFFFFF"):Solid(p.Accent);
            resources["NavInactive"]=Solid("#00FFFFFF");
            resources["SecondaryFill"]=glass?(Brush)Blend("#FCFFFFFF",Alpha(p.Soft,alpha)):Solid(p.Soft);
            resources["PrimaryFill"]=glass?(Brush)Blend(p.Accent,p.Deep):Solid(p.Accent);
            resources["HeroButton"]=glass?(Brush)Blend(Alpha(p.Soft,alpha),"#FCFFFFFF"):Solid(p.Soft);
            resources["HeroButtonInk"]=Solid(p.Deep);resources["HeroSubtle"]=glass?(Brush)Blend("#2BFFFFFF","#14FFFFFF"):Solid(p.Accent);
            resources["CardEdge"]=glass?(Brush)Blend("#FAFFFFFF",Alpha(p.Glow,luminous?"F0":"A0"),"#E8FFFFFF"):Solid("#00FFFFFF");
            resources["ButtonEdge"]=glass?(Brush)Blend("#BFFFFFFF",Alpha(p.Glow,"98")):Solid("#00FFFFFF");
            resources["GlassShine"]=Solid(glass?"#B8FFFFFF":"#00FFFFFF");
            resources["CardStroke"]=new Thickness(glass?1:0);resources["ButtonStroke"]=new Thickness(glass?1:0);
            resources["GlassVisibility"]=glass?Visibility.Visible:Visibility.Collapsed;
            resources["LuminousVisibility"]=luminous?Visibility.Visible:Visibility.Collapsed;
            resources["QuietGlassVisibility"]=data.PageStyle=="glass"?Visibility.Visible:Visibility.Collapsed;
            resources["AmbientOne"]=Glow(p.Glow);resources["AmbientTwo"]=Glow(p.Soft);
            resources["RibbonFill"]=Blend(Alpha(p.Glow,"18"),"#48FFFFFF",Alpha(p.Glow,"20"));
            resources["InputFill"]=Solid(glass?"#FAFFFFFF":"#FFFFFF");
            resources["LuminousMint"]=Glow(p.Glow);resources["LuminousViolet"]=Glow("#B19DE1");resources["LuminousWarm"]=Glow("#EAC59D");
            resources["LuminousRibbon"]=Blend(Alpha(p.Glow,"10"),Alpha(p.Glow,"70"),"#68FFFFFF",Alpha(p.Glow,"08"));
            resources["LuminousStrength"]=data.LuminousIntensity=="vivid"?1.0:data.LuminousIntensity=="balanced"?.7:.42;
            resources["ChartPast"]=Solid(p.Glow);
        }
    }
    // The single decorative backdrop survives page navigation. Only its light
    // shapes move; content never inherits opacity, blur, caching or transforms.
    public sealed class PageGlassBackdrop : Viewbox {
        readonly TranslateTransform driftOne=new TranslateTransform(),driftTwo=new TranslateTransform();
        readonly Canvas lights;
        Window owner;
        bool luminous,moving;
        public bool HasActiveAnimations {get{return driftOne.HasAnimatedProperties||driftTwo.HasAnimatedProperties;}}
        public PageGlassBackdrop() {
            Stretch=Stretch.Fill;IsHitTestVisible=false;SetResourceReference(VisibilityProperty,"GlassVisibility");
            var layers=new Grid{Width=1240,Height=840,ClipToBounds=true};
            var scene=new Canvas{Width=1240,Height=840,ClipToBounds=true};
            scene.SetResourceReference(VisibilityProperty,"QuietGlassVisibility");layers.Children.Add(scene);
            var first=new Ellipse{Width=750,Height=640};first.SetResourceReference(Shape.FillProperty,"AmbientOne");Canvas.SetLeft(first,20);Canvas.SetTop(first,-260);scene.Children.Add(first);
            var second=new Ellipse{Width=920,Height=760};second.SetResourceReference(Shape.FillProperty,"AmbientOne");Canvas.SetLeft(second,710);Canvas.SetTop(second,330);scene.Children.Add(second);
            var haze=new Ellipse{Width=850,Height=590};haze.SetResourceReference(Shape.FillProperty,"AmbientTwo");Canvas.SetLeft(haze,320);Canvas.SetTop(haze,80);scene.Children.Add(haze);
            var ribbon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M -90,650 C 220,380 460,840 790,540 S 1190,385 1370,390 L 1370,470 C 1060,405 1010,555 800,640 S 260,505 -90,740 Z")};ribbon.SetResourceReference(Shape.FillProperty,"RibbonFill");scene.Children.Add(ribbon);
            lights=new Canvas{Width=1240,Height=840};lights.SetResourceReference(VisibilityProperty,"LuminousVisibility");lights.SetResourceReference(OpacityProperty,"LuminousStrength");layers.Children.Add(lights);
            var flow=new Canvas{RenderTransform=driftOne};lights.Children.Add(flow);
            AddLight(flow,"LuminousMint",960,800,180,-280);AddLight(flow,"LuminousViolet",1000,850,560,130);
            var shimmer=new Canvas{RenderTransform=driftTwo};lights.Children.Add(shimmer);AddLight(shimmer,"LuminousWarm",800,660,120,420);
            var stream=new System.Windows.Shapes.Path{Data=Geometry.Parse("M 80,600 C 430,170 690,760 1280,210 L 1280,290 C 730,850 400,270 80,690 Z")};stream.SetResourceReference(Shape.FillProperty,"LuminousRibbon");shimmer.Children.Add(stream);
            Child=layers;
            Loaded+=delegate{Connect();};Unloaded+=delegate{Disconnect();};
        }
        static void AddLight(Canvas canvas,string resource,double width,double height,double left,double top) {
            var light=new Ellipse{Width=width,Height=height};light.SetResourceReference(Shape.FillProperty,resource);Canvas.SetLeft(light,left);Canvas.SetTop(light,top);canvas.Children.Add(light);
        }
        public void Configure(UserData data){luminous=data.PageStyle=="luminous";moving=data.LuminousMotion;RefreshMotion();}
        void Connect() {
            Disconnect();owner=Window.GetWindow(this);if(owner==null)return;
            owner.Activated+=OwnerChanged;owner.Deactivated+=OwnerChanged;owner.StateChanged+=OwnerChanged;owner.IsVisibleChanged+=OwnerVisibleChanged;
            SystemParameters.StaticPropertyChanged+=SystemChanged;RefreshMotion();
        }
        void Disconnect() {
            if(owner!=null){owner.Activated-=OwnerChanged;owner.Deactivated-=OwnerChanged;owner.StateChanged-=OwnerChanged;owner.IsVisibleChanged-=OwnerVisibleChanged;SystemParameters.StaticPropertyChanged-=SystemChanged;owner=null;}Stop();
        }
        void OwnerChanged(object sender,EventArgs args){RefreshMotion();}
        void OwnerVisibleChanged(object sender,DependencyPropertyChangedEventArgs args){RefreshMotion();}
        void SystemChanged(object sender,System.ComponentModel.PropertyChangedEventArgs args){if(Dispatcher.CheckAccess())RefreshMotion();else Dispatcher.BeginInvoke(new Action(RefreshMotion));}
        void RefreshMotion(){UpdateMotion(owner!=null&&owner.IsActive,owner!=null&&owner.IsVisible,owner!=null&&owner.WindowState==WindowState.Minimized);}
        internal void UpdateMotion(bool active,bool visible,bool minimized) {
            bool enabled=luminous&&moving&&Motion.Enabled&&(RenderCapability.Tier>>16)>0&&active&&visible&&!minimized;
            if(!enabled){Stop();return;}if(HasActiveAnimations)return;
            Animate(driftOne,45,-20,22);Animate(driftTwo,-38,26,28);
        }
        static void Animate(TranslateTransform transform,double x,double y,double seconds) {
            transform.BeginAnimation(TranslateTransform.XProperty,Flow(x,seconds));transform.BeginAnimation(TranslateTransform.YProperty,Flow(y,seconds));
        }
        static DoubleAnimation Flow(double value,double seconds) {
            var animation=new DoubleAnimation(0,value,TimeSpan.FromSeconds(seconds)){AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase{EasingMode=EasingMode.EaseInOut}};
            Timeline.SetDesiredFrameRate(animation,24);return animation;
        }
        public void Stop(){foreach(var transform in new[]{driftOne,driftTwo}){transform.BeginAnimation(TranslateTransform.XProperty,null);transform.BeginAnimation(TranslateTransform.YProperty,null);}}
        public void Release(){Disconnect();}
    }
}
