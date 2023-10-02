using System.Net;
using System.Net.Http.Headers;
using ResumableUploader.Client.Models;
using FileInfo = System.IO.FileInfo;

namespace ResumableUploader.Client.Lib;

public class ResumableUploadClient : IResumableUploadClient
{
    private const int BUFFER_SIZE = 8388608;
    private readonly byte[] buffer;
    private readonly HttpClient _uploadClient;
    
    private string? _sessionUri = null;
    private long _fileSize;
    private UploadProgress? _currentProgress = null;
    public ResumableUploadClient(HttpClient? httpClient = null)
    {
        _uploadClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5214/")
        };
        buffer = new byte[BUFFER_SIZE];
    }


    public async Task InitiateUpload(FileInfo file, IProgress<UploadProgress> progress, CancellationToken ct)
    {
        var response = await _uploadClient.PostAsync("/Upload", null, ct);
        _sessionUri = response.Headers.Location?.PathAndQuery;
        _fileSize = file.Length;
        await using var fs = file.OpenRead();
        try
        {
            await Upload(fs, progress, ct);
        }
        catch (OperationCanceledException)
        {
            fs.Close();
            throw;
        }
       
    }

    private async Task Upload(Stream fs, IProgress<UploadProgress> progress, CancellationToken ct)
    {
        _currentProgress??= new UploadProgress();
        do
        {
            //  Cancel if requested
            ct.ThrowIfCancellationRequested();

            //  Read chunk into buffer
            var currentByte = fs.Position;
            var chunkSize = await fs.ReadAsync(buffer, ct);

            //  Create API Request
            var req = new ByteArrayContent(buffer, 0, chunkSize);
            req.Headers.ContentRange = new ContentRangeHeaderValue(currentByte, fs.Position-1, _fileSize);
            var response = await _uploadClient.PutAsync(_sessionUri, req, ct);
            response.EnsureSuccessStatusCode();
                
            //  Update progress
            _currentProgress!.BytesTransferred += chunkSize;
            _currentProgress.PercentageComplete = (double)_currentProgress.BytesTransferred / _fileSize;
            progress.Report(_currentProgress);

        } while (_currentProgress.BytesTransferred != _fileSize);
    }

    public async Task ResumeUpload(FileInfo file, IProgress<UploadProgress> progress, CancellationToken ct)
    {
        //  Check upload status
        if (_sessionUri == null) 
            throw new OperationCanceledException();

        _fileSize = file.Length;
        
        var response = await _uploadClient.PutAsync(_sessionUri, null, ct);
        if (response.StatusCode != HttpStatusCode.PartialContent)
            throw new OperationCanceledException();
        
        await using var fs = file.OpenRead();
        
        //  Get progress
        var range = response.Headers.First(x => x.Key == "Range").Value.First();
        var parsedRange = RangeHeaderValue.Parse(range).Ranges.First();
        _currentProgress = new UploadProgress
        {
            BytesTransferred = parsedRange.To ?? 0,
            PercentageComplete = (double)parsedRange.To! / _fileSize
        };
        fs.Seek(parsedRange.To ?? 0, SeekOrigin.Begin);
        
        
        //  Resume upload
        try
        {
            await Upload(fs, progress, ct);
        }
        catch (OperationCanceledException)
        {
            fs.Close();
            throw;
        }
    }
}