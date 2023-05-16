using Drastic.Services;

namespace Drastic.Whisper.MauiUI.Services
{
    public class MauiAppDispatcherService : IAppDispatcher
    {
        private IDispatcher dispatcher;

        public MauiAppDispatcherService(IServiceProvider provider)
        {
            this.dispatcher = provider.GetRequiredService<IDispatcher>();
        }

        public bool Dispatch(Action action)
        {
            return this.dispatcher.Dispatch(action);
        }
    }
}
