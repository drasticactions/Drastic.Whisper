using Drastic.Whisper.UI.ViewModels;
using Microsoft.Maui.Controls.PlatformConfiguration;
#if IOS || MACCATALYST
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;
#endif

namespace Drastic.Whisper.MauiUI.Pages;

public partial class WhisperDownloadModelPage : ContentPage
{
    public WhisperDownloadModelPage(IServiceProvider provider)
    {
        InitializeComponent();
        this.BindingContext = this.Vm = provider.GetRequiredService<WhisperModelDownloadViewModel>();
#if IOS || MACCATALYST
        On<iOS>().SetModalPresentationStyle(UIModalPresentationStyle.FormSheet);
#endif
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