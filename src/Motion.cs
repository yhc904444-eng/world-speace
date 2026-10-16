using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LexisDesk {
    public static class Motion {
        internal static bool? EnabledOverride;
        public static bool Enabled {get{return EnabledOverride??SystemParameters.ClientAreaAnimation;}}
        sealed class Interaction {
            public bool Hovered,Pressed;
            public double HoverScale,PressScale;
            public FrameworkElement Surface;
        }
        static readonly ConditionalWeakTable<FrameworkElement,Interaction> interactions=new ConditionalWeakTable<FrameworkElement,Interaction>();
        public static TransformGroup Transforms(FrameworkElement element) {
            var group=element.RenderTransform as TransformGroup;
            if(group==null){group=new TransformGroup();element.RenderTransform=group;}
            bool scale=false,translate=false;
            foreach(var transform in group.Children){if(transform is ScaleTransform)scale=true;if(transform is TranslateTransform)translate=true;}
            if(!scale)group.Children.Insert(0,new ScaleTransform(1,1));
            if(!translate)group.Children.Add(new TranslateTransform());
            element.RenderTransformOrigin=new Point(.5,.5);return group;
        }
        public static ScaleTransform Scale(FrameworkElement element) {
            foreach(var transform in Transforms(element).Children){var scale=transform as ScaleTransform;if(scale!=null)return scale;}
            throw new InvalidOperationException("Missing scale transform.");
        }
        public static TranslateTransform Translation(FrameworkElement element) {
            foreach(var transform in Transforms(element).Children){var translation=transform as TranslateTransform;if(translation!=null)return translation;}
            throw new InvalidOperationException("Missing translation transform.");
        }
        static DoubleAnimation Tween(double from,double to,int milliseconds,int delay=0) {
            return new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(milliseconds)){
                BeginTime=TimeSpan.FromMilliseconds(delay),EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
        }
        static void Change(Animatable target,DependencyProperty property,double value,int milliseconds) {
            double previous=(double)target.GetValue(property);
            target.BeginAnimation(property,null);target.SetValue(property,value);
            if(Enabled&&milliseconds>0)target.BeginAnimation(property,Tween(previous,value,milliseconds),HandoffBehavior.SnapshotAndReplace);
        }
        public static FrameworkElement Surface(FrameworkElement element) {
            var card=element as MotionCard;if(card!=null)return card.Surface;
            var control=element as Control;if(control==null)return null;
            control.ApplyTemplate();return control.Template==null?null:control.Template.FindName("MotionSurface",control) as FrameworkElement;
        }
        public static void AttachInteraction(FrameworkElement element,double hoverScale,double pressScale) {
            Interaction existing;if(interactions.TryGetValue(element,out existing))return;
            var state=new Interaction{HoverScale=hoverScale,PressScale=pressScale};interactions.Add(element,state);
            Action update=()=>{
                state.Surface=Surface(element);if(state.Surface==null)return;
                double target=!Enabled||!element.IsEnabled?1:state.Pressed?state.PressScale:state.Hovered?state.HoverScale:1;
                var transform=Scale(state.Surface);int time=state.Pressed?75:145;
                Change(transform,ScaleTransform.ScaleXProperty,target,time);Change(transform,ScaleTransform.ScaleYProperty,target,time);
            };
            element.Loaded+=delegate{state.Hovered=element.IsMouseOver;update();};
            element.MouseEnter+=delegate{state.Hovered=true;update();};
            element.MouseLeave+=delegate{state.Hovered=false;state.Pressed=false;update();};
            element.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((s,e)=>{state.Pressed=true;update();}),true);
            element.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler((s,e)=>{state.Pressed=false;update();}),true);
            element.LostMouseCapture+=delegate{state.Pressed=false;update();};
            if(element is ButtonBase){
                element.PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Space||e.Key==Key.Enter){state.Pressed=true;update();}};
                element.PreviewKeyUp+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Space||e.Key==Key.Enter){state.Pressed=false;update();}};
                element.LostKeyboardFocus+=delegate{state.Pressed=false;update();};
            }
            element.IsEnabledChanged+=delegate{if(!element.IsEnabled){state.Hovered=false;state.Pressed=false;}update();};
            element.Unloaded+=delegate{
                state.Hovered=false;state.Pressed=false;
                if(state.Surface==null)return;
                var transform=Scale(state.Surface);transform.BeginAnimation(ScaleTransform.ScaleXProperty,null);transform.BeginAnimation(ScaleTransform.ScaleYProperty,null);transform.ScaleX=1;transform.ScaleY=1;
            };
        }
        public static void RevealIcon(Image element,int delay=0,double distance=8,int duration=240) {
            var translation=Translation(element);
            element.BeginAnimation(UIElement.OpacityProperty,null);translation.BeginAnimation(TranslateTransform.YProperty,null);
            element.Opacity=1;translation.Y=0;
            if(!Enabled)return;
            element.BeginAnimation(UIElement.OpacityProperty,Tween(0,1,duration,delay),HandoffBehavior.SnapshotAndReplace);
            translation.BeginAnimation(TranslateTransform.YProperty,Tween(distance,0,duration,delay),HandoffBehavior.SnapshotAndReplace);
        }
        public static void RevealSurface(FrameworkElement element,int delay=0) {
            var surface=Surface(element);if(surface==null)return;
            // Animate only the decorative sibling. Text never inherits a scale,
            // fractional translation, opacity animation or bitmap cache.
            var scale=Scale(surface);
            double x=(double)scale.GetAnimationBaseValue(ScaleTransform.ScaleXProperty),y=(double)scale.GetAnimationBaseValue(ScaleTransform.ScaleYProperty);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,null);scale.BeginAnimation(ScaleTransform.ScaleYProperty,null);surface.BeginAnimation(UIElement.OpacityProperty,null);surface.Opacity=1;
            if(!Enabled)return;
            scale.BeginAnimation(ScaleTransform.ScaleXProperty,Tween(.985,x,180,delay),HandoffBehavior.SnapshotAndReplace);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty,Tween(.985,y,180,delay),HandoffBehavior.SnapshotAndReplace);
            surface.BeginAnimation(UIElement.OpacityProperty,Tween(.92,1,180,delay),HandoffBehavior.SnapshotAndReplace);
        }
        public static void RevealSurfaceOnFirstLoad(FrameworkElement element,int delay=0) {
            bool shown=false;
            element.Loaded+=delegate{if(shown)return;shown=true;RevealSurface(element,delay);};
        }
        // Pump the dispatcher for captures/tests so WPF can paint and complete its clocks.
        // This is never used to delay ordinary user interaction.
        internal static void Pump(int milliseconds) {
            var frame=new DispatcherFrame();var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(milliseconds)};
            timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
        }
    }
    public sealed class MotionCard : Grid {
        public readonly Border Surface;
        public readonly Border Shine;
        public MotionCard(UIElement content,Brush background,double padding) {
            Background=Brushes.Transparent;
            Surface=new Border{Background=background,CornerRadius=new CornerRadius(16),IsHitTestVisible=false};Children.Add(Surface);
            Shine=new Border{CornerRadius=new CornerRadius(13),Margin=new Thickness(2),BorderThickness=new Thickness(0,1,0,0),IsHitTestVisible=false};Surface.Child=Shine;
            Children.Add(new Border{Padding=new Thickness(padding),Child=content});
        }
    }
}
