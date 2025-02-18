using Microsoft.AspNetCore.Mvc;
using FileStorage.Services;

namespace FileStorage.Controllers;

[ApiController]
[Route("[controller]")]
public class FileStorageController : ControllerBase
{
    private readonly ILogger<FileStorageController> _logger;

    public FileStorageController(ILogger<FileStorageController> logger)
    {
        _logger = logger;
    }

    [RequestSizeLimit((2L * 1024 + 100) * 1024 * 1024)]
    [HttpPost]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }

        using (var fileStream = file.OpenReadStream())
        {
            long fileSize = file.Length;
            string fileName = file.FileName;

            try
            {
                await FileProcessing.UploadAsync(fileStream, fileSize, fileName);
                return Ok();
            }
            catch (Exception e)
            {
                _logger.LogError($"{e}");
                return StatusCode(500, "File upload failed.");
            }
        }
    }
}
