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
using Drastic.Media.Core.Model;
using Drastic.Media.Core.Model.Feeds;
using Drastic.Media.Core.Services;
using Drastic.Services;
using Drastic.Whisper;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
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

        var podcastCommand = new Command("podcast")
        {
            Handler = CommandHandler.Create(Podcast),
        };

        podcastCommand.Add(localModelOption);
        podcastCommand.Add(localLanguageOption);
        podcastCommand.Add(new Option<string>("--rss", "Podcast RSS URL"));

        var benchmarkCommand = new Command("benchmark")
        {
            Handler = CommandHandler.Create(Benchmark)
        };

        benchmarkCommand.Add(new Option<string>("--modelFolder", "Whisper GGML Model Folder"));
        benchmarkCommand.Add(localLanguageOption);

        var transcript = new Command("transcript")
        {
            Handler = CommandHandler.Create(Transcript),
        };

        transcript.Add(localModelOption);
        transcript.Add(localLanguageOption);
        this.root = new RootCommand
        {
            filesCommand,
            podcastCommand,
            benchmarkCommand,
            transcript,
        };
    }

    const int audioSampleLengthS = 1;
    const int audioSampleLengthMs = audioSampleLengthS * 1000;
    const int totalBufferLength = 30 / audioSampleLengthS;

    private async Task Transcript(string model, string languageCode)
    {
        model = await this.GetModelPrompt(model);
        var language = this.GetLanguagePrompt(languageCode);
        List<float[]> slidingBuffer = new(totalBufferLength + 1);
        var devices = new List<AudioDevices>();
#if WINDOWS
        for (int i = 0; i < WaveIn.DeviceCount; i++)
        {
            var capabilities = WaveIn.GetCapabilities(i);
            devices.Add(new AudioDevices() { DeviceNumber = i, DeviceName = capabilities.ProductName });
        }
#endif
        if (!devices.Any())
        {
            return;
        }
        var audioDevice = Prompt.Select("Select Audio Device", devices.Select(n => n.DeviceName));
        var selectedDevice = devices.First(n => n.DeviceName == audioDevice)!;
        var factory = global::Whisper.net.WhisperFactory.FromPath(model);
        var processor = factory.CreateBuilder()
            .WithLanguage("auto")
            .WithSegmentEventHandler((e) =>
            {
                Console.WriteLine($"Segment: {e.Text}");
            })
            .Build();

        WaveInEvent waveIn = new()
        {
            DeviceNumber = selectedDevice.DeviceNumber, // indicates which microphone to use
            WaveFormat = new WaveFormat(rate: 16000, bits: 16, channels: 1), // must be supported by the microphone
            BufferMilliseconds = audioSampleLengthMs,
        };

        waveIn.DataAvailable += WaveInDataAvailable;
        waveIn.StartRecording();

        void WaveInDataAvailable(object? sender, WaveInEventArgs e)
        {
            var values = new short[e.Buffer.Length / 2];
            Buffer.BlockCopy(e.Buffer, 0, values, 0, e.Buffer.Length);
            var samples = values.Select(x => x / (short.MaxValue + 1f)).ToArray();

            var silenceCount = samples.Count(x => IsSilence(x, -40));

            if (silenceCount < values.Length - values.Length / 12)
            {
                slidingBuffer.Add(samples);

                if (slidingBuffer.Count > totalBufferLength)
                {
                    slidingBuffer.RemoveAt(0);
                }

                processor.Process(slidingBuffer.SelectMany(x => x).ToArray());
            }
        }

        Console.WriteLine("Press any key to quit.");
        Console.ReadLine();
    }

    static bool IsSilence(float amplitude, sbyte threshold)
    => GetDecibelsFromAmplitude(amplitude) < threshold;

    static double GetDecibelsFromAmplitude(float amplitude)
        => 20 * Math.Log10(Math.Abs(amplitude));


    private class AudioDevices
    {
        public int DeviceNumber { get; set; }

        public string DeviceName { get; set; } = string.Empty;
    }

    private async Task Podcast(string model, string languageCode, string rss)
    {
        model = await this.GetModelPrompt(model);
        var language = this.GetLanguagePrompt(languageCode);
        if (string.IsNullOrEmpty(rss))
        {
            rss = Prompt.Input<string>("Enter RSS feed");
        }

        var podcast = await this.GetPodcastAsync(rss);
        Console.WriteLine(podcast!.Title);

        foreach (var item in podcast.Episodes)
        {
            Console.WriteLine(item.Title);
            await this.RunModelAsync(
                model,
                language,
                item.OnlinePath?.ToString() ?? throw new NullReferenceException(nameof(item.OnlinePath)),
                item.Title);
        }
    }

    private async Task<PodcastShowItem> GetPodcastAsync(string rssFeedUrl)
    {
        var podcastService = new PodcastService();
        return await podcastService.FetchPodcastShowAsync(new Uri(rssFeedUrl), this.cts.Token) ??
               throw new NullReferenceException(nameof(rssFeedUrl));
    }

    private async Task LocalFiles(string model, string languageCode, List<string> file)
    {
        model = await this.GetModelPrompt(model);
        var language = this.GetLanguagePrompt(languageCode);
        var files = this.GetFilesPrompt(file).Where(n => File.Exists(n));

        foreach (var f in files)
        {
            await this.RunModelAsync(model, language, f);
        }
    }

    private async Task Benchmark(string modelFolder, string languageCode)
    {
        var language = this.GetLanguagePrompt(languageCode);
        if (string.IsNullOrEmpty(modelFolder))
        {
            modelFolder = Prompt.Input<string>("Enter Model Path",
                defaultValue: Path.Combine(WhisperStatic.DefaultPath, "classic"));
        }

        var files = Directory.GetFiles(modelFolder, "*.bin");

        var file = Prompt.Input<string>("Enter File Path",
            defaultValue: Path.Combine(WhisperStatic.DefaultPath, "generated.wav"));

        foreach (var model in files)
        {
            var baseDirectory = Path.GetFileName(Path.GetDirectoryName(model));
            await this.RunModelAsync(model, language, file, $"{Path.GetFileNameWithoutExtension(file)}-{baseDirectory}");
        }
    }

    public async Task<int> RunAsync()
    {
        return await this.root.InvokeAsync(this.args);
    }

    private async Task RunModelAsync(string modelPath, WhisperLanguage language, string path, string? filename = null)
    {
        if (!File.Exists(modelPath))
        {
            throw new ArgumentNullException("Model does not exist");
        }

        var srtPath = Path.Combine(WhisperStatic.DefaultPath, "srt");
        Directory.CreateDirectory(srtPath);

        this.whisperService.InitModel(modelPath, language);

        var audio = await this.transcodeService.ProcessFile(path);
        if (string.IsNullOrEmpty(audio) || !File.Exists(audio))
        {
            throw new ArgumentNullException($"Could not generate audio file: {path}");
        }

        var srtFile = Path.Combine(srtPath,
            ConvertToValidFilename(filename ?? Path.GetFileNameWithoutExtension(path)) +
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
        await this.whisperService.ProcessAsync(audio, this.cts.Token);
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

        var model = Prompt.Select("Select a model", this.modelService.AllModels, textSelector: (item) => item.Name);
        if (model.Exists)
        {
            return model.FileLocation;
        }

        Console.WriteLine("Downloading Model...");
        var whisperDownloader =
            new WhisperDownload(model, this.modelService, Ioc.Default.GetRequiredService<IAppDispatcher>());
        whisperDownloader.DownloadService.DownloadProgressChanged += (s, e) =>
        {
            //progressBar.Update((int)e.ProgressPercentage);
        };
        await whisperDownloader.DownloadCommand.ExecuteAsync();

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