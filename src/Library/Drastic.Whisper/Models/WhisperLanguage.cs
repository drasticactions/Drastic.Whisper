// <copyright file="WhisperLanguage.cs" company="Drastic Actions">
// Copyright (c) Drastic Actions. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Drastic.Whisper.Models
{
    public class WhisperLanguage
    {
        public WhisperLanguage(CultureInfo info)
        {
            this.CultureInfo = info;
            this.Language = info.DisplayName;
            this.LanguageCode = info.IetfLanguageTag;
        }

        public WhisperLanguage(WhisperLanguages language)
        {
            var code = GetCode(language);
            this.CultureInfo = System.Globalization.CultureInfo.GetCultureInfo(code);
            this.Language = this.CultureInfo.DisplayName;
            this.LanguageCode = this.CultureInfo.IetfLanguageTag;
        }

        public WhisperLanguage()
        {
            this.IsAutomatic = true;
            this.CultureInfo = CultureInfo.CurrentCulture;
            this.LanguageCode = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
            this.Language = Translations.Common.LanguageAuto;
        }

        public string Language { get; }

        public bool IsAutomatic { get; }

        public CultureInfo CultureInfo { get; }

        public string LanguageCode { get; }

        public static IReadOnlyList<WhisperLanguage> GenerateWhisperLangauages()
        {
            var list = new List<WhisperLanguage>() { };

            foreach (WhisperLanguages value in Enum.GetValues(typeof(WhisperLanguages)))
            {
                list.Add(new WhisperLanguage(value));
            }

            var orderedList = list.OrderBy(n => n.Language);
            var newList = orderedList.ToList();
            newList.Insert(0, new WhisperLanguage());
            return newList.AsReadOnly();
        }

        [SkipLocalsInit]
        public static string GetCode(WhisperLanguages lang)
        {
            unsafe
            {
                sbyte* ptr = stackalloc sbyte[5];
                *(uint*)ptr = (uint)lang;
                ptr[4] = 0;
                return new string(ptr);
            }
        }
    }
}
