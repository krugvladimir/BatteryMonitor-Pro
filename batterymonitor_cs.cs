// batterymonitor_cs.cs — монитор батареи с графиками на C# (WPF)

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BatteryMonitorWPF
{
    public partial class MainWindow : Window
    {
        private const int HISTORY_SIZE = 60;
        private Queue<double> chargeHistory = new Queue<double>(HISTORY_SIZE);
        private Queue<double> tempHistory = new Queue<double>(HISTORY_SIZE);
        private Queue<long> timeHistory = new Queue<long>(HISTORY_SIZE);
        private double currentCharge = 100.0;
        private double currentTemp = 25.0;
        private int remainingTime = 0;
        private bool notifiedLow = false, notifiedHighTemp = false;
        private Random rand = new Random();
        private DispatcherTimer timer;
        private string configFile = "battery_config.json";

        private Label chargeLabel, timeLabel, tempLabel, statusLabel;
        private Canvas graphCanvas;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfig();
            CreateUI();
            InitializeData();
            StartTimer();
        }

        private void CreateUI()
        {
            Title = "🔋 BatteryMonitor Pro — C#";
            Width = 800;
            Height = 600;
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Info panel
            var infoPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            chargeLabel = new Label { Content = "Заряд: 100%", FontSize = 14 };
            timeLabel = new Label { Content = "Время: --", FontSize = 14, Margin = new Thickness(20,0,0,0) };
            tempLabel = new Label { Content = "Температура: 25°C", FontSize = 14, Margin = new Thickness(20,0,0,0) };
            statusLabel = new Label { Content = "Статус: Норма", FontSize = 14, Foreground = Brushes.Green, Margin = new Thickness(20,0,0,0) };
            infoPanel.Children.Add(chargeLabel);
            infoPanel.Children.Add(timeLabel);
            infoPanel.Children.Add(tempLabel);
            infoPanel.Children.Add(statusLabel);
            Grid.SetRow(infoPanel, 0);
            grid.Children.Add(infoPanel);

            // Graph Canvas
            graphCanvas = new Canvas { Background = Brushes.White };
            Grid.SetRow(graphCanvas, 1);
            grid.Children.Add(graphCanvas);

            // Buttons
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10) };
            var exportBtn = new Button { Content = "Экспорт CSV", Width = 100 };
            var resetBtn = new Button { Content = "Сбросить данные", Width = 100 };
            btnPanel.Children.Add(exportBtn);
            btnPanel.Children.Add(resetBtn);
            Grid.SetRow(btnPanel, 2);
            grid.Children.Add(btnPanel);

            exportBtn.Click += (s, e) => ExportCSV();
            resetBtn.Click += (s, e) => ResetData();

            Content = grid;
        }

        private void InitializeData()
        {
            for (int i = 0; i < 10; i++)
            {
                chargeHistory.Enqueue(currentCharge);
                tempHistory.Enqueue(currentTemp);
                timeHistory.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            DrawGraph();
        }

        private void StartTimer()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (s, e) => UpdateData();
            timer.Start();
        }

        private void UpdateData()
        {
            // Симуляция
            currentCharge += rand.NextDouble() * 0.6 - 0.5;
            currentCharge = Math.Max(0, Math.Min(100, currentCharge));
            currentTemp += rand.NextDouble() * 7 - 2 + (100 - currentCharge) * 0.1;
            currentTemp = Math.Max(20, Math.Min(60, currentTemp));

            // Время
            if (chargeHistory.Count > 5)
            {
                double[] arr = chargeHistory.ToArray();
                double rate = (arr[arr.Length-1] - arr[0]) / arr.Length;
                if (rate < 0)
                    remainingTime = (int)(currentCharge / Math.Abs(rate) * 60);
                else
                    remainingTime = 999;
            }

            // История
            chargeHistory.Enqueue(currentCharge);
            tempHistory.Enqueue(currentTemp);
            timeHistory.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            if (chargeHistory.Count > HISTORY_SIZE)
            {
                chargeHistory.Dequeue();
                tempHistory.Dequeue();
                timeHistory.Dequeue();
            }

            // Обновление UI
            chargeLabel.Content = $"Заряд: {currentCharge:F1}%";
            if (remainingTime < 999)
                timeLabel.Content = $"Время: {remainingTime/60}ч {remainingTime%60}мин";
            else
                timeLabel.Content = "Время: ∞";
            tempLabel.Content = $"Температура: {currentTemp:F1}°C";

            if (currentCharge > 80)
            {
                statusLabel.Content = "Статус: Отлично";
                statusLabel.Foreground = Brushes.Green;
            }
            else if (currentCharge > 50)
            {
                statusLabel.Content = "Статус: Хорошо";
                statusLabel.Foreground = Brushes.Blue;
            }
            else if (currentCharge > 20)
            {
                statusLabel.Content = "Статус: Нормально";
                statusLabel.Foreground = Brushes.Orange;
            }
            else
            {
                statusLabel.Content = "Статус: Критично!";
                statusLabel.Foreground = Brushes.Red;
            }

            DrawGraph();

            // Уведомления
            if (currentCharge < 20 && !notifiedLow)
            {
                notifiedLow = true;
                MessageBox.Show($"Уровень батареи {currentCharge:F1}%! Подключите зарядку.", "Низкий заряд", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (currentCharge >= 25)
                notifiedLow = false;

            if (currentTemp > 50 && !notifiedHighTemp)
            {
                notifiedHighTemp = true;
                MessageBox.Show($"Температура батареи {currentTemp:F1}°C! Охладите устройство.", "Высокая температура", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else if (currentTemp <= 45)
                notifiedHighTemp = false;
        }

        private void DrawGraph()
        {
            if (graphCanvas == null || chargeHistory.Count < 2) return;
            graphCanvas.Children.Clear();

            double w = graphCanvas.ActualWidth;
            double h = graphCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            int margin = 40;
            int graphW = (int)(w - 2 * margin);
            int graphH = (int)(h - 2 * margin);

            // Сетка
            for (int i = 0; i <= 5; i++)
            {
                int y = (int)(h - margin - i * graphH / 5.0);
                Line line = new Line { X1 = margin, Y1 = y, X2 = w - margin, Y2 = y, Stroke = Brushes.LightGray, StrokeThickness = 1 };
                graphCanvas.Children.Add(line);
                TextBlock tb = new TextBlock { Text = (i * 20).ToString(), FontSize = 10 };
                Canvas.SetLeft(tb, 5);
                Canvas.SetTop(tb, y - 8);
                graphCanvas.Children.Add(tb);
            }

            // Оси
            Line axisX = new Line { X1 = margin, Y1 = h - margin, X2 = w - margin, Y2 = h - margin, Stroke = Brushes.Black, StrokeThickness = 2 };
            Line axisY = new Line { X1 = margin, Y1 = margin, X2 = margin, Y2 = h - margin, Stroke = Brushes.Black, StrokeThickness = 2 };
            graphCanvas.Children.Add(axisX);
            graphCanvas.Children.Add(axisY);

            // Подписи осей
            TextBlock xLabel = new TextBlock { Text = "Время (сек)", FontSize = 10 };
            Canvas.SetLeft(xLabel, w/2 - 30);
            Canvas.SetTop(xLabel, h - 15);
            graphCanvas.Children.Add(xLabel);
            TextBlock yLabel = new TextBlock { Text = "Заряд (%)", FontSize = 10 };
            Canvas.SetLeft(yLabel, 5);
            Canvas.SetTop(yLabel, 5);
            graphCanvas.Children.Add(yLabel);

            // Данные
            double[] chargeArr = chargeHistory.ToArray();
            double[] tempArr = tempHistory.ToArray();
            int n = chargeArr.Length;

            double maxCharge = 105, minCharge = 0;
            double maxTemp = 60, minTemp = 0;

            // Рисуем заряд (синий)
            Polyline chargeLine = new Polyline { Stroke = Brushes.Blue, StrokeThickness = 2 };
            for (int i = 0; i < n; i++)
            {
                double x = margin + i * (double)graphW / (n-1);
                double y = h - margin - (chargeArr[i] - minCharge) * graphH / (maxCharge - minCharge);
                chargeLine.Points.Add(new Point(x, y));
            }
            graphCanvas.Children.Add(chargeLine);

            // Рисуем температуру (красный)
            Polyline tempLine = new Polyline { Stroke = Brushes.Red, StrokeThickness = 1.5 };
            for (int i = 0; i < n; i++)
            {
                double x = margin + i * (double)graphW / (n-1);
                double y = h - margin - (tempArr[i] - minTemp) * graphH / (maxTemp - minTemp);
                tempLine.Points.Add(new Point(x, y));
            }
            graphCanvas.Children.Add(tempLine);

            // Легенда
            TextBlock legend1 = new TextBlock { Text = "Заряд", Foreground = Brushes.Blue, FontSize = 10 };
            Canvas.SetLeft(legend1, w - 60);
            Canvas.SetTop(legend1, 10);
            graphCanvas.Children.Add(legend1);
            TextBlock legend2 = new TextBlock { Text = "Температура", Foreground = Brushes.Red, FontSize = 10 };
            Canvas.SetLeft(legend2, w - 60);
            Canvas.SetTop(legend2, 25);
            graphCanvas.Children.Add(legend2);
        }

        private void ExportCSV()
        {
            if (chargeHistory.Count < 2)
            {
                MessageBox.Show("Недостаточно данных для экспорта");
                return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "CSV (*.csv)|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                using (var sw = new StreamWriter(dialog.FileName))
                {
                    sw.WriteLine("Время,Заряд(%),Температура(°C)");
                    var chargeArr = chargeHistory.ToArray();
                    var tempArr = tempHistory.ToArray();
                    var timeArr = timeHistory.ToArray();
                    for (int i = 0; i < chargeArr.Length; i++)
                        sw.WriteLine($"{timeArr[i]},{chargeArr[i]:F2},{tempArr[i]:F2}");
                }
                MessageBox.Show("Данные сохранены в " + dialog.FileName);
            }
        }

        private void ResetData()
        {
            chargeHistory.Clear();
            tempHistory.Clear();
            timeHistory.Clear();
            currentCharge = 100;
            currentTemp = 25;
            remainingTime = 0;
            for (int i = 0; i < 10; i++)
            {
                chargeHistory.Enqueue(currentCharge);
                tempHistory.Enqueue(currentTemp);
                timeHistory.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            }
            DrawGraph();
        }

        private void LoadConfig()
        {
            // Можно загружать настройки, но для простоты пропускаем
        }

        private void SaveConfig()
        {
            // Можно сохранять настройки
        }

        [STAThread]
        static void Main()
        {
            var app = new Application();
            app.Run(new MainWindow());
        }
    }
}
