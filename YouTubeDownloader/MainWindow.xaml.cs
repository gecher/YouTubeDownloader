using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;

namespace YouTubeDownloader
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Browse button click handler
        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a folder to save MP3 files";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    txtDownloadFolder.Text = dialog.SelectedPath;
                }
            }
        }

        // Download button click handler
        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            btnDownload.IsEnabled = false;
            progressBar.IsIndeterminate = true;
            lblStatus.Text = "Status: Downloading...";

            string mixUrl = txtPlaylistUrl.Text;
            // Set path to yt-dlp.exe in the Tools folder relative to the project directory
            string ytDlpPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\Tools\yt-dlp.exe");

            // Get the full path for debugging
            ytDlpPath = System.IO.Path.GetFullPath(ytDlpPath);

            // Log the ytDlpPath for debugging
            Console.WriteLine($"ytDlpPath: {ytDlpPath}"); // For debugging purposes

            // Check if the file exists before attempting to run it
            if (!File.Exists(ytDlpPath))
            {
                System.Windows.MessageBox.Show("yt-dlp.exe not found. Please ensure it is in the Tools folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string outputPath = txtDownloadFolder.Text; // Get the selected folder path

            if (string.IsNullOrEmpty(mixUrl))
            {
                System.Windows.MessageBox.Show("Please enter a valid YouTube Mix/Playlist URL.");
                btnDownload.IsEnabled = true;
                return;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                System.Windows.MessageBox.Show("Please select a download folder.");
                btnDownload.IsEnabled = true;
                return;
            }

            string arguments = $"-x --audio-format mp3 --yes-playlist --no-check-certificate -o \"{outputPath}\\%(title)s.%(ext)s\" {mixUrl}";

            await Task.Run(() => DownloadMP3(ytDlpPath, arguments));

            progressBar.IsIndeterminate = false;
            btnDownload.IsEnabled = true;
        }

        // Resets the form for a new download session
        private void ResetForm()
        {
            txtPlaylistUrl.Clear();
            txtDownloadFolder.Clear();
            progressBar.Value = 0;
            lstLog.Items.Clear();
            lblStatus.Text = "Status: Idle";
        }

        // Method to download the MP3 using yt-dlp
        private void DownloadMP3(string ytDlpPath, string arguments)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = ytDlpPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using (Process process = Process.Start(processInfo))
                {
                    process.OutputDataReceived += (sender, data) => HandleOutput(data.Data);
                    process.ErrorDataReceived += (sender, data) => AppendLog(data.Data);

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();
                }

                // Log download completion
                Dispatcher.Invoke(() =>
                {
                    lblStatus.Text = "Status: Download Complete.";
                    AppendLog("Download complete.");
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendLog($"An error occurred: {ex.Message}"));
            }
        }

        // Handle the output to extract filename and download progress
        // Handle the output to extract filename and download progress
        private void HandleOutput(string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                Dispatcher.Invoke(() =>
                {
                    // Regex to match file download progress (for yt-dlp)
                    var progressMatch = Regex.Match(message, @"\[download\]\s+(\d+\.\d+)%\s+of\s+\~?([\d\.]+\w+)\s+at\s+\~?([\d\.]+\w+)");

                    if (progressMatch.Success)
                    {
                        string progress = progressMatch.Groups[1].Value;
                        lblStatus.Text = $"Status: Downloading... {progress}% complete";
                        progressBar.Value = Convert.ToDouble(progress);
                    }
                    else
                    {
                        // Regex to match downloaded filename
                        var completedFileMatch = Regex.Match(message, @"\[download\]\s+Destination:\s+(.*)");

                        if (completedFileMatch.Success)
                        {
                            string filename = System.IO.Path.GetFileName(completedFileMatch.Groups[1].Value);
                            lblStatus.Text = $"Status: Download complete: {filename}";

                            // Append to log that the file has been downloaded
                            AppendLog($"Downloaded: {filename}");
                        }
                    }
                });
            }
        }

        // Appends log messages to the log listbox
        private void AppendLog(string message)
        {
            // Filter out errors and warnings from log
            if (!string.IsNullOrWhiteSpace(message) && !message.Contains("ERROR") && !message.Contains("WARNING"))
            {
                Dispatcher.Invoke(() =>
                {
                    lstLog.Items.Add(message);
                    lstLog.ScrollIntoView(lstLog.Items[lstLog.Items.Count - 1]);
                });
            }
        }
    }
}
