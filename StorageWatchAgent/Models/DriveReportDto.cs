namespace StorageWatch.Models;

public class DriveReportDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public double TotalSpaceGb { get; set; }

    public double FreeSpaceGb { get; set; }

    public double UsedPercent { get; set; }
}
