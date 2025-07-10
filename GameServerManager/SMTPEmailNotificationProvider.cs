using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;

namespace GameServerManager
{
    public class SMTPEmailNotificationProvider : INotificationProvider
    {
        private readonly IConfiguration _config;
        public SMTPEmailNotificationProvider(IConfiguration config)
        {
            _config = config;
        }
        public void Notify(string subject, string message)
        {
            var smtpHost = _config["Notification:SmtpHost"];
            if (!int.TryParse(_config["Notification:SmtpPort"], out var smtpPort))
            {
                throw new InvalidOperationException("SMTP port is not configured correctly.");
            }
            var smtpUser = _config["Notification:SmtpUser"];
            var smtpPass = _config["Notification:SmtpPass"];
            var recipient = _config["Notification:Recipient"];

            if (string.IsNullOrEmpty(smtpUser) || string.IsNullOrEmpty(recipient))
            {
                throw new InvalidOperationException("SMTP user or recipient cannot be null or empty.");
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                Credentials = new NetworkCredential(smtpUser, smtpPass),
                EnableSsl = true
            };
            var mail = new MailMessage(smtpUser, recipient, subject, message);
            client.Send(mail);
        }
    }
}
