using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DiskSpaceService.Services.Alerts
{
    public class AlertStateStore
    {
        private readonly string _filePath;

        public AlertStateStore(string filePath)
        {
            _filePath = filePath;
        }

        public Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                    return new Dictionary<string, string>();

                string json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public void Save(Dictionary<string, string> state)
        {
            string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}