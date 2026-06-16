namespace CrayonShinchanNotification;

public class MessageData
{
    public string Type { get; set; } = "text"; // "text" or "image"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
