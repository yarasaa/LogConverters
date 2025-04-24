namespace LogConverter.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Message { get; set; } = "";

    // Opsiyonel: hata varsa stack trace vs.
    public string? Exception { get; set; }
    // Opsiyonel: event ID, kullanıcı ID, vb.
    public string? EventId { get; set; }

    // Herhangi başka meta-veriyi ekleyebilmek için:
    public Dictionary<string, object?> Properties { get; } = new();

    // HTML/CSS renk kodu
    public string LevelColor =>
        Level.ToUpper() switch
        {
            "ERROR" => "red",
            "WARNING" => "orange",
            "DEBUG" => "gray",
            "INFO" => "green",
            _ => "black"
        };
}
