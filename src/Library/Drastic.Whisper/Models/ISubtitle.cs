// <copyright file="ISubtitle.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace Drastic.Whisper.Models
{
    public interface ISubtitle
    {
        List<ISubtitleLine> Lines { get; set; }
    }
}
