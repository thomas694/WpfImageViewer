# WpfImageViewer

A simple fullscreen WPF-based image viewer that closes when you press Esc and also supports a slideshow mode.

## Functionality

The image viewer can be used as a standalone application or its window for image viewing can be included as a dialog into another application.
It supports animated GIFs and can either run them or show a preview image only.

### Mouse

* Left Mouse Button: drag the image around the screen
    * double-click: choose a file/folder to show
* Right Mouse Button: copy directory of current image to clipboard
    * double-click: copy full path of current image to clipboard
* Middle Mouse Button: open Explorer with current file selected
* Mousewheel: Zoom in/out

### Keyboard commands

* H/I/F1: show a help screen
* Esc: close help / slideshow / application
* Home: first file in folder
* End: last file in folder
* Left: previous file in folder
* Right: next file in folder
* Spacebar: center image & reset zoom level

#### in zoom modus

* Alt + Left/Right/Up/Down: move the image around

#### in slideshow modus

* PageUp/PageDown: change the image duration (1..30 seconds)

### Config

Some parameters can be overridden in WpfImageViewer.exe.config.

* ApplicationTitle
    * set the name of the application, e.g. shown during Alt-Tab
    * default: Wpf Image Viewer
* FadeoutSeconds
    * decimal amount of seconds before status text disappears
    * default: 2 seconds
    * 0 disables status text
    * negative values disable fadeout
* IncludedFileFormats
    * extensions to include when trying to view files
    * include dots before extension, separate by only comma, no space
    * default: .bmp,.gif,.jpeg,.jpg,.png,.tif,.tiff
* BackgroundColor
    * tries to set background using a color name, e.g. Pink
    * default: Black
* ZoomMin
    * minimum zoom amount
    * default: 0.1
* ZoomMax
    * maximum zoom amount
    * default: 5
* ZoomStep
    * change in zoom amount per step
    * default: 1.25
* ShowHelpOnLoad
	* shows a help screen on application/dialog load
	* default: False
* RunAnimatedGifs
	* run the animated gif or show a preview only
	* default: True
* ImageDuration
	* decimal amount of seconds to show the image in slideshow mode
	* default: 2 seconds

### Usage as dialog inside another application

Either include the whole project to your solution or include a reference to the exe only. Then you can create a window object 
```csharp
WpfImageViewer.MainWindow wnd = new WpfImageViewer.MainWindow()
wnd.ShowDialog();
```
optionally you can use an overloaded constructor and specify non-default settings
```csharp
// declaration of overloaded constructor
WpfImageViewer.MainWindow wnd = new WpfImageViewer.MainWindow(string folder, bool showHelpOnLoad, bool runAnimatedGifs,
    string backgroundColor, string includedFileFormats, int imageDuration, double fadeoutSeconds, double zoomMin, double zoomMax, double zoomStep)
```
<br/><br/>
