using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using WpfAnimatedGif;

namespace WpfImageViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [Flags]
        private enum Modi
        {
            Normal = 1,
            Zoom = 2,
            Slideshow = 4,
            HelpShown = 8
        }

        // settings
        string _applicationTitle;
        string _backgroundColor;
        string _msgColor;
        string _includedFileExtensions;
        string[] fileExtensions;
        double _msgFadeoutSeconds;
        double _zoomMin;
        double _zoomMax;
        double _zoomStep;
        int _imageDurationSeconds;
        bool _showHelpOnLoad;
        bool _runAnimatedGifs;

        // file navigation
        string _directory;
        IEnumerable<string> fileList = new List<string>();
        int currentFileIndex;

        // states
        bool _isApplication;
        Modi _mode = Modi.Normal;
        bool slideshowRunning = false;
        private bool _imageLoaded = false;

        // image dragging
        Vector kv;
        Point origin;
        Point start;
        double maxX;
        double maxY;

        DispatcherTimer timerMessage = new DispatcherTimer();
        DispatcherTimer timerSlideshow = new DispatcherTimer();

        public MainWindow()
        {
            ReadSettings();
            Initialize();
        }

        /// <summary>
        /// Constructor with settings read from the app.config for showing the window in another application
        /// </summary>
        /// <param name="folder"></param>
        public MainWindow(string folder)
        {
            _directory = folder;
            ReadSettings();
            Initialize();
        }

        /// <summary>
        /// Constructor with settings for showing the window in another application
        /// </summary>
        /// <param name="folder">the folder with the images</param>
        /// <param name="showHelpOnLoad">whether to show help screen at start</param>
        /// <param name="runAnimatedGifs">whether to show preview image or run gifs</param>
        /// <param name="backgroundColor">System.Windows.Media.Colors constant name</param>
        /// <param name="msgColor">System.Windows.Media.Colors constant name</param>
        /// <param name="includedFileExtensions">file formats to show (e.g. ".jpg,.png")</param>
        /// <param name="imageDurationSeconds">image duration in sec</param>
        /// <param name="fadeoutSeconds">tooltip duration in sec</param>
        /// <param name="zoomMin">minimum zoom level (e.g. 0.1)</param>
        /// <param name="zoomMax">maximum zoom level (e.g. 5.0)</param>
        /// <param name="zoomStep">zoom step in percentage (e.g. 1.25)</param>
        public MainWindow(string folder, bool showHelpOnLoad, bool runAnimatedGifs,
            string backgroundColor, string msgColor, string includedFileExtensions, int imageDurationSeconds, double fadeoutSeconds, double zoomMin, double zoomMax, double zoomStep)
        {
            _directory = folder;
            _showHelpOnLoad = showHelpOnLoad;
            _runAnimatedGifs = runAnimatedGifs;
            _backgroundColor = backgroundColor;
            _msgColor = msgColor;
            _includedFileExtensions = includedFileExtensions;
            _imageDurationSeconds = imageDurationSeconds;
            _msgFadeoutSeconds = fadeoutSeconds;
            _zoomMin = zoomMin;
            _zoomMax = zoomMax;
            _zoomStep = zoomStep;

            Initialize();
        }

        private void ReadSettings()
        {
            var processFilename = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName).ToLower();
            string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string codeFilename = Path.GetFileName(Uri.UnescapeDataString(uri.Path)).ToLower();
            _isApplication = processFilename == codeFilename;

            if (_isApplication)
            {
                _applicationTitle = Properties.Settings.Default.ApplicationTitle;
                _backgroundColor = Properties.Settings.Default.BackgroundColor;
                _msgColor = Properties.Settings.Default.MsgColor;
                _includedFileExtensions = Properties.Settings.Default.IncludedFileExtensions;
                _msgFadeoutSeconds = Properties.Settings.Default.MsgFadeoutSeconds;
                _zoomMin = Properties.Settings.Default.ZoomMin;
                _zoomMax = Properties.Settings.Default.ZoomMax;
                _zoomStep = Properties.Settings.Default.ZoomStep;
                _imageDurationSeconds = Properties.Settings.Default.ImageDurationSeconds;
                _showHelpOnLoad = Properties.Settings.Default.ShowHelpOnLoad;
                _runAnimatedGifs = Properties.Settings.Default.RunAnimatedGifs;
            }
            else
            {
                Configuration conf = ConfigurationManager.OpenExeConfiguration(codeFilename);
                ClientSettingsSection section = (ClientSettingsSection)conf.GetSection("userSettings/WpfImageViewer.Properties.Settings");

                _applicationTitle = section.Settings.Get("ApplicationTitle").Value.ValueXml.InnerText;
                _backgroundColor = section.Settings.Get("BackgroundColor").Value.ValueXml.InnerText;
                _msgColor = section.Settings.Get("MsgColor").Value.ValueXml.InnerText;
                _includedFileExtensions = section.Settings.Get("IncludedFileExtensions").Value.ValueXml.InnerText;
                _msgFadeoutSeconds = double.Parse(section.Settings.Get("MsgFadeoutSeconds").Value.ValueXml.InnerText);
                _zoomMin = double.Parse(section.Settings.Get("ZoomMin").Value.ValueXml.InnerText);
                _zoomMax = double.Parse(section.Settings.Get("ZoomMax").Value.ValueXml.InnerText);
                _zoomStep = double.Parse(section.Settings.Get("ZoomStep").Value.ValueXml.InnerText);
                _imageDurationSeconds = int.Parse(section.Settings.Get("ImageDurationSeconds").Value.ValueXml.InnerText);
                _showHelpOnLoad = bool.Parse(section.Settings.Get("ShowHelpOnLoad").Value.ValueXml.InnerText);
                _runAnimatedGifs = bool.Parse(section.Settings.Get("RunAnimatedGifs").Value.ValueXml.InnerText);
            }
        }

        private void Initialize()
        {
            InitializeComponent();

            Window1.Title = string.IsNullOrWhiteSpace(_applicationTitle) ? "Wpf Image Viewer" : _applicationTitle;

            SolidColorBrush backgroundBrush;
            try
            {
                backgroundBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(_backgroundColor);
            }
            catch
            {
                backgroundBrush = (SolidColorBrush)new BrushConverter().ConvertFromString("Black");
            }
            Window1.Background = backgroundBrush;
            Grid1.Background = backgroundBrush;
            Border1.Background = backgroundBrush;

            try
            {
                Label1.Foreground = (Brush)new BrushConverter().ConvertFromString(_msgColor);
            }
            catch
            {
                Label1.Foreground = (Brush)new BrushConverter().ConvertFromString("Green");
            }

            try
            {
                fileExtensions = _includedFileExtensions.Split(',');
            }
            catch
            {
                fileExtensions = new[] { ".bmp", ".gif", ".jpeg", ".jpg", ".png", ".tif", ".tiff" };
            }

            PreviewKeyDown += new KeyEventHandler(Window1_PreviewKeyDown);
        }

        private void Window1_Loaded(object sender, RoutedEventArgs e)
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 1)
            {
                SetImage(Path.GetFullPath(args[1]));
            }
            else if (!string.IsNullOrEmpty(_directory))
            {
                SetImage(_directory);
            }
            else
            {
                _showHelpOnLoad = true;
            }
            if (_showHelpOnLoad)
            {
                ShopHelpText();
            }
        }

        private bool IsImageLoaded()
        {
            return _imageLoaded;
        }

        /// <summary>
        /// sets an image
        /// </summary>
        /// <param name="imagePath">(optional) use the given image path or else the file list's current index</param>
        private void SetImage(string imagePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    imagePath = fileList.ElementAt(currentFileIndex);

                    Uri imageUri = new Uri(imagePath);
                    BitmapImage imageBitmap = new BitmapImage(imageUri);
                    Image1.Stretch = (imageBitmap.Width <= Grid1.ActualWidth && imageBitmap.Height <= Grid1.ActualHeight) ? Stretch.None : Stretch.Uniform;
                    if (_runAnimatedGifs && imagePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        Image1.Source = null;
                        ImageBehavior.SetAnimatedSource(Image1, imageBitmap);
                    }
                    else
                    {
                        ImageBehavior.SetAnimatedSource(Image1, null);
                        Image1.Source = imageBitmap;
                    }
                    Image1.Visibility = Visibility.Visible;
                    _imageLoaded = true;

                    ResetZoomLevel();

                    ShowMessage(imagePath);
                }
                else
                {
                    if (File.Exists(imagePath))
                    {
                        LoadFileList(Path.GetDirectoryName(imagePath));
                        currentFileIndex = fileList.ToList().IndexOf(imagePath);
                        SetImage();
                    }
                    else if (Directory.Exists(imagePath))
                    {
                        LoadFileList(imagePath);
                        currentFileIndex = 0;
                        SetImage();
                    }
                    else
                    {
                        ShowMessage("Invalid path: " + imagePath);
                        Image1.Source = null;
                        _imageLoaded = false;
                    }
                }
            }
            catch
            {
                ShowMessage("Invalid image: " + imagePath);

                Image1.Visibility = Visibility.Hidden;
            }
        }

        /// <summary>
        /// load a fileList, optionally only include files whose extensions are in IncludedFileExtensions
        /// </summary>
        /// <param name="directoryName"></param>
        private void LoadFileList(string directoryName)
        {
            fileList = fileExtensions[0] == "" || fileExtensions[0] == "*"
                ? Directory
                    .EnumerateFiles(directoryName)
                    .OrderBy(x => x, new NaturalStringComparer())
                : Directory
                    .EnumerateFiles(directoryName)
                    .Where(f => fileExtensions.Any(f.ToLower().EndsWith))
                    .OrderBy(x => x, new NaturalStringComparer());
        }

        private void Window1_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((_mode & Modi.HelpShown) != 0 && !(Keyboard.Modifiers == ModifierKeys.None && (e.Key == Key.H || e.Key == Key.I || e.Key == Key.F1)))
            {
                Label2.Visibility = Visibility.Hidden;
                _mode &= ~Modi.HelpShown;
                return;
            }

            if (Keyboard.Modifiers == ModifierKeys.None)
            {
                switch (e.Key)
                {
                    case Key.H:
                    case Key.I:
                    case Key.F1:
                        ShopHelpText();
                        break;

                    case Key.Escape:
                        if ((_mode & Modi.Slideshow) != 0)
                        {
                            StopSlideshow(2);
                        }
                        else
                        {
                            Close();
                        }
                        break;

                    case Key.Left:
                        if (currentFileIndex > 0)
                        {
                            currentFileIndex--;
                            ResetZoomLevel();
                            SetImage();
                        }
                        break;

                    case Key.Right:
                        if (currentFileIndex < fileList.Count() - 1)
                        {
                            currentFileIndex++;
                            ResetZoomLevel();
                            SetImage();
                        }
                        break;

                    case Key.Home:
                        if (currentFileIndex > 0)
                        {
                            currentFileIndex = 0;
                            ResetZoomLevel();
                            SetImage();
                        }
                        break;

                    case Key.End:
                        if (currentFileIndex < fileList.Count() - 1)
                        {
                            currentFileIndex = fileList.Count() - 1;
                            ResetZoomLevel();
                            SetImage();
                        }
                        break;

                    case Key.Space:
                        if ((_mode & Modi.Zoom) != 0)
                        {
                            ResetZoomLevel();
                        }
                        else
                        {
                            if (slideshowRunning)
                            {
                                StopSlideshow(1);
                            }
                            else
                            {
                                StartSlideshow(true);
                            }
                        }
                        break;

                    case Key.Add:
                    case Key.OemPlus:
                        Zoom(1);
                        break;

                    case Key.Subtract:
                    case Key.OemMinus:
                        Zoom(-1);
                        break;

                    case Key.PageDown:
                        if (_imageDurationSeconds > 1) _imageDurationSeconds--;
                        ShowMessage($"Image duration: {_imageDurationSeconds} sec");
                        StartSlideshow(false);
                        break;

                    case Key.PageUp:
                        if (_imageDurationSeconds < 30) _imageDurationSeconds++;
                        ShowMessage($"Image duration: {_imageDurationSeconds} sec");
                        StartSlideshow(false);
                        break;

                    default:
                        break;
                }
            }
            else if (Keyboard.Modifiers == ModifierKeys.Alt)
            {
                switch (e.SystemKey)
                {
                    case Key.Left:
                        AdjustKeyboardVector(-1, 0);
                        MoveToNewPosition(kv);
                        break;
                    case Key.Right:
                        AdjustKeyboardVector(1, 0);
                        MoveToNewPosition(kv);
                        break;
                    case Key.Up:
                        AdjustKeyboardVector(0, -1);
                        MoveToNewPosition(kv);
                        break;
                    case Key.Down:
                        AdjustKeyboardVector(0, 1);
                        MoveToNewPosition(kv);
                        break;
                    default:
                        if (IsImageLoaded())
                        {
                            var tt = (TranslateTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is TranslateTransform);
                            origin = new Point(0, 0);
                            kv = new Vector(-tt.X, -tt.Y);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// calculates how much the (scaled) image is outside the visible area
        /// </summary>
        private void SetMaxPanValues()
        {
            var rect = new Rect(Image1.RenderSize);
            var bounds = Image1.TransformToAncestor(Border1).TransformBounds(rect);
            maxX = (bounds.Width > Window1.Width) ? (bounds.Width - Window1.Width) / 2.0 : 0;
            maxY = (bounds.Height > Window1.Height) ? (bounds.Height - Window1.Height) / 2.0 : 0;
        }

        /// <summary>
        /// resets the zoom level
        /// </summary>
        private void ResetZoomLevel()
        {
            _mode &= ~Modi.Zoom;

            Image1.RenderTransformOrigin = new Point(0.5, 0.5);
            var tg = new TransformGroup();
            tg.Children.Add(new ScaleTransform());
            tg.Children.Add(new TranslateTransform());
            Image1.RenderTransform = tg;

            SetMaxPanValues();
        }

        /// <summary>
        /// handles mouse click functionality
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid1_MouseDown(object sender, MouseButtonEventArgs e)
        {
            string imagePath = null;
            if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                if (openFileDialog.ShowDialog() == true)
                {
                    imagePath = openFileDialog.FileName;
                    SetImage(imagePath);
                }
            }

            if (IsImageLoaded())
            {
                if (imagePath == null) imagePath = fileList.ElementAt(currentFileIndex);

                if (e.ChangedButton == MouseButton.Middle)
                {
                    Process.Start("explorer.exe", "/select, \"" + imagePath + "\"");
                }

                if (e.ChangedButton == MouseButton.Right)
                {
                    if (e.ClickCount == 2)
                    {
                        Clipboard.SetText(imagePath);
                        ShowMessage("Image path copied to clipboard");
                    }
                    else
                    {
                        var imageDir = Path.GetDirectoryName(imagePath);
                        Clipboard.SetText(imageDir);
                        ShowMessage("Directory path copied to clipboard");
                    }
                }
            }
        }

        /// <summary>
        /// handles mouse wheel zooming
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid1_HandleMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (IsImageLoaded())
            {
                Zoom(e.Delta);
            }
        }

        /// <summary>
        /// handles the start of a drag operation
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image1_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsImageLoaded() && Image1.CaptureMouse())
            {
                var tt = (TranslateTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is TranslateTransform);
                start = e.GetPosition(Border1);
                origin = new Point(tt.X, tt.Y);
            }
        }

        /// <summary>
        /// handles dragging the image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image1_MouseMove(object sender, MouseEventArgs e)
        {
            if (Image1.IsMouseCaptured)
            {
                Vector v = start - e.GetPosition(Border1);
                MoveToNewPosition(v);
            }
        }

        /// <summary>
        /// calculates the new vector while moving with the keyboard
        /// </summary>
        /// <param name="x">-1/1 - left/right</param>
        /// <param name="y">-1/1 - up/down</param>
        private void AdjustKeyboardVector(double x, double y)
        {
            if (x != 0)
            {
                if (Math.Abs(origin.X - kv.X - x) < maxX)
                    kv.X += Math.Sign(x) * 10;
                else if (Math.Abs(origin.X - kv.X - x) >= maxX)
                    kv.X += Math.Sign(x) * (maxX - Math.Abs(origin.X - kv.X));
            }
            else if (y != 0)
            {
                if (Math.Abs(origin.Y - kv.Y - y) < maxY)
                    kv.Y += Math.Sign(y) * 10;
                else if (Math.Abs(origin.Y - kv.Y - y) >= maxY)
                    kv.Y += Math.Sign(y) * (maxY - Math.Abs(origin.Y - kv.Y));
            }
        }

        /// <summary>
        /// moves the image to the new point
        /// </summary>
        /// <param name="v"></param>
        private void MoveToNewPosition(Vector v)
        {
            var tt = (TranslateTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is TranslateTransform);
            tt.X = maxX == 0 ? tt.X : (Math.Abs(origin.X - v.X) >= maxX) ? Math.Sign(origin.X - v.X) * maxX : origin.X - v.X;
            tt.Y = maxY == 0 ? tt.Y : (Math.Abs(origin.Y - v.Y) >= maxY) ? Math.Sign(origin.Y - v.Y) * maxY : origin.Y - v.Y;
        }

        /// <summary>
        /// releases the mouse capture
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Image1_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Image1.ReleaseMouseCapture();
        }

        /// <summary>
        /// uses a scale transform to zoom
        /// </summary>
        /// <param name="delta">-1/1 - smaller/bigger</param>
        private void Zoom(double delta)
        {
            var st = (ScaleTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is ScaleTransform);

            if (delta < 0 && st.ScaleX < _zoomMin || delta > 0 && st.ScaleX > _zoomMax) return;

            var tt = (TranslateTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is TranslateTransform);

            double zoom = delta > 0 ? _zoomStep : 1.0 / _zoomStep;
            st.ScaleX = st.ScaleY *= zoom;

            if (st.ScaleX == 1.0)
                _mode &= ~Modi.Zoom;
            else
                _mode |= Modi.Zoom;

            SetMaxPanValues();

            tt.X = maxX == 0 ? 0 : (Math.Abs(tt.X) > maxX) ? Math.Sign(tt.X) * maxX : tt.X;
            tt.Y = maxY == 0 ? 0 : (Math.Abs(tt.Y) > maxY) ? Math.Sign(tt.Y) * maxY : tt.Y;
        }

        /// <summary>
        /// shows an overlay with help text
        /// </summary>
        private void ShopHelpText()
        {
            Label2.Content = "General\n" +
                "---------\n" +
                "H/I/F1  -  Show this help screen\n" +
                "ESC       -  Close help / End slideshow / Close " + (_isApplication ? "application" : "window") + "\n" +
                "Space   -  End zoom mode / Start/Stop slideshow\n" +
                "\n" +
                "Mouse Navigation\n" +
                "---------------------\n" +
                "Wheel  -  Change zoom level\n" +
                "\n" +
                "Clicks:\n" +
                "Left double    -  Choose new image folder\n" +
                "Middle           -  Open Explorer inside folder and select image file\n" +
                "Right              -  Copy directory name to clipboard\n" +
                "Right double  -  Copy filepath to clipboard\n" +
                "\n" +
                "Move/Drag:\n" +
                "Click+Left/Right/Up/Down  -  Move the image\n" +
                "\n" +
                "Keyboard Navigation\n" +
                "------------------------\n" +
                "Left/Right   -  Show previous/next image\n" +
                "Home/End  -  Show first/last image\n" +
                "\n" +
                "in zoom mode:\n" +
                "Alt+Left/Right/Up/Down  -  Move the image\n" +
                "\n" +
                "in slideshow mode:\n" +
                "PageUp/PageDown  -  Change image duration (1..30 seconds)";
            Label2.Visibility = Visibility.Visible;
            _mode |= Modi.HelpShown;
        }

        /// <summary>
        /// shows the message text and starts a fadeout timer
        /// </summary>
        /// <param name="text"></param>
        private void ShowMessage(string text)
        {
            Label1.Content = text;
            StartMessageFadeoutTimer();
        }

        /// <summary>
        /// starts a timer to hide the message text after specified period
        /// </summary>
        private void StartMessageFadeoutTimer()
        {
            // zero value disables status text
            if (_msgFadeoutSeconds == 0) return;

            Label1.Visibility = Visibility.Visible;

            // negative value disables fadeout
            if (_msgFadeoutSeconds < 0) return;

            timerMessage.Interval = TimeSpan.FromSeconds(_msgFadeoutSeconds);
            timerMessage.Tick += TimerMessage_Tick;
            timerMessage.Start();
        }

        private void TimerMessage_Tick(object sender, EventArgs e)
        {
            timerMessage.Stop();
            timerMessage.Tick -= TimerMessage_Tick;
            Label1.Visibility = Visibility.Hidden;
        }

        /// <summary>
        /// starts the slideshow
        /// </summary>
        /// <param name="showMessage">show message only on real slideshow starts</param>
        private void StartSlideshow(bool showMessage)
        {
            if (slideshowRunning)
            {
                StopSlideshow(0);
            }
            if (showMessage)
            {
                ShowMessage("Slideshow started");
            }
            _mode |= Modi.Slideshow;
            slideshowRunning = true;
            timerSlideshow.Interval = TimeSpan.FromSeconds(_imageDurationSeconds);
            timerSlideshow.Tick += TimerSlideshow_Tick;
            timerSlideshow.Start();
        }

        /// <summary>
        /// pauses or stops the slideshow
        /// </summary>
        /// <param name="mode">0 - stop for restart, 1 - pause, 2 - stop</param>
        private void StopSlideshow(int mode)
        {
            slideshowRunning = false;

            if (mode == 1)
            {
                ShowMessage("Slideshow paused");
            }
            else if (mode == 2)
            {
                _mode &= ~Modi.Slideshow;
                ShowMessage("Slideshow stopped");
            }

            timerSlideshow.Stop();
            timerSlideshow.Tick -= TimerSlideshow_Tick;
        }

        private void TimerSlideshow_Tick(object sender, EventArgs e)
        {
            if (currentFileIndex < fileList.Count() - 1)
            {
                currentFileIndex++;
                SetImage();
            }
            else
            {
                StopSlideshow(2);
            }
        }
    }
}
