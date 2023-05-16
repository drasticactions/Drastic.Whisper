// <copyright file="TranscriptionViewModel.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using Drastic.Tools;
using Drastic.ViewModels;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;
using Drastic.Whisper.UI.Tools;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Drastic.Whisper.UI.ViewModels
{
    public class TranscriptionViewModel : BaseViewModel, IDisposable
    {
        private WhisperModelService modelService;
        private IWhisperService whisper;
        private double progress;
        private ILogger? diagLogger;
        private bool disposedValue;
        private ITranscodeService transcodeService;
        private CancellationTokenSource cts;
        private WhisperLanguage selectedLanguage;
        private string? urlField;
        private bool canStart = true;

        public TranscriptionViewModel(IServiceProvider services)
            : base(services)
        {
            this.diagLogger = services.GetService<ILogger>();
            this.modelService = services.GetService(typeof(WhisperModelService)) as WhisperModelService ?? throw new NullReferenceException(nameof(WhisperModelService));
            this.whisper = this.Services.GetRequiredService<IWhisperService>()!;
            this.transcodeService = this.Services.GetRequiredService<ITranscodeService>();
            this.whisper.OnProgress = this.OnProgress;
            this.whisper.OnNewWhisperSegment += this.OnNewWhisperSegment;
            this.modelService.OnUpdatedSelectedModel += this.ModelServiceOnUpdatedSelectedModel;
            this.modelService.OnAvailableModelsUpdate += this.ModelServiceOnAvailableModelsUpdate;
            this.WhisperLanguages = WhisperLanguage.GenerateWhisperLangauages();
            this.selectedLanguage = this.WhisperLanguages[0];
            this.cts = new CancellationTokenSource();
            this.StartCommand = new AsyncCommand(this.StartAsync, () => this.canStart, this.Dispatcher, this.ErrorHandler);
        }

        public TranscriptionViewModel(string srtText, IServiceProvider services)
            : this(services)
        {
            var subtitle = new SrtSubtitle(srtText);
            this.Subtitles.Clear();
            foreach (var item in subtitle.Lines)
            {
                this.Subtitles.Add(item);
            }
        }

        public AsyncCommand StartCommand { get; }

        public IReadOnlyList<WhisperLanguage> WhisperLanguages { get; }

        public WhisperLanguage SelectedLanguage {
            get {
                return this.selectedLanguage;
            }

            set {
                this.SetProperty(ref this.selectedLanguage, value);
                this.RaiseCanExecuteChanged();
            }
        }

        public string? UrlField {
            get {
                return this.urlField;
            }

            set {
                this.SetProperty(ref this.urlField, value);
                this.RaiseCanExecuteChanged();
            }
        }


        public double Progress {
            get {
                return this.progress;
            }

            set {
                this.SetProperty(ref this.progress, value);
            }
        }

        public void OnProgress(double progress)
            => this.Progress = progress;

        /// <summary>
        /// Gets the subtitles.
        /// </summary>
        public ObservableCollection<ISubtitleLine> Subtitles { get; } = new ObservableCollection<ISubtitleLine>();

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private async Task StartAsync()
        {
            ArgumentNullException.ThrowIfNull(nameof(this.UrlField));
            ArgumentNullException.ThrowIfNull(nameof(this.modelService.SelectedModel));

            if (File.Exists(this.UrlField))
            {
                await this.LocalFileParseAsync(this.UrlField!, this.cts.Token);
            }
        }

        private async Task LocalFileParseAsync(string filepath, CancellationToken token)
        {
            string? audioFile = string.Empty;

            if (!File.Exists(filepath))
            {
                return;
            }

            if (!DrasticWhisperFileExtensions.Supported.Contains(Path.GetExtension(filepath)))
            {
                return;
            }

            await this.ParseAsync(filepath, token);
        }

        private async Task ParseAsync(string filepath, CancellationToken token)
        {
            string? audioFile = string.Empty;

            audioFile = await this.transcodeService.ProcessFile(filepath);
            if (string.IsNullOrEmpty(audioFile) || !File.Exists(audioFile))
            {
                return;
            }

            await this.PerformBusyAsyncTask(
                async () =>
                {
                    await this.GenerateCaptionsAsync(audioFile, token);
                },
                Whisper.Translations.Common.GeneratingSubtitles);
        }

        private Task GenerateCaptionsAsync(string audioFile, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(nameof(this.modelService.SelectedModel));

            return this.PerformBusyAsyncTask(
                async () =>
                {
                    this.whisper.InitModel(this.modelService.SelectedModel!.FileLocation, this.SelectedLanguage);

                    await this.whisper.ProcessAsync(audioFile, token);
                },
                Translations.Common.GeneratingSubtitles);
        }

        private void OnNewWhisperSegment(object? sender, OnNewSegmentEventArgs segment)
        {
            var e = segment.Segment;
            this.diagLogger?.LogDebug($"CSSS {e.Start} ==> {e.End} : {e.Text}");

            var item = new SrtSubtitleLine() { Start = e.Start, End = e.End, Text = e.Text, LineNumber = this.Subtitles.Count() + 1 };

            this.Dispatcher.Dispatch(() => this.Subtitles.Add(item));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.modelService.OnUpdatedSelectedModel -= this.ModelServiceOnUpdatedSelectedModel;
                    this.whisper.OnNewWhisperSegment -= this.OnNewWhisperSegment;
                }

                disposedValue = true;
            }
        }

        private void ModelServiceOnUpdatedSelectedModel(object? sender, EventArgs e)
        {
            this.RaiseCanExecuteChanged();
        }

        private void ModelServiceOnAvailableModelsUpdate(object? sender, EventArgs e)
        {
            this.RaiseCanExecuteChanged();
        }
    }
}
