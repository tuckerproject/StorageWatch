using System.Collections.Generic;
using System.Xml.Serialization;

namespace DiskSpaceService.Config
{
    [XmlRoot("DiskSpaceServiceConfig")]
    public class ServiceConfig
    {
        public string CollectionTime { get; set; }
        public bool RunMissedCollection { get; set; }

        [XmlArray("Drives")]
        [XmlArrayItem("Drive")]
        public List<string> Drives { get; set; }

        public DatabaseConfig Database { get; set; }

        public AlertConfig Alert { get; set; }
    }

    public class DatabaseConfig
    {
        public string ConnectionString { get; set; }
    }

    public class AlertConfig
    {
        public decimal ThresholdPercent { get; set; }
        public string GroupMeBotId { get; set; }
    }
}