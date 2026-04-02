using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace NixTraceability
{
    public partial class QrLocationWindow : Window
    {
        private bool isDrawing = false;
        private Point startPoint;
        public string ResultQrRect { get; private set; } = "";
        
        private double imageActualWidth;
        private double imageActualHeight;

        public QrLocationWindow(string imagePath, string existingQrRect)
        {
            InitializeComponent();
            LoadImage(imagePath);
            RestoreExistingRect(existingQrRect);
        }

        private void LoadImage(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(path, UriKind.Absolute);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    
                    imgPart.Source = bmp;
                    
                    // We bind canvas size to image size so drawing maps 1:1 with image source pixels
                    drawingGrid.Width = bmp.PixelWidth;
                    drawingGrid.Height = bmp.PixelHeight;
                    drawingCanvas.Width = bmp.PixelWidth;
                    drawingCanvas.Height = bmp.PixelHeight;
                    
                    imageActualWidth = bmp.PixelWidth;
                    imageActualHeight = bmp.PixelHeight;
                }
                else
                {
                    MessageBox.Show("Image could not be loaded or path is invalid.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("QrLocationWindow.LoadImage", ex);
                MessageBox.Show("Failed to open image.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void RestoreExistingRect(string rectConfig)
        {
            if (string.IsNullOrWhiteSpace(rectConfig)) return;
            
            try
            {
                string[] parts = rectConfig.Split(',');
                if (parts.Length == 4)
                {
                    double xRatio = double.Parse(parts[0], CultureInfo.InvariantCulture);
                    double yRatio = double.Parse(parts[1], CultureInfo.InvariantCulture);
                    double wRatio = double.Parse(parts[2], CultureInfo.InvariantCulture);
                    double hRatio = double.Parse(parts[3], CultureInfo.InvariantCulture);

                    double x = xRatio * imageActualWidth;
                    double y = yRatio * imageActualHeight;
                    double w = wRatio * imageActualWidth;
                    double h = hRatio * imageActualHeight;

                    Canvas.SetLeft(selectionRect, x);
                    Canvas.SetTop(selectionRect, y);
                    selectionRect.Width = w;
                    selectionRect.Height = h;
                    selectionRect.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("QrLocationWindow.RestoreExistingRect", ex);
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                isDrawing = true;
                startPoint = e.GetPosition(drawingCanvas);
                Canvas.SetLeft(selectionRect, startPoint.X);
                Canvas.SetTop(selectionRect, startPoint.Y);
                selectionRect.Width = 0;
                selectionRect.Height = 0;
                selectionRect.Visibility = Visibility.Visible;
                drawingGrid.CaptureMouse();
            }
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDrawing)
            {
                Point currentPoint = e.GetPosition(drawingCanvas);

                double x = Math.Min(currentPoint.X, startPoint.X);
                double y = Math.Min(currentPoint.Y, startPoint.Y);
                double w = Math.Abs(currentPoint.X - startPoint.X);
                double h = Math.Abs(currentPoint.Y - startPoint.Y);
                
                // Constrain within image bounds
                if (x < 0) x = 0;
                if (y < 0) y = 0;
                if (x + w > imageActualWidth) w = imageActualWidth - x;
                if (y + h > imageActualHeight) h = imageActualHeight - y;

                Canvas.SetLeft(selectionRect, x);
                Canvas.SetTop(selectionRect, y);
                selectionRect.Width = w;
                selectionRect.Height = h;
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDrawing)
            {
                isDrawing = false;
                drawingGrid.ReleaseMouseCapture();
                
                // If it's too small, consider it a mis-click and hide it
                if (selectionRect.Width < 10 || selectionRect.Height < 10)
                {
                    selectionRect.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            selectionRect.Visibility = Visibility.Collapsed;
            ResultQrRect = "";
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void btnSave_Click(object sender, RoutedEventArgs e)
        {
            if (selectionRect.Visibility == Visibility.Visible && imageActualWidth > 0 && imageActualHeight > 0)
            {
                double x = Canvas.GetLeft(selectionRect);
                double y = Canvas.GetTop(selectionRect);
                double w = selectionRect.Width;
                double h = selectionRect.Height;

                // Store relative to image size to avoid scaling resolution disparities
                double xRatio = x / imageActualWidth;
                double yRatio = y / imageActualHeight;
                double wRatio = w / imageActualWidth;
                double hRatio = h / imageActualHeight;

                ResultQrRect = $"{xRatio.ToString(CultureInfo.InvariantCulture)},{yRatio.ToString(CultureInfo.InvariantCulture)},{wRatio.ToString(CultureInfo.InvariantCulture)},{hRatio.ToString(CultureInfo.InvariantCulture)}";
            }
            else
            {
                ResultQrRect = "";
            }
            
            this.DialogResult = true;
            this.Close();
        }
    }
}
