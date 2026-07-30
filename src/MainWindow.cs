using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace MsiHardwareConsole
{
    internal sealed class MainWindow : Window
    {
        private static readonly Brush BackgroundBrush = MakeBrush("#F4F7FB");
        private static readonly Brush SurfaceBrush = MakeBrush("#FFFFFF");
        private static readonly Brush TextBrush = MakeBrush("#17233C");
        private static readonly Brush MutedBrush = MakeBrush("#68758A");
        private static readonly Brush AccentBrush = MakeBrush("#2879E8");
        private static readonly Brush TealBrush = MakeBrush("#10A6A2");
        private static readonly Brush GoodBrush = MakeBrush("#16856A");
        private static readonly Brush WarningBrush = MakeBrush("#D97816");
        private static readonly Brush DangerBrush = MakeBrush("#D84A4A");
        private static readonly Brush PurpleBrush = MakeBrush("#7959C8");
        private static readonly Brush CardBorderBrush = MakeBrush("#E0E7F0");

        private readonly MsiWmiController controller = new MsiWmiController();
        private readonly SystemUsageReader usageReader = new SystemUsageReader();
        private readonly DispatcherTimer refreshTimer = new DispatcherTimer();
        private readonly AppSettings settings;
        private readonly bool skipAutoStartSetup;
        private readonly Dictionary<string, ModeCardInfo> modeCards = new Dictionary<string, ModeCardInfo>();
        private readonly Dictionary<string, StorageCardInfo> storageCards = new Dictionary<string, StorageCardInfo>();
        private UniformGrid storageGrid;
        private Grid overlayLayer;
        private Border overlayPanel;
        private ScrollViewer mainScroll;
        private StackPanel mainContentRoot;
        private PerformanceHistoryChart overlayUsageChart;
        private PerformanceHistoryChart overlayTemperatureChart;
        private PerformanceHistoryChart dashboardCpuUsageChart;
        private PerformanceHistoryChart dashboardCpuTemperatureChart;
        private PerformanceHistoryChart dashboardGpuUsageChart;
        private PerformanceHistoryChart dashboardGpuTemperatureChart;
        private PerformanceHistoryChart dashboardIntegratedGpuUsageChart;
        private string overlayMetric;
        private readonly List<int> cpuUsageHistory = new List<int>();
        private readonly List<int> discreteGpuUsageHistory = new List<int>();
        private readonly List<int> integratedGpuUsageHistory = new List<int>();
        private readonly List<int> cpuTemperatureHistory = new List<int>();
        private readonly List<int> gpuTemperatureHistory = new List<int>();

        private Forms.NotifyIcon trayIcon;
        private TextBlock cpuTemperature;
        private TextBlock cpuUsage;
        private RatioBar cpuUsageBar;
        private TextBlock gpuTemperature;
        private TextBlock gpuUsage;
        private RatioBar gpuUsageBar;
        private TextBlock integratedGpuUsage;
        private RatioBar integratedGpuUsageBar;
        private TextBlock fanStatus;
        private Slider fixedSlider;
        private TextBlock fixedValue;
        private Button fixedOffButton;
        private Slider sustainedProtectionSlider;
        private Slider emergencyProtectionSlider;
        private Slider releaseProtectionSlider;
        private TextBlock sustainedProtectionValue;
        private TextBlock emergencyProtectionValue;
        private TextBlock releaseProtectionValue;
        private CheckBox autoStartCheckBox;
        private TextBlock autoStartDetail;
        private Border blastCard;
        private FrameworkElement fanHeaderElement;
        private string activeMode;
        private string modeBeforeBlast = "Automatic";
        private bool connected;
        private bool fanControlVerified;
        private HardwareCompatibility compatibility;
        private bool applying;
        private bool modeUsesFullBlast;
        private DateTime fullBlastHighSinceUtc = DateTime.MinValue;
        private DateTime fullBlastCoolSinceUtc = DateTime.MinValue;
        private const int FullBlastConfirmationSeconds = 20;
        private bool fixedFanOff;
        private bool exitRequested;
        private bool initializingSettings = true;
        private bool refreshInProgress;
        private bool adaptiveSizeApplied;
        private WindowState lastVisibleWindowState = WindowState.Normal;
        private int refreshCycle;
        private int lastCpuTemperature;
        private int lastGpuTemperature;
        private int lastFanRpm;

        public MainWindow(bool skipAutoStartSetup, string forcedLanguage)
        {
            this.skipAutoStartSetup = skipAutoStartSetup;
            settings = SettingsStore.Load();
            NormalizeSettings();
            if (!string.IsNullOrEmpty(forcedLanguage)) settings.Language = forcedLanguage;
            Localization.Chinese = string.Equals(settings.Language, "zh-CN", StringComparison.OrdinalIgnoreCase);
            fixedFanOff = settings.FixedFanSpeed == 0;
            activeMode = NormalizeMode(settings.SelectedFanMode);

            Title = T("MSI Hardware Console", "MSI 硬件控制台");
            MinWidth = 720;
            MinHeight = 420;
            Width = 960;
            Height = 640;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = BackgroundBrush;
            FontFamily = new FontFamily("Microsoft YaHei UI");
            FontSize = 14;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            SetWindowIcon();

            Content = BuildContent();
            Loaded += delegate
            {
                if (adaptiveSizeApplied) return;
                ApplyAdaptiveWindowSize();
                adaptiveSizeApplied = true;
            };
            Closing += OnClosing;
            PreviewKeyDown += delegate(object sender, KeyEventArgs e)
            {
                if (e.Key == Key.Escape && overlayLayer != null && overlayLayer.Visibility == Visibility.Visible)
                {
                    CloseOverlay();
                    e.Handled = true;
                }
            };
            StateChanged += delegate
            {
                if (WindowState != WindowState.Minimized) lastVisibleWindowState = WindowState;
            };
            refreshTimer.Interval = TimeSpan.FromSeconds(1);
            refreshTimer.Tick += delegate { RefreshMetricsAsync(); };
            CreateTrayIcon();
            SetModeSelection(activeMode);
        }

        public void InitializeRuntime()
        {
            if (SecurityContext.IsAdministrator())
            {
                try
                {
                    controller.Connect();
                    connected = true;
                    compatibility = HardwareCompatibility.Detect(controller.Version);
                    fanControlVerified = compatibility.FanControlVerified;
                    if (fanControlVerified) ApplyMode(activeMode, false);
                    else SetFanStatus(T("Monitoring only · fan writes are locked on unverified hardware · ", "仅监控 · 未验证硬件已锁定风扇写入 · ") + compatibility.Model + " · WMI " + compatibility.WmiVersion, WarningBrush);
                }
                catch (Exception ex)
                {
                    connected = false;
                    SetFanStatus(T("Hardware connection failed · ", "硬件连接失败 · ") + Localization.Error(ex.Message), DangerBrush);
                }
            }
            else
            {
                SetFanStatus(T("Administrator permission is required to read and control the fan", "需要管理员权限才能读取和控制风扇"), DangerBrush);
            }

            if (SecurityContext.IsAdministrator() && settings.StartWithWindowsToTray && !skipAutoStartSetup)
            {
                try { AutoStartManager.EnsureInstalled(); }
                catch (Exception ex) { SetFanStatus(T("Startup setup failed · ", "开机自启设置失败 · ") + Localization.Error(ex.Message), DangerBrush); }
            }
            UpdateAutoStartDetail();
            initializingSettings = false;
            RefreshMetricsAsync();
            refreshTimer.Start();
        }

        private UIElement BuildContent()
        {
            var host = new Grid();
            mainScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var root = new StackPanel { Margin = new Thickness(30, 22, 30, 30), Background = BackgroundBrush };
            mainContentRoot = root;
            mainScroll.Content = root;
            root.Children.Add(BuildHeader());
            root.Children.Add(BuildPerformanceHeader());
            root.Children.Add(BuildPerformanceGrid());
            root.Children.Add(SectionHeader(T("Storage", "硬盘空间"), null));
            root.Children.Add(BuildStorageGrid());
            fanHeaderElement = BuildFanHeader();
            root.Children.Add(fanHeaderElement);
            root.Children.Add(BuildBlastCard());
            root.Children.Add(BuildModeGrid());
            root.Children.Add(SectionHeader(T("Thermal guard", "高温保护"), null));
            root.Children.Add(BuildProtectionSettingsCard());
            root.Children.Add(SectionHeader(T("Startup and notification area", "启动与托盘"), null));
            root.Children.Add(BuildSettingsCard());
            root.Children.Add(new TextBlock
            {
                Text = T("Performance cards: open 60-second charts  ·  Mode cards: click to apply, right-click to inspect curves",
                    "左键性能卡：查看 60 秒图表  ·  左键模式卡：立即应用  ·  右键模式卡：在浮层中查看曲线"),
                Foreground = MutedBrush,
                FontSize = 11,
                Margin = new Thickness(8, 12, 8, 0)
            });
            host.Children.Add(mainScroll);

            overlayLayer = new Grid
            {
                Visibility = Visibility.Collapsed,
                Background = new SolidColorBrush(Color.FromArgb(150, 20, 29, 45))
            };
            overlayLayer.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (!ReferenceEquals(e.OriginalSource, overlayLayer)) return;
                e.Handled = true;
                CloseOverlay();
            };
            overlayPanel = new Border
            {
                Background = BackgroundBrush,
                BorderBrush = CardBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(20),
                Padding = new Thickness(24),
                Margin = new Thickness(42),
                Width = 800,
                MaxWidth = 860,
                MaxHeight = 720,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Color.FromRgb(16, 28, 47), BlurRadius = 28, ShadowDepth = 8, Opacity = 0.28 }
            };
            overlayLayer.Children.Add(overlayPanel);
            Panel.SetZIndex(overlayLayer, 10);
            host.Children.Add(overlayLayer);
            return host;
        }

        private UIElement BuildHeader()
        {
            var grid = new Grid { Margin = new Thickness(3, 0, 3, 16) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var logo = new Image { Width = 72, Height = 72, Margin = new Thickness(0, 0, 17, 0) };
            string logoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MSIHardwareConsole-header.png");
            if (File.Exists(logoPath)) logo.Source = new BitmapImage(new Uri(logoPath));
            grid.Children.Add(logo);

            var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleStack.Children.Add(new TextBlock { Text = T("MSI Hardware Console", "MSI 硬件控制台"), FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            titleStack.Children.Add(new TextBlock { Text = T("Temperatures, utilization, storage, and fan control at a glance", "温度、占用率、硬盘与风扇，一眼就够"), FontSize = 13, Foreground = MutedBrush, Margin = new Thickness(1, 5, 0, 0) });
            Grid.SetColumn(titleStack, 1);
            grid.Children.Add(titleStack);

            var language = SoftButton(Localization.Chinese ? "English" : "中文");
            language.Padding = new Thickness(13, 7, 13, 7);
            language.Click += delegate { ToggleLanguage(); };
            Grid.SetColumn(language, 2);
            grid.Children.Add(language);

            return grid;
        }

        private UIElement BuildPerformanceHeader()
        {
            var grid = new Grid { Margin = new Thickness(3, 18, 3, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = T("Performance overview", "性能概览"), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center });
            var charts = SoftButton(T("▥  All charts", "▥  全部图表"));
            charts.Padding = new Thickness(13, 7, 13, 7);
            charts.Click += delegate { ShowPerformanceDashboardOverlay(); };
            Grid.SetColumn(charts, 1);
            grid.Children.Add(charts);
            return grid;
        }

        private UIElement SectionHeader(string title, string note)
        {
            var grid = new Grid { Margin = new Thickness(3, 18, 3, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            if (!string.IsNullOrEmpty(note))
            {
                var right = new TextBlock { Text = note, Foreground = MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Bottom };
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
            }
            return grid;
        }

        private UIElement BuildPerformanceGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var cpu = BuildProcessorCard("CPU", T("Processor", "处理器"), AccentBrush, out cpuTemperature, out cpuUsage, out cpuUsageBar);
            cpu.Margin = new Thickness(5);
            cpu.Cursor = Cursors.Hand;
            cpu.ToolTip = T("Open the last 60 seconds of utilization and temperature", "点击查看最近 60 秒的使用率与温度");
            cpu.MouseLeftButtonUp += delegate { ShowPerformanceOverlay("CPU"); };
            grid.Children.Add(cpu);
            var gpu = BuildProcessorCard("GPU", T("Discrete GPU", "独立显卡"), TealBrush, out gpuTemperature, out gpuUsage, out gpuUsageBar);
            gpu.Margin = new Thickness(5);
            gpu.Cursor = Cursors.Hand;
            gpu.ToolTip = T("Open the last 60 seconds of utilization and temperature", "点击查看最近 60 秒的使用率与温度");
            gpu.MouseLeftButtonUp += delegate { ShowPerformanceOverlay("GPU"); };
            Grid.SetColumn(gpu, 1);
            grid.Children.Add(gpu);
            var integratedGpu = BuildIntegratedGpuCard(out integratedGpuUsage, out integratedGpuUsageBar);
            integratedGpu.Margin = new Thickness(5);
            integratedGpu.Cursor = Cursors.Hand;
            integratedGpu.ToolTip = T("Open the last 60 seconds of utilization", "点击查看最近 60 秒的使用率");
            integratedGpu.MouseLeftButtonUp += delegate { ShowPerformanceOverlay("iGPU"); };
            Grid.SetColumn(integratedGpu, 2);
            grid.Children.Add(integratedGpu);
            return grid;
        }

        private Border BuildIntegratedGpuCard(out TextBlock usage, out RatioBar bar)
        {
            var card = Card(new Thickness(18), 17);
            card.MinHeight = 142;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = "iGPU", FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            var subtitle = new TextBlock { Text = T("Integrated GPU", "集成显卡"), Foreground = TextBrush, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(subtitle, 1);
            heading.Children.Add(subtitle);
            root.Children.Add(heading);

            var identity = new StackPanel { Margin = new Thickness(0, 12, 0, 10) };
            identity.Children.Add(new TextBlock { Text = T("Graphics core", "图形核心"), Foreground = MutedBrush, FontSize = 11 });
            identity.Children.Add(new TextBlock { Text = "Intel UHD", Foreground = TextBrush, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetRow(identity, 1);
            root.Children.Add(identity);

            var usageLine = new Grid();
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            usageLine.Children.Add(new TextBlock { Text = T("Usage", "使用率"), Foreground = MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            bar = new RatioBar(WarningBrush) { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 1);
            usageLine.Children.Add(bar);
            usage = new TextBlock { Text = "--%", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = WarningBrush, Width = 48, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(usage, 2);
            usageLine.Children.Add(usage);
            Grid.SetRow(usageLine, 2);
            root.Children.Add(usageLine);
            card.Child = root;
            return card;
        }

        private Border BuildProcessorCard(string title, string subtitle, Brush color, out TextBlock temperature, out TextBlock usage, out RatioBar bar)
        {
            var card = Card(new Thickness(18), 17);
            card.MinHeight = 142;
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            var sub = new TextBlock { Text = subtitle, Foreground = TextBrush, FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sub, 1);
            heading.Children.Add(sub);
            root.Children.Add(heading);

            var values = new Grid { Margin = new Thickness(0, 12, 0, 10) };
            var tempStack = new StackPanel();
            tempStack.Children.Add(new TextBlock { Text = T("Temperature", "温度"), Foreground = MutedBrush, FontSize = 11 });
            temperature = new TextBlock { Text = "-- °C", FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = TextBrush, Margin = new Thickness(0, 2, 0, 0) };
            tempStack.Children.Add(temperature);
            values.Children.Add(tempStack);
            Grid.SetRow(values, 1);
            root.Children.Add(values);

            var usageLine = new Grid();
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            usageLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            usageLine.Children.Add(new TextBlock { Text = T("Usage", "使用率"), Foreground = MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });
            bar = new RatioBar(color) { VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 1);
            usageLine.Children.Add(bar);
            usage = new TextBlock { Text = "--%", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = color, Width = 48, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(usage, 2);
            usageLine.Children.Add(usage);
            Grid.SetRow(usageLine, 2);
            root.Children.Add(usageLine);
            card.Child = root;
            return card;
        }

        private UIElement BuildStorageGrid()
        {
            storageGrid = new UniformGrid { Columns = 1 };
            SynchronizeStorageCards();
            return storageGrid;
        }

        private void SynchronizeStorageCards()
        {
            if (storageGrid == null) return;
            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady) continue;
                current.Add(drive.Name);
                if (storageCards.ContainsKey(drive.Name)) continue;
                var info = new StorageCardInfo();
                var card = Card(new Thickness(16), 15);
                card.Margin = new Thickness(5);
                card.MinHeight = 120;
                var root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.Children.Add(new TextBlock { Text = drive.Name.TrimEnd('\\') + T(" drive", " 盘"), FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
                var percentLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 4), VerticalAlignment = VerticalAlignment.Center };
                info.Label = new TextBlock { Text = T("Free", "剩余"), FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = TealBrush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 8, 0) };
                info.Percent = new TextBlock { Text = "--%", FontSize = 27, FontWeight = FontWeights.SemiBold, Foreground = TealBrush };
                info.Remaining = new TextBlock { Text = "-- GB", FontSize = 11, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(9, 0, 0, 5) };
                percentLine.Children.Add(info.Label);
                percentLine.Children.Add(info.Percent);
                percentLine.Children.Add(info.Remaining);
                Grid.SetRow(percentLine, 1);
                root.Children.Add(percentLine);
                var bottom = new StackPanel();
                info.Detail = new TextBlock { Text = T("Reading…", "正在读取…"), FontSize = 11, Foreground = MutedBrush, Margin = new Thickness(0, 0, 0, 8) };
                bottom.Children.Add(info.Detail);
                info.Bar = new RatioBar(TealBrush) { Height = 6 };
                bottom.Children.Add(info.Bar);
                Grid.SetRow(bottom, 2);
                root.Children.Add(bottom);
                card.Child = root;
                info.Card = card;
                storageGrid.Children.Add(card);
                storageCards[drive.Name] = info;
            }

            var removed = new List<string>();
            foreach (var pair in storageCards)
                if (!current.Contains(pair.Key)) removed.Add(pair.Key);
            foreach (string name in removed)
            {
                storageGrid.Children.Remove(storageCards[name].Card);
                storageCards.Remove(name);
            }
            storageGrid.Columns = Math.Max(1, Math.Min(3, storageCards.Count));
        }

        private FrameworkElement BuildFanHeader()
        {
            var grid = new Grid { Margin = new Thickness(3, 20, 3, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = T("Fan control", "风扇控制"), FontSize = 18, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            fanStatus = new TextBlock { Text = T("Connecting to fan…", "正在连接风扇…"), FontSize = 12, Foreground = MutedBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(fanStatus, 1);
            grid.Children.Add(fanStatus);
            return grid;
        }

        private UIElement BuildProtectionSettingsCard()
        {
            var card = Card(new Thickness(18), 18);
            card.Margin = new Thickness(5, 3, 5, 8);
            card.Background = new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(247, 250, 255), 0);
            var root = new StackPanel();
            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var icon = new Border
            {
                Width = 42,
                Height = 42,
                CornerRadius = new CornerRadius(14),
                Background = MakeBrush("#FFF0E8"),
                Margin = new Thickness(0, 0, 13, 0),
                Child = new TextBlock { Text = "♨", FontSize = 21, Foreground = WarningBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            };
            heading.Children.Add(icon);
            var headingText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            headingText.Children.Add(new TextBlock { Text = T("Independent 100% cooling rules", "独立 100% 散热规则"), FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            headingText.Children.Add(new TextBlock
            {
                Text = T("Independent of fan curves; only controls when emergency 100% cooling starts and stops", "独立于风扇曲线，只决定何时进入或退出 100% 紧急散热"),
                FontSize = 11,
                Foreground = MutedBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });
            Grid.SetColumn(headingText, 1);
            heading.Children.Add(headingText);
            root.Children.Add(heading);

            var controls = new UniformGrid { Columns = 3, Margin = new Thickness(0, 14, 0, 0) };
            controls.Children.Add(BuildProtectionTemperatureControl(
                T("Sustained heat", "持续高温"), T("Waits 20 seconds", "达到后等待 20 秒"), T("Enter 100% cooling", "进入 100% 散热"), T("Adjustable: 85–95°C", "可调范围 85–95°C"),
                85, 95, settings.SustainedFullBlastTemperature, WarningBrush,
                out sustainedProtectionSlider, out sustainedProtectionValue,
                T("Enables 100% after this temperature is sustained for 20 seconds", "达到该温度并持续 20 秒后开启 100%")));
            controls.Children.Add(BuildProtectionTemperatureControl(
                T("Emergency heat", "紧急高温"), T("No waiting", "达到后无需等待"), T("Enter 100% immediately", "立即进入 100% 散热"), T("Adjustable: 90–100°C", "可调范围 90–100°C"),
                90, 100, settings.EmergencyFullBlastTemperature, DangerBrush,
                out emergencyProtectionSlider, out emergencyProtectionValue,
                T("Enables 100% immediately at this temperature", "达到该温度后立即开启 100%")));
            controls.Children.Add(BuildProtectionTemperatureControl(
                T("Restore curve", "恢复曲线"), T("Waits 20 seconds after cooling", "降温后等待 20 秒"), T("Leave 100% and restore the active mode", "退出 100% 并恢复当前模式"), T("Adjustable: 70–92°C", "可调范围 70–92°C"),
                70, 92, settings.FullBlastReleaseTemperature, GoodBrush,
                out releaseProtectionSlider, out releaseProtectionValue,
                T("Restores the selected curve after staying below this temperature for 20 seconds", "降到该温度并持续 20 秒后恢复所选曲线")));
            root.Children.Add(controls);
            var safetyNote = new Border
            {
                Background = MakeBrush("#EEF4FC"),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(11, 7, 11, 7),
                Margin = new Thickness(4, 10, 4, 0),
                Child = new TextBlock
                {
                    Text = T(
                        "Safety rule: emergency must be at least 3°C above sustained, and restore at least 3°C below. Conflicting values are corrected automatically.",
                        "安全约束：紧急温度至少比持续温度高 3°C；恢复温度至少低 3°C。冲突的设置会自动修正。"),
                    FontSize = 10,
                    Foreground = AccentBrush
                }
            };
            root.Children.Add(safetyNote);
            UpdateProtectionTemperatureLabels();
            card.Child = root;
            return card;
        }

        private Border BuildProtectionTemperatureControl(string title, string timing, string action, string range,
            int minimum, int maximum, int value, Brush accent, out Slider slider, out TextBlock valueText, string toolTip)
        {
            var card = new Border
            {
                Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(14, 12, 14, 11),
                CornerRadius = new CornerRadius(15),
                Background = MakeTint(accent, 0.055),
                BorderBrush = MakeTint(accent, 0.22),
                BorderThickness = new Thickness(1),
                ToolTip = toolTip
            };
            var content = new StackPanel();
            var heading = new Grid();
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel();
            labels.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            labels.Children.Add(new TextBlock { Text = timing, FontSize = 10, Foreground = MutedBrush, Margin = new Thickness(0, 2, 0, 0) });
            heading.Children.Add(labels);
            valueText = new TextBlock { FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = accent, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(valueText, 1);
            heading.Children.Add(valueText);
            content.Children.Add(heading);
            content.Children.Add(new TextBlock { Text = action, FontSize = 11, Foreground = accent, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 0) });
            slider = new Slider
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 2,
                IsSnapToTickEnabled = true,
                Margin = new Thickness(0, 9, 0, 0),
                Cursor = Cursors.Hand
            };
            slider.ValueChanged += delegate { UpdateProtectionTemperatureLabels(); };
            slider.PreviewMouseLeftButtonUp += delegate { CommitProtectionTemperatureSettings(); };
            slider.KeyUp += delegate { CommitProtectionTemperatureSettings(); };
            content.Children.Add(slider);
            content.Children.Add(new TextBlock { Text = range, FontSize = 9, Foreground = MutedBrush, Margin = new Thickness(0, 4, 0, 0) });
            card.Child = content;
            return card;
        }

        private void UpdateProtectionTemperatureLabels()
        {
            if (sustainedProtectionValue != null && sustainedProtectionSlider != null)
                sustainedProtectionValue.Text = Math.Round(sustainedProtectionSlider.Value) + "°C";
            if (emergencyProtectionValue != null && emergencyProtectionSlider != null)
                emergencyProtectionValue.Text = Math.Round(emergencyProtectionSlider.Value) + "°C";
            if (releaseProtectionValue != null && releaseProtectionSlider != null)
                releaseProtectionValue.Text = Math.Round(releaseProtectionSlider.Value) + "°C";
        }

        private void CommitProtectionTemperatureSettings()
        {
            if (initializingSettings || sustainedProtectionSlider == null || emergencyProtectionSlider == null || releaseProtectionSlider == null) return;
            settings.SustainedFullBlastTemperature = (int)Math.Round(sustainedProtectionSlider.Value);
            settings.EmergencyFullBlastTemperature = (int)Math.Round(emergencyProtectionSlider.Value);
            settings.FullBlastReleaseTemperature = (int)Math.Round(releaseProtectionSlider.Value);
            NormalizeProtectionTemperatures();
            sustainedProtectionSlider.Value = settings.SustainedFullBlastTemperature;
            emergencyProtectionSlider.Value = settings.EmergencyFullBlastTemperature;
            releaseProtectionSlider.Value = settings.FullBlastReleaseTemperature;
            UpdateProtectionTemperatureLabels();
            SettingsStore.Save(settings);
            ResetFullBlastGuard();
            if (fanControlVerified && connected && !applying) RefreshFanStatus();
        }

        private UIElement BuildModeGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 3; i++) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            AddModeCard(grid, "Automatic", T("Automatic", "自动"), T("Controlled by MSI firmware", "由 MSI 固件自行调速"), T("Continuously combines temperature, load, and power; best for everyday use.", "系统综合温度、负载与功耗实时调整，适合日常使用。"), AccentBrush, 0, 0, null);

            var fixedExtra = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var fixedLine = new Grid();
            fixedLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fixedLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fixedLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fixedSlider = new Slider
            {
                Minimum = 30,
                Maximum = 60,
                TickFrequency = 1,
                SmallChange = 1,
                LargeChange = 5,
                IsSnapToTickEnabled = false,
                Value = settings.FixedRunningFanSpeed,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand,
                ToolTip = T("Keeps the selected fan duty after you release the slider (30–60%)", "拖动后，风扇会持续保持所选转速（30–60%）")
            };
            fixedValue = new TextBlock { Width = 50, TextAlignment = TextAlignment.Right, FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = PurpleBrush, VerticalAlignment = VerticalAlignment.Center };
            fixedOffButton = new Button
            {
                Content = T("Off", "关闭"),
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(12, 6, 12, 6),
                BorderThickness = new Thickness(1),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand,
                ToolTip = T("Turn the fixed fan duty off or back on", "切换固定转速的开启或关闭")
            };
            ApplyRoundedButtonTemplate(fixedOffButton, 12);
            fixedSlider.ValueChanged += delegate
            {
                UpdateFixedControls();
            };
            fixedSlider.PreviewMouseLeftButtonUp += delegate { if (activeMode == "Fixed") ApplyMode("Fixed", true); };
            fixedSlider.KeyUp += delegate { if (activeMode == "Fixed") ApplyMode("Fixed", true); };
            fixedOffButton.Click += delegate(object sender, RoutedEventArgs e)
            {
                e.Handled = true;
                if (!fanControlVerified || !connected || applying)
                {
                    ApplyMode("Fixed", true);
                    return;
                }
                fixedFanOff = !fixedFanOff;
                UpdateFixedControls();
                ApplyMode("Fixed", true);
            };
            fixedLine.Children.Add(fixedSlider);
            Grid.SetColumn(fixedValue, 1);
            fixedLine.Children.Add(fixedValue);
            Grid.SetColumn(fixedOffButton, 2);
            fixedLine.Children.Add(fixedOffButton);
            fixedExtra.Children.Add(fixedLine);
            UpdateFixedControls();
            AddModeCard(grid, "Fixed", T("Fixed", "固定"), T("Keep one constant fan duty", "始终保持同一转速"), T("Keeps the slider value while on; turns the ordinary fan off while preserving the thermal guard.", "开启时维持滑条设定；关闭时停止普通风扇，高温保护仍然有效。"), PurpleBrush, 0, 2, fixedExtra);

            var customExtra = new TextBlock { Text = T("Right-click to edit   ●━━●━━●", "右键编辑曲线   ●━━●━━●"), Foreground = AccentBrush, FontSize = 11, Margin = new Thickness(0, 10, 0, 0), FontWeight = FontWeights.SemiBold };
            AddModeCard(grid, "Custom", T("Custom", "自定义"), T("Follow your temperature curve", "按你设置的温度曲线调速"), T("Right-click to edit; every point supports 0% or 30–60%.", "右键打开曲线编辑器；每个节点可设置为 0% 或 30–60%。"), AccentBrush, 1, 0, customExtra);
            AddModeCard(grid, "Silent", T("Silent", "静音"), T("Quieter under light load", "低负载时更安静"), T("Ramps gently with temperature; suited to office work, browsing, and light loads.", "风扇随温度缓慢提升，适合办公、网页和轻负载。"), TealBrush, 0, 1, null);
            AddModeCard(grid, "Balanced", T("Balanced", "均衡"), T("Everyday balance of heat and noise", "温度与噪音的日常平衡"), T("Ramps more actively than Silent; suited to most games and everyday use.", "升速比静音更积极，适合多数游戏和常规使用。"), GoodBrush, 1, 1, null);
            AddModeCard(grid, "Boost", T("Boost", "强冷"), T("Reach 60% earlier", "更早提升到 60%"), T("Prioritizes lower temperatures for gaming, rendering, and sustained heavy loads.", "优先压低温度，适合游戏、渲染与长时间高负载。"), WarningBrush, 1, 2, null);
            return grid;
        }

        private void AddModeCard(Grid parent, string key, string title, string subtitle, string description, Brush accent, int column, int row, UIElement extra)
        {
            var card = Card(new Thickness(14), 15);
            card.Margin = new Thickness(5);
            card.Height = 140;
            card.Cursor = Cursors.Hand;

            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            left.Children.Add(new TextBlock { Text = subtitle, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = accent, Margin = new Thickness(0, 4, 0, 0) });
            left.Children.Add(new TextBlock { Text = description, FontSize = 12, Foreground = MutedBrush, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0), LineHeight = 18 });
            if (extra != null) left.Children.Add(extra);
            root.Children.Add(left);
            var badge = new TextBlock
            {
                Text = T("✓ Selected", "✓ 已选择"),
                Foreground = accent,
                Background = MakeBrush("#EFF5FF"),
                Padding = new Thickness(9, 5, 9, 5),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(badge, 1);
            root.Children.Add(badge);
            card.Child = root;
            card.PreviewMouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                if (key == "Fixed" && (HasVisualParent<Slider>(e.OriginalSource as DependencyObject) || HasVisualParent<Button>(e.OriginalSource as DependencyObject))) return;
                ApplyMode(key, true);
            };
            card.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                e.Handled = true;
                OpenCurve(key);
            };
            modeCards[key] = new ModeCardInfo { Card = card, Badge = badge, Accent = accent };
            Grid.SetColumn(card, column);
            Grid.SetRow(card, row);
            parent.Children.Add(card);
        }

        private UIElement BuildBlastCard()
        {
            blastCard = new Border
            {
                CornerRadius = new CornerRadius(18),
                Margin = new Thickness(5, 5, 5, 7),
                Padding = new Thickness(19, 15, 19, 15),
                BorderBrush = MakeBrush("#F2B9B9"),
                BorderThickness = new Thickness(1),
                Background = new LinearGradientBrush(Color.FromRgb(255, 248, 248), Color.FromRgb(255, 238, 238), 0),
                Cursor = Cursors.Hand,
                Effect = new DropShadowEffect { Color = Color.FromRgb(111, 36, 36), BlurRadius = 15, ShadowDepth = 2, Opacity = 0.07 }
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(15), Background = MakeBrush("#FCE1E1"), Margin = new Thickness(0, 0, 15, 0) };
            icon.Child = new TextBlock { Text = "⚡", FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = DangerBrush };
            grid.Children.Add(icon);
            var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            text.Children.Add(new TextBlock { Text = T("Full Blast", "狂暴散热"), FontSize = 19, FontWeight = FontWeights.SemiBold, Foreground = MakeBrush("#A33131") });
            text.Children.Add(new TextBlock { Text = T("Runs at 100% immediately · strongest and loudest · click again to restore the previous mode", "立即以 100% 运行 · 最强散热也最响 · 再次点击恢复此前模式"), FontSize = 12, Foreground = MakeBrush("#9B5A5A"), Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetColumn(text, 1);
            grid.Children.Add(text);
            blastCard.Child = grid;
            blastCard.MouseLeftButtonUp += delegate { ToggleBlast(); };
            blastCard.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; ShowCurveWindow(T("Full Blast", "狂暴散热"), T("100% fan speed across the full temperature range.", "全温区 100% 风扇转速。"), new FanCurve(new[] { 40, 50, 57, 64, 71, 78, 85 }, new[] { 100, 100, 100, 100, 100, 100, 100 }), false, DangerBrush); };
            return blastCard;
        }

        private UIElement BuildSettingsCard()
        {
            var card = Card(new Thickness(20), 16);
            card.Margin = new Thickness(5);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            autoStartCheckBox = new CheckBox
            {
                Content = T("Start with Windows in the notification area", "随 Windows 启动并驻留系统托盘"),
                IsChecked = settings.StartWithWindowsToTray,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            autoStartCheckBox.Checked += OnAutoStartChanged;
            autoStartCheckBox.Unchecked += OnAutoStartChanged;
            left.Children.Add(autoStartCheckBox);
            autoStartDetail = new TextBlock { Foreground = MutedBrush, FontSize = 11, Margin = new Thickness(24, 7, 0, 0) };
            left.Children.Add(autoStartDetail);
            grid.Children.Add(left);
            var hide = SoftButton(T("Minimize to notification area", "最小化到托盘"));
            hide.Click += delegate { HideToTray(); };
            Grid.SetColumn(hide, 1);
            grid.Children.Add(hide);
            card.Child = grid;
            return card;
        }

        private void ApplyMode(string key, bool userInitiated)
        {
            if (!fanControlVerified)
            {
                if (userInitiated) SetFanStatus(T("Fan control is locked because this model has not been verified", "此机型尚未验证，风扇控制已锁定"), WarningBrush);
                return;
            }
            if (!connected || applying)
            {
                if (userInitiated) SetFanStatus(T("Fan interface is not connected", "风扇接口尚未连接"), DangerBrush);
                return;
            }
            applying = true;
            try
            {
                ApplyHardwareMode(key);
                activeMode = key;
                settings.SelectedFanMode = key;
                if (key == "Fixed")
                {
                    settings.FixedFanSpeed = FixedFanDutyFromSlider();
                    if (settings.FixedFanSpeed > 0) settings.FixedRunningFanSpeed = settings.FixedFanSpeed;
                    UpdateFixedControls();
                }
                SettingsStore.Save(settings);
                SetModeSelection(key);
                SetFanStatus(T("Switched to ", "已切换到") + ModeDisplayName(key), GoodBrush);
            }
            catch (Exception ex) { SetFanStatus(T("Apply failed · ", "应用失败 · ") + Localization.Error(ex.Message), DangerBrush); }
            finally
            {
                applying = false;
                RefreshFanStatus();
            }
        }

        private void ToggleBlast()
        {
            if (!fanControlVerified)
            {
                SetFanStatus(T("Full Blast is locked because this model has not been verified", "此机型尚未验证，狂暴散热已锁定"), WarningBrush);
                return;
            }
            if (!connected || applying) return;
            applying = true;
            try
            {
                if (activeMode == "Blast")
                {
                    activeMode = NormalizeMode(modeBeforeBlast);
                    ApplyHardwareMode(activeMode);
                    SetModeSelection(activeMode);
                    SetFanStatus(T("Full Blast disabled; returned to ", "狂暴散热已关闭，已返回") + ModeDisplayName(activeMode), GoodBrush);
                }
                else
                {
                    modeBeforeBlast = activeMode;
                    ResetFullBlastGuard();
                    controller.SetFullBlast(true);
                    modeUsesFullBlast = false;
                    HardwareSnapshot snapshot = controller.GetSnapshot();
                    if (!snapshot.FullBlast)
                        throw new InvalidOperationException("固件没有确认狂暴散热状态。");
                    activeMode = "Blast";
                    SetModeSelection("Blast");
                    SetFanStatus(T("Full Blast enabled", "狂暴散热已开启"), DangerBrush);
                }
            }
            catch (Exception ex) { SetFanStatus(T("Full Blast switch failed · ", "狂暴模式切换失败 · ") + Localization.Error(ex.Message), DangerBrush); }
            finally
            {
                applying = false;
                RefreshFanStatus();
            }
        }

        private void ApplyHardwareMode(string key)
        {
            FanCurve expectedCurve = key == "Automatic" ? null : GetCurveForMode(key);
            ResetFullBlastGuard();

            if (key == "Automatic") controller.SetAutomatic();
            else controller.SetFanCurve(expectedCurve);

            HardwareSnapshot temperatureSnapshot = controller.GetSnapshot();
            bool expectedFullBlast = ShouldUseFullBlast(key, expectedCurve, temperatureSnapshot, false);
            controller.SetFullBlast(expectedFullBlast);
            modeUsesFullBlast = expectedFullBlast;

            HardwareSnapshot snapshot = controller.GetSnapshot();
            byte expectedFanMode = key == "Automatic" ? (byte)0x0D : (byte)0x8D;
            if (snapshot.FullBlast != expectedFullBlast || snapshot.FanMode != expectedFanMode)
                throw new InvalidOperationException("固件回读状态与所选模式不一致。");

            if (expectedCurve == null) return;
            FanCurve actualCurve = controller.GetFanCurve();
            for (int i = 0; i < 7; i++)
            {
                if (actualCurve.Speeds[i] != expectedCurve.Speeds[i])
                    throw new InvalidOperationException("固件没有完整保存风扇曲线。");
                if (i > 0 && actualCurve.Temperatures[i] != expectedCurve.Temperatures[i])
                    throw new InvalidOperationException("固件没有完整保存温度节点。");
            }
        }

        private bool ShouldUseFullBlast(string key, FanCurve curve, HardwareSnapshot snapshot, bool currentlyActive)
        {
            if (key == "Automatic" || curve == null)
            {
                ResetFullBlastGuard();
                return false;
            }

            int temperature = Math.Max(snapshot.CpuTemperature, snapshot.GpuTemperature);
            DateTime now = DateTime.UtcNow;

            if (currentlyActive)
            {
                fullBlastHighSinceUtc = DateTime.MinValue;
                if (temperature <= settings.FullBlastReleaseTemperature)
                {
                    if (fullBlastCoolSinceUtc == DateTime.MinValue) fullBlastCoolSinceUtc = now;
                    if ((now - fullBlastCoolSinceUtc).TotalSeconds >= FullBlastConfirmationSeconds)
                    {
                        ResetFullBlastGuard();
                        return false;
                    }
                }
                else fullBlastCoolSinceUtc = DateTime.MinValue;
                return true;
            }

            fullBlastCoolSinceUtc = DateTime.MinValue;
            if (temperature >= settings.EmergencyFullBlastTemperature)
            {
                fullBlastHighSinceUtc = DateTime.MinValue;
                return true;
            }

            if (temperature >= settings.SustainedFullBlastTemperature)
            {
                if (fullBlastHighSinceUtc == DateTime.MinValue) fullBlastHighSinceUtc = now;
                if ((now - fullBlastHighSinceUtc).TotalSeconds >= FullBlastConfirmationSeconds)
                {
                    fullBlastHighSinceUtc = DateTime.MinValue;
                    return true;
                }
            }
            else fullBlastHighSinceUtc = DateTime.MinValue;
            return false;
        }

        private void ResetFullBlastGuard()
        {
            fullBlastHighSinceUtc = DateTime.MinValue;
            fullBlastCoolSinceUtc = DateTime.MinValue;
        }

        private FanCurve GetCurveForMode(string key)
        {
            int[] temps = { 40, 50, 57, 64, 71, 78, 85 };
            switch (key)
            {
                case "Silent": return new FanCurve(temps, new[] { 30, 32, 35, 42, 50, 56, 60 });
                case "Balanced": return new FanCurve(temps, new[] { 34, 38, 44, 50, 54, 58, 60 });
                case "Boost": return new FanCurve(temps, new[] { 42, 48, 54, 58, 60, 60, 60 });
                case "Fixed":
                    int fixedSpeed = fixedSlider == null ? settings.FixedFanSpeed : FixedFanDutyFromSlider();
                    return new FanCurve(temps, new[] { fixedSpeed, fixedSpeed, fixedSpeed, fixedSpeed, fixedSpeed, fixedSpeed, Math.Max(fixedSpeed, 60) });
                case "Custom": return new FanCurve(settings.CustomTemperatures, settings.CustomSpeeds);
                default: return null;
            }
        }

        private void OpenCurve(string key)
        {
            if (key == "Custom")
            {
                var chart = new FanCurveChart(GetCurveForMode("Custom"), true, AccentBrush) { Height = 350 };
                ShowOverlay(T("Custom", "自定义"), T("Drag points, then save to write the curve to MSI WMI2.", "拖动节点后保存，曲线会立即写入 MSI WMI2。"), chart,
                    T("All seven points allow 0% or 30–60%.", "七个节点均可用 0% 或 30–60%。"), T("Save and apply", "保存并应用"), delegate
                {
                    FanCurve result = chart.Curve;
                    settings.CustomTemperatures = result.Temperatures;
                    settings.CustomSpeeds = result.Speeds;
                    NormalizeSettings();
                    SettingsStore.Save(settings);
                    CloseOverlay();
                    ApplyMode("Custom", true);
                });
                return;
            }
            if (key == "Automatic")
            {
                var info = new Border
                {
                    Background = MakeTint(AccentBrush, 0.07),
                    BorderBrush = MakeTint(AccentBrush, 0.24),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(24)
                };
                var content = new StackPanel();
                content.Children.Add(new TextBlock
                {
                    Text = T("Dynamically controlled by MSI firmware", "由 MSI 固件动态控制"),
                    FontSize = 20,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextBrush
                });
                content.Children.Add(new TextBlock
                {
                    Text = T(
                        "Automatic mode does not expose a readable fixed temperature-to-duty curve. Firmware continuously considers temperature, load, power, and system state, so this view no longer shows guessed percentages.",
                        "自动模式没有可读取的固定温度—转速曲线。固件会综合温度、负载、功耗和系统状态实时调整，因此这里不再显示猜测的百分比。"),
                    FontSize = 13,
                    Foreground = MutedBrush,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 23,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                info.Child = content;
                ShowOverlay(T("Automatic", "自动"), T("Dynamic firmware policy", "固件动态策略"), info,
                    T("Selecting Automatic returns complete fan control to MSI firmware.", "选择自动模式后，应用会把风扇控制权完整交还 MSI 固件。"), null, null);
                return;
            }
            ShowFanCurveOverlay(ModeDisplayName(key),
                T("This temperature-to-fan-duty curve is written to the hardware.", "这是该模式将写入硬件的温度—转速百分比。"),
                GetCurveForMode(key), modeCards[key].Accent);
        }

        private void ShowCurveWindow(string title, string subtitle, FanCurve curve, bool editable, Brush accent)
        {
            ShowFanCurveOverlay(title, subtitle, curve, accent);
        }

        private void ShowFanCurveOverlay(string title, string subtitle, FanCurve curve, Brush accent)
        {
            var chart = new FanCurveChart(curve, false, accent) { Height = 350 };
            ShowOverlay(title, subtitle, chart, T("Temperature is horizontal; fan duty is vertical.", "横轴是温度，纵轴是风扇转速百分比。"), null, null);
        }

        private void ShowPerformanceOverlay(string metric)
        {
            overlayMetric = metric;
            var body = new StackPanel();
            string title;
            string subtitle;
            Brush usageColor;
            bool showTemperature = metric != "iGPU";
            if (metric == "CPU")
            {
                title = T("CPU performance", "CPU 性能");
                subtitle = T("Processor utilization and temperature over the last 60 seconds", "最近 60 秒的处理器使用率与温度");
                usageColor = AccentBrush;
            }
            else if (metric == "GPU")
            {
                title = T("Discrete GPU performance", "独显性能");
                subtitle = T("NVIDIA utilization and temperature over the last 60 seconds", "最近 60 秒的 NVIDIA 使用率与温度");
                usageColor = TealBrush;
            }
            else
            {
                title = T("Integrated GPU performance", "核显性能");
                subtitle = T("Intel UHD utilization over the last 60 seconds", "最近 60 秒的 Intel UHD 使用率");
                usageColor = WarningBrush;
            }

            overlayUsageChart = new PerformanceHistoryChart(T("Usage", "使用率"), "%", usageColor, 100) { Height = 220 };
            body.Children.Add(overlayUsageChart);
            if (showTemperature)
            {
                overlayTemperatureChart = new PerformanceHistoryChart(T("Temperature", "温度"), "°C", WarningBrush, 100) { Height = 220, Margin = new Thickness(0, 14, 0, 0) };
                body.Children.Add(overlayTemperatureChart);
            }
            else overlayTemperatureChart = null;
            RefreshOverlayCharts();
            ShowOverlay(title, subtitle, body, T("Charts update every second using persistent Windows performance counters.", "图表每秒更新；数据来自 Windows 持久性能计数器。"), null, null);
        }

        private void ShowPerformanceDashboardOverlay()
        {
            overlayMetric = "All";
            var body = new UniformGrid { Columns = 2 };
            dashboardCpuUsageChart = DashboardChart(T("CPU usage", "CPU 使用率"), "%", AccentBrush);
            dashboardCpuTemperatureChart = DashboardChart(T("CPU temperature", "CPU 温度"), "°C", WarningBrush);
            dashboardGpuUsageChart = DashboardChart(T("Discrete GPU usage", "独显使用率"), "%", TealBrush);
            dashboardGpuTemperatureChart = DashboardChart(T("Discrete GPU temperature", "独显温度"), "°C", DangerBrush);
            dashboardIntegratedGpuUsageChart = DashboardChart(T("Integrated GPU usage", "核显使用率"), "%", WarningBrush);
            body.Children.Add(dashboardCpuUsageChart);
            body.Children.Add(dashboardCpuTemperatureChart);
            body.Children.Add(dashboardGpuUsageChart);
            body.Children.Add(dashboardGpuTemperatureChart);
            body.Children.Add(dashboardIntegratedGpuUsageChart);
            RefreshOverlayCharts();
            ShowOverlay(T("Performance charts", "性能图表"), T("Combined 60-second view of CPU, discrete GPU, and integrated GPU", "CPU、独显与核显最近 60 秒的集中视图"), body,
                T("Charts update every second; integrated GPU temperature is not shown.", "图表每秒更新；核显不显示温度。"), null, null);
        }

        private static PerformanceHistoryChart DashboardChart(string title, string unit, Brush color)
        {
            return new PerformanceHistoryChart(title, unit, color, 100) { Height = 180, Margin = new Thickness(5) };
        }

        private void ShowOverlay(string title, string subtitle, UIElement body, string footerText, string saveText, Action saveAction)
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var heading = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headingText = new StackPanel();
            headingText.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = TextBrush });
            headingText.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = MutedBrush, Margin = new Thickness(0, 5, 0, 0) });
            heading.Children.Add(headingText);
            var closeTop = SoftButton(T("Close", "关闭"));
            closeTop.Padding = new Thickness(13, 7, 13, 7);
            closeTop.Click += delegate { CloseOverlay(); };
            Grid.SetColumn(closeTop, 1);
            heading.Children.Add(closeTop);
            root.Children.Add(heading);

            var bodyScroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            Grid.SetRow(bodyScroll, 1);
            root.Children.Add(bodyScroll);

            var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(new TextBlock { Text = footerText, Foreground = MutedBrush, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            if (saveAction != null)
            {
                var save = SoftButton(saveText);
                save.Background = AccentBrush;
                save.Foreground = Brushes.White;
                save.Click += delegate { saveAction(); };
                Grid.SetColumn(save, 1);
                footer.Children.Add(save);
            }
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            overlayPanel.Child = root;
            if (mainScroll != null) mainScroll.IsHitTestVisible = false;
            overlayLayer.Visibility = Visibility.Visible;
        }

        private void CloseOverlay()
        {
            overlayLayer.Visibility = Visibility.Collapsed;
            if (mainScroll != null) mainScroll.IsHitTestVisible = true;
            overlayPanel.Child = null;
            overlayMetric = null;
            overlayUsageChart = null;
            overlayTemperatureChart = null;
            dashboardCpuUsageChart = null;
            dashboardCpuTemperatureChart = null;
            dashboardGpuUsageChart = null;
            dashboardGpuTemperatureChart = null;
            dashboardIntegratedGpuUsageChart = null;
        }

        private void SetModeSelection(string key)
        {
            foreach (var pair in modeCards)
            {
                bool selected = pair.Key == key;
                pair.Value.Card.BorderBrush = selected ? pair.Value.Accent : CardBorderBrush;
                pair.Value.Card.BorderThickness = new Thickness(selected ? 2 : 1);
                pair.Value.Card.Background = selected ? MakeTint(pair.Value.Accent, 0.075) : SurfaceBrush;
                pair.Value.Badge.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            }
            bool blast = key == "Blast";
            if (blastCard != null)
            {
                blastCard.BorderThickness = new Thickness(blast ? 2 : 1);
                blastCard.BorderBrush = blast ? DangerBrush : MakeBrush("#F2B9B9");
            }
        }

        private async void RefreshMetricsAsync()
        {
            if (refreshInProgress || exitRequested) return;
            refreshInProgress = true;
            try
            {
                int cycle = refreshCycle++;
                bool readHardware = connected && !applying && cycle % 2 == 0;
                bool updateStorage = cycle % 2 == 0;
                RefreshPayload payload = await Task.Run(delegate
                {
                    var result = new RefreshPayload { Usage = usageReader.Read() };
                    if (readHardware)
                    {
                        try { result.Hardware = controller.GetSnapshot(); }
                        catch (Exception ex) { result.HardwareError = ex.Message; }
                    }
                    return result;
                });
                if (exitRequested) return;
                ApplyUsage(payload.Usage);
                if (updateStorage) RefreshStorage();
                if (payload.Hardware != null && !applying) ApplyFanSnapshot(payload.Hardware);
                else if (!string.IsNullOrEmpty(payload.HardwareError)) SetFanStatus(T("Read failed · ", "读取失败 · ") + Localization.Error(payload.HardwareError), DangerBrush);
            }
            finally { refreshInProgress = false; }
        }

        private void ApplyUsage(SystemUsageSnapshot usage)
        {
            Brush cpuUsageColor = UsageBrush(usage.CpuPercent);
            Brush gpuUsageColor = UsageBrush(usage.DiscreteGpuPercent);
            Brush integratedGpuUsageColor = UsageBrush(usage.IntegratedGpuPercent);
            cpuUsage.Text = usage.CpuPercent + "%";
            cpuUsage.Foreground = cpuUsageColor;
            cpuUsageBar.SetColor(cpuUsageColor);
            cpuUsageBar.SetValue(usage.CpuPercent);
            gpuUsage.Text = usage.DiscreteGpuPercent + "%";
            gpuUsage.Foreground = gpuUsageColor;
            gpuUsageBar.SetColor(gpuUsageColor);
            gpuUsageBar.SetValue(usage.DiscreteGpuPercent);
            integratedGpuUsage.Text = usage.IntegratedGpuPercent + "%";
            integratedGpuUsage.Foreground = integratedGpuUsageColor;
            integratedGpuUsageBar.SetColor(integratedGpuUsageColor);
            integratedGpuUsageBar.SetValue(usage.IntegratedGpuPercent);
            AppendHistory(cpuUsageHistory, usage.CpuPercent);
            AppendHistory(discreteGpuUsageHistory, usage.DiscreteGpuPercent);
            AppendHistory(integratedGpuUsageHistory, usage.IntegratedGpuPercent);
            AppendHistory(cpuTemperatureHistory, lastCpuTemperature);
            AppendHistory(gpuTemperatureHistory, lastGpuTemperature);
            RefreshOverlayCharts();
        }

        private static void AppendHistory(List<int> values, int value)
        {
            values.Add(value);
            if (values.Count > 60) values.RemoveAt(0);
        }

        private void RefreshOverlayCharts()
        {
            if (string.IsNullOrEmpty(overlayMetric)) return;
            if (overlayMetric == "All")
            {
                if (dashboardCpuUsageChart != null) dashboardCpuUsageChart.SetValues(cpuUsageHistory);
                if (dashboardCpuTemperatureChart != null) dashboardCpuTemperatureChart.SetValues(cpuTemperatureHistory);
                if (dashboardGpuUsageChart != null) dashboardGpuUsageChart.SetValues(discreteGpuUsageHistory);
                if (dashboardGpuTemperatureChart != null) dashboardGpuTemperatureChart.SetValues(gpuTemperatureHistory);
                if (dashboardIntegratedGpuUsageChart != null) dashboardIntegratedGpuUsageChart.SetValues(integratedGpuUsageHistory);
                return;
            }
            if (overlayUsageChart == null) return;
            if (overlayMetric == "CPU")
            {
                overlayUsageChart.SetValues(cpuUsageHistory);
                if (overlayTemperatureChart != null) overlayTemperatureChart.SetValues(cpuTemperatureHistory);
            }
            else if (overlayMetric == "GPU")
            {
                overlayUsageChart.SetValues(discreteGpuUsageHistory);
                if (overlayTemperatureChart != null) overlayTemperatureChart.SetValues(gpuTemperatureHistory);
            }
            else overlayUsageChart.SetValues(integratedGpuUsageHistory);
        }

        private void RefreshStorage()
        {
            SynchronizeStorageCards();
            foreach (var pair in storageCards)
            {
                try
                {
                    var drive = new DriveInfo(pair.Key);
                    if (!drive.IsReady) continue;
                    long usedBytes = drive.TotalSize - drive.AvailableFreeSpace;
                    double freePercent = drive.AvailableFreeSpace * 100.0 / drive.TotalSize;
                    double usedPercent = 100.0 - freePercent;
                    Brush freeColor = StorageBrush(freePercent);
                    pair.Value.Percent.Text = FormatPercent(freePercent) + "%";
                    pair.Value.Remaining.Text = ToGb(drive.AvailableFreeSpace) + " GB";
                    pair.Value.Label.Foreground = freeColor;
                    pair.Value.Percent.Foreground = freeColor;
                    pair.Value.Remaining.Foreground = freeColor;
                    pair.Value.Detail.Text = ToGb(usedBytes) + " / " + ToGb(drive.TotalSize) + " GB";
                    pair.Value.Bar.SetColor(freeColor);
                    pair.Value.Bar.SetValue(usedPercent);
                }
                catch { }
            }
        }

        private void RefreshFanStatus()
        {
            if (!connected || applying) return;
            try
            {
                ApplyFanSnapshot(controller.GetSnapshot());
            }
            catch (Exception ex) { SetFanStatus(T("Read failed · ", "读取失败 · ") + Localization.Error(ex.Message), DangerBrush); }
        }

        private void ApplyFanSnapshot(HardwareSnapshot snapshot)
        {
            try
            {
                if (fanControlVerified && activeMode != "Automatic" && activeMode != "Blast")
                {
                    FanCurve activeCurve = GetCurveForMode(activeMode);
                    bool shouldUseFullBlast = ShouldUseFullBlast(activeMode, activeCurve, snapshot, modeUsesFullBlast);
                    if (snapshot.FullBlast != shouldUseFullBlast)
                    {
                        controller.SetFullBlast(shouldUseFullBlast);
                        modeUsesFullBlast = shouldUseFullBlast;
                        snapshot = controller.GetSnapshot();
                    }
                }
                lastFanRpm = snapshot.FanRpm;
                lastCpuTemperature = snapshot.CpuTemperature;
                lastGpuTemperature = snapshot.GpuTemperature;
                if (cpuTemperatureHistory.Count > 0) cpuTemperatureHistory[cpuTemperatureHistory.Count - 1] = lastCpuTemperature;
                if (gpuTemperatureHistory.Count > 0) gpuTemperatureHistory[gpuTemperatureHistory.Count - 1] = lastGpuTemperature;
                cpuTemperature.Text = snapshot.CpuTemperature + " °C";
                gpuTemperature.Text = snapshot.GpuTemperature + " °C";
                cpuTemperature.Foreground = TemperatureBrush(snapshot.CpuTemperature);
                gpuTemperature.Foreground = TemperatureBrush(snapshot.GpuTemperature);
                string current = activeMode == "Blast"
                    ? T("Full Blast", "狂暴散热")
                    : ModeDisplayName(activeMode) + (modeUsesFullBlast ? T(" · Full Blast", " · 全速") : "");
                fanStatus.Text = fanControlVerified
                    ? T("Current: ", "当前 ") + current + "  ·  " + PerformanceModeDisplay(snapshot.PerformanceMode) + "  ·  " + snapshot.FanRpm.ToString("N0") + " RPM"
                    : T("Monitoring only · fan writes locked · ", "仅监控 · 风扇写入已锁定 · ") + (compatibility == null ? "Unknown" : compatibility.Model) + "  ·  " + snapshot.FanRpm.ToString("N0") + " RPM";
                fanStatus.Foreground = snapshot.FullBlast ? DangerBrush : GoodBrush;
                if (snapshot.FullBlast && activeMode != "Blast" && !modeUsesFullBlast) activeMode = "Blast";
                SetModeSelection(activeMode);
                RefreshOverlayCharts();
            }
            catch (Exception ex) { SetFanStatus(T("Read failed · ", "读取失败 · ") + Localization.Error(ex.Message), DangerBrush); }
        }

        private void SetFanStatus(string text, Brush color)
        {
            if (fanStatus == null) return;
            fanStatus.Text = text + (lastFanRpm > 0 ? "  ·  " + lastFanRpm.ToString("N0") + " RPM" : "");
            fanStatus.Foreground = color;
        }

        private void OnAutoStartChanged(object sender, RoutedEventArgs e)
        {
            if (initializingSettings) return;
            bool enabled = autoStartCheckBox.IsChecked == true;
            try
            {
                if (enabled) AutoStartManager.EnsureInstalled(); else AutoStartManager.Remove();
                settings.StartWithWindowsToTray = enabled;
                SettingsStore.Save(settings);
                UpdateAutoStartDetail();
            }
            catch (Exception ex)
            {
                initializingSettings = true;
                autoStartCheckBox.IsChecked = !enabled;
                initializingSettings = false;
                SetFanStatus(T("Startup setup failed · ", "开机自启设置失败 · ") + Localization.Error(ex.Message), DangerBrush);
            }
        }

        private void UpdateAutoStartDetail()
        {
            bool installed = false;
            try { installed = AutoStartManager.IsInstalled(); } catch { }
            autoStartDetail.Text = installed
                ? T("An elevated sign-in task is installed; startup is silent in the notification area.", "已创建最高权限登录任务；开机时静默进入托盘。")
                : T("The first setup requires administrator permission; later starts are silent.", "首次启用需要管理员权限；之后开机静默启动。");
        }

        private void CreateTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon { Text = T("MSI Hardware Console", "MSI 硬件控制台"), Visible = true };
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MSIHardwareConsole.ico");
            trayIcon.Icon = File.Exists(iconPath) ? new Drawing.Icon(iconPath) : Drawing.SystemIcons.Application;
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add(T("Open console", "打开控制台"), null, delegate { Dispatcher.BeginInvoke(new Action(ShowFromTray)); });
            menu.Items.Add(T("Restore automatic fan control", "恢复自动风扇"), null, delegate { Dispatcher.BeginInvoke(new Action(delegate { ApplyMode("Automatic", true); })); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(T("Exit", "退出"), null, delegate { Dispatcher.BeginInvoke(new Action(ExitApplication)); });
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += delegate { Dispatcher.BeginInvoke(new Action(ShowFromTray)); };
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == WindowState.Minimized) WindowState = lastVisibleWindowState;
            Activate();
            Topmost = true;
            Topmost = false;
            Focus();
        }

        private void ApplyAdaptiveWindowSize()
        {
            Forms.Screen screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
            Drawing.Rectangle area = screen.WorkingArea;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            if (mainScroll != null) mainScroll.ScrollToTop();

            int width = Math.Max(720, (int)Math.Round(area.Width * 0.80));
            int provisionalHeight = Math.Max(420, (int)Math.Round(area.Height * 0.80));
            int left = area.Left + (area.Width - width) / 2;
            int provisionalTop = area.Top + (area.Height - provisionalHeight) / 2;
            SetWindowPos(handle, IntPtr.Zero, left, provisionalTop, width, provisionalHeight, 0x0004 | 0x0010);
            UpdateLayout();

            uint dpi = GetDpiForWindow(handle);
            double scaleY = dpi > 0 ? dpi / 96.0 : 1.0;
            NativeRect windowRect;
            NativeRect clientRect;
            int chromeHeight = GetWindowRect(handle, out windowRect) && GetClientRect(handle, out clientRect)
                ? Math.Max(0, windowRect.Bottom - windowRect.Top - (clientRect.Bottom - clientRect.Top))
                : (int)Math.Round(38 * scaleY);
            double fanBottomDip = fanHeaderElement == null || mainContentRoot == null
                ? 560
                : fanHeaderElement.TranslatePoint(new Point(0, fanHeaderElement.ActualHeight), mainContentRoot).Y + mainContentRoot.Margin.Top;
            int height = (int)Math.Ceiling((fanBottomDip + 14) * scaleY) + chromeHeight;
            height = Math.Max(420, Math.Min((int)Math.Round(area.Height * 0.92), height));
            int top = area.Top + (area.Height - height) / 2;
            SetWindowPos(handle, IntPtr.Zero, left, top, width, height, 0x0004 | 0x0010);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr windowHandle, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr windowHandle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rectangle);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetClientRect(IntPtr windowHandle, out NativeRect rectangle);

        private void HideToTray()
        {
            if (WindowState != WindowState.Minimized) lastVisibleWindowState = WindowState;
            Hide();
        }

        private void ToggleLanguage()
        {
            settings.Language = Localization.Chinese ? "en-US" : "zh-CN";
            Localization.Chinese = !Localization.Chinese;
            SettingsStore.Save(settings);
            Title = T("MSI Hardware Console", "MSI 硬件控制台");
            modeCards.Clear();
            storageCards.Clear();
            overlayMetric = null;
            Content = BuildContent();
            SetModeSelection(activeMode);
            UpdateFixedControls();
            UpdateAutoStartDetail();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            CreateTrayIcon();
            RefreshMetricsAsync();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (exitRequested) return;
            e.Cancel = true;
            HideToTray();
        }

        private void ExitApplication()
        {
            exitRequested = true;
            Application.Current.Shutdown();
        }

        public void DisposeResources()
        {
            refreshTimer.Stop();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
                trayIcon = null;
            }
            usageReader.Dispose();
            controller.Dispose();
        }

        public void SavePreview(string path)
        {
            var host = Content as Grid;
            var scroll = host == null || host.Children.Count == 0 ? null : host.Children[0] as ScrollViewer;
            var visual = scroll == null ? Content as FrameworkElement : scroll.Content as FrameworkElement;
            if (visual == null) throw new InvalidOperationException("UI preview root is unavailable.");
            visual.Measure(new Size(1072, double.PositiveInfinity));
            Size desired = visual.DesiredSize;
            visual.Arrange(new Rect(new Point(0, 0), desired));
            visual.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(desired.Width));
            int height = Math.Max(1, (int)Math.Ceiling(desired.Height));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        internal void ShowPerformancePreviewForQa()
        {
            ShowPerformanceOverlay("CPU");
        }

        public void SaveViewportPreview(string path)
        {
            var visual = Content as FrameworkElement;
            if (visual == null) throw new InvalidOperationException("UI viewport is unavailable.");
            UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(visual.ActualWidth));
            int height = Math.Max(1, (int)Math.Ceiling(visual.ActualHeight));
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        private Border Card(Thickness padding, double radius)
        {
            return new Border
            {
                Background = SurfaceBrush,
                BorderBrush = CardBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius),
                Padding = padding,
                Effect = new DropShadowEffect { Color = Color.FromRgb(38, 62, 99), BlurRadius = 14, ShadowDepth = 2, Opacity = 0.07 }
            };
        }

        private Button SoftButton(string text)
        {
            var button = new Button
            {
                Content = text,
                Background = MakeBrush("#EAF2FF"),
                Foreground = AccentBrush,
                BorderBrush = MakeBrush("#CFE0FA"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15, 9, 15, 9),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
            ApplyRoundedButtonTemplate(button, 13);
            return button;
        }

        private static void ApplyRoundedButtonTemplate(Button button, double radius)
        {
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(radius));
            border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetBinding(ContentPresenter.ContentProperty, new Binding("Content") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(ContentPresenter.MarginProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(System.Windows.Documents.TextElement.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(System.Windows.Documents.TextElement.FontSizeProperty, new Binding("FontSize") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            presenter.SetBinding(System.Windows.Documents.TextElement.FontWeightProperty, new Binding("FontWeight") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.AppendChild(presenter);
            template.VisualTree = border;

            var hover = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            hover.Setters.Add(new Setter(UIElement.OpacityProperty, 0.86));
            template.Triggers.Add(hover);
            var pressed = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.68));
            template.Triggers.Add(pressed);
            var disabled = new Trigger { Property = Button.IsEnabledProperty, Value = false };
            disabled.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
            template.Triggers.Add(disabled);

            button.Template = template;
        }

        private void SetWindowIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MSIHardwareConsole.ico");
                if (!File.Exists(iconPath)) return;
                using (var icon = new Drawing.Icon(iconPath))
                    Icon = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            catch { }
        }

        private void NormalizeSettings()
        {
            if (settings.CustomTemperatures == null || settings.CustomTemperatures.Length != 7)
                settings.CustomTemperatures = new[] { 40, 50, 57, 64, 71, 78, 85 };
            if (settings.CustomSpeeds == null || settings.CustomSpeeds.Length != 7)
                settings.CustomSpeeds = new[] { 30, 35, 43, 52, 62, 76, 100 };
            if (!string.Equals(settings.Language, "zh-CN", StringComparison.OrdinalIgnoreCase))
                settings.Language = "en-US";
            settings.CustomTemperatures[0] = 40;
            bool fixedOff = settings.FixedFanSpeed <= 0;
            int requestedFixedSpeed = fixedOff ? settings.FixedRunningFanSpeed : settings.FixedFanSpeed;
            settings.FixedRunningFanSpeed = NormalizeRunningFanDuty(requestedFixedSpeed);
            settings.FixedFanSpeed = fixedOff ? 0 : settings.FixedRunningFanSpeed;
            NormalizeProtectionTemperatures();
            for (int i = 0; i < 7; i++)
            {
                settings.CustomSpeeds[i] = NormalizeFanDuty(settings.CustomSpeeds[i]);
                if (i > 0 && settings.CustomSpeeds[i] < settings.CustomSpeeds[i - 1])
                    settings.CustomSpeeds[i] = settings.CustomSpeeds[i - 1];
            }
        }

        private void NormalizeProtectionTemperatures()
        {
            int sustained = settings.SustainedFullBlastTemperature <= 0 ? 92 : settings.SustainedFullBlastTemperature;
            settings.SustainedFullBlastTemperature = Math.Max(85, Math.Min(95, sustained));
            int emergency = settings.EmergencyFullBlastTemperature <= 0 ? 97 : settings.EmergencyFullBlastTemperature;
            settings.EmergencyFullBlastTemperature = Math.Max(
                settings.SustainedFullBlastTemperature + 3,
                Math.Max(90, Math.Min(100, emergency)));
            int release = settings.FullBlastReleaseTemperature <= 0 ? 87 : settings.FullBlastReleaseTemperature;
            settings.FullBlastReleaseTemperature = Math.Max(
                70,
                Math.Min(Math.Min(92, settings.SustainedFullBlastTemperature - 3), release));
        }

        private int FixedFanDutyFromSlider()
        {
            if (fixedFanOff) return 0;
            if (fixedSlider == null) return NormalizeRunningFanDuty(settings.FixedRunningFanSpeed);
            return NormalizeRunningFanDuty((int)Math.Round(fixedSlider.Value));
        }

        private void UpdateFixedControls()
        {
            if (fixedSlider != null)
            {
                fixedSlider.IsEnabled = !fixedFanOff;
                fixedSlider.Opacity = fixedFanOff ? 0.38 : 1.0;
                fixedSlider.Cursor = fixedFanOff ? Cursors.Arrow : Cursors.Hand;
            }
            if (fixedValue != null)
            {
                fixedValue.Text = fixedFanOff ? T("Off", "已关闭") : NormalizeRunningFanDuty((int)Math.Round(fixedSlider.Value)) + "%";
                fixedValue.Foreground = fixedFanOff ? MutedBrush : PurpleBrush;
            }
            if (fixedOffButton == null) return;
            fixedOffButton.Background = fixedFanOff ? PurpleBrush : MakeBrush("#F6F7FA");
            fixedOffButton.Foreground = fixedFanOff ? Brushes.White : MutedBrush;
            fixedOffButton.BorderBrush = fixedFanOff ? PurpleBrush : CardBorderBrush;
            fixedOffButton.Content = fixedFanOff ? T("Turn on", "开启") : T("Turn off", "关闭");
            fixedOffButton.ToolTip = fixedFanOff
                ? T("Restore the previous fixed duty and restart the fan", "恢复上次设定的固定转速并重新启动风扇")
                : T("Stop the fan; sustained high temperature still enables Full Blast protection", "立即关闭风扇；持续高温时仍会自动启用全速保护");
        }

        private static int NormalizeFanDuty(int duty)
        {
            if (duty <= 0) return 0;
            return Math.Max(30, Math.Min(60, duty));
        }

        private static int NormalizeRunningFanDuty(int duty)
        {
            return Math.Max(30, Math.Min(60, duty));
        }

        private static string NormalizeMode(string mode)
        {
            switch (mode)
            {
                case "Silent": case "Balanced": case "Boost": case "Fixed": case "Custom": return mode;
                default: return "Automatic";
            }
        }

        private static string ModeDisplayName(string key)
        {
            switch (key)
            {
                case "Silent": return T("Silent", "静音");
                case "Balanced": return T("Balanced", "均衡");
                case "Boost": return T("Boost", "强冷");
                case "Fixed": return T("Fixed", "固定");
                case "Custom": return T("Custom", "自定义");
                case "Blast": return T("Full Blast", "狂暴散热");
                default: return T("Automatic", "自动");
            }
        }

        private static Brush TemperatureBrush(int value)
        {
            if (value >= 90) return DangerBrush;
            if (value >= 80) return WarningBrush;
            return GoodBrush;
        }

        private static Brush UsageBrush(int value)
        {
            if (value >= 85) return DangerBrush;
            if (value >= 65) return WarningBrush;
            return GoodBrush;
        }

        private static Brush StorageBrush(double freePercent)
        {
            if (freePercent < 10) return DangerBrush;
            if (freePercent < 25) return WarningBrush;
            return GoodBrush;
        }

        private static string ToGb(long bytes)
        {
            return (bytes / 1073741824.0).ToString("0.0");
        }

        private static string FormatPercent(double value)
        {
            return value < 10 ? value.ToString("0.0") : value.ToString("0");
        }

        private static string PerformanceModeDisplay(byte mode)
        {
            switch (mode)
            {
                case 0xC1: return T("Silent performance", "静音性能");
                case 0xC2: return T("Power saver", "节能性能");
                case 0xC4: return T("Maximum performance", "最高性能");
                default: return T("Balanced performance", "均衡性能");
            }
        }

        private static string T(string english, string chinese)
        {
            return Localization.T(english, chinese);
        }

        private static bool HasVisualParent<T>(DependencyObject value) where T : DependencyObject
        {
            while (value != null)
            {
                if (value is T) return true;
                value = VisualTreeHelper.GetParent(value);
            }
            return false;
        }

        private static Brush MakeTint(Brush source, double opacity)
        {
            var solid = source as SolidColorBrush;
            if (solid == null) return SurfaceBrush;
            Color c = solid.Color;
            byte r = (byte)(255 - (255 - c.R) * opacity);
            byte g = (byte)(255 - (255 - c.G) * opacity);
            byte b = (byte)(255 - (255 - c.B) * opacity);
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static SolidColorBrush MakeBrush(string hex)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            brush.Freeze();
            return brush;
        }

        private sealed class ModeCardInfo
        {
            public Border Card;
            public TextBlock Badge;
            public Brush Accent;
        }

        private sealed class StorageCardInfo
        {
            public Border Card;
            public TextBlock Label;
            public TextBlock Percent;
            public TextBlock Remaining;
            public TextBlock Detail;
            public RatioBar Bar;
        }

        private sealed class RefreshPayload
        {
            public SystemUsageSnapshot Usage;
            public HardwareSnapshot Hardware;
            public string HardwareError;
        }
    }

    internal sealed class RatioBar : Grid
    {
        private readonly Border fill;
        private double percent;

        public RatioBar(Brush color)
        {
            Height = 7;
            Background = new SolidColorBrush(Color.FromRgb(236, 240, 246));
            ClipToBounds = true;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            fill = new Border
            {
                Background = color,
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Children.Add(fill);
            SizeChanged += delegate { UpdateWidth(); };
        }

        public void SetValue(double value)
        {
            percent = Math.Max(0, Math.Min(100, value));
            UpdateWidth();
        }

        public void SetColor(Brush color)
        {
            fill.Background = color;
        }

        private void UpdateWidth()
        {
            fill.Width = ActualWidth * percent / 100.0;
            fill.Height = ActualHeight;
        }
    }
}
