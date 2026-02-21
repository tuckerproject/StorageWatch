using StorageWatch.Models;

namespace StorageWatch.Services.Monitoring
{
    public interface IDiskStatusProvider
    {
        DiskStatus GetStatus(string driveLetter);
    }
}
