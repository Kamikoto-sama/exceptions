﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using NLog;

namespace Exceptions
{
    public class ConverterProgram
    {
        private static readonly Logger log = LogManager.GetCurrentClassLogger();

        public static void Main(params string[] args)
        {
            try
            {
                var filenames = args.Any() ? args : new[] {"text.txt"};
                var settings = LoadSettings();
                ConvertFiles(filenames, settings);
            }
            catch (InvalidOperationException)
            {
                log.Error(new XmlException("Не удалось прочитать файл настроек"));
            }
            catch (Exception e)
            {
                log.Error(e);
            }
        }

        private static void ConvertFiles(string[] filenames, Settings settings)
        {
            var tasks = filenames
                .Select(fn => Task.Run(() => ConvertFile(fn, settings))) 
                .ToArray();
            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException)
            {
                log.Error("Некорректная строка");
            }
        }

        private static Settings LoadSettings() 
        {
            try
            {
                var serializer = new XmlSerializer(typeof(Settings));
                var content = File.ReadAllText("settings.xml");
                return (Settings) serializer.Deserialize(new StringReader(content));
            }
            catch (FileNotFoundException)
            {
                log.Error("Файл настроек .* отсутствует.");
                return Settings.Default;
            }
        }

        private static void ConvertFile(string filename, Settings settings)
        {
            if (!File.Exists(filename))
            {
                log.Error(new FileNotFoundException($"Не удалось сконвертировать {filename}"));
                return;
            }
            
            Thread.CurrentThread.CurrentCulture = new CultureInfo(settings.SourceCultureName);
            if (settings.Verbose)
            {
                log.Info("Processing file " + filename);
                log.Info("Source Culture " + Thread.CurrentThread.CurrentCulture.Name);
            }
            IEnumerable<string> lines;
            try 
            {
                lines = PrepareLines(filename); 
            }
            catch
            {
                log.Error($"File {filename} not found"); 
                return;
            }
            var convertedLines = lines
                .Select(ConvertLine)
                .Select(s => s.Length + " " + s);
            File.WriteAllLines(filename + ".out", convertedLines);
        }

        private static IEnumerable<string> PrepareLines(string filename)
        {
            var lineIndex = 0;
            foreach (var line in File.ReadLines(filename))
            {
                if (line == "") continue;
                yield return line.Trim();
                lineIndex++;
            }
            yield return lineIndex.ToString();
        }

        public static string ConvertLine(string arg)
        {
            if (double.TryParse(arg, out var number))
                return number.ToString(CultureInfo.InvariantCulture);
            return DateTime.TryParse(arg, out var dateTime)
                ? dateTime.ToString(CultureInfo.InvariantCulture)
                : ConvertAsCharIndexInstruction(arg);
        }

        private static string ConvertAsCharIndexInstruction(string s)
        {
            var parts = s.Split();
            if (parts.Length < 2) return null;
            var charIndex = int.Parse(parts[0]);
            if ((charIndex < 0) || (charIndex >= parts[1].Length))
                return null;
            var text = parts[1];
            return text[charIndex].ToString();
        }
    }
}