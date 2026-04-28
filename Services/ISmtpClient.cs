using System.Net.Mail;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public interface ISmtpClient
    {
        Task SendMailAsync(MailMessage mailMessage);
    }
}
