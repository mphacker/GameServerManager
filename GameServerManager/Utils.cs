using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace GameServerManager
{
    public static class Utils
    {
        public static void Log(string message)
        {
            // Log the message to the console
            Console.WriteLine($"[{DateTime.Now}] {message}");

            string logFileName = $"GameServerManager_{DateTime.Now:yyyy-MM-dd}.log";
            string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logFileName);
            using (StreamWriter writer = new StreamWriter(logFilePath, true))
            {
                writer.WriteLine($"[{DateTime.Now}] {message}");
            }

            // Call method to delete old log files
            DeleteOldLogFiles();
        }

        private static void DeleteOldLogFiles()
        {
            string logDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var logFiles = Directory.GetFiles(logDirectory, "GameServerManager_*.log");

            foreach (var logFile in logFiles)
            {
                var creationTime = File.GetCreationTime(logFile);
                if ((DateTime.Now - creationTime).TotalDays > 10)
                {
                    File.Delete(logFile);
                }
            }
        }
    }
}
