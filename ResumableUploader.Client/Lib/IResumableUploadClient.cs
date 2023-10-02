using ResumableUploader.Client.Models;
using FileInfo = System.IO.FileInfo;

namespace ResumableUploader.Client.Lib;

public interface IResumableUploadClient
{
    Task InitiateUpload(FileInfo file, IProgress<UploadProgress> progress, CancellationToken ct);
    Task ResumeUpload(FileInfo file, IProgress<UploadProgress> progress, CancellationToken ct);
}