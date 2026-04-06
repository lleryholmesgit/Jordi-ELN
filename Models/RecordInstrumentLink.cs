namespace ElectronicLabNotebook.Models;

public sealed class RecordInstrumentLink
{
    public int ExperimentRecordId { get; set; }

    public ExperimentRecord? ExperimentRecord { get; set; }

    public int InstrumentId { get; set; }

    public Instrument? Instrument { get; set; }

    public string LinkedByUserId { get; set; } = string.Empty;

    public DateTimeOffset LinkedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string UsageNote { get; set; } = string.Empty;

    public decimal? UsageHours { get; set; }
}
