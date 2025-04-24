using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using LogConverter.Formatters;
using LogConverter.Models;

namespace LogConverter.Test
{
    public static class TestRunner
    {
        public static void Run()
        {
            // --- 1) INLINE JSON TESTİ ---
            Console.WriteLine("=== Inline JSON Test ===");
            var inlineJson = @"
            [
              {
                ""Timestamp"": ""2025-04-24T10:00:00+03:00"",
                ""Level"": ""INFO"",
                ""Message"": ""Sunucu başlatıldı"",
                ""Exception"": null,
                ""EventId"": ""STARTUP"",
                ""Properties"": { ""Host"": ""localhost"", ""Port"": 8080 }
              },
              {
                ""Timestamp"": ""2025-04-24T10:05:00+03:00"",
                ""Level"": ""ERROR"",
                ""Message"": ""Veritabanı bağlantı hatası"",
                ""Exception"": ""TimeoutException: Bağlanılamadı"",
                ""EventId"": ""DB1001"",
                ""Properties"": { ""Database"": ""Orders"", ""Retries"": 3 }
              }
            ]";
            var logsFromJson = LoadLogsFromJsonString(inlineJson);
            PrintAllFormats(logsFromJson, "json-inline");

            // --- 2) INLINE CSV TESTİ ---
            Console.WriteLine("\n=== Inline CSV Test ===");
            var inlineCsv = @"
Timestamp,Level,Message,Exception,EventId,Host,Port,Database,Retries
2025-04-24T10:00:00+03:00,INFO,Sunucu başlatıldı,,STARTUP,localhost,8080,,
2025-04-24T10:05:00+03:00,ERROR,Veritabanı bağlantı hatası,TimeoutException: Bağlanılamadı,DB1001,,,Orders,3
".Trim();
            var logsFromCsv = LoadLogsFromCsvString(inlineCsv);
            PrintAllFormats(logsFromCsv, "csv-inline");
        }

        private static List<LogEntry> LoadLogsFromJsonString(string json)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Deserialize<List<LogEntry>>(json, opts)
                   ?? new List<LogEntry>();
        }

        private static List<LogEntry> LoadLogsFromCsvString(string csv)
        {
            var lines = csv
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ToArray();
            var headers = lines[0]
                .Split(',', StringSplitOptions.None);

            var result = new List<LogEntry>();
            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(',', StringSplitOptions.None);
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Parse(cols[0]),
                    Level = cols[1],
                    Message = cols[2],
                    Exception = string.IsNullOrEmpty(cols[3]) ? null : cols[3],
                    EventId = string.IsNullOrEmpty(cols[4]) ? null : cols[4],
                };

                // Dinamik ekstra kolonlar -> Properties
                for (int c = 5; c < headers.Length && c < cols.Length; c++)
                {
                    var key = headers[c];
                    if (int.TryParse(cols[c], out var iv))
                        entry.Properties[key] = iv;
                    else if (!string.IsNullOrEmpty(cols[c]))
                        entry.Properties[key] = cols[c];
                }

                result.Add(entry);
            }

            return result;
        }
        private static List<LogEntry> LoadLogsFromTextFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var list = new List<LogEntry>();
            var rx = new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) - (?<msg>.+)$");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var m = rx.Match(line);
                if (!m.Success)
                {
                    // timestamp yoksa bir önceki entry'nin Message'ına satır ekleyebiliriz
                    if (list.Count > 0)
                        list[^1].Message += "\n" + line;
                    continue;
                }

                var ts = DateTime.Parse(m.Groups["ts"].Value);
                var msg = m.Groups["msg"].Value;

                // Basit seviye tespiti: içinde "hata" geçiyorsa ERROR
                var lvl = msg.IndexOf("hata", StringComparison.OrdinalIgnoreCase) >= 0
                          ? "ERROR"
                          : "INFO";

                list.Add(new LogEntry
                {
                    Timestamp = ts,
                    Level = lvl,
                    Message = msg
                });
            }

            return list;
        }

        private static void PrintAllFormats(IEnumerable<LogEntry> logs, string tag)
        {
            foreach (LogFormat fmt in Enum.GetValues(typeof(LogFormat)))
            {
                Console.WriteLine($"\n--- {tag.ToUpper()} -> {fmt} ---");
                // Renkli çıktı istiyorsanız UseColor = true
                var opts = new LogFormatterOptions { UseColor = true };
                var output = LogFormatter.Format(logs, fmt, opts);
                Console.WriteLine(output);
            }
        }
    }
}
