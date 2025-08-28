using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace gMat.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ConversionController : ControllerBase
    {
        // I changed the endpoint name to reflect that it creates an MP3.
        [HttpPost("convert-to-mp3")]
        public async Task<IActionResult> ConvertAudio(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }

            // Generate unique temporary file paths
            var inputFileName = Path.GetRandomFileName();
            var outputFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".mp3");

            var tempPath = Path.GetTempPath(); // e.g., "/tmp/" on Linux
            var inputPath = Path.Combine(tempPath, inputFileName);
            var outputPath = Path.Combine(tempPath, outputFileName);

            try
            {
                // BUG FIX #1: You MUST save the uploaded file to disk first.
                // This code is now active.
                using (var stream = new FileStream(inputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // BUG FIX #2: You MUST use the full paths in the arguments for ffmpeg.
                // Always wrap file paths in quotes to handle special characters.
                string arguments = $"-i \"{inputPath}\" -y \"{outputPath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg", // This is correct for your Docker container
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                // Capture errors for debugging
                string stdErr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    // If ffmpeg fails, log the error and return a server error
                    Console.WriteLine($"FFmpeg Exit Code: {process.ExitCode}");
                    Console.WriteLine($"FFmpeg Error: {stdErr}");
                    return StatusCode(500, $"An error occurred during conversion: {stdErr}");
                }

                // Read the converted file into a byte array
                var fileBytes = await System.IO.File.ReadAllBytesAsync(outputPath);

                // Return the file as a downloadable stream.
                // "converted.mp3" is the filename the client will see.
                return File(fileBytes, "audio/mpeg", "converted.mp3");
            }
            finally
            {
                // Clean up temporary files
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