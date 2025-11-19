using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GameServerManager
{
    public class NotificationManager
    {
        private readonly List<INotificationProvider> _providers = new();
        
        public NotificationManager(IConfiguration config)
        {
            try
            {
                if (config.GetValue<bool>("Notification:EnableEmail"))
                {
                    _providers.Add(new SMTPEmailNotificationProvider(config));
                    Log.Information("Email notifications enabled");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize notification providers: {Message}", ex.Message);
            }
        }
        
        public void NotifyError(string subject, string message)
        {
            if (_providers.Count == 0)
            {
                Log.Debug("No notification providers configured, skipping notification");
                return;
            }
            
            foreach (var provider in _providers)
            {
                try
                {
                    provider.Notify(subject, message);
                    Log.Debug("Notification sent successfully via {Provider}: {Subject}", provider.GetType().Name, subject);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to send notification via {Provider}: {Message}", provider.GetType().Name, ex.Message);
                }
            }
        }
    }
}
