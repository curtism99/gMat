using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;

namespace gMat
{
    public partial class MainPage : ContentPage
    {
        private readonly IConfiguration _configuration;
        private readonly string _groqApiKey;
        private readonly string _transcriptionBaseUrl;

        private FileResult selectedFile;

        public MainPage(IConfiguration configuration)
        {
            InitializeComponent();
            _configuration = configuration;

            // Access the setting using "SectionName:SettingName"
            _groqApiKey = _configuration["Settings:ApiKey"];
            _transcriptionBaseUrl = _configuration["Settings:ApiBaseUrl"];
        }


        private async void SelectFileButton_Clicked(object sender, EventArgs e)
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                { DevicePlatform.Android, new[] { "audio/*" } },
                { DevicePlatform.WinUI, new[] { ".mp3", ".m4a", ".wav", ".aac" } },
                { DevicePlatform.iOS, new[] { "public.audio" } }
                });

            var pickOptions = new PickOptions
            {
                PickerTitle = "Please select an audio file",
                FileTypes = customFileType,
            };

            try
            {
                var result = await FilePicker.Default.PickAsync(pickOptions);
                if (result != null)
                {
                    selectedFile = result;
                    TranscriptionOutputLabel.Text = $"Selected file: {result.FileName}";
                    TranscribeButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                TranscriptionOutputLabel.Text = $"An error occurred: {ex.Message}";
            }
        }

        private async void TranscribeButton_Clicked(object sender, EventArgs e)
        {
            if (selectedFile == null)
            {
                TranscriptionOutputLabel.Text = "Please select a file first.";
                return;
            }

            LoadingSpinner.IsVisible = true;
            LoadingSpinner.IsRunning = true;
            TranscribeButton.IsEnabled = false;
            SelectFileButton.IsEnabled = false;
            TranscriptionOutputLabel.Text = "Preparing to upload...";

            string tempFileFullPath = null;
            FileResult fileToUpload = selectedFile;

            try
            {
                if (selectedFile.FileName.EndsWith(".aac", StringComparison.OrdinalIgnoreCase))
                {
                    TranscriptionOutputLabel.Text = "Converting .aac to .mp3...";
                    tempFileFullPath = Path.Combine(FileSystem.CacheDirectory, $"temp_{Guid.NewGuid()}.mp3");
                    string command = $"-i \"{selectedFile.FullPath}\" -b:a 192k \"{tempFileFullPath}\"";

                    // The API call itself is the same, thanks to the wrapper's design
                    //var session = await FFmpegKit.ExecuteAsync(command);
                    //var returnCode = await session.GetReturnCodeAsync();

                    if (false)
                    {
                        fileToUpload = new FileResult(tempFileFullPath, "audio/mpeg");
                        TranscriptionOutputLabel.Text = "Conversion successful. Uploading...";
                    }
                    else
                    {
                        //var logs = await session.GetAllLogsAsStringAsync();
                        //throw new Exception($"FFmpeg conversion failed. Logs: {logs}");
                    }
                }

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _groqApiKey);
                using var formData = new MultipartFormDataContent();
                using var fileStream = await fileToUpload.OpenReadAsync();
                using var streamContent = new StreamContent(fileStream);

                streamContent.Headers.ContentType = new MediaTypeHeaderValue(fileToUpload.ContentType ?? "audio/mpeg");
                formData.Add(streamContent, "file", fileToUpload.FileName);
                formData.Add(new StringContent("whisper-large-v3"), "model");
                var response = await client.PostAsync(_transcriptionBaseUrl, formData);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var transcriptionResult = JsonSerializer.Deserialize<GroqResponse>(jsonResponse);
                    TranscriptionOutputLabel.Text = transcriptionResult?.text;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    TranscriptionOutputLabel.Text = $"API Error: {response.ReasonPhrase}\n{errorContent}";
                }
            }
            catch (Exception ex)
            {
                TranscriptionOutputLabel.Text = $"A critical error occurred: {ex.Message}";
            }
            finally
            {
                if (tempFileFullPath != null && File.Exists(tempFileFullPath))
                {
                    File.Delete(tempFileFullPath);
                    Console.WriteLine("Temporary file cleaned up.");
                }
                LoadingSpinner.IsVisible = false;
                LoadingSpinner.IsRunning = false;
                TranscribeButton.IsEnabled = true;
                SelectFileButton.IsEnabled = true;
            }
        }
    }

}

public record GroqResponse(string text);