﻿// <copyright file="IOnlineVideoService.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drastic.Whisper.Services
{
    public interface IOnlineVideoService
    {
        bool IsValidUrl(string url);

        Task<string> GetAudioUrlAsync(string url);
    }
}
