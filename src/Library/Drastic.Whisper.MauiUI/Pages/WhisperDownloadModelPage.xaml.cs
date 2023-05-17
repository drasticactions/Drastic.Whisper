using Drastic.Whisper.UI.ViewModels;

namespace Drastic.Whisper.MauiUI.Pages;

public partial class WhisperDownloadModelPage : ContentPage
{
    public WhisperDownloadModelPage(IServiceProvider provider)
    {
        InitializeComponent();
        this.BindingContext = this.Vm = provider.GetRequiredService<WhisperModelDownloadViewModel>();
    }

    public WhisperModelDownloadViewModel Vm;

    private void Button_Clicked(object sender, EventArgs e)
    {
#if WINDOWS
        Application.Current?.CloseWindow(this.Window);
#else
        this.Navigation.PopModalAsync();
#endif
    }
}