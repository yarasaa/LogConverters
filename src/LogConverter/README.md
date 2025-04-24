# LogConverter

**Kullanımı kolay, genişletilebilir log formatlama kütüphanesi**

LogConverter; standart .NET `LogEntry` modelinizi çeşitli formatlara (Markdown, HTML, CSV, JSON, XML) dönüştürmenizi sağlayan, renklendirme, özet, grup başlıkları ve katlanabilir detaylar gibi gelişmiş özelleştirmeler sunan bir NuGet paketidir.

---

## Özellikler

- **Markdown** , **HTML** , **CSV** , **JSON** , **XML** çıktı desteği
- Seviyeye göre (INFO/WARN/ERROR) renkli veya renksiz stil uygulama
- **Özet** bölümü: Toplam, hata, uyarı, bilgi sayıları
- **Gruplama** : Herhangi bir `LogEntry.Properties` anahtarına göre satır başlıkları
- **Katlanabilir detay** (`<details>`): Uzun mesaj ve exception metinlerini özetle saklama
- Dosya (_.txt, _.csv, _.json, _.xml\* ) okuma ve tek adımda işleyip formatlama

---

## Kurulum

```bash
# .NET CLI ile
dotnet add package LogConverter --version 1.0.0

# Paket varsa güncellemek için
dotnet package update LogConverter
```

NuGet üzerinde en güncel sürümü kontrol edebilirsiniz: [https://www.nuget.org/packages/LogConverter](https://www.nuget.org/packages/LogConverter)

---

## Hızlı Başlangıç

### Basit Markdown çıktısı

```csharp
using LogConverter.Formatters;
using LogConverter.Models;

var logs = new List<LogEntry> {
  new LogEntry { Timestamp = DateTime.Now, Level = "INFO", Message = "Başlatılıyor" },
  new LogEntry { Timestamp = DateTime.Now, Level = "ERROR", Message = "Bir hata oluştu" }
};

string markdown = LogFormatter.Format(
  logs,
  LogFormat.Markdown,
  new LogFormatterOptions { UseColor = true }
);
Console.WriteLine(markdown);
```

### Tam HTML raporu (WebView için ideal)

```csharp
var html = LogFormatter.Format(
  logs,
  LogFormat.Html,
  new LogFormatterOptions {
    UseColor      = true,
    IncludeStyles = true,
    EnableSummary = true
  }
);
// WebView.Source = new HtmlWebViewSource { Html = html };
```

### JSON çıktısını dosyaya yazma

```csharp
byte[] bytes = LogFormatter.FormatToBytes(
  logs,
  LogFormat.Json,
  new LogFormatterOptions { AsFile = true, FileName = "logs.json" }
);
File.WriteAllBytes("logs.json", bytes);
```

---

## Dosya Okuma & Formatlama

```csharp
// log.txt, logs.csv, logs.json, logs.xml gibi dosyaları otomatik algılar:
string html = LogFormatter.FormatFile(
  "path/to/log.txt",
  LogFormat.Html,
  new LogFormatterOptions { UseColor = true }
);
```

---

## API Referansı

### `LogFormatter.Format`

```csharp
string Format(
  IEnumerable<LogEntry> logs,
  LogFormat format,
  LogFormatterOptions options
);
```

- **logs** : İşlenecek `LogEntry` listesi
- **format** : `Markdown`, `Html`, `Csv`, `Json`, `Xml`
- **options** : Renk, özet, grup, stil, katlanabilir detay gibi seçenekler

### `LogFormatter.FormatFile`

```csharp
string FormatFile(
  string filePath,
  LogFormat format,
  LogFormatterOptions options
);
```

- Dosyayı (`.txt`, `.json`, `.csv`, `.xml`) okuyup direkt formatlar

### `LogFormatterOptions`

| Özellik           | Açıklama                                                        | Varsayılan |
| ----------------- | --------------------------------------------------------------- | ---------- |
| UseColor          | Seviyeye göre renk uygulasın mı                                 | false      |
| AsFile            | Format sonucu byte[] dönsün mü (dosya için)                     | false      |
| FileName          | Dosya adı (AsFile=true olduğunda)                               | ""         |
| IncludeStyles     | HTML içinde dahili CSS eklesin mi                               | true       |
| EnableSummary     | Özet bilgisini eklesin mi                                       | true       |
| FoldLongMessages  | Uzun mesajları `<details>`ile katlasın mı                       | true       |
| FoldMessageLength | Katlama eşiği (karakter)                                        | 100        |
| GroupByProperty   | `LogEntry.Properties`içinden grup başlığı eklemek mi? (anahtar) | null       |

---

## Katkıda Bulunma

1. Forklayın
2. Feature branch açın
3. PR gönderin
4. Kodumuza bakılarak merge edilecektir

---

## Lisans

MIT © [Mehmet Akbaba]
