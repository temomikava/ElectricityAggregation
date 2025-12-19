namespace Electricity.Domain.Enums;

public enum ProcessingStatus
{
    Started,
    Downloading,
    Parsing,
    Aggregating,
    Saving,
    Completed,
    Failed
}
