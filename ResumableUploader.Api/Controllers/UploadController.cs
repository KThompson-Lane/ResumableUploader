using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ResumableUploader.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UploadController : ControllerBase
{
    /// <summary>
    /// Initiates a file upload, generating a session URI to be used when uploading the file.
    /// </summary>
    /// <returns>A CreatedAt response, including the session URI as a location header</returns>
    [HttpPost]
    public async Task<IActionResult> Upload()
    {
        Console.Out.WriteLine("Received initiate upload request");
        
        //  Create guid and temporary file for uploading
        var sessionGuid = Guid.NewGuid();
        Directory.CreateDirectory("uploads");
        System.IO.File.Create($"uploads/{sessionGuid}.temp").Close();
        return CreatedAtAction(nameof(Upload), new {guid = sessionGuid}, "Initiated Upload");
    }

    /// <summary>
    /// Uploads a file, accepting both whole and chunked uploads
    /// </summary>
    /// <param name="guid"></param>
    /// <returns></returns>
    [HttpPut("{guid}")]
    public async Task<IActionResult> Upload(Guid guid)
    {
        Console.Out.WriteLine("Received upload request");
        
        //  Check content length
        var length = Request.ContentLength;

        //  Status request
        if (length == 0)
        {
            if (System.IO.File.Exists($"uploads/{guid}.zip"))
                return Ok("Upload complete");

            if (!System.IO.File.Exists($"uploads/{guid}.temp"))
                return NoContent();
            
            var fileSize = new FileInfo($"uploads/{guid}.temp").Length;
            if (fileSize == 0)
                return StatusCode(308, "Restart upload");
            
            Response.Headers.Range = new RangeHeaderValue(0, fileSize).ToString();
            return StatusCode(206, "Resume upload");   
        }
        
        //  Check guid is valid
        if (!System.IO.File.Exists($"uploads/{guid}.temp"))
            return NoContent();
        
        await using var file = new FileStream($"uploads/{guid}.temp", FileMode.Open);
        if (Request.Headers.ContentRange.Count != 0)
        {
            var range = ContentRangeHeaderValue.Parse(Request.Headers.ContentRange.ToString());
            file.Seek(range.From!.Value, SeekOrigin.Begin);
            await Request.Body.CopyToAsync(file);
            if (file.Position != range.Length) 
                return StatusCode(206, "Continue upload");
        }
        else
        {
            await Request.Body.CopyToAsync(file);
        }

        await file.FlushAsync();
        var size = file.Length;
        file.Close();
        System.IO.File.Move($"uploads/{guid}.temp", $"uploads/{guid}.zip");
        Console.Out.WriteLine($"Received file of size {size}");
        return Ok($"Upload complete");
    }

    [HttpDelete("{guid}")]
    public async Task<IActionResult> Cancel(Guid guid)
    {
        System.IO.File.Delete($"uploads/{guid}.temp");
        return NoContent();
    }
}