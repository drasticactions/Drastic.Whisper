// <copyright file="ISubtitleLine.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;

namespace Drastic.Whisper.Models
{
    public interface ISubtitleLine
    {
        TimeSpan Start { get; set; }

        TimeSpan End { get; set; }

        string Text { get; set; }
    }
}
