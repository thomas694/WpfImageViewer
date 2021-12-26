# WpfImageViewer

A simple fullscreen WPF-based image viewer that closes when you press Esc and also supports a slideshow mode.

## Functionality / Usage

The image viewer can be used as a standalone application or its window for image viewing can be included as a dialog into another application.
It supports animated GIFs and GIFVs and can either run them or show a preview image only.

You can assign the application as default viewer for any supported media format, but especially for gif/gifv files, by using the windows explorer "open with" "choose another app" "always use this app" functionality.

### Mouse

* Left mouse button: drag the image around the screen
    * double-click: choose a file/folder to show
* Right mouse button: copy directory of current image to clipboard
    * double-click: copy full path of current image to clipboard
* Middle mouse button: open Explorer with current file selected
* Mouse wheel: zoom in/out

### Keyboard commands

* H/I/F1: show a help screen
* Esc: close help / stop slideshow / close application/window
* Home/End: first/last file in image list
* Left/Right: previous/next file in image list
* Spacebar: center image & reset zoom level / start/pause slideshow

#### in zoom mode

* Alt + Left/Right/Up/Down: move the image around

#### in slideshow mode

* PageUp/PageDown: change the image duration (1..30 seconds)

### Config / Parameters

Some settings can be overridden in `WpfImageViewer.exe.config`.

* ApplicationTitle
    * set the name of the application, e.g. shown during Alt-Tab
    * default: Wpf Image Viewer
* BackgroundColor
    * set background color using a color name
    * default: Black
* CloseOnLostFocus
    * closes the application if no longer in focus
    * default: True
* ImageDurationSeconds
    * number of seconds to show the image in slideshow mode
    * default: 2 seconds
* IncludedFileExtensions
    * extensions to include when loading images from folder
    * include dot before extension, separate by only comma, no space
    * default: .bmp,.gif,.gifv,.jpeg,.jpg,.png,.tif,.tiff
* MsgColor
    * set message text color using a color name
    * default: Green
* MsgFadeoutSeconds
    * decimal value of seconds before status text disappears
    * default: 2 seconds
    * 0 disables the status text
    * a negative value disables fadeout
* RunAnimatedGifs
    * run the animated gif/gifv or show a preview only
    * default: True
* ShowHelpOnLoad
    * shows a help screen on application/dialog load
    * default: False
* ZoomMax
    * maximum zoom value
    * default: 5
* ZoomMin
    * minimum zoom value
    * default: 0.1
* ZoomStep
    * change in zoom value per step
    * default: 1.25
<br/>

## Usage as dialog inside another application

Either include the whole project to your solution or include a reference to the exe only. Then you can create a window object 
```csharp
WpfImageViewer.MainWindow wnd = new WpfImageViewer.MainWindow()
wnd.ShowDialog();
```
optionally you can use an overloaded constructor and specify non-default settings
```csharp
// declaration of overloaded constructor
WpfImageViewer.MainWindow wnd = new WpfImageViewer.MainWindow(string folder, bool showHelpOnLoad, bool runAnimatedGifs, bool closeOnLostFocus,
    string backgroundColor, string msgColor, string includedFileExtensions, int imageDurationSeconds, double fadeoutSeconds, double zoomMin, double zoomMax, double zoomStep)
```
or if you only want to specify the folder to load:
```csharp
// declaration of overloaded constructor
WpfImageViewer.MainWindow wnd = new WpfImageViewer.MainWindow(string folder)
```
<br/>

## Enable Gifv file thumbnails in Windows Explorer (Win 10)

By default no thumbnails are shown for gifv files even they are compatible to standard gif files. But you can enable a setting in windows so that thumbnails for gifv files are provided by windows itself.

To enable thumbnails for the current user only, copy the following lines into a text file named e.g. `enable.reg` and double-click it to import the setting into the windows registry:
```
Windows Registry Editor Version 5.00

[HKEY_CURRENT_USER\SOFTWARE\Classes\.gifv\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{C7657C4A-9F68-40fa-A4DF-96BC08EB3551}"
```

To enable thumbnails for all users on the system, copy the following lines into a text file named e.g. `enable.reg` and double-click it to import the setting into the windows registry:
```
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Classes\.gifv\ShellEx\{e357fccd-a995-4576-b01f-234630154e96}]
@="{C7657C4A-9F68-40fa-A4DF-96BC08EB3551}"
```

Afterwards you need to restart your pc or at least log out and in before thumbnails are shown.
<br/><br/>
