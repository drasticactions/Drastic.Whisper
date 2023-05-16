using Drastic.Whisper.MauiUI.Pages;

namespace Drastic.Whisper.MauiDebug;

public partial class MainPage : ContentPage
{
    int count = 0;
    private IServiceProvider provider;

    public MainPage()
    {
        InitializeComponent();
        this.provider = App.Current!.Handler!.MauiContext!.Services;
    }

    private void OnCounterClicked(object sender, EventArgs e)
    {
        this.Navigation.PushModalAsync(new WhisperDownloadModelPage(this.provider));
    }
}