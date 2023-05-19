// <copyright file="IMicrophoneService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;

namespace Drastic.Whisper.MauiUI.Services
{
    public interface IMicrophoneService
    {
        /// <summary>
		/// Occurs when new audio has been streamed.
		/// </summary>
		event EventHandler<AudioCapturedEventArgs>? OnBroadcast;

        /// <summary>
        /// Gets the sample rate.
        /// </summary>
        /// <value>The sample rate in hertz.</value>
        int SampleRate { get; }

        /// <summary>
        /// Gets the channel count.
        /// </summary>
        /// <value>The channel count.</value>
        int ChannelCount { get; }

        /// <summary>
        /// Gets bits per sample.
        /// </summary>
        /// <value>The bits per sample.</value>
        int BitsPerSample { get; }

        /// <summary>
        /// Gets the average data transfer rate
        /// </summary>
        /// <value>The average data transfer rate in bytes per second.</value>
        //int AverageBytesPerSecond { get; }

        IEnumerable<int> SupportedSampleRates { get; }

        /// <summary>
        /// Starts the specified sample rate.
        /// </summary>
        /// <param name="sampleRate">The sample rate.</param>
        /// <returns>Task&lt;System.Boolean&gt;.</returns>
        Task<bool> Start(int sampleRate);

        /// <summary>
        /// Stops this instance.
        /// </summary>
        /// <returns>Task.</returns>
        Task Stop();
    }

    public class AudioCapturedEventArgs : EventArgs
    {
        public AudioCapturedEventArgs(byte[] capture)
        {
            this.Capture = capture;
        }

        public byte[] Capture { get; }
    }
}