namespace StorageWatch.Models;

public class AlertDto
{
    public string DriveLetter { get; set; } = string.Empty;

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}
