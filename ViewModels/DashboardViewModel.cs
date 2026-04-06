using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.ViewModels;

public sealed class DashboardViewModel
{
    public required IReadOnlyList<ExperimentRecord> RecentRecords { get; init; }
    public required IReadOnlyList<Instrument> RecentInstruments { get; init; }
    public required IReadOnlyList<AuditLog> RecentAuditLogs { get; init; }
    public int DraftCount { get; init; }
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
}