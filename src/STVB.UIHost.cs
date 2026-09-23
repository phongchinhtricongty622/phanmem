using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Interop;

[assembly: ComVisible(false)]
[assembly: AssemblyVersion("3.0.0.40")]
[assembly: AssemblyFileVersion("3.0.0.40")]
[assembly: Guid("E68C7D57-65C6-4DA7-A6A1-BA1E7B58A135")]

namespace STVBUIHost
{
    [ComVisible(true)]
    [Guid("A2462E02-62B7-4D4A-9B67-87E74554B3A1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    public interface ISTVBUIHost
    {
        [DispId(1)] int Ping();
        [DispId(2)] string GetVersion();
        [DispId(3)] int Confirm(string documentName, string headline, string body, string primaryText, string cancelText);
        [DispId(4)] void ShowProgress(string documentName, int stepIndex, int totalSteps, string statusText);
        [DispId(5)] void UpdateProgress(string documentName, int stepIndex, int totalSteps, string statusText);
        [DispId(6)] void CloseProgress(int succeeded);
        [DispId(7)] void ShowCompletion(string summaryText, string reminderText);
    }

    [ComVisible(true)]
    [Guid("14762D8B-9DC4-49BD-B33F-739BFA5D21BE")]
    [ProgId("STVB.UIHost")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComDefaultInterface(typeof(ISTVBUIHost))]
    public sealed class STVBUIHost : ISTVBUIHost
    {
        private ProgressWindow _progress;
        private string _runDocumentName = string.Empty;
        private DateTime _runStartedAt = DateTime.MinValue;
        private const string VersionText = "STVB.UIHost WPF COM FIX40";

        public STVBUIHost() { }

        public int Ping()
        {
            UIThread.Invoke(delegate
            {
                ModernConfirmWindow start = new ModernConfirmWindow("STVB_TEST.docx", "Bắt đầu chuẩn hóa văn bản", "UI constructor self-test", "Bắt đầu", "Hủy");
                start.Close();
                ProgressWindow progress = new ProgressWindow();
                progress.UpdateState("STVB_TEST.docx", 2, 16, "Chuẩn hóa font legacy và mã ký tự");
                progress.CloseSafe();
                ModernCompletionWindow done = new ModernCompletionWindow("STVB_TEST.docx", TimeSpan.FromSeconds(1), "Đã có thay đổi", "Khuyến nghị kiểm tra và sửa lỗi");
                done.Close();
            });
            return 1;
        }

        public string GetVersion() { return VersionText; }

        public int Confirm(string documentName, string headline, string body, string primaryText, string cancelText)
        {
            return UIThread.Invoke<int>(delegate
            {
                ModernConfirmWindow window = new ModernConfirmWindow(documentName, headline, body, primaryText, cancelText);
                SetOwner(window);
                bool? result = window.ShowDialog();
                return result == true ? 1 : 0;
            });
        }

        public void ShowProgress(string documentName, int stepIndex, int totalSteps, string statusText)
        {
            UIThread.Invoke(delegate
            {
                CloseProgressCore();
                _runDocumentName = documentName ?? string.Empty;
                _runStartedAt = DateTime.Now;
                _progress = new ProgressWindow();
                SetOwner(_progress);
                _progress.UpdateState(_runDocumentName, stepIndex, totalSteps, statusText);
                _progress.Show();
                _progress.Activate();
            });
        }

        public void UpdateProgress(string documentName, int stepIndex, int totalSteps, string statusText)
        {
            UIThread.Invoke(delegate
            {
                if (_progress == null)
                {
                    _runDocumentName = documentName ?? string.Empty;
                    if (_runStartedAt == DateTime.MinValue) _runStartedAt = DateTime.Now;
                    _progress = new ProgressWindow();
                    SetOwner(_progress);
                    _progress.Show();
                }
                _progress.UpdateState(documentName, stepIndex, totalSteps, statusText);
            });
        }

        public void CloseProgress(int succeeded)
        {
            UIThread.Invoke(delegate { CloseProgressCore(); });
        }

        public void ShowCompletion(string summaryText, string reminderText)
        {
            UIThread.Invoke(delegate
            {
                CloseProgressCore();
                TimeSpan elapsed = _runStartedAt == DateTime.MinValue ? TimeSpan.Zero : DateTime.Now - _runStartedAt;
                ModernCompletionWindow window = new ModernCompletionWindow(_runDocumentName, elapsed, summaryText, reminderText);
                SetOwner(window);
                window.ShowDialog();
                _runStartedAt = DateTime.MinValue;
                _runDocumentName = string.Empty;
            });
        }

        private void CloseProgressCore()
        {
            if (_progress != null)
            {
                try { _progress.CloseSafe(); } catch { }
                _progress = null;
            }
        }

        private static void SetOwner(Window window)
        {
            try
            {
                IntPtr h = Process.GetCurrentProcess().MainWindowHandle;
                if (h != IntPtr.Zero) new WindowInteropHelper(window).Owner = h;
            }
            catch { }
        }
    }

    internal static class UIThread
    {
        private static readonly object Sync = new object();
        private static readonly AutoResetEvent Ready = new AutoResetEvent(false);
        private static Thread _thread;
        private static Dispatcher _dispatcher;

        public static void Ensure()
        {
            lock (Sync)
            {
                if (_dispatcher != null && !_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished) return;
                Ready.Reset();
                _thread = new Thread(new ThreadStart(delegate
                {
                    _dispatcher = Dispatcher.CurrentDispatcher;
                    Ready.Set();
                    Dispatcher.Run();
                }));
                _thread.Name = "STVB.UIHost.WPF";
                _thread.IsBackground = true;
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
                if (!Ready.WaitOne(5000) || _dispatcher == null) throw new InvalidOperationException("WPF_UI_THREAD_START_FAILED");
            }
        }

        public static void Invoke(Action action)
        {
            Ensure();
            if (_dispatcher.CheckAccess()) action();
            else _dispatcher.Invoke(action);
        }

        public static T Invoke<T>(Func<T> func)
        {
            Ensure();
            if (_dispatcher.CheckAccess()) return func();
            object value = _dispatcher.Invoke(func);
            return (T)value;
        }
    }

    internal static class Theme
    {
        public static readonly Color NavyColor = Color.FromRgb(13, 58, 119);
        public static readonly Color BlueColor = Color.FromRgb(23, 113, 222);
        public static readonly Color Blue2Color = Color.FromRgb(50, 144, 242);
        public static readonly Color GreenColor = Color.FromRgb(17, 157, 72);
        public static readonly Color RedColor = Color.FromRgb(238, 35, 35);
        public static readonly Color TextColor = Color.FromRgb(28, 39, 63);
        public static readonly Color MutedColor = Color.FromRgb(103, 116, 139);
        public static readonly Color LineColor = Color.FromRgb(216, 225, 237);
        public static readonly Color BackColor = Color.FromRgb(248, 251, 255);
        public static readonly Brush Navy = new SolidColorBrush(NavyColor);
        public static readonly Brush Blue = new SolidColorBrush(BlueColor);
        public static readonly Brush Green = new SolidColorBrush(GreenColor);
        public static readonly Brush Red = new SolidColorBrush(RedColor);
        public static readonly Brush Text = new SolidColorBrush(TextColor);
        public static readonly Brush Muted = new SolidColorBrush(MutedColor);
        public static readonly Brush Line = new SolidColorBrush(LineColor);
        public static readonly Brush Back = new SolidColorBrush(BackColor);

        public static void ConfigureWindow(Window window, double width, double height, string title)
        {
            window.Title = title;
            window.Width = width;
            window.Height = height;
            window.MinWidth = width;
            window.MinHeight = height;
            window.MaxWidth = width;
            window.MaxHeight = height;
            window.ResizeMode = ResizeMode.NoResize;
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.Background = Back;
            window.FontFamily = new FontFamily("Segoe UI");
            window.FontSize = 13;
            window.ShowInTaskbar = false;
            window.SizeToContent = SizeToContent.Manual;
        }

        public static Border Card(UIElement child, Thickness margin, Thickness padding)
        {
            Border border = new Border();
            border.Margin = margin;
            border.Padding = padding;
            border.Background = Brushes.White;
            border.BorderBrush = Line;
            border.BorderThickness = new Thickness(1);
            border.CornerRadius = new CornerRadius(8);
            border.Child = child;
            return border;
        }

        public static Border Circle(string text, Brush background, double size)
        {
            Border circle = new Border();
            circle.Width = size;
            circle.Height = size;
            circle.CornerRadius = new CornerRadius(size / 2.0);
            circle.Background = background;
            circle.BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255));
            circle.BorderThickness = new Thickness(3);
            circle.Effect = new DropShadowEffect { BlurRadius = 12, ShadowDepth = 1, Opacity = 0.16 };
            TextBlock t = new TextBlock();
            t.Text = text;
            t.Foreground = Brushes.White;
            t.FontWeight = FontWeights.Bold;
            t.FontSize = size * 0.52;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            circle.Child = t;
            return circle;
        }

        public static Border WordIcon()
        {
            Border b = new Border();
            b.Width = 34;
            b.Height = 34;
            b.CornerRadius = new CornerRadius(5);
            b.Background = Blue;
            TextBlock t = new TextBlock();
            t.Text = "W";
            t.Foreground = Brushes.White;
            t.FontWeight = FontWeights.Bold;
            t.FontSize = 18;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            return b;
        }

        public static TextBlock TextBlock(string text, double size, Brush color, FontWeight weight)
        {
            TextBlock tb = new TextBlock();
            tb.Text = text ?? string.Empty;
            tb.FontSize = size;
            tb.Foreground = color;
            tb.FontWeight = weight;
            tb.TextWrapping = TextWrapping.Wrap;
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }

        public static Button Button(string text, bool primary, double width)
        {
            Button button = new Button();
            button.Content = text;
            button.Width = width;
            button.Height = 36;
            button.Margin = new Thickness(8, 0, 0, 0);
            button.Padding = new Thickness(14, 4, 14, 4);
            button.FontSize = 13;
            button.FontWeight = FontWeights.SemiBold;
            button.Cursor = System.Windows.Input.Cursors.Hand;
            button.Background = primary ? Blue : Brushes.White;
            button.Foreground = primary ? Brushes.White : Text;
            button.BorderBrush = primary ? Blue : Line;
            button.BorderThickness = new Thickness(1);
            return button;
        }

        public static Grid DataGrid(string[,] rows, double labelWidth)
        {
            int count = rows.GetLength(0);
            Grid grid = new Grid();
            grid.Background = Brushes.White;
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(labelWidth) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int i;
            for (i = 0; i < count; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(35) });

            for (i = 0; i < count; i++)
            {
                Border left = new Border();
                left.BorderBrush = Line;
                left.BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 1, 0);
                left.Background = new SolidColorBrush(Color.FromRgb(250, 252, 255));
                left.Padding = new Thickness(12, 0, 8, 0);
                left.Child = TextBlock(rows[i, 0], 12.5, Text, FontWeights.SemiBold);
                Grid.SetRow(left, i); Grid.SetColumn(left, 0); grid.Children.Add(left);

                Border right = new Border();
                right.BorderBrush = Line;
                right.BorderThickness = new Thickness(0, i == 0 ? 0 : 1, 0, 0);
                right.Padding = new Thickness(14, 0, 10, 0);
                TextBlock value = TextBlock(rows[i, 1], 12.5, Text, FontWeights.Normal);
                value.TextTrimming = TextTrimming.CharacterEllipsis;
                right.Child = value;
                Grid.SetRow(right, i); Grid.SetColumn(right, 1); grid.Children.Add(right);
            }
            return grid;
        }

        public static string Shorten(string value, int max)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= max) return value ?? string.Empty;
            if (max < 5) return value.Substring(0, max);
            return value.Substring(0, max - 3) + "...";
        }

        public static string DetectMode(string headline, string body)
        {
            string all = ((headline ?? string.Empty) + " " + (body ?? string.Empty)).ToLowerInvariant();
            return all.IndexOf("đảng", StringComparison.Ordinal) >= 0 ? "OneClick Đảng" : "OneClick Chính quyền";
        }

        public static string DetectResult(string summary)
        {
            string s = (summary ?? string.Empty).ToLowerInvariant();
            if (s.IndexOf("không có lỗi", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("không có thay đổi", StringComparison.Ordinal) >= 0 ||
                s.IndexOf("không cần", StringComparison.Ordinal) >= 0)
                return "Không có thay đổi";
            return "Đã có thay đổi";
        }

        public static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
            int hours = (int)Math.Floor(elapsed.TotalHours);
            return hours.ToString("00") + ":" + elapsed.Minutes.ToString("00") + ":" + elapsed.Seconds.ToString("00");
        }
    }

    internal sealed class ModernConfirmWindow : Window
    {
        public ModernConfirmWindow(string documentName, string headline, string body, string primaryText, string cancelText)
        {
            Theme.ConfigureWindow(this, 660, 470, "Hỗ trợ STVB 3.0.0 - OneClick");
            Grid root = new Grid();
            root.Margin = new Thickness(24, 20, 24, 20);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            this.Content = root;

            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border icon = Theme.Circle("i", new LinearGradientBrush(Theme.Blue2Color, Theme.BlueColor, 90), 66);
            icon.Margin = new Thickness(4, 2, 12, 2);
            Grid.SetColumn(icon, 0); hero.Children.Add(icon);
            StackPanel heroText = new StackPanel();
            TextBlock title = Theme.TextBlock(string.IsNullOrWhiteSpace(headline) ? "BẮT ĐẦU CHUẨN HÓA VĂN BẢN" : headline.ToUpperInvariant(), 22, Theme.Navy, FontWeights.Bold);
            title.Margin = new Thickness(0, 0, 0, 7);
            TextBlock subtitle = Theme.TextBlock("Hệ thống sẽ tạo bản sao an toàn và thực hiện quy trình OneClick gồm 16 bước.", 13, Theme.Text, FontWeights.Normal);
            heroText.Children.Add(title); heroText.Children.Add(subtitle);
            Grid.SetColumn(heroText, 1); hero.Children.Add(heroText);
            Grid.SetRow(hero, 0); root.Children.Add(hero);

            Grid doc = new Grid();
            doc.Margin = new Thickness(4, 14, 4, 12);
            doc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            doc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border w = Theme.WordIcon(); Grid.SetColumn(w, 0); doc.Children.Add(w);
            TextBlock fileText = Theme.TextBlock("Tệp:  " + Theme.Shorten(documentName, 66), 13.5, Theme.Text, FontWeights.SemiBold);
            Grid.SetColumn(fileText, 1); doc.Children.Add(fileText);
            Grid.SetRow(doc, 1); root.Children.Add(doc);

            string[,] rows = new string[,] {
                { "Chế độ", Theme.DetectMode(headline, body) },
                { "Số bước", "16" },
                { "Sao lưu an toàn", "Có" },
                { "Tự khắc phục", "Bật" }
            };
            Border table = Theme.Card(Theme.DataGrid(rows, 215), new Thickness(4, 0, 4, 0), new Thickness(0));
            Grid.SetRow(table, 2); root.Children.Add(table);

            TextBlock detail = Theme.TextBlock(Theme.Shorten((body ?? string.Empty).Replace("\r", " ").Replace("\n", "  "), 220), 11.5, Theme.Muted, FontWeights.Normal);
            detail.Margin = new Thickness(8, 10, 8, 8);
            detail.MaxHeight = 38;
            Grid.SetRow(detail, 3); root.Children.Add(detail);

            StackPanel buttons = new StackPanel();
            buttons.Orientation = Orientation.Horizontal;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            buttons.Margin = new Thickness(0, 4, 0, 0);
            Button cancel = Theme.Button(string.IsNullOrWhiteSpace(cancelText) ? "Hủy" : cancelText, false, 86);
            Button primary = Theme.Button(string.IsNullOrWhiteSpace(primaryText) ? "Bắt đầu" : primaryText, true, 104);
            cancel.Click += delegate { this.DialogResult = false; };
            primary.Click += delegate { this.DialogResult = true; };
            buttons.Children.Add(cancel); buttons.Children.Add(primary);
            Grid.SetRow(buttons, 4); root.Children.Add(buttons);
        }
    }

    internal sealed class ProgressWindow : Window
    {
        private bool _allowClose;
        private TextBlock _file;
        private TextBlock _step;
        private TextBlock _percent;
        private TextBlock _task;
        private TextBlock _backup;
        private Border _fill;
        private Border _track;

        public ProgressWindow()
        {
            Theme.ConfigureWindow(this, 660, 450, "Hỗ trợ STVB 3.0.0 - OneClick");
            this.Closing += delegate(object sender, System.ComponentModel.CancelEventArgs e) { if (!_allowClose) e.Cancel = true; };

            Grid root = new Grid();
            root.Margin = new Thickness(24, 20, 24, 18);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            this.Content = root;

            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border icon = Theme.Circle("i", new LinearGradientBrush(Theme.Blue2Color, Theme.BlueColor, 90), 66);
            icon.Margin = new Thickness(4, 2, 12, 2); Grid.SetColumn(icon, 0); hero.Children.Add(icon);
            StackPanel text = new StackPanel();
            TextBlock title = Theme.TextBlock("ĐANG CHẠY ONECLICK", 22, Theme.Navy, FontWeights.Bold);
            title.Margin = new Thickness(0, 0, 0, 6);
            TextBlock sub = Theme.TextBlock("Hệ thống đang kiểm tra thể thức, chuẩn hóa trình bày và cập nhật kết quả.", 13, Theme.Text, FontWeights.Normal);
            text.Children.Add(title); text.Children.Add(sub); Grid.SetColumn(text, 1); hero.Children.Add(text);
            Grid.SetRow(hero, 0); root.Children.Add(hero);

            Grid doc = new Grid(); doc.Margin = new Thickness(4, 12, 4, 10);
            doc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            doc.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border w = Theme.WordIcon(); Grid.SetColumn(w, 0); doc.Children.Add(w);
            _file = Theme.TextBlock("", 13.5, Theme.Text, FontWeights.SemiBold); Grid.SetColumn(_file, 1); doc.Children.Add(_file);
            Grid.SetRow(doc, 1); root.Children.Add(doc);

            Grid tableGrid = new Grid();
            tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(215) });
            tableGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int i; for (i = 0; i < 5; i++) tableGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(34) });
            AddProgressRow(tableGrid, 0, "Trạng thái", out _dummy, "Đang thực hiện");
            AddProgressRow(tableGrid, 1, "Bước hiện tại", out _step, "1 / 16");
            AddProgressRow(tableGrid, 2, "Tiến độ", out _percent, "6%");
            AddProgressRow(tableGrid, 3, "Tác vụ", out _task, "Đang khởi tạo");
            AddProgressRow(tableGrid, 4, "Sao lưu an toàn", out _backup, "Đang tạo");
            Border table = Theme.Card(tableGrid, new Thickness(4, 0, 4, 0), new Thickness(0));
            Grid.SetRow(table, 2); root.Children.Add(table);

            Grid progress = new Grid(); progress.Margin = new Thickness(4, 14, 4, 0);
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progress.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(56) });
            _track = new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Color.FromRgb(226, 233, 243)), HorizontalAlignment = HorizontalAlignment.Stretch };
            _fill = new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = new LinearGradientBrush(Theme.Blue2Color, Theme.BlueColor, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 4 };
            Grid trackGrid = new Grid(); trackGrid.Children.Add(_track); trackGrid.Children.Add(_fill);
            Grid.SetColumn(trackGrid, 0); progress.Children.Add(trackGrid);
            TextBlock pct = Theme.TextBlock("", 12.5, Theme.Text, FontWeights.SemiBold); pct.HorizontalAlignment = HorizontalAlignment.Right; pct.Name = "PctText";
            Grid.SetColumn(pct, 1); progress.Children.Add(pct);
            progress.Tag = pct;
            Grid.SetRow(progress, 3); root.Children.Add(progress);

            TextBlock hint = Theme.TextBlock("Không đóng Word trong khi OneClick đang chạy.", 11.5, Theme.Muted, FontWeights.Normal);
            hint.Margin = new Thickness(6, 10, 4, 0); Grid.SetRow(hint, 4); root.Children.Add(hint);
        }

        private TextBlock _dummy;

        private static void AddProgressRow(Grid grid, int row, string label, out TextBlock value, string initial)
        {
            Border left = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, row == 0 ? 0 : 1, 1, 0), Background = new SolidColorBrush(Color.FromRgb(250, 252, 255)), Padding = new Thickness(12, 0, 8, 0) };
            left.Child = Theme.TextBlock(label, 12.2, Theme.Text, FontWeights.SemiBold); Grid.SetRow(left, row); Grid.SetColumn(left, 0); grid.Children.Add(left);
            Border right = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, row == 0 ? 0 : 1, 0, 0), Padding = new Thickness(14, 0, 10, 0) };
            value = Theme.TextBlock(initial, 12.2, Theme.Text, FontWeights.Normal); value.TextTrimming = TextTrimming.CharacterEllipsis;
            right.Child = value; Grid.SetRow(right, row); Grid.SetColumn(right, 1); grid.Children.Add(right);
        }

        public void UpdateState(string documentName, int stepIndex, int totalSteps, string statusText)
        {
            if (totalSteps <= 0) totalSteps = 16;
            if (stepIndex < 1) stepIndex = 1;
            if (stepIndex > totalSteps) stepIndex = totalSteps;
            int pct = (int)Math.Round(100.0 * stepIndex / totalSteps);
            _file.Text = "Tệp:  " + Theme.Shorten(documentName, 66);
            _step.Text = stepIndex.ToString() + " / " + totalSteps.ToString();
            _percent.Text = pct.ToString() + "%";
            _task.Text = Theme.Shorten(statusText, 78);
            _backup.Text = stepIndex <= 1 ? "Đang tạo" : "Đã tạo";
            Grid progressGrid = null;
            DependencyObject p = _track.Parent;
            if (p is Grid) progressGrid = ((Grid)p).Parent as Grid;
            if (progressGrid != null && progressGrid.Tag is TextBlock) ((TextBlock)progressGrid.Tag).Text = pct.ToString() + "%";
            double trackWidth = _track.ActualWidth > 20.0 ? _track.ActualWidth : 528.0;
            _fill.Width = Math.Max(4.0, trackWidth * pct / 100.0);
        }

        public void CloseSafe()
        {
            _allowClose = true;
            this.Close();
        }
    }

    internal sealed class ModernCompletionWindow : Window
    {
        public ModernCompletionWindow(string documentName, TimeSpan elapsed, string summaryText, string reminderText)
        {
            Theme.ConfigureWindow(this, 660, 520, "Hỗ trợ STVB 3.0.0 - Chuẩn hóa văn bản");
            Grid root = new Grid();
            root.Margin = new Thickness(24, 20, 24, 18);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            this.Content = root;

            Grid hero = new Grid();
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(82) });
            hero.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border icon = Theme.Circle("✓", new LinearGradientBrush(Color.FromRgb(38, 193, 88), Theme.GreenColor, 90), 66);
            icon.Margin = new Thickness(4, 2, 12, 2); Grid.SetColumn(icon, 0); hero.Children.Add(icon);
            StackPanel text = new StackPanel();
            TextBlock title = Theme.TextBlock("HOÀN TẤT CHUẨN HÓA VĂN BẢN", 22, Theme.Navy, FontWeights.Bold); title.Margin = new Thickness(0, 0, 0, 6);
            TextBlock sub = Theme.TextBlock("Quá trình chuẩn hóa văn bản đã hoàn tất. Tài liệu đã được xử lý theo quy trình OneClick.", 13, Theme.Text, FontWeights.Normal);
            text.Children.Add(title); text.Children.Add(sub); Grid.SetColumn(text, 1); hero.Children.Add(text);
            Grid.SetRow(hero, 0); root.Children.Add(hero);

            string[,] rows = new string[,] {
                { "Trạng thái", "Hoàn tất" },
                { "Kết quả", Theme.DetectResult(summaryText) },
                { "Tổng thời gian", Theme.FormatElapsed(elapsed) },
                { "Số bước thực hiện", "16 / 16" },
                { "Đã sao lưu", "Có" },
                { "Tệp", Theme.Shorten(documentName, 64) }
            };
            Border table = Theme.Card(Theme.DataGrid(rows, 185), new Thickness(4, 14, 4, 0), new Thickness(0));
            Grid.SetRow(table, 1); root.Children.Add(table);

            TextBlock summary = Theme.TextBlock(Theme.Shorten((summaryText ?? string.Empty).Replace("\r", " ").Replace("\n", "  "), 230), 11.2, Theme.Muted, FontWeights.Normal);
            summary.Margin = new Thickness(8, 8, 8, 0); summary.MaxHeight = 35;
            Grid.SetRow(summary, 2); root.Children.Add(summary);

            Border reminderCard = new Border();
            reminderCard.Margin = new Thickness(8, 10, 8, 0);
            reminderCard.Padding = new Thickness(10, 7, 10, 7);
            reminderCard.CornerRadius = new CornerRadius(6);
            reminderCard.Background = new SolidColorBrush(Color.FromRgb(255, 245, 245));
            TextBlock reminder = Theme.TextBlock(reminderText, 12.5, Theme.Red, FontWeights.Bold);
            reminder.TextAlignment = TextAlignment.Center;
            reminderCard.Child = reminder;
            Grid.SetRow(reminderCard, 3); root.Children.Add(reminderCard);

            Grid footer = new Grid(); footer.Margin = new Thickness(8, 10, 4, 0);
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TextBlock thanks = Theme.TextBlock("Cảm ơn Anh/Chị đã sử dụng Hỗ trợ STVB!", 12.2, Theme.Text, FontWeights.Normal); Grid.SetColumn(thanks, 0); footer.Children.Add(thanks);
            Button close = Theme.Button("OK", true, 92); close.Click += delegate { this.DialogResult = true; }; Grid.SetColumn(close, 1); footer.Children.Add(close);
            Grid.SetRow(footer, 4); root.Children.Add(footer);
        }
    }
}