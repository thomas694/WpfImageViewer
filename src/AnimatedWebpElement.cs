using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WebPWrapper;
using static WebPWrapper.WebP;

namespace WpfImageViewer
{
    public class AnimatedWebpElement : Canvas
    {
        // Create a collection of child visual objects.
        private readonly VisualCollection _children;

        private Bitmap _bitmap;
        private BitmapSource _bitmapSource;
        private bool _started;
        private static System.Timers.Timer _timer = new System.Timers.Timer();

        private WebP _webp = new WebP();
        private List<FrameData> _frames;
        private int _currentIndex = 0;

        public delegate void FrameUpdatedEventHandler();

        public AnimatedWebpElement()
        {
            _children = new VisualCollection(this)
            {
                CreateDrawingVisualImage()
            };
            _timer.Elapsed += OnFrameChanged;
        }

        // Register a custom routed event using the Bubble routing strategy.
        public static readonly RoutedEvent MediaLoadedEvent = EventManager.RegisterRoutedEvent(
            name: "MediaLoaded",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(AnimatedWebpElement));

        // Provide CLR accessors for assigning an event handler.
        public event RoutedEventHandler MediaLoaded
        {
            add { AddHandler(MediaLoadedEvent, value); }
            remove { RemoveHandler(MediaLoadedEvent, value); }
        }

        private void RaiseMediaLoadedRoutedEvent()
        {
            // Create a RoutedEventArgs instance.
            RoutedEventArgs routedEventArgs = new RoutedEventArgs(routedEvent: MediaLoadedEvent);

            // Raise the event, which will bubble up through the element tree.
            RaiseEvent(routedEventArgs);
        }

        public int NaturalImageWidth => (_frames.Count > 0) ? _frames[0].Bitmap.Width : 0;
        public int NaturalImageHeight => (_frames.Count > 0) ? _frames[0].Bitmap.Height : 0;

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(Uri), typeof(AnimatedWebpElement), new PropertyMetadata(OnSourcePropertyChanged));

        public Uri Source
        {
            get => (Uri)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static void OnSourcePropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)DesignerProperties.IsInDesignModeProperty.GetMetadata(typeof(DependencyObject)).DefaultValue) { return; }

            AnimatedWebpElement control = source as AnimatedWebpElement;
            Uri url = (Uri)e.NewValue;
            control.StopAnimate();
            control.AnimatedWebpElement_Loaded(control, null);
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_bitmapSource != null)
            {
                drawingContext.DrawImage(_bitmapSource, new Rect(0, 0, ActualWidth, ActualHeight));
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject([In] IntPtr hObject);

        protected override void OnInitialized(EventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }

            base.OnInitialized(e);
            Loaded += new RoutedEventHandler(AnimatedWebpElement_Loaded);
            Unloaded += new RoutedEventHandler(AnimatedWebpElement_Unloaded);

            Window wnd = GetWindow();
            wnd.StateChanged += AnimatedWebpElement_WindowStateChanged;
            wnd.SizeChanged += AnimatedWebpElement_WindowSizeChanged;
        }

        private Window GetWindow()
        {
            FrameworkElement el = this;
            do
            {
                el = el.Parent as FrameworkElement;

            } while (!(el is Window) && !el.GetType().Name.EndsWith("WindowInstance"));

            return el as Window;
        }

        private void AnimatedWebpElement_WindowStateChanged(object sender, EventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }

            if (GetWindow().WindowState == WindowState.Minimized)
            {
                StopAnimate();
            }
            else
            {
                StartAnimate();
            }
        }

        private void AnimatedWebpElement_WindowSizeChanged(object sender, EventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }

            if (_bitmapSource != null)
            {
                _bitmapSource = GetBitmapSource();

                double factor = _bitmapSource.Width / _bitmapSource.Height;

                double parentWidth;
                double parentHeight;
                if (sender.Equals(GetWindow()))
                {
                    parentWidth = ((FrameworkElement)sender).ActualWidth;
                    parentHeight = ((FrameworkElement)sender).ActualHeight;
                }
                else
                {
                    parentWidth = ((FrameworkElement)((FrameworkElement)sender).Parent).ActualWidth;
                    parentHeight = ((FrameworkElement)((FrameworkElement)sender).Parent).ActualHeight;
                }

                if (factor > 1)
                {
                    Width = Math.Min(parentWidth, _bitmapSource.Width);
                    Height = Width / factor;
                }
                else
                {
                    Height = Math.Min(parentHeight, _bitmapSource.Height);
                    Width = Height * factor;
                }
                Thickness tn = Margin;
                if (parentHeight - Height > 0)
                {
                    tn.Top = (parentHeight - Height) / 2;
                }
                if (parentWidth - Width > 0)
                {
                    tn.Left = (parentWidth - Width) / 2;
                }
                //Margin = tn;
            }
        }

        /// <summary>
        /// Load the in the property Source specified image and start animation.
        /// </summary>
        void AnimatedWebpElement_Loaded(object sender, RoutedEventArgs e)
        {
            if (DesignerProperties.GetIsInDesignMode(this)) { return; }

            if (!string.IsNullOrEmpty(Source?.ToString()))
            {
                _frames = new List<FrameData>(_webp.AnimLoad(Source.LocalPath));
                _currentIndex = 0;
                _bitmap = _frames[0].Bitmap;
                _bitmapSource = GetBitmapSource();
                if (_bitmapSource != null)
                {
                    _bitmapSource.Freeze();
                }

                RaiseMediaLoadedRoutedEvent();

                AnimatedWebpElement_WindowSizeChanged(this, EventArgs.Empty);

                InvalidateVisual();

                StartAnimate();
            }
        }

        private void AnimatedWebpElement_Unloaded(object sender, RoutedEventArgs e)
        {
            StopAnimate();
        }

        private void OnFrameChanged(Object source, System.Timers.ElapsedEventArgs e)
        {
            _timer.Stop();

            Dispatcher.BeginInvoke(DispatcherPriority.Normal, new FrameUpdatedEventHandler(FrameUpdatedCallback));
        }

        private void FrameUpdatedCallback()
        {
            _currentIndex = (_currentIndex + 1 < _frames.Count) ? _currentIndex + 1 : 0;

            _bitmap = _frames[_currentIndex].Bitmap;
            _bitmapSource = GetBitmapSource();
            if (_bitmapSource != null)
            {
                _bitmapSource.Freeze();
            }

            InvalidateVisual();

            SetTimer(_frames[_currentIndex].Duration);
        }

        private void SetTimer(int duration)
        {
            if (duration == 0) { return; }
            
            _timer.Interval = duration;
            _timer.Start();
        }

        private void StartAnimate()
        {
            if (_started) { return; }
            _started = true;

            SetTimer(_frames[_currentIndex].Duration);
        }

        private void StopAnimate()
        {
            if (!_started) { return; }
            _started = false;

            _timer.Stop();
        }

        private BitmapSource GetBitmapSource()
        {
            IntPtr handle = IntPtr.Zero;

            try
            {
                handle = _bitmap.GetHbitmap();
                _bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                    handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            finally
            {
                if (handle != IntPtr.Zero)
                {
                    DeleteObject(handle);
                }
            }

            return _bitmapSource;
        }

        // Create a DrawingVisual that contains a rectangle.
        private DrawingVisual CreateDrawingVisualImage()
        {
            DrawingVisual drawingVisual = new DrawingVisual();

            // Retrieve the DrawingContext in order to create new drawing content.
            DrawingContext drawingContext = drawingVisual.RenderOpen();

            // Persist the drawing content.
            drawingContext.Close();

            return drawingVisual;
        }

        // Provide a required override for the VisualChildrenCount property.
        protected override int VisualChildrenCount => _children.Count;

        // Provide a required override for the GetVisualChild method.
        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            return _children[index];
        }
    }
}
