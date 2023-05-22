using Drastic.Tools;
using Drastic.Whisper.MauiDebug.ViewModels;
using Drastic.Whisper.MauiUI.Pages;
using Drastic.Whisper.MauiUI.Services;
using Drastic.Whisper.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Drastic.Whisper.MauiDebug;

public partial class MainPage : ContentPage
{
    int count = 0;
    private IServiceProvider provider;

    public MainPage(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        this.provider = serviceProvider;
        this.BindingContext = this.ViewModel = this.provider.GetRequiredService<TranscriptionViewModel>();
    }

    public TranscriptionViewModel ViewModel { get; }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        var platform = new DefaultMauiPlatformServices(this.Window);
        platform.OpenInModalAsync(new WhisperDownloadModelPage(this.provider));
    }

    private async void Button_Clicked(object sender, EventArgs e)
    {
        PickOptions options = new()
        {
            PickerTitle = Drastic.Whisper.Translations.Common.OpenFileButton,
        };

        try
        {
            var result = await FilePicker.Default.PickAsync();
            if (result != null)
            {
                if (Drastic.Whisper.UI.Tools.DrasticWhisperFileExtensions.VideoExtensions.Contains(Path.GetExtension(result.FileName)) || Drastic.Whisper.UI.Tools.DrasticWhisperFileExtensions.AudioExtensions.Contains(Path.GetExtension(result.FileName)))
                {
                    this.ViewModel.UrlField = result.FullPath;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }
}