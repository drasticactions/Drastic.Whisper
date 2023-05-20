using Drastic.AudioRecorder;
using Drastic.Tools;
using Drastic.Whisper.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drastic.Whisper.MauiDebug.ViewModels
{
    public class AudioTranscribeDebugViewModel : TranscriptionViewModel
    {
        private AsyncCommand? recordCommand;
        private AudioRecorderService recorder;

        public AudioTranscribeDebugViewModel(IServiceProvider services)
            : base(services)
        {
            this.recorder = new AudioRecorderService
            {
                PreferredSampleRate = 16000,
                StopRecordingAfterTimeout = false,
                StopRecordingOnSilence = false,
            };

            this.ModelService.SelectedModel = this.ModelService.AvailableModels.FirstOrDefault();
            this.recorder.OnBroadcast += this.Recorder_OnBroadcast;
        }

        public AsyncCommand RecordCommand => this.recordCommand ??= new AsyncCommand(this.RecordAsync, null, this.Dispatcher, this.ErrorHandler);

        private async Task RecordAsync()
        {
            if (this.ModelService.SelectedModel is null)
            {
                return;
            }

            this.Whisper.InitModel(this.ModelService.SelectedModel.FileLocation, this.SelectedLanguage);
            
            var audioRecordTask = await (await this.recorder.StartRecording());
        }

        private void Recorder_OnBroadcast(object? sender, byte[] e)
        {
            this.Whisper.ProcessBytes(e).FireAndForgetSafeAsync();
        }
    }
}
