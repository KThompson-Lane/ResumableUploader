namespace ResumableUploader.Client.Models;

public class UploadProgress
{
    public double PercentageComplete { get; set; } = 0d;
    public long BytesTransferred { get; set; } = 0;
}