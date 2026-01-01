using System;
using System.IO;
using System.Xml.Serialization;
using DiskSpaceService.Models;

namespace DiskSpaceService.Services
{
    public class MissedRunDetector
    {
        private const string LastRunFileName = "LastRunInfo.xml";

        public DateTime LoadLastRunUtc()
        {
            if (!File.Exists(LastRunFileName))
                return DateTime.MinValue;

            try
            {
                var serializer = new XmlSerializer(typeof(LastRunInfo));
                using var stream = File.OpenRead(LastRunFileName);
                var info = (LastRunInfo)serializer.Deserialize(stream);
                return info.LastRunUtc;
            }
            catch
            {
                return DateTime.MinValue;
            }
        }

        public void SaveLastRunUtc()
        {
            var info = new LastRunInfo { LastRunUtc = DateTime.UtcNow };

            var serializer = new XmlSerializer(typeof(LastRunInfo));
            using var stream = File.Create(LastRunFileName);
            serializer.Serialize(stream, info);
        }
    }
}