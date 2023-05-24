// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using CommunityToolkit.Mvvm.DependencyInjection;
using Drastic.Media.Core.Services;
using Drastic.Services;
using Drastic.Whisper;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;
using Microsoft.Extensions.DependencyInjection;
using Sharprompt;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.Clear();
        Console.WriteLine("Drastic.PodcastWhisper");

        Ioc.Default.ConfigureServices(
               new ServiceCollection()
               .AddSingleton<IErrorHandlerService>(new BasicErrorHandler())
               .AddSingleton<IAppDispatcher>(new AppDispatcher())
               .AddTransient<IWhisperService, DefaultWhisperService>()
               .AddSingleton<ITranscodeService, FFMpegTranscodeService>()
               .AddSingleton<WhisperModelService>()
               .BuildServiceProvider());

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

        var subtitles = new List<SrtSubtitleLine>();
        var whisperService = Ioc.Default.GetRequiredService<IWhisperService>();
        whisperService.OnNewWhisperSegment += (s, segment) =>
        {
            var e = segment.Segment;
            Console.WriteLine($"CSSS {e.Start} ==> {e.End} : {e.Text}");
            var item = new SrtSubtitleLine() { Start = e.Start, End = e.End, Text = e.Text.Trim(), LineNumber = subtitles.Count() + 1 };
        };

        var srtPath = Path.Combine(WhisperStatic.DefaultPath, "srt");
        Directory.CreateDirectory(srtPath);

        whisperService.InitModel(model.FileLocation, languages[0]);

        var transcodeService = Ioc.Default.GetRequiredService<ITranscodeService>();

        var rssFeedUrl = Prompt.Input<string>("Enter RSS feed");
        var podcastService = new PodcastService();
        var result = await podcastService.FetchPodcastShowAsync(new Uri(rssFeedUrl), CancellationToken.None);
        Console.WriteLine(result!.Title);

        foreach (var item in result.Episodes)
        {
            Console.WriteLine(item.Title);
            subtitles.Clear();
            var srtFile = Path.Combine(srtPath, ConvertToValidFilename(item.Title ?? Path.GetTempFileName()) + ".srt");
            if (File.Exists(srtFile))
            {
                continue;
            }

            var audio = await transcodeService.ProcessFile(item.OnlinePath!.ToString());
            if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
            {
                continue;
            }

            await whisperService.ProcessAsync(audio, CancellationToken.None);

            if (subtitles.Any())
            {
                var subtitle = new SrtSubtitle();
                foreach (var sub in subtitles)
                {
                    subtitle.Lines.Add(sub);
                }

                await File.WriteAllTextAsync(srtFile, subtitle.ToString());
            }

            File.Delete(audio);
        }
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