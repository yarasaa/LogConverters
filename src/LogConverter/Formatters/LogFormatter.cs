using LogConverter.Models;
using System.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Xml.Linq;
using System.Globalization;

namespace LogConverter.Formatters
{
    public enum LogFormat { Markdown, Html, Json, Xml, Csv }

    public class LogFormatterOptions
    {
        /// <summary>Seviye renklerini uygulansın mı?</summary>
        public bool UseColor { get; set; } = false;
        /// <summary>Çıktıyı byte[] olarak mı alacağız (diske yazmak için)?</summary>
        public bool AsFile { get; set; } = false;
        /// <summary>Dosya olarak kaydedilirse hangi isimle?</summary>
        public string FileName { get; set; } = "";
        // Yeni generic özellikler:
        /// <summary>HTML içine stil eklesin mi?</summary>
        public bool IncludeStyles { get; set; } = true;
        /// <summary>HTML raporuna özet bilgisi eklesin mi?</summary>
        public bool EnableSummary { get; set; } = true;
        /// <summary>Mesajları detay olarak katlanabilir yapsın mı (long > threshold)?</summary>
        public bool FoldLongMessages { get; set; } = true;
        public int FoldMessageLength { get; set; } = 100;
        /// <summary>Gruplama yapılacaksa hangi property'e göre group header eklesin (null=kapalı)</summary>
        public string? GroupByProperty { get; set; } = null;
    }

    public static class LogFormatter
    {
        /// <summary>
        /// Tek bir API ile istediğiniz formata çevirir.
        /// </summary>
        public static string Format(IEnumerable<LogEntry> logs, LogFormat format, LogFormatterOptions opts)
        {
            opts ??= new LogFormatterOptions();

            return format switch
            {
                LogFormat.Markdown => ToMarkdown(logs, opts),
                LogFormat.Html => ToHtml(logs, opts),
                LogFormat.Csv => ToCsv(logs),
                LogFormat.Json => ToJson(logs, opts),
                LogFormat.Xml => ToXml(logs),
                _ => throw new ArgumentOutOfRangeException(nameof(format))
            };
        }

        public static byte[] FormatToBytes(IEnumerable<LogEntry> logs, LogFormat format, LogFormatterOptions opts)
        {
            var content = Format(logs, format, opts);
            return Encoding.UTF8.GetBytes(content);
        }

        // ————————————————

        /// <summary>
        /// Bir dosya yolunu alır, içinde JSON/CSV/XML/TXT ne varsa parse edip LogEntry listesine dönüştürür,
        /// sonra da istediğiniz formata çevirir.
        /// </summary>
        public static string FormatFile(string filePath, LogFormat format, LogFormatterOptions opts)
        {
            var logs = LoadFromFile(filePath);
            return Format(logs, format, opts);
        }

        /// <summary>
        /// Dosyayı parse edip LogEntry listesi döner.
        /// Uzantısına göre JSON/CSV/XML veya düz metin (TXT) ayrıştırması yapar.
        /// </summary>
        private static List<LogEntry> LoadFromFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".json" => LoadLogsFromJson(File.ReadAllText(filePath)),
                ".csv" => LoadLogsFromCsv(File.ReadAllText(filePath)),
                ".xml" => LoadLogsFromXml(File.ReadAllText(filePath)),
                ".txt" => LoadLogsFromText(File.ReadAllText(filePath)),
                _ => throw new NotSupportedException($"Desteklenmeyen dosya uzantısı: {ext}")
            };
        }

        // Aşağıdaki yardımcı metotları mevcut kodunuzdan veya önceki önerilerden kopyalayabilirsiniz:

        private static List<LogEntry> LoadLogsFromJson(string json)
        {
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            return JsonSerializer.Deserialize<List<LogEntry>>(json, opts)
                   ?? new List<LogEntry>();
        }

        private static List<LogEntry> LoadLogsFromCsv(string csv)
        {
            var lines = csv
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
                return new List<LogEntry>();

            var headers = lines[0]
                .Split(',', StringSplitOptions.None)
                .Select(h => h.Trim())
                .ToArray();

            var entries = new List<LogEntry>();

            for (int i = 1; i < lines.Length; i++)
            {
                var cols = lines[i]
                    .Split(',', StringSplitOptions.None)
                    .Select(c => c.Trim())
                    .ToArray();

                var entry = new LogEntry();

                if (DateTime.TryParse(cols[0], null, DateTimeStyles.RoundtripKind, out var ts))
                    entry.Timestamp = ts;
                else
                    entry.Timestamp = DateTime.MinValue;

                entry.Level = cols.Length > 1 ? cols[1] : string.Empty;
                entry.Message = cols.Length > 2 ? cols[2] : string.Empty;
                entry.Exception = cols.Length > 3 && !string.IsNullOrEmpty(cols[3]) ? cols[3] : null;
                entry.EventId = cols.Length > 4 && !string.IsNullOrEmpty(cols[4]) ? cols[4] : null;

                for (int c = 5; c < headers.Length && c < cols.Length; c++)
                {
                    var key = headers[c];
                    var raw = cols[c];
                    if (int.TryParse(raw, out var ival))
                        entry.Properties[key] = ival;
                    else if (bool.TryParse(raw, out var bval))
                        entry.Properties[key] = bval;
                    else
                        entry.Properties[key] = raw;
                }

                entries.Add(entry);
            }

            return entries;
        }

        private static List<LogEntry> LoadLogsFromXml(string xml)
        {
            var doc = XDocument.Parse(xml);
            var logs = new List<LogEntry>();

            foreach (var logElem in doc.Root?.Elements("log") ?? Enumerable.Empty<XElement>())
            {
                var entry = new LogEntry();
                var tsElem = logElem.Element("timestamp");
                var lvlElem = logElem.Element("level");
                var msgElem = logElem.Element("message");
                var exElem = logElem.Element("exception");
                var idElem = logElem.Element("eventId");

                if (tsElem != null && DateTime.TryParse(tsElem.Value, null, DateTimeStyles.RoundtripKind, out var tsVal))
                    entry.Timestamp = tsVal;
                else if (tsElem != null && DateTime.TryParse(tsElem.Value, out tsVal))
                    entry.Timestamp = tsVal;

                entry.Level = lvlElem?.Value ?? string.Empty;
                entry.Message = msgElem?.Value ?? string.Empty;
                entry.Exception = string.IsNullOrEmpty(exElem?.Value) ? null : exElem.Value;
                entry.EventId = string.IsNullOrEmpty(idElem?.Value) ? null : idElem.Value;

                foreach (var child in logElem.Elements())
                {
                    var name = child.Name.LocalName;
                    if (name == "timestamp" || name == "level" || name == "message" || name == "exception" || name == "eventId")
                        continue;

                    var raw = child.Value;
                    if (int.TryParse(raw, out var ival))
                        entry.Properties[name] = ival;
                    else if (bool.TryParse(raw, out var bval))
                        entry.Properties[name] = bval;
                    else
                        entry.Properties[name] = raw;
                }

                logs.Add(entry);
            }

            return logs;
        }

        private static List<LogEntry> LoadLogsFromText(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var entries = new List<LogEntry>();
            var rx = new Regex(@"^(?<ts>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) - (?<msg>.*)$");

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var match = rx.Match(line);
                if (match.Success)
                {
                    var ts = DateTime.Parse(match.Groups["ts"].Value);
                    var msg = match.Groups["msg"].Value;
                    var level = msg.IndexOf("hata", StringComparison.OrdinalIgnoreCase) >= 0 ? "ERROR" : "INFO";

                    entries.Add(new LogEntry
                    {
                        Timestamp = ts,
                        Level = level,
                        Message = msg
                    });
                }
                else if (entries.Count > 0)
                {
                    entries[^1].Message += "\n" + line;
                }
            }

            return entries;
        }

        private static string ToMarkdown(IEnumerable<LogEntry> logs, LogFormatterOptions opts)
        {
            var standardCols = new[] { "Timestamp", "Level", "Message", "Exception", "EventId" };
            var extraCols = logs.SelectMany(l => l.Properties.Keys).Distinct().ToArray();
            var headers = standardCols.Concat(extraCols).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine("| " + string.Join(" | ", headers) + " |");
            sb.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---")) + " |");

            foreach (var log in logs)
            {
                var row = new List<string>
                {
                    log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")
                };

                var levelCell = opts.UseColor
                    ? $"<span style=\"color:{log.LevelColor}\">{log.Level}</span>"
                    : log.Level;
                row.Add(levelCell);

                row.Add(log.Message);
                row.Add(log.Exception ?? "");
                row.Add(log.EventId ?? "");

                foreach (var col in extraCols)
                {
                    log.Properties.TryGetValue(col, out var val);
                    row.Add(val?.ToString() ?? "");
                }

                sb.AppendLine("| " + string.Join(" | ", row) + " |");
            }

            return sb.ToString();
        }

        private static string ToHtml(IEnumerable<LogEntry> logs, LogFormatterOptions opts)
        {
            var list = logs.ToList();
            var total = list.Count;
            var errorCount = list.Count(l => l.Level.Equals("ERROR", StringComparison.OrdinalIgnoreCase));
            var warnCount = list.Count(l => l.Level.Equals("WARN", StringComparison.OrdinalIgnoreCase));
            var infoCount = total - errorCount - warnCount;

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"tr\">");
            sb.AppendLine("<head><meta charset=\"utf-8\"><title>Log Raporu</title>");
            if (opts.IncludeStyles)
            {
                sb.AppendLine("<style>");
                sb.AppendLine("table{border-collapse:collapse;width:100%;}th,td{padding:8px;border:1px solid #ccc;}" +
                              "tr.error{background:#fdd;}tr.warn{background:#ffeec0;}tr.info{background:#e8f4fd;}" +
                              ".group-header th{background:#333;color:#fff;text-align:left;}summary{cursor:pointer;font-weight:bold;}pre{white-space:pre-wrap;}");
                sb.AppendLine("</style>");
            }
            sb.AppendLine("</head><body>");
            if (opts.EnableSummary)
            {
                sb.AppendLine(
                    $"<div class=\"summary\">Toplam: {total} &nbsp; " +
                    $"<span style=\"color:red;\">Hata: {errorCount}</span> " +
                    $"<span style=\"color:orange;\">Uyarı: {warnCount}</span> " +
                    $"<span style=\"color:green;\">Bilgi: {infoCount}</span></div>"
                );
            }

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr>");
            var standardCols = new[] { "Timestamp", "Level", "Message", "Exception", "EventId" };
            var extraCols = list.SelectMany(l => l.Properties.Keys).Distinct();
            var headers = standardCols.Concat(extraCols).ToArray();
            foreach (var h in headers)
                sb.AppendLine($"<th>{h}</th>");
            sb.AppendLine("</tr></thead><tbody>");

            string? currentGroup = null;
            foreach (var log in list)
            {
                // Grup header
                if (!string.IsNullOrEmpty(opts.GroupByProperty) &&
                    log.Properties.TryGetValue(opts.GroupByProperty, out var grpVal))
                {
                    var grp = grpVal?.ToString() ?? "";
                    if (grp != currentGroup)
                    {
                        currentGroup = grp;
                        sb.AppendLine($"<tr class=\"group-header\"><th colspan=\"{headers.Length}\">{opts.GroupByProperty}: {grp}</th></tr>");
                    }
                }

                var lvlClass = log.Level.ToLowerInvariant();
                sb.AppendLine($"<tr class=\"{lvlClass}\">   ");
                sb.AppendLine($"<td>{log.Timestamp:yyyy-MM-dd HH:mm:ss}</td>");
                sb.AppendLine($"<td{(opts.UseColor ? " style=\"color:" + log.LevelColor + "\"" : "")}>{log.Level}</td>");

                // Fold long messages
                var msg = System.Security.SecurityElement.Escape(log.Message);
                if (opts.FoldLongMessages && msg.Length > opts.FoldMessageLength)
                {
                    var summary = msg.Substring(0, opts.FoldMessageLength) + "…";
                    sb.AppendLine($"<td><details><summary>{summary}</summary><pre>{msg}</pre></details></td>");
                }
                else sb.AppendLine($"<td>{msg}</td>");

                var exc = System.Security.SecurityElement.Escape(log.Exception ?? "");
                sb.AppendLine(opts.FoldLongMessages && exc.Length > opts.FoldMessageLength
                    ? $"<td><details><summary>Detay</summary><pre>{exc}</pre></details></td>"
                    : $"<td>{exc}</td>");

                sb.AppendLine($"<td>{log.EventId}</td>");

                // Extra
                foreach (var col in extraCols)
                {
                    log.Properties.TryGetValue(col, out var val);
                    sb.AppendLine($"<td>{System.Security.SecurityElement.Escape(val?.ToString() ?? "")}</td>");
                }

                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }


        private static string ToCsv(IEnumerable<LogEntry> logs)
        {
            var standardCols = new[] { "Timestamp", "Level", "Message", "Exception", "EventId" };
            var extraCols = logs.SelectMany(l => l.Properties.Keys).Distinct().ToArray();
            var headers = standardCols.Concat(extraCols);

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", headers));

            foreach (var log in logs)
            {
                var row = new List<string>
                {
                    log.Timestamp.ToString("o"),
                    log.Level,
                    CsvEscape(log.Message),
                    CsvEscape(log.Exception),
                    CsvEscape(log.EventId)
                };
                foreach (var col in extraCols)
                {
                    log.Properties.TryGetValue(col, out var val);
                    row.Add(CsvEscape(val?.ToString()));
                }
                sb.AppendLine(string.Join(",", row));
            }

            return sb.ToString();
        }

        private static string ToJson(IEnumerable<LogEntry> logs, LogFormatterOptions opts)
        {
            // Burada .NET JsonSerializer kullanmanızı öneririm:
            var jsOpts = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = opts.UseColor
                    ? System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                    : System.Text.Encodings.Web.JavaScriptEncoder.Default
            };
            return System.Text.Json.JsonSerializer.Serialize(logs, jsOpts);
        }

        private static string ToXml(IEnumerable<LogEntry> logs)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<logs>");
            foreach (var log in logs)
            {
                sb.AppendLine("  <log>");
                sb.AppendLine($"    <timestamp>{log.Timestamp:O}</timestamp>");
                sb.AppendLine($"    <level>{log.Level}</level>");
                sb.AppendLine($"    <message>{System.Security.SecurityElement.Escape(log.Message)}</message>");
                if (!string.IsNullOrEmpty(log.Exception))
                    sb.AppendLine($"    <exception>{System.Security.SecurityElement.Escape(log.Exception)}</exception>");
                if (!string.IsNullOrEmpty(log.EventId))
                    sb.AppendLine($"    <eventId>{log.EventId}</eventId>");

                foreach (var kv in log.Properties)
                    sb.AppendLine($"    <{kv.Key}>{System.Security.SecurityElement.Escape(kv.Value?.ToString() ?? "")}</{kv.Key}>");

                sb.AppendLine("  </log>");
            }
            sb.AppendLine("</logs>");
            return sb.ToString();
        }

        private static string CsvEscape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"'))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
