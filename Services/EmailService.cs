using CenzasBackend.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace CenzasBackend.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;
    private readonly ISmtpClientFactory _smtpClientFactory;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger, ISmtpClientFactory smtpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _smtpClientFactory = smtpClientFactory;
    }

    public async Task SendApplicationEmailAsync(LoanApplication application)
    {
        // 1. Data Preparation (Email Body Formatting)
        var body = $@"
            <html>
            <body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333;'>
                <h2 style='color: #176be0;'>Gauta nauja paskolos paraiška</h2>
                <p><strong>Vardas ir Pavardė:</strong> {application.Name}</p>
                <p><strong>El. paštas:</strong> {application.Email}</p>
                <p><strong>Tel. numeris:</strong> {application.Phone}</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                <p><strong>Norima suma:</strong> {application.Amount} €</p>
                <p><strong>Paskolos terminas:</strong> {application.LoanTerm}</p>
                <p><strong>Turimas NT:</strong> {application.PropertyType}</p>
                <p><strong>NT Adresas:</strong> {application.PropertyAddress}</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                <p><strong>Kita informacija:</strong></p>
                <p style='background: #f9f9f9; padding: 15px; border-radius: 5px;'>{application.Other}</p>
            </body>
            </html>";

        // 2. Email Messaging
        using (var mailMessage = new MailMessage())
        {
            mailMessage.From = new MailAddress(_settings.SenderEmail);
            mailMessage.To.Add(_settings.RecipientEmail);
            mailMessage.Subject = _settings.SubjectLine;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = true;

            // 3. SMTP Dispatch
            var credentials = new NetworkCredential(_settings.SenderEmail, _settings.SenderPassword);
            var client = _smtpClientFactory.CreateClient(_settings.SmtpServer, _settings.SmtpPort, credentials, _settings.EnableSsl);

            try
            {
                _logger.LogInformation("Attempting to send email via {SmtpServer}:{SmtpPort} to {RecipientEmail}", 
                    _settings.SmtpServer, _settings.SmtpPort, _settings.RecipientEmail);
                
                await client.SendMailAsync(mailMessage);
                
                _logger.LogInformation("Email successfully delivered to: {RecipientEmail}", _settings.RecipientEmail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "STMP Error: Critical failure while sending email to {RecipientEmail}", _settings.RecipientEmail);
                throw; // Re-throw to inform the controller
            }
        }
    }
}
