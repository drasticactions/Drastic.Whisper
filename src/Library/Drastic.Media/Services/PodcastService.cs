// <copyright file="PodcastService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System.Xml.Serialization;
using Drastic.Media.Core.Infrastructure;
using Drastic.Media.Core.Model;
using Drastic.Media.Core.Model.Feeds;
using Microsoft.Extensions.Logging;

namespace Drastic.Media.Core.Services
{
    /// <summary>
    /// Podcast Service.
    /// </summary>
    public class PodcastService : IPodcastService
    {
        private readonly HttpClient httpClient;
        private static readonly XmlSerializer XmlSerializer = new(typeof(Rss));
        private ILogger? logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="PodcastService"/> class.
        /// </summary>
        /// <param name="client">HttpClient.</param>
        /// <param name="logger">Logger.</param>
        public PodcastService(HttpClient? client = default, ILogger? logger = default)
        {
            this.logger = logger;
            this.httpClient = client ?? new HttpClient();
        }

        /// <inheritdoc/>
        public async Task<PodcastShowItem?> FetchPodcastShowAsync(Uri podcastUri, CancellationToken cancellationToken)
        {
            try
            {
                using var feedContent = await this.httpClient.GetStreamAsync(podcastUri);
                var rss = XmlSerializer.Deserialize(feedContent) as Rss;
                if (rss is null)
                {
                    throw new ArgumentNullException("Feed not RSS");
                }

                var updatedShow = Mapper.Map(podcastUri, rss);
                return updatedShow;
            }
            catch (Exception ex)
            {
                this.logger?.LogError(ex, "FetchPodcastShowAsync");
                throw;
            }
        }
    }
}
