using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Button = System.Windows.Controls.Button;

namespace SmartLampApp.Services
{
    public partial class ColorPickerWindow : Window
    {
        public string SelectedHex { get; private set; } = "#00E5FF";
        private bool _isDraggingSpectrum = false;
        private float _currentHue = 180.0f;

        public ColorPickerWindow(string initialHex = "#00E5FF")
        {
            InitializeComponent();
            SelectedHex = initialHex;
            SetHexColor(initialHex);
        }

        private void SetHexColor(string hex)
        {
            try
            {
                var c = (Color)ColorConverter.ConvertFromString(hex);
                SelectedHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
                if (ColorPreviewFrame != null)
                    ColorPreviewFrame.Background = new SolidColorBrush(c);
                if (TxtSelectedHex != null)
                    TxtSelectedHex.Text = SelectedHex;
            }
            catch { }
        }

        private void SpectrumBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpectrum = true;
            SpectrumBar.CaptureMouse();
            UpdateHueFromMouse(e.GetPosition(SpectrumBar).X);
        }

        private void SpectrumBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingSpectrum)
            {
                UpdateHueFromMouse(e.GetPosition(SpectrumBar).X);
            }
        }

        private void SpectrumBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDraggingSpectrum = false;
            SpectrumBar.ReleaseMouseCapture();
        }

        private void UpdateHueFromMouse(double mouseX)
        {
            double width = SpectrumBar.ActualWidth;
            if (width <= 0) return;

            double ratio = Math.Clamp(mouseX / width, 0.0, 1.0);
            _currentHue = (float)(ratio * 360.0);
            UpdateColorFromHsv();
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateColorFromHsv();
        }

        private void UpdateColorFromHsv()
        {
            if (SliderVal == null || ColorPreviewFrame == null || TxtSelectedHex == null) return;

            float v = (float)(SliderVal.Value / 100.0);
            var color = HsvToRgb(_currentHue, 1.0f, v);

            SelectedHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            ColorPreviewFrame.Background = new SolidColorBrush(color);
            TxtSelectedHex.Text = SelectedHex;
        }

        private void BtnPresetColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hex)
            {
                SetHexColor(hex);
            }
        }

        public static Color HsvToRgb(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60.0f) % 2 - 1));
            float m = v - c;

            float r = 0, g = 0, b = 0;
            if (h < 60) { r = c; g = x; b = 0; }
            else if (h < 120) { r = x; g = c; b = 0; }
            else if (h < 180) { r = 0; g = c; b = x; }
            else if (h < 240) { r = 0; g = x; b = c; }
            else if (h < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            byte red = (byte)Math.Round((r + m) * 255);
            byte green = (byte)Math.Round((g + m) * 255);
            byte blue = (byte)Math.Round((b + m) * 255);

            return Color.FromRgb(red, green, blue);
        }

        private void BtnSelect_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
