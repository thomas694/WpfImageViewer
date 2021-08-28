using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

        // import settings from config file
        string applicationTitle = Properties.Settings.Default.ApplicationTitle;
        string _backgroundColor = Properties.Settings.Default.BackgroundColor;
        string _includedFileFormats = Properties.Settings.Default.IncludedFileFormats;
        string[] fileFormats;
        double _fadeoutSeconds = Properties.Settings.Default.FadeoutSeconds;
        double _zoomMin = Properties.Settings.Default.ZoomMin;
        double _zoomMax = Properties.Settings.Default.ZoomMax;
        double _zoomStep = Properties.Settings.Default.ZoomStep;
        int _imageDuration = Properties.Settings.Default.ImageDuration;
        bool _showHelpOnLoad = Properties.Settings.Default.ShowHelpOnLoad;
        bool _runAnimatedGifs = Properties.Settings.Default.RunAnimatedGifs;

        // related to file navigation
        string _directory;
        IEnumerable<string> fileList = new List<string>();
        int currentFileIndex;

        Modi _mode = Modi.Normal;
        bool slideshowRunning = false;

        // related to image dragging
        Vector kv;
        Point origin;
        Point start;
        double maxX;
        double maxY;

        DispatcherTimer timerFilename = new DispatcherTimer();
        DispatcherTimer timerSlideshow = new DispatcherTimer();

        public MainWindow()
        {
            Initialize();
        }

        /// <summary>
        /// Constructor with settings for showing the window in another application
        /// </summary>
        /// <param name="folder">the folder with the images</param>
        /// <param name="showHelpOnLoad">whether to show help screen at start</param>
        /// <param name="runAnimatedGifs">whether to show preview image or run gifs</param>
        /// <param name="backgroundColor">System.Windows.Media.Colors constant name</param>
        /// <param name="includedFileFormats">file formats to show (e.g. ".jpg,.png")</param>
        /// <param name="imageDuration">image duration in sec</param>
        /// <param name="fadeoutSeconds">tooltip duration in sec</param>
        /// <param name="zoomMin">minimum zoom level (e.g. 0.1)</param>
        /// <param name="zoomMax">maximum zoom level (e.g. 5.0)</param>
        /// <param name="zoomStep">zoom step in percentage (e.g. 1.25)</param>
        public MainWindow(string folder, bool showHelpOnLoad, bool runAnimatedGifs,
            string backgroundColor, string includedFileFormats, int imageDuration, double fadeoutSeconds, double zoomMin, double zoomMax, double zoomStep)
        {
            _directory = folder;
            _showHelpOnLoad = showHelpOnLoad;
            _runAnimatedGifs = runAnimatedGifs;
            _backgroundColor = backgroundColor;
            _includedFileFormats = includedFileFormats;
            _imageDuration = imageDuration;
            _fadeoutSeconds = fadeoutSeconds;
            _zoomMin = zoomMin;
            _zoomMax = zoomMax;
            _zoomStep = zoomStep;

            Initialize();
        }

        private void Initialize()
        {
            InitializeComponent();

            // application title from config file
            try
            {
                Window1.Title = applicationTitle;
            }
            catch
            {
                Window1.Title = "WpfImageViewer";
            }

            // application background color from config file
            SolidColorBrush backgroundFill;
            try
            {
                backgroundFill = (SolidColorBrush)new BrushConverter().ConvertFromString(_backgroundColor);
            }
            catch
            {
                backgroundFill = (SolidColorBrush)new BrushConverter().ConvertFromString("Black");
            }
            Window1.Background = backgroundFill;
            Grid1.Background = backgroundFill;
            Border1.Background = backgroundFill;

            // file formats from config file
            try
            {
                fileFormats = _includedFileFormats.Split(',');
            }
            catch
            {
                fileFormats = new[] { ".jpg", ".png", ".bmp", ".jpeg", ".gif", ".tif", ".tiff" };
            }

            PreviewKeyDown += new KeyEventHandler(HandleKey);
        }

        /// <summary>
        /// wait until Window1 is Loaded() so we can get its size before displaying an image
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
            if (_showHelpOnLoad)
            {
                ShopHelpText();
            }
        }

        private bool _imageLoaded = false;

        private bool IsImageLoaded()
        {
            return _imageLoaded;
        }

        /// <summary>
        /// sets the image, updates the file list/index
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

                    Label1.Content = imagePath;
                }
                else
                {
                    if (File.Exists(imagePath))
                    {
                        UpdateFileList(Path.GetDirectoryName(imagePath));
                        currentFileIndex = fileList.ToList().IndexOf(imagePath);
                        SetImage();
                    }
                    else if (Directory.Exists(imagePath))
                    {
                        UpdateFileList(imagePath);
                        currentFileIndex = 0;
                        SetImage();
                    }
                    else
                    {
                        Label1.Content = "Invalid path: " + imagePath;
                        Image1.Source = null;
                        _imageLoaded = false;
                    }
                }
            }
            catch
            {
                Label1.Content = "Invalid image: " + imagePath;

                //still need to update fileList & index
                var directoryName = Path.GetDirectoryName(imagePath);
                UpdateFileList(directoryName);
                currentFileIndex = fileList.ToList().IndexOf(imagePath);

                //Image1.Source = null;
                Image1.Visibility = Visibility.Hidden;  //source file is used for middle-click, so can't set to null
            }

            // set the text visible and start the countdown to make it disappear
            StartFadeTimer();
        }

        /// <summary>
        /// updates the fileList with only files whose extensions are in imageFileExtensions
        /// </summary>
        /// <param name="directoryName"></param>
        private void UpdateFileList(string directoryName)
        {
            if (fileFormats[0] == "" || fileFormats[0] == "*")
            {
                fileList = Directory
                    .EnumerateFiles(directoryName)
                    .OrderBy(x => x, new NaturalStringComparer());
            }
            else
            {
                fileList = Directory
                    .EnumerateFiles(directoryName)
                    .Where(f => fileFormats.Any(f.ToLower().EndsWith))
                    .OrderBy(x => x, new NaturalStringComparer());
            }
        }

        /// <summary>
        /// handles keypresses
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void HandleKey(object sender, KeyEventArgs e)
        {
            if ((_mode & Modi.HelpShown) != 0 && !(Keyboard.Modifiers == ModifierKeys.None && (e.Key == Key.H || e.Key == Key.I || e.Key == Key.F1)))
            {
                Label2.Visibility = Visibility.Hidden;
                _mode &= ~Modi.HelpShown;
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

                    case Key.Right:
                        if (currentFileIndex < fileList.Count() - 1)
                        {
                            currentFileIndex++;
                            ResetZoomLevel();
                            SetImage();
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
                        if (_imageDuration > 1) _imageDuration--;
                        ShowMessage($"Image duration: {_imageDuration} sec");
                        StartSlideshow(false);
                        break;

                    case Key.PageUp:
                        if (_imageDuration < 30) _imageDuration++;
                        ShowMessage($"Image duration: {_imageDuration} sec");
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
        /// handles mouse buttons on the Grid
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
                        Label1.Content = "Image path copied to clipboard";
                    }
                    else
                    {
                        var imageDir = Path.GetDirectoryName(imagePath);
                        Clipboard.SetText(imageDir);
                        Label1.Content = "Directory path copied to clipboard";
                    }
                    StartFadeTimer();
                }
            }
        }

        /// <summary>
        /// handles mousewheel scrolling
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
        /// handles clicks on an image
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
        /// handles mouse moves on the image
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
        /// releases the mouse cursor
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
        /// <param name="eDelta">-1/1 - smaller/bigger</param>
        private void Zoom(double eDelta)
        {
            var st = (ScaleTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is ScaleTransform);

            if (eDelta < 0 && st.ScaleX < _zoomMin || eDelta > 0 && st.ScaleX > _zoomMax) return;

            var tt = (TranslateTransform)((TransformGroup)Image1.RenderTransform).Children.First(tr => tr is TranslateTransform);

            double zoom = eDelta > 0 ? _zoomStep : 1.0 / _zoomStep;
            st.ScaleX = st.ScaleY *= zoom;

            if (st.ScaleX == 1.0)
                _mode &= ~Modi.Zoom;
            else
                _mode |= Modi.Zoom;

            SetMaxPanValues();

            tt.X = maxX == 0 ? 0 : (Math.Abs(tt.X) > maxX) ? Math.Sign(tt.X) * maxX : tt.X;
            tt.Y = maxY == 0 ? 0 : (Math.Abs(tt.Y) > maxY) ? Math.Sign(tt.Y) * maxY : tt.Y;
        }

        private void ShopHelpText()
        {
            Label2.Content = "General\n" +
                "---------\n" +
                "H/I/F1  -  Show this help screen\n" +
                "ESC       -  Close help / End slideshow / Close application\n" +
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

        private void ShowMessage(string text)
        {
            Label1.Content = text;
            StartFadeTimer();
        }

        /// <summary>
        /// starts a countdown to hide the status text after some time
        /// </summary>
        private void StartFadeTimer()
        {
            // zero value in config file disables status text
            if (_fadeoutSeconds == 0) return;

            Label1.Visibility = Visibility.Visible;

            // negative value in config file disables fadeout
            if (_fadeoutSeconds < 0) return;

            timerFilename.Interval = TimeSpan.FromSeconds(_fadeoutSeconds);
            timerFilename.Tick += TimerFilename_Tick;
            timerFilename.Start();
        }

        /// <summary>
        /// hides the text and stops the timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerFilename_Tick(object sender, EventArgs e)
        {
            timerFilename.Stop();
            timerFilename.Tick -= TimerFilename_Tick;
            Label1.Visibility = Visibility.Hidden;
        }

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
            timerSlideshow.Interval = TimeSpan.FromSeconds(_imageDuration);
            timerSlideshow.Tick += TimerSlideshow_Tick;
            timerSlideshow.Start();
        }

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
