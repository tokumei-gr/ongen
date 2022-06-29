using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using WinRT;
using System.Runtime.InteropServices;
using System.Collections.ObjectModel;
using FFMpegCore;
using FFMpegCore.Enums;
using NReco.VideoConverter;
using System.Threading.Tasks;
using Windows.Storage;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Ongen
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private string cwd;

        private AppWindow appWindow;
        WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        SourceGame selectedGame;
        ObservableCollection<SourceGame> games = new();
        bool running = false;
        bool closePending = false;
        string steamAppsPath;
        Status status = Status.IDLE;

        public MainWindow()
        {
            this.InitializeComponent();

            cwd = System.AppDomain.CurrentDomain.BaseDirectory;

            DisableInterface();

            RefreshPlayKey();
            if (ConfigurationManager.AppSettings["PlayKey"] == ConfigurationManager.AppSettings["RelayKey"])
            {
                ConfigurationManager.AppSettings.Set("RelayKey", "=");
            }

            if (Convert.ToBoolean(ConfigurationManager.AppSettings["UpdateCheck"]))
            {
                CheckForUpdate();
            }

            SetGames();

            //ReloadTracks();
            //RefreshTrackList();

            /*if (Convert.ToBoolean(ConfigurationManager.AppSettings["StartEnabled"]))
            {
                StartPoll();
            }*/

            SetupTitlebar();
        }

        private void SetupTitlebar()
        {
            appWindow = GetAppWindowForCurrentWindow();
            Title = "Ongen";
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var titlebar = appWindow.TitleBar;
                titlebar.IconShowOptions = IconShowOptions.HideIconAndSystemMenu;
                titlebar.ExtendsContentIntoTitleBar = true;
                titlebar.PreferredHeightOption = TitleBarHeightOption.Tall;
                titlebar.ButtonBackgroundColor = Colors.Transparent;
                AppTitleBar.Loaded += AppTitleBar_Loaded;
                AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;
                TrySetMicaBackdrop();
            } else
            {
                AppTitleBar.Visibility = Visibility.Collapsed;
            }
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 800, Height = 500 });
        }

        bool TrySetMicaBackdrop()
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // Hooking up the policy object
                m_configurationSource = new Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration();
                this.Activated += Window_Activated;
                this.Closed += Window_Closed;
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // Initial configuration state.
                m_configurationSource.IsInputActive = true;
                SetConfigurationSourceTheme();

                m_micaController = new Microsoft.UI.Composition.SystemBackdrops.MicaController();

                // Enable the system backdrop.
                // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
                m_micaController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                m_micaController.SetSystemBackdropConfiguration(m_configurationSource);
                return true; // succeeded
            }

            return false; // Mica is not supported on this system
        }

        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
            // use this closed window.
            if (m_micaController != null)
            {
                m_micaController.Dispose();
                m_micaController = null;
            }
            this.Activated -= Window_Activated;
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
{
    switch (((FrameworkElement)this.Content).ActualTheme)
    {
        case ElementTheme.Dark:    m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Dark; break;
        case ElementTheme.Light:   m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Light; break;
        case ElementTheme.Default: m_configurationSource.Theme = Microsoft.UI.Composition.SystemBackdrops.SystemBackdropTheme.Default; break;
        }
        }

        private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                SetDragRegionForCustomTitleBar(appWindow);
            }
        }

        private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                SetDragRegionForCustomTitleBar(appWindow);
            }
        }
        // some extreme amounts of microsoft bullshit
        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        internal enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        private double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }

        private void SetDragRegionForCustomTitleBar(AppWindow appWindow)
        {
            // Check to see if customization is supported.
            // Currently only supported on Windows 11.
            if (AppWindowTitleBar.IsCustomizationSupported()
                && appWindow.TitleBar.ExtendsContentIntoTitleBar)
            {
                double scaleAdjustment = GetScaleAdjustment();

                RightPaddingColumn.Width = new GridLength(appWindow.TitleBar.RightInset / scaleAdjustment);
                LeftPaddingColumn.Width = new GridLength(appWindow.TitleBar.LeftInset / scaleAdjustment);

                List<Windows.Graphics.RectInt32> dragRectsList = new();

                Windows.Graphics.RectInt32 dragRectL;
                dragRectL.X = (int)((LeftPaddingColumn.ActualWidth) * scaleAdjustment);
                dragRectL.Y = 0;
                dragRectL.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectL.Width = (int)((IconColumn.ActualWidth
                                        + TitleColumn.ActualWidth
                                        + LeftDragColumn.ActualWidth) * scaleAdjustment);
                dragRectsList.Add(dragRectL);

                Windows.Graphics.RectInt32 dragRectR;
                dragRectR.X = (int)((LeftPaddingColumn.ActualWidth
                                    + IconColumn.ActualWidth
                                    + TitleTextBlock.ActualWidth
                                    + LeftDragColumn.ActualWidth
                                    + SearchColumn.ActualWidth) * scaleAdjustment);
                dragRectR.Y = 0;
                dragRectR.Height = (int)(AppTitleBar.ActualHeight * scaleAdjustment);
                dragRectR.Width = (int)(RightDragColumn.ActualWidth * scaleAdjustment);
                dragRectsList.Add(dragRectR);

                Windows.Graphics.RectInt32[] dragRects = dragRectsList.ToArray();

                appWindow.TitleBar.SetDragRectangles(dragRects);
            }
        }


        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(wndId);
        }

        private async void WaveCreator(string file, string output, SourceGame game)
        {
            // fuck it, raw mode!
            Process ffmpeg;
            var command = $"-y -i \"{file}\" -f wav -flags bitexact -map_metadata -1 -vn -acodec pcm_s16le -ar {game.SampleRate} -ac {game.Channels} \"{Path.GetFullPath(output)}\"";
            try
            {
                ProcessStartInfo ffmpegInfo = new ProcessStartInfo()
                {
                    FileName = "ffmpeg",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = false,
                };
                ffmpeg = Process.Start(ffmpegInfo);
                ffmpeg.WaitForExit();
                info.IsOpen = true;
                info.Title = $"Import successful";
                info.Message = $"{output} is now ready to be used";
                info.Severity = InfoBarSeverity.Success;
            } catch (Exception)
            {
                throw;
            }
        }

        private void StartPoll()
        {
            running = true;
            DisableInterface();
        }

        private void DisableInterface()
        {
            ImportButton.IsEnabled = false;
            YTImportButton.IsEnabled = false;
        }

        private void EnableInterface()
        {
            ImportButton.IsEnabled = true;
            YTImportButton.IsEnabled = true;
        }

        private void SetGames()
        {
            games.Add(new SourceGame
            {
                Name = "Counter-Strike: Global Offensive",
                Id = 730,
                Directory = "common\\Counter-Strike Global Offensive\\",
                ToCfg = "csgo\\cfg\\",
                LibraryName = "csgo\\",
                ExeName = "csgo",
                SampleRate = 22050,
                Blacklist = new()
                {"attack", "attack2", "autobuy", "back", "buy", "buyammo1", "buyammo2", "buymenu", "callvote", "cancelselect", "cheer", "compliment", "coverme", "drop", "duck", "enemydown", "enemyspot", "fallback", "followme", "forward", "getout", "go", "holdpos", "inposition", "invnext", "invprev", "jump", "lastinv", "messagemode", "messagemode2", "moveleft", "moveright", "mute", "negative", "quit", "radio1", "radio2", "radio3", "rebuy", "regroup", "reload", "report", "reportingin", "roger", "sectorclear", "showscores", "slot1", "slot10", "slot2", "slot3", "slot4", "slot5", "slot6", "slot7", "slot8", "slot9", "speed", "sticktog", "takepoint", "takingfire", "teammenu", "thanks", "toggleconsole", "use", "voicerecord"},
                VoiceFadeOut = false
            });

            // can't be arsed to add games I don't play atm

        }

        private void RefreshTrackList()
        {
        }

        private void ReloadTracks()
        {
            if (Directory.Exists(Path.Combine(cwd, selectedGame.LibraryName)))
            {
                selectedGame.Tracks.Clear();
                foreach (var file in Directory.GetFiles(Path.Combine(cwd, selectedGame.LibraryName)))
                {
                    if (selectedGame.FileExtension == Path.GetExtension(file))
                    {
                        var track = new Track()
                        {
                            Name = Path.GetFileNameWithoutExtension(file)
                        };
                        selectedGame.Tracks.Add(track);
                    }
                }
            }
            else
            {
                Directory.CreateDirectory(Path.Combine(cwd, selectedGame.LibraryName));
            }
        }

        private void CheckForUpdate()
        {
            
        }

        private void RefreshPlayKey()
        {
            
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DisableInterface();
            selectedGame = ((sender as ComboBox).SelectedItem as SourceGame);
            ReloadTracks();
            EnableInterface();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new Windows.Storage.Pickers.FileOpenPicker();
            openFileDialog.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.VideosLibrary;
            openFileDialog.FileTypeFilter.Add(".wav");
            openFileDialog.FileTypeFilter.Add(".mp4");
            openFileDialog.FileTypeFilter.Add(".webm");
            openFileDialog.FileTypeFilter.Add(".mp3");
            openFileDialog.FileTypeFilter.Add(".mov");

            var hwnd = this.As<IWindowNative>().WindowHandle;
            var initializeWithWindow = openFileDialog.As<IInitializeWithWindow>();
            initializeWithWindow.Initialize(hwnd);

            IReadOnlyList<Windows.Storage.StorageFile> files = await openFileDialog.PickMultipleFilesAsync();

            bool ready = false;
            if (files.Count > 0)
            {
                DisableInterface();
                // convert them all into wav and place them into a folder with the game name
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Maximum = 1;
                ready = ImportFiles(files);

                if (ready)
                {
                    EnableInterface();
                    ReloadTracks();
                }
            }
        }

        private bool ImportFiles(IReadOnlyList<StorageFile> files)
        {
            int i = 0;
            foreach (var file in files)
            {
                try
                {
                    var outfile = Path.Combine(cwd, selectedGame.LibraryName, Path.GetFileNameWithoutExtension(file.Name) + ".wav");
                    if (!Directory.Exists(Path.Combine(cwd, selectedGame.LibraryName))) {
                        Directory.CreateDirectory(Path.Combine(cwd, selectedGame.LibraryName)); 
                    }
                    WaveCreator(file.Path, outfile, selectedGame);
                    ProgressBar.Value += Math.Clamp(++i, 0, 1);
                }
                catch (Exception)
                {
                    throw;
                }
            }

            return true;
        }

        private async void YTImportButton_Click(object sender, RoutedEventArgs e)
        {
            YTImport dialog = new YTImport();
            dialog.XamlRoot = dataGrid.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.Title = "Import from YouTube";
            dialog.PrimaryButtonText = "Import";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Primary;
            await dialog.ShowAsync();
            
        }

        private void PlayKeyButton_Click(object sender, RoutedEventArgs e)
        {

        }

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        internal interface IWindowNative
        {
            IntPtr WindowHandle { get; }
        }
    }
}
