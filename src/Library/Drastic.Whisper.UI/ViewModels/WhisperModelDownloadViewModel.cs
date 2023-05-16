// <copyright file="WhisperModelDownloadViewModel.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using Drastic.ViewModels;
using Drastic.Whisper.Models;
using Drastic.Whisper.Services;

namespace Drastic.Whisper.UI.ViewModels
{
    public class WhisperModelDownloadViewModel
        : BaseViewModel
    {
        private WhisperModelService modelService;

        public WhisperModelDownloadViewModel(IServiceProvider services) : base(services)
        {
            this.modelService = services.GetService(typeof(WhisperModelService)) as WhisperModelService ?? throw new NullReferenceException(nameof(WhisperModelService));
            foreach (var item in this.modelService.AllModels)
            {
                this.Downloads.Add(new WhisperDownload(item, this.modelService, this.Dispatcher));
            }
        }

        public WhisperModelService ModelService => this.modelService;

        public ObservableCollection<WhisperDownload> Downloads { get; } = new ObservableCollection<WhisperDownload>();
    }
}
