// <copyright file="DefaultMauiPlatformServices.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using Drastic.Whisper.MauiUI.Controls;

namespace Drastic.Whisper.MauiUI.Services
{
    public class DefaultMauiPlatformServices : IMauiPlatformServices
    {
        private Window window;

        public DefaultMauiPlatformServices(Window window)
        {
            this.window = window;
        }

        public Task OpenInModalAsync(Page page)
        {
#if WINDOWS
            var modal = new ModalWindow(page);
            Microsoft.Maui.Controls.Application.Current?.OpenWindow(modal);
            return Task.CompletedTask;
#else
            return this.window.Navigation.PushModalAsync(page);
#endif
        }
    }
}
