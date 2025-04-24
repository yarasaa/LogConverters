// See https://aka.ms/new-console-template for more information
using System.Text;
using LogConverter.Formatters;
using LogConverter.Test;

// TestRunner.Run();
var html = LogFormatter.FormatFile("log.txt", LogFormat.Html, new LogFormatterOptions
{
    UseColor = true,
    AsFile = true,           // opsiyonel, sadece semantik olarak
    FileName = "log.html",
    EnableSummary = true, // opsiyonel, sadece semantik olarak,
    FoldLongMessages = true, // opsiyonel, sadece semantik olarak
});
System.Console.WriteLine(html);

// Dosyayı sizin yazmanız:
File.WriteAllText("log.html", html, Encoding.UTF8);