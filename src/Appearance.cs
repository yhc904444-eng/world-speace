using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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
            if(data.PageStyle!="solid"&&data.PageStyle!="glass")data.PageStyle="glass";
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
            Normalize(data);var p=Palette(data.PagePalette);bool glass=data.PageStyle=="glass";
            resources["Ink"]=Solid(p.Ink);resources["Muted"]=Solid(p.Muted);resources["Accent"]=Solid(p.Accent);
            resources["OnAccent"]=Solid("#FFFFFF");resources["HeroMuted"]=Solid("#E0EBE6");resources["NavMuted"]=Solid("#D4E2DF");
            resources["Canvas"]=Solid(p.Canvas);resources["Edge"]=Solid(p.Soft);resources["Line"]=Solid(p.Soft);
            resources["PageFill"]=glass?(Brush)Blend(p.Canvas,"#FBFDFC",p.Soft):Solid(p.Canvas);
            resources["CardFill"]=glass?(Brush)Blend("#E8FFFFFF",Alpha(p.Soft,"C5"),"#E8FFFFFF"):Solid("#FFFFFF");
            resources["SelectedFill"]=glass?(Brush)Blend("#E2FFFFFF",Alpha(p.Soft,"DC"),Alpha(p.Soft,"F5")):Solid(p.Soft);
            resources["HeroFill"]=glass?(Brush)Blend(p.Deep,p.Accent,p.Deep):Solid(p.Deep);
            resources["SidebarFill"]=glass?(Brush)Blend(p.Deep,p.Accent,p.Deep):Solid(p.Deep);
            resources["NavActive"]=glass?(Brush)Blend("#3DFFFFFF","#20FFFFFF"):Solid(p.Accent);
            resources["NavInactive"]=Solid("#00FFFFFF");
            resources["SecondaryFill"]=glass?(Brush)Blend("#BFFFFFFF",Alpha(p.Soft,"DF")):Solid(p.Soft);
            resources["PrimaryFill"]=glass?(Brush)Blend(p.Accent,p.Deep):Solid(p.Accent);
            resources["HeroButton"]=glass?(Brush)Blend(Alpha(p.Soft,"D5"),"#F4FFFFFF"):Solid(p.Soft);
            resources["HeroButtonInk"]=Solid(p.Deep);resources["HeroSubtle"]=glass?(Brush)Blend("#2BFFFFFF","#14FFFFFF"):Solid(p.Accent);
            resources["CardEdge"]=glass?(Brush)Blend("#FAFFFFFF",Alpha(p.Glow,"A0"),"#CBFFFFFF"):Solid("#00FFFFFF");
            resources["ButtonEdge"]=glass?(Brush)Blend("#BFFFFFFF",Alpha(p.Glow,"98")):Solid("#00FFFFFF");
            resources["GlassShine"]=Solid(glass?"#B8FFFFFF":"#00FFFFFF");
            resources["CardStroke"]=new Thickness(glass?1:0);resources["ButtonStroke"]=new Thickness(glass?1:0);
            resources["GlassVisibility"]=glass?Visibility.Visible:Visibility.Collapsed;
            resources["AmbientOne"]=Glow(p.Glow);resources["AmbientTwo"]=Glow(p.Soft);
            resources["RibbonFill"]=Blend(Alpha(p.Glow,"18"),"#48FFFFFF",Alpha(p.Glow,"20"));
            resources["InputFill"]=Solid(glass?"#DAFFFFFF":"#FFFFFF");
            resources["ChartPast"]=Solid(p.Glow);
        }
    }
    // One static background for the whole window; no continuous animation or
    // background blur is carried into the learning and navigation pages.
    public sealed class PageGlassBackdrop : Viewbox {
        public PageGlassBackdrop() {
            Stretch=Stretch.Fill;IsHitTestVisible=false;SetResourceReference(VisibilityProperty,"GlassVisibility");
            var scene=new Canvas{Width=1240,Height=840,ClipToBounds=true};
            var first=new Ellipse{Width=750,Height=640};first.SetResourceReference(Shape.FillProperty,"AmbientOne");Canvas.SetLeft(first,20);Canvas.SetTop(first,-260);scene.Children.Add(first);
            var second=new Ellipse{Width=920,Height=760};second.SetResourceReference(Shape.FillProperty,"AmbientOne");Canvas.SetLeft(second,710);Canvas.SetTop(second,330);scene.Children.Add(second);
            var haze=new Ellipse{Width=850,Height=590};haze.SetResourceReference(Shape.FillProperty,"AmbientTwo");Canvas.SetLeft(haze,320);Canvas.SetTop(haze,80);scene.Children.Add(haze);
            var ribbon=new System.Windows.Shapes.Path{Data=Geometry.Parse("M -90,650 C 220,380 460,840 790,540 S 1190,385 1370,390 L 1370,470 C 1060,405 1010,555 800,640 S 260,505 -90,740 Z")};ribbon.SetResourceReference(Shape.FillProperty,"RibbonFill");scene.Children.Add(ribbon);Child=scene;
        }
    }
}
