using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CenzasBackend.Services
{
    public class SmtpClientWrapper : ISmtpClient
    {
        private readonly string _host;
        private readonly int _port;
        private readonly NetworkCredential _credentials;
        private readonly bool _enableSsl;

        public SmtpClientWrapper(string host, int port, NetworkCredential credentials, bool enableSsl)
        {
            _host = host;
            _port = port;
            _credentials = credentials;
            _enableSsl = enableSsl;
        }

        public async Task SendMailAsync(MailMessage mailMessage)
        {
            using (var client = new SmtpClient(_host, _port))
            {
                client.UseDefaultCredentials = false;
                client.Credentials = _credentials;
                client.EnableSsl = _enableSsl;
                await client.SendMailAsync(mailMessage);
            }
        }
    }

    public interface ISmtpClientFactory
    {
        ISmtpClient CreateClient(string host, int port, NetworkCredential credentials, bool enableSsl);
    }

    public class SmtpClientFactory : ISmtpClientFactory
    {
        public ISmtpClient CreateClient(string host, int port, NetworkCredential credentials, bool enableSsl)
        {
            return new SmtpClientWrapper(host, port, credentials, enableSsl);
        }
    }
}
