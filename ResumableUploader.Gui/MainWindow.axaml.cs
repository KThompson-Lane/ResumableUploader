using System;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ResumableUploader.Client.Lib;
using ResumableUploader.Client.Models;
using FileInfo = System.IO.FileInfo;

namespace ResumableUploader.Gui;

public partial class MainWindow : Window
{
    private readonly ResumableUploadClient _uploadClient;
    private CancellationTokenSource _cts = new();
    private IStorageFile? _chosenFile = null;
    public MainWindow()
    {
        InitializeComponent();
        _uploadClient = new ResumableUploadClient();
    }

    private async void ChooseFile(object source, RoutedEventArgs args)
    {
        var toplevel = TopLevel.GetTopLevel(this);
     
        var files = await toplevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            Title = "Upload file",
            AllowMultiple = false
        });

        if (files.Count >= 1)
        {
            _chosenFile = files[0];
        }
    }

    private void UpdateProgress(object? sender, UploadProgress prog)
    {
        UploadProgress.Value = prog.PercentageComplete;
        Console.Out.WriteLine($"Progress: {prog.PercentageComplete}");
    }

    private void PauseUpload_OnClick(object? sender, RoutedEventArgs e)
    {
        _cts.Cancel();
    }

    private async void ResumeUpload_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_chosenFile == null)
        {
            return;
        }
        var progress = new Progress<UploadProgress>();
        progress.ProgressChanged += UpdateProgress;
        _cts = new CancellationTokenSource();
        var fileInfo = new FileInfo(_chosenFile!.TryGetLocalPath()!);
        await Console.Out.WriteLineAsync($"Resuming {_chosenFile.Name}");
        try
        {
            await _uploadClient.ResumeUpload(fileInfo, progress, _cts.Token);
        }
        catch (OperationCanceledException exception)
        {
            Console.WriteLine("Cancelled!");
        }
    }

    private async void StartUpload_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_chosenFile == null)
        {
            return;
        }
        var progress = new Progress<UploadProgress>();
        progress.ProgressChanged += UpdateProgress;
        
        var fileInfo = new FileInfo(_chosenFile!.TryGetLocalPath()!);
        await Console.Out.WriteLineAsync($"Uploading {_chosenFile.Name} with size {fileInfo.Length}");
        try
        {
            await _uploadClient.InitiateUpload(fileInfo, progress, _cts.Token);
        }
        catch (OperationCanceledException exception)
        {
            Console.WriteLine("Cancelled!");
        }
        
    }
}