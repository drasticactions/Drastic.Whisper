using Drastic.Tools;
using Drastic.Whisper.MauiDebug.ViewModels;
using Drastic.Whisper.MauiUI.Pages;
using Drastic.Whisper.MauiUI.Services;

namespace Drastic.Whisper.MauiDebug;

public partial class MainPage : ContentPage
{
    int count = 0;
    private IServiceProvider provider;

    public MainPage()
    {
        InitializeComponent();
        this.provider = App.Current!.Handler!.MauiContext!.Services;
        this.BindingContext = this.ViewModel = new AudioTranscribeDebugViewModel(this.provider);
    }

    public AudioTranscribeDebugViewModel ViewModel { get; }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        var platform = new DefaultMauiPlatformServices(this.Window);
        platform.OpenInModalAsync(new WhisperDownloadModelPage(this.provider));
    }
}