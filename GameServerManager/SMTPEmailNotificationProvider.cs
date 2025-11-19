using System;
using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GameServerManager
{
    public class SMTPEmailNotificationProvider : INotificationProvider
    {
        private readonly IConfiguration _config;
        
        public SMTPEmailNotificationProvider(IConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }
        
        public void Notify(string subject, string message)
        {
            try
            {
                var smtpHost = _config["Notification:SmtpHost"];
                if (string.IsNullOrWhiteSpace(smtpHost))
                {
                    Log.Error("SMTP host is not configured");
                    return;
                }
                
                if (!int.TryParse(_config["Notification:SmtpPort"], out var smtpPort))
                {
                    Log.Error("SMTP port is not configured correctly");
                    return;
                }
                
                var smtpUser = _config["Notification:SmtpUser"];
                var smtpPass = _config["Notification:SmtpPass"];
                var recipient = _config["Notification:Recipient"];

                if (string.IsNullOrWhiteSpace(smtpUser))
                {
                    Log.Error("SMTP user is not configured");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(recipient))
                {
                    Log.Error("Email recipient is not configured");
                    return;
                }

                using var client = new SmtpClient(smtpHost, smtpPort)
                {
                    Credentials = new NetworkCredential(smtpUser, smtpPass),
                    EnableSsl = true,
                    Timeout = 30000 // 30 second timeout
                };
                
                using var mail = new MailMessage(smtpUser, recipient, subject, message);
                client.Send(mail);
                
                Log.Debug("Email sent successfully to {Recipient}: {Subject}", recipient, subject);
            }
            catch (SmtpException smtpEx)
            {
                Log.Error(smtpEx, "SMTP error sending email notification: {Message}", smtpEx.Message);
            }
            catch (InvalidOperationException ioEx)
            {
                Log.Error(ioEx, "Invalid SMTP configuration: {Message}", ioEx.Message);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error sending email notification: {Message}", ex.Message);
            }
        }
    }
}
