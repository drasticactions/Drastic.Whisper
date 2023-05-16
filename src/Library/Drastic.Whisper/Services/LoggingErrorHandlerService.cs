// <copyright file="LoggingErrorHandlerService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using Drastic.Services;
using Microsoft.Extensions.Logging;

namespace Drastic.Whisper.Services
{
    /// <summary>
    /// Logging Error Handler Service.
    /// </summary>
    public class LoggingErrorHandlerService : IErrorHandlerService
    {
        private ILogger logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggingErrorHandlerService"/> class.
        /// </summary>
        /// <param name="logger">Logger.</param>
        public LoggingErrorHandlerService(ILogger logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public void HandleError(Exception ex)
        {
            this.logger.LogError("Error", ex);
        }
    }
}
