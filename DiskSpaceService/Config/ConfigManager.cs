using System.IO;
using System.Xml.Serialization;

namespace DiskSpaceService.Config
{
    public static class ConfigManager
    {
        private static readonly string ConfigFilePath = Path.Combine(AppContext.BaseDirectory, "DiskSpaceConfig.xml");

        public static ServiceConfig Load()
        {
            if (!File.Exists(ConfigFilePath))
                throw new FileNotFoundException($"Configuration file '{ConfigFilePath}' not found.");

            var serializer = new XmlSerializer(typeof(ServiceConfig));

            using var stream = File.OpenRead(ConfigFilePath);
            return (ServiceConfig)serializer.Deserialize(stream);
        }
    }
}