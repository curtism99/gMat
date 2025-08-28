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
                    string tempMp3Path = null;
                    try
                    {
                        // 1. Get the local path to the converted .ogg file
                        tempMp3Path = await GetConvertedOggFilePathAsync(selectedFile.FullPath);

                        if (string.IsNullOrEmpty(tempMp3Path))
                        {
                            Console.WriteLine("Could not prepare the audio file for transcription.");
                            return;
                        }

                        fileToUpload = new FileResult(tempMp3Path);
                    }
                    catch (Exception ex)
                    {
                        // Handle any errors during transcription
                        Console.WriteLine($"An error occurred during transcription: {ex.Message}");
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

        public async Task<byte[]> ConvertAudioFileAsync(string localFilePath)
        {
            // Replace with your actual API's URL when deployed.
            // For local development with Android, you must use 10.0.2.2 to access your PC's localhost.
            // NEW URL - CORRECT FOR LOCAL ANDROID EMULATOR DEVELOPMENT
            //string apiUrl = "http://10.0.2.2:5129/api/conversion/convert-to-ogg"; // Use your HTTP port
                                                                                  // Then in your method:
            string baseUrl = GetApiBaseUrl();
            string apiUrl = $"{baseUrl}/api/Conversion/convert-to-mp3";

            // On iOS simulator, you can use localhost:
            // string apiUrl = "http://localhost:5000/api/conversion/convert-to-ogg";

            // HttpClient logic to upload the file and get the byte[] response...
            // This is the method from the previous answer.
            // For brevity, I'm not repeating its full code here.

            // Placeholder for the actual implementation
            using (var httpClient = new HttpClient())
            using (var multipartFormContent = new MultipartFormDataContent())
            {
                var fileStream = File.OpenRead(localFilePath);
                var streamContent = new StreamContent(fileStream);
                multipartFormContent.Add(streamContent, "file", Path.GetFileName(localFilePath));
                
                try
                {
                    var response = await httpClient.PostAsync(apiUrl, multipartFormContent);
                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsByteArrayAsync();
                    }
                    else
                    {
                        Console.WriteLine($"API Error: {response.ReasonPhrase}");
                        return null;
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error uploading file: {ex.Message}");
                    TranscriptionOutputLabel.Text = $"Error uploading file: {ex.Message}";
                    return null;
                }
            }
        }

        /// <summary>
        /// Uploads an audio file, converts it via API, saves the result locally,
        /// and returns the path to the temporary .ogg file.
        /// </summary>
        /// <param name="originalAacPath">The path to the source .aac file on the device.</param>
        /// <returns>The local file path to the temporary .ogg file, or null if an error occurs.</returns>
        public async Task<string> GetConvertedOggFilePathAsync(string originalAacPath)
        {
            // 1. Call the API to get the converted .ogg file as a byte array
            byte[] oggData = await ConvertAudioFileAsync(originalAacPath);

            if (oggData == null || oggData.Length == 0)
            {
                TranscriptionOutputLabel.Text = "Failed to get converted audio data from the API.";
                Console.WriteLine("Failed to get converted audio data from the API.");
                return null;
            }

            try
            {
                // 2. Define a path for the temporary file in the app's cache directory.
                // FileSystem.CacheDirectory is the correct cross-platform way to get this location.
                string tempFileName = $"{Guid.NewGuid()}.mp3";
                string tempFilePath = Path.Combine(FileSystem.CacheDirectory, tempFileName);

                // 3. Write the byte array to the temporary file
                await File.WriteAllBytesAsync(tempFilePath, oggData);

                Console.WriteLine($"Temporary .mp3 file saved at: {tempFilePath}");

                // 4. Return the path to the newly created file
                return tempFilePath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving temporary file: {ex.Message}");
                return null;
            }
        }

        public string GetApiBaseUrl()
        {
            return "https://gmat-e8ccfbhbegebg9fd.centralus-01.azurewebsites.net";

            // Android Emulator listens on 10.0.2.2 for the host machine
            if (DeviceInfo.Platform == DevicePlatform.Android)
                return "http://10.0.2.2:8080"; // Use your HTTP port

            // iOS Simulator can connect to localhost
            if (DeviceInfo.Platform == DevicePlatform.iOS && DeviceInfo.DeviceType == DeviceType.Virtual)
                return "http://localhost:5129";

            // For physical devices, you'd need your machine's local IP, e.g., "http://192.168.1.100:5129"
            // This is a more complex scenario requiring you to fetch the IP dynamically or configure it.
            // For now, we'll default to localhost for other platforms like Windows.

            return "https://gmat-e8ccfbhbegebg9fd.centralus-01.azurewebsites.net";

        }



    }
}

public record GroqResponse(string text);