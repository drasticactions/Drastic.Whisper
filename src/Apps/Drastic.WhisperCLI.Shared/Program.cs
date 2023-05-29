// <copyright file="Program.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.NamingConventionBinder;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.DependencyInjection;
using Drastic.Media.Core.Model.Feeds;
using Drastic.Media.Core.Services;
using Drastic.Services;
using Drastic.Whisper;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;
using Microsoft.Extensions.DependencyInjection;
using Sharprompt;

namespace Drastic.WhisperCLI;

internal class Program
{
    internal static MainProgram? program;

    private static async Task<int> Main(string[] args)
    {
        program = new MainProgram(args);
        return await program.RunAsync();
    }
}

internal class MainProgram
{
    private RootCommand root;
    private WhisperModelService modelService;
    private ITranscodeService transcodeService;
    private IWhisperService whisperService;
    private string[] args;
    private CancellationTokenSource cts;
    private string driverType;

    public MainProgram(string[] args)
    {
#if WHISPER_COREML
        driverType = "CoreML";
#else
        driverType = "Generic";
#endif

        this.cts = new CancellationTokenSource();
        this.args = args;
        var generatedFilename = "generated";
        Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddSingleton<IErrorHandlerService>(new BasicErrorHandler())
                .AddSingleton<IAppDispatcher>(new AppDispatcher())
                .AddTransient<IWhisperService, DefaultWhisperService>()
                .AddSingleton<ITranscodeService>(new FFMpegTranscodeService(WhisperStatic.DefaultPath,
                    generatedFilename))
                .AddSingleton<WhisperModelService>()
                .BuildServiceProvider());

        this.modelService = Ioc.Default.GetRequiredService<WhisperModelService>();
        this.transcodeService = Ioc.Default.GetRequiredService<ITranscodeService>();
        this.whisperService = Ioc.Default.GetRequiredService<IWhisperService>();

        var localLanguageOption = new Option<string>("--language", "Whisper Language Code");
        var localModelOption = new Option<string>("--model", "Whisper GGML Model");
        var filesCommand = new Command("files")
        {
            Handler = CommandHandler.Create(LocalFiles),
        };

        filesCommand.Add(localModelOption);
        filesCommand.Add(localLanguageOption);
        filesCommand.Add(new Option<List<string>>("--file", "Local file"));
        this.root = new RootCommand
        {
            filesCommand,
        };

        // Set the default command handler
        this.root.Handler = CommandHandler.Create(LocalFiles);
    }

    public async Task LocalFiles(string model, string languageCode, List<string> file)
    {
        model = await this.GetModelPrompt(model);
        var language = this.GetLanguagePrompt(languageCode);
        var files = this.GetFilesPrompt(file).Where(n => File.Exists(n));

        foreach (var f in files)
        {
            await this.RunModelAsync(model, language, f);
        }
    }

    public async Task<int> RunAsync()
    {
        return await this.root.InvokeAsync(this.args);
    }

    private async Task RunModelAsync(string modelPath, WhisperLanguage language, string filePath)
    {
        if (!File.Exists(modelPath))
        {
            throw new ArgumentNullException("Model does not exist");
        }

        if (!File.Exists(filePath))
        {
            throw new ArgumentNullException("File does not exist");
        }

        var srtPath = Path.Combine(WhisperStatic.DefaultPath, "srt");
        Directory.CreateDirectory(srtPath);

        if (!this.whisperService.IsInitialized)
        {
            this.whisperService.InitModel(modelPath, language);
        }

        var audio = await this.transcodeService.ProcessFile(filePath);
        if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
        {
            throw new ArgumentNullException($"Could not generate audio file: {filePath}");
        }

        var srtFile = Path.Combine(srtPath,
            ConvertToValidFilename(Path.GetFileNameWithoutExtension(filePath)) +
            $"_{Path.GetFileNameWithoutExtension(modelPath)}_{driverType}.srt");

        using StreamWriter writer = new StreamWriter(srtFile);
        using StreamWriter stopwatchWriter =
            new StreamWriter(Path.Combine(WhisperStatic.DefaultPath, "stopwatch.txt"), true);

        Stopwatch stopwatch = new Stopwatch();
        var subtitles = 0;

        void WhisperServiceOnOnNewWhisperSegment(object? sender, OnNewSegmentEventArgs segment)
        {
            // Start when we get the first subtitle.
            if (!stopwatch.IsRunning)
            {
                stopwatch.Start();
            }

            subtitles = subtitles + 1;
            var e = segment.Segment;
            Console.WriteLine($"CSSS {e.Start} ==> {e.End} : {e.Text}");
            var item = new SrtSubtitleLine()
                { Start = e.Start, End = e.End, Text = e.Text.Trim(), LineNumber = subtitles };
            writer.WriteLine(item.ToString());
            writer.Flush();
        }

        this.whisperService.OnNewWhisperSegment += WhisperServiceOnOnNewWhisperSegment;
        await this.whisperService.ProcessAsync(audio, CancellationToken.None);
        this.whisperService.OnNewWhisperSegment -= WhisperServiceOnOnNewWhisperSegment;
        stopwatch.Stop();
        Console.WriteLine(stopwatch.Elapsed);
        stopwatchWriter.WriteLine($"{srtFile}: {stopwatch.Elapsed}");
        stopwatchWriter.Flush();
    }

    private List<string> GetFilesPrompt(List<string> existingFiles)
    {
        existingFiles = existingFiles.Where(n => File.Exists(n)).ToList();
        if (existingFiles.Any())
        {
            return existingFiles;
        }

        var file = Prompt.Input<string>("Enter File Path",
            defaultValue: Path.Combine(WhisperStatic.DefaultPath, "generated.wav"));
        existingFiles.Add(file);

        return existingFiles;
    }

    private WhisperLanguage GetLanguagePrompt(string languageCode = "")
    {
        var languages = WhisperLanguage.GenerateWhisperLangauages();
        var language = languages.FirstOrDefault(n => n.LanguageCode == languageCode)
                       ?? Prompt.Select("Select a Language", languages, textSelector: (item) => item.Language);
        return language;
    }

    private async Task<string> GetModelPrompt(string? modelPath = "")
    {
        if (File.Exists(modelPath))
        {
            return modelPath;
        }

        var modelService = Ioc.Default.GetRequiredService<WhisperModelService>();
        var model = Prompt.Select("Select a model", modelService.AllModels, textSelector: (item) => item.Name);
        if (!model.Exists)
        {
            Console.WriteLine("Downloading Model...");
            var whisperDownloader =
                new WhisperDownload(model, modelService, Ioc.Default.GetRequiredService<IAppDispatcher>());
            whisperDownloader.DownloadService.DownloadProgressChanged += (s, e) =>
            {
                //progressBar.Update((int)e.ProgressPercentage);
            };
            await whisperDownloader.DownloadCommand.ExecuteAsync();
        }

        return model.FileLocation;
    }

    private string ConvertToValidFilename(string input)
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