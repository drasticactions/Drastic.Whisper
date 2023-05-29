// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using CommunityToolkit.Mvvm.DependencyInjection;
using Drastic.Media.Core.Model.Feeds;
using Drastic.Media.Core.Services;
using Drastic.Services;
using Drastic.Whisper;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;
using Microsoft.Extensions.DependencyInjection;
using Sharprompt;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Drastic.WhisperCLI;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("Drastic.WhisperCLI");
        var generatedFilename = "generated";
        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddSingleton<IErrorHandlerService>(new BasicErrorHandler())
                .AddSingleton<IAppDispatcher>(new AppDispatcher())
                .AddTransient<IWhisperService, DefaultWhisperService>()
                .AddSingleton<ITranscodeService>(new FFMpegTranscodeService(WhisperStatic.DefaultPath, generatedFilename))
                .AddSingleton<WhisperModelService>()
                .BuildServiceProvider());

        var driverType = string.Empty;

#if WHISPER_COREML
        driverType = "CoreML";
#else
        driverType = "Generic";
#endif

        var languages = WhisperLanguage.GenerateWhisperLangauages();
        var modelService = Ioc.Default.GetRequiredService<WhisperModelService>();
        var model = Prompt.Select("Select a model", modelService.AllModels, textSelector: (item) => item.Name);
        if (!model.Exists)
        {
            Console.WriteLine("Downloading Model...");
            var whisperDownloader = new WhisperDownload(model, modelService, Ioc.Default.GetRequiredService<IAppDispatcher>());
            whisperDownloader.DownloadService.DownloadProgressChanged += (s, e) =>
            {
                //progressBar.Update((int)e.ProgressPercentage);
            };
            await whisperDownloader.DownloadCommand.ExecuteAsync();
        }

        var whisperService = Ioc.Default.GetRequiredService<IWhisperService>();
        var subtitles = 0;

        var srtPath = Path.Combine(WhisperStatic.DefaultPath, "srt");
        Directory.CreateDirectory(srtPath);

        whisperService.InitModel(model.FileLocation, languages.First(n => n.LanguageCode == "en"));

        var transcodeService = Ioc.Default.GetRequiredService<ITranscodeService>();

        var filePath = Prompt.Input<string>("Enter File Path", defaultValue: Path.Combine(WhisperStatic.DefaultPath, "generated.wav"));

        var audio = await transcodeService.ProcessFile(filePath);
        if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
        {
            return;
        }

        var srtFile = Path.Combine(srtPath, ConvertToValidFilename(Path.GetFileNameWithoutExtension(filePath)) + $"_{model.Name}_{driverType}.srt");

        using StreamWriter writer = new StreamWriter(srtFile);
        using StreamWriter stopwatchWriter = new StreamWriter(Path.Combine(WhisperStatic.DefaultPath, "stopwatch.txt"), true);
        Stopwatch stopwatch = new Stopwatch();
        whisperService.OnNewWhisperSegment += (s, segment) =>
        {
            // Start when we get the first subtitle.
            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }

            subtitles = subtitles + 1;
            var e = segment.Segment;
            Console.WriteLine($"CSSS {e.Start} ==> {e.End} : {e.Text}");
            var item = new SrtSubtitleLine() { Start = e.Start, End = e.End, Text = e.Text.Trim(), LineNumber = subtitles };
            writer.WriteLine(item.ToString());
            writer.Flush();
        };

        await whisperService.ProcessAsync(audio, CancellationToken.None);
        stopwatch.Stop();

        Console.WriteLine(stopwatch.Elapsed);
        stopwatchWriter.WriteLine($"{srtFile}: {stopwatch.Elapsed}");
        stopwatchWriter.Flush();
    }

    static string ConvertToValidFilename(string input)
    {
        // Remove invalid characters
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidReStr = string.Format(@"[{0}]+", invalidChars);
        string sanitizedInput = Regex.Replace(input, invalidReStr, "_");

        // Trim leading/trailing whitespaces and dots
        sanitizedInput = sanitizedInput.Trim().Trim('.');

        // Ensure filename is not empty
        if (string.IsNullOrEmpty(sanitizedInput))
        {
            return "_";
        }

        // Ensure filename is not too long
        int maxFilenameLength = 255;
        if (sanitizedInput.Length > maxFilenameLength)
        {
            sanitizedInput = sanitizedInput.Substring(0, maxFilenameLength);
        }

        return sanitizedInput;
    }
}

internal class BasicErrorHandler : IErrorHandlerService
{
    public void HandleError(Exception ex)
    {
    }
}

internal class AppDispatcher : IAppDispatcher
{
    public bool Dispatch(Action action)
    {
        action.Invoke();
        return true;
    }
}