using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace GameServerManager
{
    public class NotificationManager
    {
        private readonly List<INotificationProvider> _providers = new();
        public NotificationManager(IConfiguration config)
        {
            if (config.GetValue<bool>("Notification:EnableEmail"))
                _providers.Add(new SMTPEmailNotificationProvider(config));
        }
        public void NotifyError(string subject, string message)
        {
            foreach (var provider in _providers)
            {
                try { provider.Notify(subject, message); } catch { /* Optionally log provider failure */ }
            }
        }
    }
}
