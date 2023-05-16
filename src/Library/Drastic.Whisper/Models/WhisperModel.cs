// <copyright file="WhisperModel.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whisper.net.Ggml;

namespace Drastic.Whisper.Models
{
    public class WhisperModel
    {
        public WhisperModel()
        {
        }

        public WhisperModel(GgmlType type)
        {
            this.GgmlType = type;
            this.Name = type.ToString();
            this.Type = WhisperModelType.Standard;
            this.FileLocation = System.IO.Path.Combine(WhisperStatic.DefaultPath, type.ToFilename());
            this.DownloadUrl = type.ToDownloadUrl();

            // TODO: Add descriptions
            this.Description = type switch
            {
                GgmlType.Tiny => "Tiny model trained on 1.5M samples",
                GgmlType.TinyEn => "Tiny model trained on 1.5M samples (English)",
                GgmlType.Base => "Base model trained on 1.5M samples",
                GgmlType.BaseEn => "Base model trained on 1.5M samples (English)",
                GgmlType.Small => "Small model trained on 1.5M samples",
                GgmlType.SmallEn => "Small model trained on 1.5M samples (English)",
                GgmlType.Medium => "Medium model trained on 1.5M samples",
                GgmlType.MediumEn => "Medium model trained on 1.5M samples (English)",
                GgmlType.LargeV1 => "Large model trained on 1.5M samples (v1)",
                GgmlType.Large => "Large model trained on 1.5M samples",
                _ => throw new NotImplementedException(),
            };
        }

        public WhisperModel(string path)
        {
            if (!System.IO.Path.Exists(path))
            {
                throw new ArgumentException(nameof(path));
            }

            this.FileLocation = path;
            this.Name = System.IO.Path.GetFileNameWithoutExtension(path);
            this.Type = WhisperModelType.User;
        }

        public WhisperModelType Type { get; set; }

        public GgmlType GgmlType { get; set; }

        public string Name { get; set; } = string.Empty;

        public string FileLocation { get; set; } = string.Empty;

        public string DownloadUrl { get; } = string.Empty;

        public string Description { get; } = string.Empty;

        public bool Exists => !string.IsNullOrEmpty(this.FileLocation) && System.IO.Path.Exists(this.FileLocation);
    }
}
