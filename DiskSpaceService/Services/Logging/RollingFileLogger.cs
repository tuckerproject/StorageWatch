using System;
using System.IO;
using System.Text;

namespace DiskSpaceService.Services.Logging
{
    public class RollingFileLogger
    {
        private readonly string _logDirectory;
        private readonly string _baseFileName;
        private readonly long _maxFileSizeBytes = 1 * 1024 * 1024; // 1 MB
        private readonly int _maxFiles = 3;
        private readonly object _lock = new();

        public RollingFileLogger(string logDirectory, string baseFileName = "DiskSpaceService")
        {
            _logDirectory = logDirectory;
            _baseFileName = baseFileName;

            Directory.CreateDirectory(_logDirectory);
        }

        public void Log(string message)
        {
            lock (_lock)
            {
                string filePath = GetCurrentLogFilePath();
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string line = $"{timestamp}  {message}{Environment.NewLine}";

                File.AppendAllText(filePath, line, Encoding.UTF8);

                RollIfNeeded(filePath);
            }
        }

        private string GetCurrentLogFilePath()
        {
            return Path.Combine(_logDirectory, $"{_baseFileName}.log");
        }

        private void RollIfNeeded(string filePath)
        {
            FileInfo fi = new(filePath);
            if (fi.Length <= _maxFileSizeBytes)
                return;

            // Delete oldest
            string oldest = $"{filePath}.3";
            if (File.Exists(oldest))
                File.Delete(oldest);

            // Shift .2 → .3
            string two = $"{filePath}.2";
            if (File.Exists(two))
                File.Move(two, $"{filePath}.3");

            // Shift .1 → .2
            string one = $"{filePath}.1";
            if (File.Exists(one))
                File.Move(one, $"{filePath}.2");

            // Shift current → .1
            File.Move(filePath, $"{filePath}.1");
        }
    }
}