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
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Dispatching;
using System.Management;
using System.Threading;
using System.Text.RegularExpressions;

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
        DispatcherQueue dispatcher = DispatcherQueue.GetForCurrentThread();
        CancellationTokenSource tk;

        private AppWindow appWindow;
        WindowsSystemDispatcherQueueHelper m_wsdqHelper;
        Microsoft.UI.Composition.SystemBackdrops.MicaController m_micaController;
        Microsoft.UI.Composition.SystemBackdrops.SystemBackdropConfiguration m_configurationSource;

        SourceGame selectedGame;
        ObservableCollection<SourceGame> games = new();
        bool paused = true;
        bool closePending = false;
        string steamAppsPath;
        Status status = Status.IDLE;
        int currentTrack = -1;

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

            StatusLabel.Text = "Idle";
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
                titlebar.InactiveBackgroundColor = Colors.Transparent;
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

        private Task<int> WaveCreator(string file, string output)
        {
            var tcs = new TaskCompletionSource<int>();
            // fuck it, raw mode!
            var command = $"-i \"{file}\" -f wav -flags bitexact -map_metadata -1 -vn -acodec pcm_s16le -ar {selectedGame.SampleRate} -ac {selectedGame.Channels} \"{Path.GetFullPath(output)}\" -y";

                Process ffmpeg = new Process
                {
                    StartInfo =
                    {
                         FileName = $"ffmpeg",
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    },
                   EnableRaisingEvents = true
                };

            ffmpeg.Exited += (sender, args) =>
            {
                tcs.SetResult(ffmpeg.ExitCode);
                ffmpeg.Dispose();
            };


                ffmpeg.Start();
            


            return tcs.Task;
        }

        private void StartPoll()
        {
            paused = false;
            StartButton.Content = "Stop";
            StatusLabel.Text = "Starting";
            DisableInterface();
            StartButton.IsEnabled = true;
            trackList.IsEnabled = true;
            ProgressBar.ShowPaused = false;
            GameSelector.IsEnabled = false;
            /*if (tk == null)
            {
                PollRelay();
            }*/
            PollRelay();
        }

        private async Task FindProcess(string gameDir, string userDataPath)
        {
                try
                {
                    if (!Convert.ToBoolean(ConfigurationManager.AppSettings["OverrideFolders"]))
                    {
                        do
                        {
                            var gameProcess = GetFilePath(selectedGame.ExeName);
                            if (!string.IsNullOrEmpty(gameProcess) && gameProcess.EndsWith(gameDir))
                            {
                                steamAppsPath = gameProcess.Remove(gameProcess.Length - gameDir.Length);
                            }
                            else
                                throw new Exception($"{selectedGame.Name} is not running.");

                            var steamProcess = GetFilePath("Steam");
                            if (!string.IsNullOrEmpty(steamProcess))
                            {
                                userDataPath = steamProcess.Remove(steamProcess.Length - "Steam.exe".Length) + "userdata\\";
                            }

                        Debug.WriteLine(userDataPath);

                            if (Directory.Exists(steamAppsPath))
                            {
                                if (!(selectedGame.Id == 0))
                                    if (Directory.Exists(userDataPath))
                                        break;
                            }
                            else
                                break;
                        } while (true);

                        StatusLabel.Text = userDataPath;

                     await Task.Delay(selectedGame.PollInterval);
                    }
                    else
                    {
                        steamAppsPath = ConfigurationManager.AppSettings["steamapps"];
                        if (Directory.Exists(ConfigurationManager.AppSettings["userdata"]))
                            userDataPath = ConfigurationManager.AppSettings["userdata"];
                        else
                            throw new Exception("Userdata folder does not exist.");
                    }

                    if (!string.IsNullOrEmpty(steamAppsPath))
                        CreateCfgFiles(steamAppsPath);
                }
                catch (Exception)
                {
                    throw;
                }

            return;
        }

        private async void PollRelay()
        {
            // this replaces the pollrelayworker in the original SLAM
            tk = new CancellationTokenSource();
            CancellationToken ct = tk.Token;

            status = Status.SEARCHING;
            var gameDir = Path.Combine(selectedGame.Directory, selectedGame.ExeName + ".exe");
            string userDataPath = null;

            try
            {

                await FindProcess(gameDir, userDataPath);
                status = Status.WORKING;
                StatusLabel.Text = "Active";
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 100;
                if (currentTrack != -1)
                {
                    LoadTrack(currentTrack);
                    DisplayLoaded(currentTrack);
                }
                /*await Task.Run(async () =>
                {
                    ct.ThrowIfCancellationRequested();
                    do
                    {
                        try
                        {
                            var gameFolder = Path.Combine(steamAppsPath, selectedGame.Directory);
                            var gameCfg = Path.Combine(gameFolder, selectedGame.ToCfg) + "ongen_relay.cfg";
                            if (!(selectedGame.Id == 0))
                            {
                                gameCfg = UserDataCFG(userDataPath);
                            }
                            if (File.Exists(gameCfg))
                            {
                                string relayCfg;
                                using (var reader = new StreamReader(gameCfg))
                                {
                                    relayCfg = reader.ReadToEnd();
                                }
                                var command = recog(relayCfg, $"bind \"{ConfigurationManager.AppSettings["RelayKey"]}\" \"(.*?)\"");
                                Debug.WriteLine(command);
                                if (!(string.IsNullOrEmpty(command)))
                                {
                                    if (command.All(char.IsNumber))
                                    {
                                        if (LoadTrack(Convert.ToInt32(command) - 1))
                                        {
                                            // report progress?
                                            Debug.WriteLine(Convert.ToInt32(command) - 1);
                                        }
                                    }
                                    File.Delete(gameCfg);
                                }
                            }
                            await Task.Delay(selectedGame.PollInterval);
                        }
                        catch (Exception ex)
                        {
                            if (!(ex.HResult == -2147024864))
                            {
                                throw;
                            }
                        }
                    } while (!ct.IsCancellationRequested);
                }, tk.Token);*/
            }
            catch (OperationCanceledException e)
            {
                ProgressBar.ShowError = true;
                var d = new ContentDialog()
                {
                    Title = "Relay Cancellation",
                    Content = $"The poll relay worker decided to cancel. Reason: {e.Message}",
                    CloseButtonText = "Ok"
                };
                d.XamlRoot = trackList.XamlRoot;
                d.CloseButtonClick += D_CloseButtonClick;
                await d.ShowAsync();
            }
            catch (Exception e)
            {
                ProgressBar.ShowError = true;
                var d = new ContentDialog()
                {
                    Title = "Failure",
                    Content = $"{e.Message}",
                    CloseButtonText = "Ok"
                };
                d.XamlRoot = trackList.XamlRoot;
                d.CloseButtonClick += D_CloseButtonClick;
                await d.ShowAsync();
            }
        }

        private void D_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            StopPoll();
        }

        private void DeleteCFGs(string steamAppsPath)
        {
            var gameDir = Path.Combine(steamAppsPath, selectedGame.Directory);
            var gameCfgFolder = Path.Combine(gameDir, selectedGame.ToCfg);
            string[] ongenFiles =
            {
                "ongen.cfg", "ongen_tracklist.cfg", "ongen_relay.cfg", "ongen_curtrack.cfg", "ongen_saycurtrack.cfg", "ongen_sayteamcurtrack.cfg"
            };
            var voicefile = Path.Combine(steamAppsPath, selectedGame.Directory) + "voice_input.wav";

            try
            {
                if (File.Exists(voicefile))
                    File.Delete(voicefile);

                foreach (var fileName in ongenFiles)
                {
                    if (File.Exists(gameCfgFolder + fileName))
                        File.Delete(gameCfgFolder + fileName);
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private string recog(string relayCfg, string v)
        {
            var keyd = Regex.Match(relayCfg, v, RegexOptions.IgnoreCase);
            return keyd.Groups[1].ToString();
        }

        private string UserDataCFG(string userDataPath)
        {
            if (Directory.Exists(userDataPath))
            {
                foreach (var userdir in Directory.GetDirectories(userDataPath))
                {
                    var cfgPath = Path.Combine(userdir, selectedGame.Id.ToString()) + "\\local\\cfg\\ongen_relay.cfg";
                    if (File.Exists(cfgPath)) return cfgPath;
                }
            }
            return null;
        }

        private void CreateCfgFiles(string steamAppsPath)
        {
            var gameDir = Path.Combine(steamAppsPath, selectedGame.Directory);
            var gameCfgFolder = Path.Combine(gameDir, selectedGame.ToCfg);

            if (!Directory.Exists(gameCfgFolder))
            {
                throw new Exception("Steamapps folder is incorrect.");
            }

            using (var ongen = new StreamWriter(gameCfgFolder + "ongen.cfg")) { 
                ongen.WriteLine("alias ongen_listtracks \"exec ongen_tracklist.cfg\"");
                ongen.WriteLine("alias list ongen_listtracks");
                ongen.WriteLine("alias tracks ongen_listtracks");
                ongen.WriteLine("alias la ongen_listtracks");
                ongen.WriteLine("alias ongen_play ongen_play_on");
                ongen.WriteLine("alias ongen_play_on \"alias ongen_play ongen_play_off; voice_inputfromfile 1; voice_loopback 1; +voicerecord\"");
                ongen.WriteLine("alias ongen_play_off \"-voicerecord; voice_inputfromfile 0; voice_loopback 0; alias ongen_play ongen_play_on\"");
                ongen.WriteLine("alias ongen_updatecfg \"host_writeconfig ongen_relay\"");
                if (Convert.ToBoolean(ConfigurationManager.AppSettings["HoldToPlay"]))
                {
                    ongen.WriteLine("alias +ongen_hold_play ongen_play_on");
                    ongen.WriteLine("alias -ongen_hold_play ongen_play_off");
                    ongen.WriteLine($"bind V +ongen_hold_play");
                }
                else
                {
                    ongen.WriteLine($"bind V ongen_play");
                }
                ongen.WriteLine("alias ongen_curtrack \"exec ongen_curtrack.cfg\"");
                ongen.WriteLine("alias ongen_saycurtrack \"exec ongen_saycurtrack.cfg\"");
                ongen.WriteLine("alias ongen_sayteamcurtrack \"exec ongen_sayteamcurtrack.cfg\"");

                foreach (var track in selectedGame.Tracks)
                {
                    var index = selectedGame.Tracks.IndexOf(track);
                    ongen.WriteLine($"alias {index + 1} \"bind {ConfigurationManager.AppSettings["RelayKey"]} {index + 1}; ongen_updatecfg; echo Loaded: {track.Name}\"");
                    foreach (var trackTag in track.Tags)
                    {
                        ongen.WriteLine($"alias {trackTag} \"bind {ConfigurationManager.AppSettings["RelayKey"]} {index + 1}; ongen_updatecfg; echo Loaded: {track.Name}\"");
                    }

                    if (!String.IsNullOrEmpty(track.Hotkey))
                    {
                        ongen.WriteLine($"alias {track.Hotkey} \"bind {ConfigurationManager.AppSettings["RelayKey"]} {index + 1}; ongen_updatecfg; echo Loaded: {track.Name}\"");
                    }
                }

                var cfgData = "voice_enable 1; voice_modenable 1; voice_forcemicrecord 0; con_enable 1";
                if (selectedGame.VoiceFadeOut)
                {
                    cfgData += "; voice_fadeouttime 0.0";
                }

                ongen.WriteLine(cfgData);
            }

            using (var ongenTrackList = new StreamWriter(gameCfgFolder + "ongen_tracklist.cfg"))
            {
                ongenTrackList.WriteLine("echo \"You can select tracks either by typing a tag, or their track number.\"");
                ongenTrackList.WriteLine("echo \"--------------------Tracks--------------------\"");
                foreach (var track in selectedGame.Tracks)
                {
                    var index = selectedGame.Tracks.IndexOf(track);
                    ongenTrackList.WriteLine($"echo \"{index + 1}. {track.Name} [{"'" + String.Join("', '", track.Tags) + "'"}]\"");
                }
                ongenTrackList.Write("echo \"----------------------------------------------\"");
            }
        }

        private string GetFilePath(string exeName)
        {
            var wmiQueryString = $"Select * from Win32_Process Where Name = \"{exeName}.exe\"";

            var searcher = new ManagementObjectSearcher(wmiQueryString);
            var results = searcher.Get();
            var process = results.Cast<ManagementObject>().FirstOrDefault();
            if (process != null)
            {
                var exePath = process["ExecutablePath"];
                StatusLabel.Text = exePath.ToString();
                var procPath = exePath != null ? exePath.ToString() : null;
                if (!string.IsNullOrWhiteSpace(procPath))
                    return process["ExecutablePath"].ToString();
            }
            return null;
        }
        private void StopPoll()
        {
            status = Status.IDLE;
            /*if (tk != null)
            {
                tk.Cancel();
                tk.Dispose();
                tk = null;
                StatusLabel.Text = "Deleting temporary files";
                if (!string.IsNullOrEmpty(steamAppsPath))
                {
                    DeleteCFGs(steamAppsPath);
                }
            }*/
            StatusLabel.Text = "Deleting temporary files";
            if (!string.IsNullOrEmpty(steamAppsPath))
            {
                DeleteCFGs(steamAppsPath);
            }
            paused = true;
            StartButton.Content = "Start";
            StatusLabel.Text = "Stopped";
            EnableInterface();
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Value = 0;
            ProgressBar.ShowPaused = true;
            ProgressBar.ShowError = false;
            GameSelector.IsEnabled = true;
        }

        private void DisableInterface()
        {
            ImportButton.IsEnabled = false;
            YTImportButton.IsEnabled = false;
            StartButton.IsEnabled = false;
            PlayKeyButton.IsEnabled = false;
            trackList.IsEnabled = false;
        }

        private void EnableInterface()
        {
            ImportButton.IsEnabled = true;
            YTImportButton.IsEnabled = true;
            StartButton.IsEnabled = true;
            PlayKeyButton.IsEnabled = true;
            trackList.IsEnabled = true;
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

            games.Add(new SourceGame
            {
                Name = "Team Fortress 2",
                Directory = "common\\Team Fortress 2\\",
                ToCfg = "tf\\cfg\\",
                LibraryName = "tf2\\",
                SampleRate = 22050,
                Blacklist = new()
                {"attack", "attack2", "attack3", "back", "build", "cancelselect", "centerview", "changeclass", "changeteam", "disguiseteam", "duck", "forward", "grab", "invnext", "invprev", "jump", "kill", "klook", "lastdisguise", "lookdown", "lookup", "moveleft", "moveright", "moveup", "pause", "quit", "reload", "say", "screenshot", "showmapinfo", "showroundinfo", "showscores", "slot1", "slot10", "slot2", "slot3", "slot4", "slot5", "slot6", "slot7", "slot8", "slot9", "strafe", "toggleconsole", "voicerecord"},
            });

            games.Add(new SourceGame
            {
                Name = "Garry's Mod",
                Directory = "common\\GarrysMod\\",
                ToCfg = "garrysmod\\cfg\\",
                LibraryName = "gmod\\"
            });

        }

        private void RefreshTrackList()
        {
            trackList.ItemsSource = null;
            trackList.ItemsSource = selectedGame.Tracks;
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
                RefreshTrackList();
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
            currentTrack = -1;
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

            if (files.Count > 0)
            {
                DisableInterface();
                // convert them all into wav and place them into a folder with the game name
                StatusLabel.Text = $"Starting the converter...";
                ProgressBar.IsIndeterminate = true;
                ProgressBar.ShowPaused = false;
                ProgressBar.Value = 0;
                ProgressBar.Maximum = files.Count;
                bool ready = await ImportFiles(files);

                if (ready)
                {
                    EnableInterface();
                    StatusLabel.Text = "Idle";
                    ReloadTracks();
                    ProgressBar.IsIndeterminate = true;
                    ProgressBar.ShowPaused = true;
                }
            }
        }

        private async Task<bool> ImportFiles(IReadOnlyList<StorageFile> files)
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
                    var result = await WaveCreator(file.Path, outfile);
                        ProgressBar.IsIndeterminate = false;
                        Debug.WriteLine(result);
                        ReloadTracks();
                        StatusLabel.Text = $"Importing {i + 1} out of {files.Count} files...";
                        ProgressBar.Value = i;
                        i++;
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
            dialog.XamlRoot = trackList.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.Title = "Import from YouTube";
            dialog.PrimaryButtonText = "Import";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = ContentDialogButton.Primary;
            await dialog.ShowAsync();
            
        }

        private async void PlayKeyButton_Click(object sender, RoutedEventArgs e)
        {
            SelectKey dialog = new SelectKey("V");
            dialog.XamlRoot = trackList.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.DefaultButton = ContentDialogButton.Primary;
            await dialog.ShowAsync();
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

        private void trackList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var track = (sender as DataGrid).SelectedIndex;
            if (track != -1)
            {
                currentTrack = track;
                SetVolumeButton.IsEnabled = true;
                TrimButton.IsEnabled = true;
                if (status == Status.WORKING)
                {
                    LoadTrack(track);
                    DisplayLoaded(track);
                }
            }
            else
            {
                SetVolumeButton.IsEnabled = false;
                TrimButton.IsEnabled = false;
            }
        }

        private void DisplayLoaded(int track)
        {
            for (int i = 0; i <= selectedGame.Tracks.Count - 1; i++)
            {
                selectedGame.Tracks[i].Loaded = false;
                //(trackList.Items[i] as Track).Loaded = false;
            }
            selectedGame.Tracks[track].Loaded = true;
            //(trackList.Items[track] as Track).Loaded = true;
        }

        private bool LoadTrack(int index)
        {
            if (selectedGame.Tracks.Count > index)
            {
                Debug.WriteLine(index);
                var track = selectedGame.Tracks[index];
                var voicefile = Path.Combine(steamAppsPath, selectedGame.Directory) + "voice_input.wav";
                // move the track to the game directory to be processed as a voice input
                try
                {
                    if (File.Exists(voicefile)) File.Delete(voicefile);

                    var trackFile = Path.Combine(cwd, selectedGame.LibraryName, track.Name + selectedGame.FileExtension);
                    if (File.Exists(trackFile))
                    {
                        if (track.Volume == 100 && track.StartPos <= 0 && track.EndPos <= 0)
                        {
                            StatusLabel.Text = track.Name;
                            File.Copy(trackFile, voicefile);
                        }
                        else
                        {
                            // do some audio black magic
                        }
                    }
                    var gameCfgFolder = Path.Combine(steamAppsPath, selectedGame.Directory, selectedGame.ToCfg);
                    using (var ongenCurTrack = new StreamWriter(gameCfgFolder + "ongen_curtrack.cfg"))
                    {
                        ongenCurTrack.WriteLine($"echo \"[ONGEN] Track name: {track.Name}\"");
                    }
                    using (var ongenSayCurTrack = new StreamWriter(gameCfgFolder + "ongen_saycurtrack.cfg"))
                    {
                        ongenSayCurTrack.WriteLine($"say \"[ONGEN] Track name: {track.Name}\"");
                    }
                    using (var ongenSayTeamCurTrack = new StreamWriter(gameCfgFolder + "ongen_sayteamcurtrack.cfg"))
                    {
                        ongenSayTeamCurTrack.WriteLine($"say_team \"[ONGEN] Track name: {track.Name}\"");
                    }
                    currentTrack = index;
                    return true;
                } catch (Exception)
                {
                    throw;
                }
            }
            else
            {
                return false;
            }
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (paused)
            {
                var reminder = new ContentDialog()
                {
                    Title = "Steps within the game",
                    Content = "Please type \"exec ongen\" in the console to ensure the sounds play",
                    CloseButtonText = "Ok"
                };
                reminder.XamlRoot = trackList.XamlRoot;
                await reminder.ShowAsync();
                StartPoll();
            }
            else
            {
                StopPoll();
            }
        }

        private async void SetVolumeButton_Click(object sender, RoutedEventArgs e)
        {
            SetVolume dialog = new SetVolume(selectedGame.Tracks[currentTrack].Volume);
            dialog.XamlRoot = trackList.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Title = $"Set a new volume for \"{selectedGame.Tracks[currentTrack].Name}\"";
            await dialog.ShowAsync();
        }

        private async void TrimButton_Click(object sender, RoutedEventArgs e)
        {
            Trim dialog = new Trim(selectedGame.Tracks[currentTrack].StartPos, selectedGame.Tracks[currentTrack].EndPos);
            dialog.XamlRoot = trackList.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.DefaultButton = ContentDialogButton.Primary;
            dialog.Title = $"Trim \"{selectedGame.Tracks[currentTrack].Name}\"";
            await dialog.ShowAsync();
        }
    }
}
