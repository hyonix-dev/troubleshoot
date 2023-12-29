
using HyonixNetworkTroubleshooter.Helpers;
using HyonixNetworkTroubleshooter.Models;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace HyonixNetworkTroubleshooter.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private int _progressValue = 0;
        public int ProgressValue
        {
            get => _progressValue;
            set => this.SetProperty(ref _progressValue, value);

        }

        private string _buttonText = "Start";
        public string ButtonText
        {
            get => _buttonText;
            set => SetProperty(ref _buttonText, value);
        }

        private IProgress<int> _progress;

        public ICommand Start
        {
            get
            {
                return new RelayCommand(
                async () =>
                {
                    if (ButtonText == "Start")
                    {
                        ButtonText = "Stop";
                        _ct = new CancellationTokenSource();
                        ProgressValue = 0;
                        _progress = new Progress<int>(percent =>
                        {
                            ProgressValue = percent;
                        });

                        _trModel = new TraceRouteModel(_progress, _ct.Token);
                        
                        try
                        {
                            await _trModel.Initialize();
                        }
                        catch (Exception ex)
                        {
                            StopCommand.Execute(null);
                            MessageBox.Show(ex.Message, "Network Error", MessageBoxButton.OK);
                            return;
                        }
                        
                        try
                        {
                            var result = await _trModel.Execute(_ct.Token);
                            if(result == null || result.Count==0)
                            {
                                throw new Exception();
                            }
                                if (_ct.IsCancellationRequested) return;
                            string report = GenerateReport(result);
                            await SaveReportBitmapAsync(report);
                            StopCommand.Execute(null);
                        }
                        catch
                        {
                            MessageBox.Show("An unknown error happened during the test. Please try again.");
                            StopCommand.Execute(null);
                        }
                        
                    }
                    else
                    {
                        // Execute stop logic
                        StopCommand.Execute(null);
                    }
                });

            }
        }
        private ICommand _stopCommand;
        public ICommand StopCommand
        {
            get
            {
                if (_stopCommand == null)
                {
                    _stopCommand = new RelayCommand(() =>
                    {
                        _ct.Cancel();
                        ProgressValue = 0;
                        ButtonText = "Start";
                    });
                }
                return _stopCommand;
            }
        }

        private TraceRouteModel _trModel;
        private CancellationTokenSource _ct;

        public MainViewModel()
        {            
           
        }

        public string GenerateReport(IDictionary<string, IEnumerable<TraceRouteResult>> tracerouteData)
        {
            var reportBuilder = new StringBuilder();
            reportBuilder.AppendLine($"DateTime: {DateTime.Now}");
            reportBuilder.AppendLine($"Public IP Address: {IPConfigHelper.GetPublicIPAddress(_trModel.PublicIPUrl)}");
            reportBuilder.AppendLine();
            //reportBuilder.AppendLine("ipconfig /all results:");
            //reportBuilder.AppendLine(IPConfigHelper.GetIpConfigOutput());
            foreach (var entry in tracerouteData)
            {
                reportBuilder.AppendLine($"Hostname: {entry.Key}");
                

                // Define column headers and widths
                // Define column headers and widths
                var headers = new StringBuilder();
                headers.Append("#".PadRight(3)).Append(" | ");
                headers.Append("IPAddress".PadRight(15)).Append(" | ");
                headers.Append("HostName".PadRight(75)).Append(" | ");
                headers.Append("Loss%".PadRight(10)).Append(" | ");
                headers.Append("Sent".PadRight(10)).Append(" | ");
                headers.Append("Received".PadRight(10)).Append(" | ");
                headers.Append("LastTime".PadRight(10)).Append(" | ");
                headers.Append("AvgTime".PadRight(10)).Append(" | ");
                headers.Append("BestTime".PadRight(10)).Append(" | ");
                headers.Append("WorstTime".PadRight(10));
                reportBuilder.AppendLine(headers.ToString());

                reportBuilder.AppendLine(new string('-', headers.Length));

                foreach (var result in entry.Value.OrderBy(x => x.RouteNumber))
                {
                    var row = new StringBuilder();
                    row.Append(result.RouteNumber.ToString().PadRight(3)).Append(" | ");
                    row.Append(result.IPAddress.ToString().PadRight(15)).Append(" | ");
                    row.Append(result.HostName.PadRight(75)).Append(" | ");
                    row.Append(result.Statistics.LossPercentage.ToString("F2", CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.Sent.ToString(CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.Received.ToString(CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.LastTime.ToString("F2", CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.AverageTime.ToString("F2", CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.BestTime.ToString("F2", CultureInfo.InvariantCulture).PadRight(10)).Append(" | ");
                    row.Append(result.Statistics.WorstTime.ToString("F2", CultureInfo.InvariantCulture).PadRight(10));


                    reportBuilder.AppendLine(row.ToString());
                }

                reportBuilder.AppendLine(); // Empty line for spacing between hosts
            }

            return reportBuilder.ToString();
        }

        public async Task SaveReportAsync(string reportContent)
        {
            var saveFileDialog = new SaveFileDialog
            {
                DefaultExt = "bmp",
                Filter = "Text file (*.txt)|*.txt|All files (*.*)|*.*",
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, reportContent);
            }
        }
        public async Task SaveReportBitmapAsync(string reportContent)
        {
            var bitmap = CreateImageFromMixedContent(reportContent, 1300);
            var saveFileDialog = new SaveFileDialog
            {
                DefaultExt = "jpg",
                Filter = "JPEG file (*.Jpeg)|*.jpg|All files (*.*)|*.*",
                AddExtension = true
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                SaveBitmapToJpegFile(bitmap, saveFileDialog.FileName, 70);
            }
        }

        public static Bitmap CreateImageFromMixedContent(string text, int imageWidth)
        {
            Font font = new Font("Consolas", 10); // Use a monospaced font
            Image dummyImg = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(dummyImg);

            // Define the widths of each column based on your format
            int[] columnWidths = { 3, 15, 75, 10, 10, 10, 10, 10, 10, 10 }; // Adjust as needed
            int[] columnPixelWidths = new int[columnWidths.Length];

            // Calculate pixel widths for each column
            for (int i = 0; i < columnWidths.Length; i++)
            {
                string sampleString = new string('W', columnWidths[i]); // Use a wide character for calculation
                columnPixelWidths[i] = (int)drawing.MeasureString(sampleString, font).Width;
            }

            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            // Calculate total image height
            int totalHeight = lines.Sum(line => (int)drawing.MeasureString(line, font, imageWidth).Height);

            dummyImg.Dispose();
            drawing.Dispose();

            Image img = new Bitmap(imageWidth, totalHeight);
            drawing = Graphics.FromImage(img);
            drawing.Clear(System.Drawing.Color.White);

            // Draw each line
            int currentHeight = 0;
            foreach (var line in lines)
            {
                if (line.Contains("|")) // Table line
                {
                    int currentWidth = 0;
                    string[] cells = line.Split('|');

                    for (int j = 0; j < cells.Length; j++)
                    {
                        drawing.DrawString(cells[j].Trim(), font, System.Drawing.Brushes.Black, new PointF(currentWidth, currentHeight));

                        if (j < columnPixelWidths.Length)
                        {
                            currentWidth += columnPixelWidths[j];
                        }
                    }
                }
                else // Non-table line
                {
                    drawing.DrawString(line, font, System.Drawing.Brushes.Black, new PointF(0, currentHeight));
                }

                currentHeight += (int)drawing.MeasureString(line, font, imageWidth).Height;
            }

            drawing.Save();
            drawing.Dispose();

            return (Bitmap)img;
        }


        public static Bitmap CreateImageFromText(string text, int maxWidth)
        {
            Font font = new Font("Arial", 10);
            Image dummyImg = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(dummyImg);

            // Calculate the size required for each line and the total image size
            string[] lines = text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            float maxWidthUsed = 0;
            float totalHeight = 0;
            foreach (var line in lines)
            {
                SizeF size = drawing.MeasureString(line, font, maxWidth);
                maxWidthUsed = Math.Max(size.Width, maxWidthUsed);
                totalHeight += size.Height;
            }

            dummyImg.Dispose();
            drawing.Dispose();

            Image img = new Bitmap((int)maxWidthUsed, (int)totalHeight);
            drawing = Graphics.FromImage(img);

            drawing.Clear(System.Drawing.Color.White);

            System.Drawing.Brush textBrush = new SolidBrush(System.Drawing.Color.Black);
            float currentHeight = 0;
            foreach (var line in lines)
            {
                SizeF size = drawing.MeasureString(line, font, maxWidth);
                drawing.DrawString(line, font, textBrush, new RectangleF(0, currentHeight, maxWidthUsed, size.Height));
                currentHeight += size.Height;
            }

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return (Bitmap)img;
        }

        /// <summary>
        /// Saves the given Bitmap to a file.
        /// </summary>
        /// <param name="bitmap">The Bitmap to save.</param>
        /// <param name="filePath">The file path where the image will be saved.</param>
        /// <param name="format">The image format to use when saving.</param>
        public static void SaveBitmapToJpegFile(Bitmap bitmap, string filePath, long quality)
        {
            // Ensure the quality is within the valid range (0-100)
            quality = Math.Max(0, Math.Min(100, quality));

            // Encoder parameter for image quality
            EncoderParameter qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

            // JPEG image codec
            ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

            EncoderParameters encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = qualityParam;

            bitmap.Save(filePath, jpegCodec, encoderParams);
        }
    }
}
