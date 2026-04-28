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
Gauta nauja paskolos paraiška:

Vardas: {application.FirstName}
Pavardė: {application.LastName}
El. paštas: {application.Email}
Tel. numeris: {application.Phone}

Norima suma: {application.Amount}
Paskolos terminas: {application.LoanTerm}

Turimas NT: {application.PropertyType}
NT Adresas: {application.PropertyAddress}

Kita informacija:
{application.Other}
        ";

        // 2. Email Messaging
        using (var mailMessage = new MailMessage())
        {
            mailMessage.From = new MailAddress(_settings.SenderEmail);
            mailMessage.To.Add(_settings.RecipientEmail);
            mailMessage.Subject = _settings.SubjectLine;
            mailMessage.Body = body;
            mailMessage.IsBodyHtml = false;

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
