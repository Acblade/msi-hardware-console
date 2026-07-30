using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MsiHardwareConsole
{
    internal sealed class FanCurveChart : FrameworkElement
    {
        private FanCurve curve;
        private int draggedPoint = -1;
        private readonly bool editable;
        private readonly Brush accent;
        private readonly int maximumDuty;
        private readonly bool compressedDutyAxis;
        private readonly int sustainedProtectionTemperature;
        private readonly int emergencyProtectionTemperature;
        private readonly int releaseProtectionTemperature;
        private const double CompressedBandFraction = 0.12;

        public event EventHandler CurveChanged;

        public FanCurveChart(FanCurve curve, bool editable, Brush accent,
            int sustainedProtectionTemperature = 0, int emergencyProtectionTemperature = 0, int releaseProtectionTemperature = 0)
        {
            this.curve = curve.Clone();
            this.editable = editable;
            this.accent = accent;
            this.sustainedProtectionTemperature = sustainedProtectionTemperature;
            this.emergencyProtectionTemperature = emergencyProtectionTemperature;
            this.releaseProtectionTemperature = releaseProtectionTemperature;
            if (editable)
            {
                for (int i = 0; i < 7; i++)
                {
                    int speed = this.curve.Speeds[i];
                    this.curve.Speeds[i] = speed <= 0 ? 0 : speed >= 100 ? 100 : Math.Max(30, Math.Min(60, speed));
                    if (i > 0 && this.curve.Speeds[i] < this.curve.Speeds[i - 1])
                        this.curve.Speeds[i] = this.curve.Speeds[i - 1];
                }
            }
            maximumDuty = 60;
            for (int i = 0; i < this.curve.Speeds.Length; i++)
                if (this.curve.Speeds[i] > 60) { maximumDuty = 100; break; }
            compressedDutyAxis = editable || maximumDuty <= 60;
            MinHeight = 330;
            Cursor = editable ? Cursors.Hand : Cursors.Arrow;
            Focusable = true;
        }

        public FanCurve Curve { get { return curve.Clone(); } }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double left = 54, top = 26, right = 25, bottom = 45;
            double width = Math.Max(1, ActualWidth - left - right);
            double height = Math.Max(1, ActualHeight - top - bottom);
            var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(226, 232, 241)), 1);
            var axisPen = new Pen(new SolidColorBrush(Color.FromRgb(116, 130, 151)), 1.2);

            dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(222, 229, 239)), 1),
                new Rect(0.5, 0.5, ActualWidth - 1, ActualHeight - 1), 16, 16);

            int[] dutyGrid = compressedDutyAxis
                ? new[] { 0, 30, 40, 50, 60, 100 }
                : new[] { 0, 20, 40, 60, 80, 100 };
            for (int i = 0; i < dutyGrid.Length; i++)
            {
                int p = dutyGrid[i];
                double y = top + height * (1 - DutyToAxisFraction(p));
                dc.DrawLine(gridPen, new Point(left, y), new Point(left + width, y));
                DrawText(dc, p + "%", 11, new Point(12, y - 8), new SolidColorBrush(Color.FromRgb(104, 117, 138)));
            }
            for (int t = 40; t <= 100; t += 10)
            {
                double x = left + width * ((t - 40) / 60.0);
                dc.DrawLine(gridPen, new Point(x, top), new Point(x, top + height));
                DrawText(dc, t + "°C", 11, new Point(x - 13, top + height + 12), new SolidColorBrush(Color.FromRgb(104, 117, 138)));
            }
            DrawProtectionMarker(dc, releaseProtectionTemperature, Localization.T("Restore", "恢复"), new SolidColorBrush(Color.FromRgb(22, 133, 106)), 0, left, top, width, height);
            DrawProtectionMarker(dc, sustainedProtectionTemperature, Localization.T("Sustained", "持续"), new SolidColorBrush(Color.FromRgb(217, 120, 22)), 1, left, top, width, height);
            DrawProtectionMarker(dc, emergencyProtectionTemperature, Localization.T("Emergency", "紧急"), new SolidColorBrush(Color.FromRgb(216, 74, 74)), 0, left, top, width, height);
            dc.DrawLine(axisPen, new Point(left, top + height), new Point(left + width, top + height));
            dc.DrawLine(axisPen, new Point(left, top), new Point(left, top + height));
            if (compressedDutyAxis)
            {
                DrawAxisBreak(dc, axisPen, left, top + height * (1 - DutyToAxisFraction(15)));
                DrawAxisBreak(dc, axisPen, left, top + height * (1 - DutyToAxisFraction(80)));
            }

            var points = new Point[7];
            for (int i = 0; i < 7; i++) points[i] = ToPoint(i, left, top, width, height);
            var fill = new StreamGeometry();
            using (var context = fill.Open())
            {
                context.BeginFigure(new Point(points[0].X, top + height), true, true);
                foreach (Point point in points) context.LineTo(point, true, false);
                context.LineTo(new Point(points[6].X, top + height), true, false);
            }
            fill.Freeze();
            var fillBrush = accent.Clone();
            fillBrush.Opacity = 0.10;
            dc.DrawGeometry(fillBrush, null, fill);

            var linePen = new Pen(accent, 3.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
            for (int i = 1; i < points.Length; i++) dc.DrawLine(linePen, points[i - 1], points[i]);
            for (int i = 0; i < points.Length; i++)
            {
                dc.DrawEllipse(Brushes.White, new Pen(accent, 3), points[i], 7.5, 7.5);
                string label = curve.Temperatures[i] + "°  " + curve.Speeds[i] + "%";
                double labelY = points[i].Y < top + 34 ? points[i].Y + 24 : points[i].Y - 25;
                DrawText(dc, label, 10, new Point(points[i].X - 22, labelY), new SolidColorBrush(Color.FromRgb(48, 64, 88)));
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!editable) return;
            Focus();
            Point mouse = e.GetPosition(this);
            double best = 22;
            int bestIndex = -1;
            for (int i = 0; i < 7; i++)
            {
                Point point = ToPoint(i, 54, 26, Math.Max(1, ActualWidth - 79), Math.Max(1, ActualHeight - 71));
                double distance = (point - mouse).Length;
                if (distance < best) { best = distance; bestIndex = i; }
            }
            if (bestIndex >= 0)
            {
                draggedPoint = bestIndex;
                CaptureMouse();
                e.Handled = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!editable || draggedPoint < 0 || e.LeftButton != MouseButtonState.Pressed) return;
            Point mouse = e.GetPosition(this);
            double width = Math.Max(1, ActualWidth - 79);
            double height = Math.Max(1, ActualHeight - 71);
            int temperature = (int)Math.Round(40 + ((mouse.X - 54) / width) * 60);
            double axisFraction = 1 - ((mouse.Y - 26) / height);
            int speed = (int)Math.Round(AxisFractionToDuty(axisFraction));
            speed = Math.Max(0, Math.Min(100, speed));
            // MSI accepts 0% as fan-off. Non-zero values below 30% are not a
            // reliable running range across laptops, so snap to 0 or 30%.
            if (speed > 0 && speed < 30) speed = speed < 15 ? 0 : 30;
            if (speed > 60 && speed < 100) speed = speed < 80 ? 60 : 100;
            int i = draggedPoint;
            if (i > 0)
            {
                int minimum = curve.Temperatures[i - 1] + 3;
                int maximum = i == 6 ? 90 : curve.Temperatures[i + 1] - 3;
                curve.Temperatures[i] = Math.Max(minimum, Math.Min(maximum, temperature));
            }
            int minimumSpeed = i == 0 ? 0 : curve.Speeds[i - 1];
            int maximumSpeed = i == 6 ? 100 : curve.Speeds[i + 1];
            curve.Speeds[i] = Math.Max(minimumSpeed, Math.Min(maximumSpeed, speed));
            InvalidateVisual();
            if (CurveChanged != null) CurveChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (draggedPoint >= 0)
            {
                draggedPoint = -1;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private Point ToPoint(int index, double left, double top, double width, double height)
        {
            double x = left + width * ((Math.Max(40, Math.Min(100, curve.Temperatures[index])) - 40) / 60.0);
            double y = top + height * (1 - DutyToAxisFraction(curve.Speeds[index]));
            return new Point(x, y);
        }

        private double DutyToAxisFraction(double duty)
        {
            duty = Math.Max(0, Math.Min(100, duty));
            if (!compressedDutyAxis) return duty / 100.0;
            if (duty <= 30) return CompressedBandFraction * duty / 30.0;
            if (duty <= 60)
                return CompressedBandFraction + (1 - 2 * CompressedBandFraction) * (duty - 30) / 30.0;
            return 1 - CompressedBandFraction + CompressedBandFraction * (duty - 60) / 40.0;
        }

        private double AxisFractionToDuty(double fraction)
        {
            fraction = Math.Max(0, Math.Min(1, fraction));
            if (!compressedDutyAxis) return fraction * 100;
            if (fraction <= CompressedBandFraction)
                return 30 * fraction / CompressedBandFraction;
            if (fraction <= 1 - CompressedBandFraction)
                return 30 + 30 * (fraction - CompressedBandFraction) / (1 - 2 * CompressedBandFraction);
            return 60 + 40 * (fraction - (1 - CompressedBandFraction)) / CompressedBandFraction;
        }

        private static void DrawAxisBreak(DrawingContext dc, Pen axisPen, double x, double y)
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(x - 6, y - 7, 12, 14));
            dc.DrawLine(axisPen, new Point(x - 4, y - 6), new Point(x + 4, y - 2));
            dc.DrawLine(axisPen, new Point(x + 4, y - 2), new Point(x - 4, y + 2));
            dc.DrawLine(axisPen, new Point(x - 4, y + 2), new Point(x + 4, y + 6));
        }

        private static void DrawProtectionMarker(DrawingContext dc, int temperature, string label, Brush brush, int row,
            double left, double top, double width, double height)
        {
            if (temperature < 40 || temperature > 100) return;
            double x = left + width * ((temperature - 40) / 60.0);
            var pen = new Pen(brush, 1.4) { DashStyle = DashStyles.Dash };
            dc.DrawLine(pen, new Point(x, top), new Point(x, top + height));
            DrawText(dc, label + " " + temperature + "°", 9.5, new Point(Math.Min(x + 4, left + width - 70), top + 3 + row * 14), brush);
        }

        private static void DrawText(DrawingContext dc, string text, double size, Point origin, Brush brush)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                size, brush, 1.0);
            dc.DrawText(formatted, origin);
        }
    }

    internal sealed class PerformanceHistoryChart : FrameworkElement
    {
        private readonly string title;
        private readonly string unit;
        private readonly Brush accent;
        private readonly int maximum;
        private int[] values = new int[0];

        public PerformanceHistoryChart(string title, string unit, Brush accent, int maximum)
        {
            this.title = title;
            this.unit = unit;
            this.accent = accent;
            this.maximum = Math.Max(1, maximum);
            MinHeight = 180;
        }

        public void SetValues(IEnumerable<int> source)
        {
            var copy = new List<int>(source);
            if (copy.Count > 60) copy.RemoveRange(0, copy.Count - 60);
            values = copy.ToArray();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double left = 42, top = 38, right = 20, bottom = 28;
            double width = Math.Max(1, ActualWidth - left - right);
            double height = Math.Max(1, ActualHeight - top - bottom);
            var border = new Pen(new SolidColorBrush(Color.FromRgb(220, 228, 239)), 1);
            var grid = new Pen(new SolidColorBrush(Color.FromRgb(231, 236, 244)), 1);
            dc.DrawRoundedRectangle(Brushes.White, border, new Rect(0.5, 0.5, ActualWidth - 1, ActualHeight - 1), 15, 15);
            DrawText(dc, title, 13, new Point(16, 11), new SolidColorBrush(Color.FromRgb(42, 56, 79)), FontWeights.SemiBold);
            int latest = values.Length == 0 ? 0 : values[values.Length - 1];
            string latestText = latest + unit;
            DrawText(dc, latestText, 13, new Point(Math.Max(left, ActualWidth - 20 - latestText.Length * 9), 11), accent, FontWeights.SemiBold);

            for (int i = 0; i <= 4; i++)
            {
                double y = top + height * i / 4.0;
                dc.DrawLine(grid, new Point(left, y), new Point(left + width, y));
            }
            for (int i = 0; i <= 6; i++)
            {
                double x = left + width * i / 6.0;
                dc.DrawLine(grid, new Point(x, top), new Point(x, top + height));
            }
            DrawText(dc, maximum + unit, 10, new Point(8, top - 7), new SolidColorBrush(Color.FromRgb(112, 125, 145)), FontWeights.Normal);
            DrawText(dc, Localization.T("60 sec", "60 秒"), 10, new Point(left, top + height + 8), new SolidColorBrush(Color.FromRgb(112, 125, 145)), FontWeights.Normal);
            DrawText(dc, Localization.T("Now", "现在"), 10, new Point(left + width - 25, top + height + 8), new SolidColorBrush(Color.FromRgb(112, 125, 145)), FontWeights.Normal);

            if (values.Length == 0) return;
            var points = new List<Point>();
            int startSlot = 60 - values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                double x = left + width * (startSlot + i) / 59.0;
                double y = top + height * (1 - Math.Max(0, Math.Min(maximum, values[i])) / (double)maximum);
                points.Add(new Point(x, y));
            }
            if (points.Count == 1) points.Insert(0, new Point(points[0].X - 1, points[0].Y));

            var fill = new StreamGeometry();
            using (var context = fill.Open())
            {
                context.BeginFigure(new Point(points[0].X, top + height), true, true);
                foreach (Point point in points) context.LineTo(point, true, false);
                context.LineTo(new Point(points[points.Count - 1].X, top + height), true, false);
            }
            var fillBrush = accent.Clone();
            fillBrush.Opacity = 0.10;
            dc.DrawGeometry(fillBrush, null, fill);
            var line = new StreamGeometry();
            using (var context = line.Open())
            {
                context.BeginFigure(points[0], false, false);
                for (int i = 1; i < points.Count; i++) context.LineTo(points[i], true, false);
            }
            dc.DrawGeometry(null, new Pen(accent, 2.2) { LineJoin = PenLineJoin.Round }, line);
        }

        private static void DrawText(DrawingContext dc, string text, double size, Point origin, Brush brush, FontWeight weight)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, weight, FontStretches.Normal),
                size, brush, 1.0);
            dc.DrawText(formatted, origin);
        }
    }

    internal sealed class CurveWindow : Window
    {
        private readonly FanCurveChart chart;
        public FanCurve Result { get; private set; }

        public CurveWindow(Window owner, string title, string subtitle, FanCurve curve, bool editable, Brush accent)
        {
            Owner = owner;
            Title = title + Localization.T(" · Fan curve", " · 风扇曲线");
            Width = 760;
            Height = 540;
            MinWidth = 650;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(245, 248, 252));
            FontFamily = new FontFamily("Microsoft YaHei UI");
            UseLayoutRounding = true;
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.ClearType);

            var root = new Grid { Margin = new Thickness(28) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = root;

            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            heading.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(23, 35, 60)) });
            heading.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 138)), Margin = new Thickness(0, 5, 0, 0) });
            root.Children.Add(heading);

            chart = new FanCurveChart(curve, editable, accent);
            Grid.SetRow(chart, 1);
            root.Children.Add(chart);

            var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.Children.Add(new TextBlock
            {
                Text = editable
                    ? Localization.T("Available duty for all seven points: 0%, 30–60%, or 100%.", "七个节点的可用转速：0%、30–60% 或 100%。")
                    : Localization.T("Temperature is horizontal; fan duty is vertical.", "横轴是温度，纵轴是风扇转速百分比。"),
                Foreground = new SolidColorBrush(Color.FromRgb(104, 117, 138)),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var close = MakeButton(editable ? Localization.T("Cancel", "取消") : Localization.T("Close", "关闭"), new SolidColorBrush(Color.FromRgb(107, 120, 142)));
            close.Click += delegate { DialogResult = false; Close(); };
            buttons.Children.Add(close);
            if (editable)
            {
                var save = MakeButton(Localization.T("Save and apply", "保存并应用"), accent);
                save.Margin = new Thickness(10, 0, 0, 0);
                save.Click += delegate { Result = chart.Curve; DialogResult = true; Close(); };
                buttons.Children.Add(save);
            }
            Grid.SetColumn(buttons, 1);
            footer.Children.Add(buttons);
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);
        }

        private static Button MakeButton(string text, Brush background)
        {
            return new Button
            {
                Content = text,
                Background = background,
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(18, 9, 18, 9),
                FontWeight = FontWeights.SemiBold,
                Cursor = Cursors.Hand
            };
        }
    }
}
