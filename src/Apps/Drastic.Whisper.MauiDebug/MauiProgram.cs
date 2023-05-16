using Drastic.Services;
using Drastic.Whisper.MauiDebug.Services;
using Drastic.Whisper.MauiUI.Services;
using Drastic.Whisper.Services;
using Drastic.Whisper.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace Drastic.Whisper.MauiDebug;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
#if MACCATALYST
        Microsoft.Maui.Handlers.ButtonHandler.Mapper.AppendToMapping("ButtonChange", (handler, view) =>
        {
            handler.PlatformView.PreferredBehavioralStyle = UIKit.UIBehavioralStyle.Pad;
        });
#endif

#if ANDROID || IOS
        LibVLCSharp.Shared.Core.Initialize();
#endif
        var generatedFilename = "generated";
        var builder = MauiApp.CreateBuilder();

        builder.Services
           .AddSingleton<IAppDispatcher, MauiAppDispatcherService>()
           .AddSingleton<IErrorHandlerService, DebugErrorHandler>()
           .AddSingleton<IWhisperService, DefaultWhisperService>()
           .AddSingleton<WhisperModelService>() 
#if ANDROID || IOS
            .AddSingleton<ITranscodeService>(new VlcTranscodeService(generatedFilename: generatedFilename))
#else
            .AddSingleton<ITranscodeService>(new FFMpegTranscodeService(WhisperStatic.DefaultPath, generatedFilename))
#endif
           .AddSingleton<WhisperModelDownloadViewModel>()
           .AddSingleton<TranscriptionViewModel>();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
