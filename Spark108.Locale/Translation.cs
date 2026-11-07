using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Xml;
using System.Xml.Linq;

namespace Spark108.Locale
{
    internal sealed class Translation
    {
        private readonly Dictionary<string, string> lines;
        private readonly List<(Regex Pattern, string Replacement)> patterns =
            new List<(Regex Pattern, string Replacement)>();

        public string Language { get; }
        public string FontFamily { get; }
        public string FontStyle { get; }
        public int Count => lines.Count + patterns.Count;

        private Translation(string language, LocaleFile file)
        {
            Language = language;
            FontFamily = file.FontFamily;
            FontStyle = file.FontStyle ?? "Regular";
            lines = file.Text ?? throw new InvalidDataException("Locale file is missing the text dictionary.");

            if (file.Patterns != null)
            {
                foreach (var entry in file.Patterns)
                {
                    if (entry.Value == null)
                        throw new InvalidDataException($"Missing replacement for pattern {entry.Key}.");

                    patterns.Add((new Regex(entry.Key, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)),
                        entry.Value));
                }
            }
        }

        public static Translation Load(string directory)
        {
            var config = ReadObject<Config>(Path.Combine(directory, "config.json"));
            var language = config.Language;
            if (language == null || !Regex.IsMatch(language, @"\A[A-Za-z]{2,8}(?:_[A-Za-z0-9]{2,8})+\z"))
                throw new InvalidDataException("config.json must contain a language ID such as ru_RU.");

            return new Translation(language, ReadObject<LocaleFile>(Path.Combine(directory, language + ".json")));
        }

        public string Get(string source)
        {
            if (string.IsNullOrEmpty(source))
                return source;

            var key = source.Replace("\r\n", "\n");
            if (lines.TryGetValue(key, out var translated))
                return translated ?? source;

            foreach (var (pattern, replacement) in patterns)
            {
                try
                {
                    var match = pattern.Match(key);
                    if (match.Success)
                        return match.Result(replacement);
                }
                catch (RegexMatchTimeoutException)
                {
                    // A custom pattern must never interrupt the game's UI.
                }
            }

            return source;
        }

        private static T ReadObject<T>(string path) where T : class
        {
            var serializer = new DataContractJsonSerializer(typeof(T),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var file = File.OpenRead(path))
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(file, XmlDictionaryReaderQuotas.Max))
            {
                // Remove comments before dictionaries are read, including repeated comment keys.
                var document = XElement.Load(reader);
                document.Descendants("__comment__").Remove();
                using (var content = document.CreateReader())
                    return serializer.ReadObject(content) as T ??
                        throw new InvalidDataException($"Expected a JSON object in {Path.GetFileName(path)}.");
            }
        }

        [DataContract]
        private sealed class Config
        {
            [DataMember(Name = "language")]
            public string Language { get; set; }
        }

        [DataContract]
        private sealed class LocaleFile
        {
            [DataMember(Name = "fontFamily")]
            public string FontFamily { get; set; }

            [DataMember(Name = "fontStyle")]
            public string FontStyle { get; set; }

            [DataMember(Name = "text")]
            public Dictionary<string, string> Text { get; set; }

            [DataMember(Name = "patterns")]
            public Dictionary<string, string> Patterns { get; set; }
        }
    }
}
