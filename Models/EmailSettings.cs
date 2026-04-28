namespace CenzasBackend.Models;

public class EmailSettings
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderPassword { get; set; } = string.Empty;
    public bool EnableSsl { get; set; }
    public string SubjectLine { get; set; } = string.Empty;
}
