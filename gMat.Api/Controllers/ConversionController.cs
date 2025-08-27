using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace gMat.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversionController : ControllerBase
    {
        [HttpPost("convert-to-ogg")]
        public async Task<IActionResult> ConvertToOgg(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // Generate unique temporary file paths
            var inputFileName = Path.GetRandomFileName();
            //var outputFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".ogg");
            var outputFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".mp3");

            var tempPath = Path.GetTempPath();
            var inputPath = Path.Combine(tempPath, inputFileName);
            var outputPath = Path.Combine(tempPath, outputFileName);

            try
            {
                //// 1. Save the uploaded file to a temporary location
                //using (var stream = new FileStream(inputPath, FileMode.Create))
                //{
                //    await file.CopyToAsync(stream);
                //}

                //// Get the absolute path to the directory where your application's DLLs are located.
                //string exePath = AppContext.BaseDirectory;
                //string ffmpegExecutableName = "ffmpeg.exe"; // Default to Windows

                //// Check if we are running on Linux and change the executable name accordingly
                //if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                //{
                //    ffmpegExecutableName = "ffmpeg";
                //}

                //// Combine it with the relative path to your ffmpeg executable.
                //string ffmpegPath = Path.Combine(exePath, "ffmpeg", ffmpegExecutableName);

                //var processStartInfo = new ProcessStartInfo
                //{
                //    FileName = ffmpegPath, // Use the full, calculated path
                //    Arguments = $"-i \"{inputPath}\" -y \"{outputPath}\"",
                //    RedirectStandardOutput = true,
                //    RedirectStandardError = true,
                //    UseShellExecute = false,
                //    CreateNoWindow = true,
                //};

                //using (var process = Process.Start(processStartInfo))
                //{
                //    // Read error output for debugging
                //    string error = await process.StandardError.ReadToEndAsync();
                //    await process.WaitForExitAsync();

                //    if (process.ExitCode != 0)
                //    {
                //        // Log the error and return a server error
                //        Console.WriteLine($"FFmpeg error: {error}");
                //        return StatusCode(500, $"An error occurred during conversion: {error}");
                //    }
                //}


                // Always wrap file paths in quotes to handle spaces!
                string arguments = $"-i \"{inputFileName}\" \"{outputFileName}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg",          // since ffmpeg is installed in PATH
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Capture output/errors for debugging
                string stdOut = await process.StandardOutput.ReadToEndAsync();
                string stdErr = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();
                Console.WriteLine($"Exit Code: {process.ExitCode}");
                Console.WriteLine(stdErr);


                // 3. Read the converted file into a byte array
                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputPath);

                // 4. Return the file as a downloadable stream
                // "converted.ogg" is the filename the client will see
                return File(fileBytes, "audio/mp3", outputPath);
            }
            finally
            {
                // 5. Clean up temporary files
                if (System.IO.File.Exists(inputPath))
                {
                    System.IO.File.Delete(inputPath);
                }
                if (System.IO.File.Exists(outputPath))
                {
                    System.IO.File.Delete(outputPath);
                }
            }
        }
    }
}
