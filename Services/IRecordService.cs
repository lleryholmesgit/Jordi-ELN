using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public interface IRecordService
{
    Task<IReadOnlyList<ExperimentRecord>> SearchAsync(RecordSearchRequest request, CancellationToken cancellationToken = default);
    Task<ExperimentRecord?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<string> GetSuggestedExperimentCodeAsync(string projectName, string client, int? recordId = null, CancellationToken cancellationToken = default);
    Task<ExperimentRecord> CreateAsync(RecordSaveRequest request, string actorUserId, string sourceClient, IEnumerable<IFormFile>? files = null, CancellationToken cancellationToken = default);
    Task<ExperimentRecord?> UpdateAsync(int id, RecordSaveRequest request, string actorUserId, string sourceClient, IEnumerable<IFormFile>? files = null, CancellationToken cancellationToken = default);
    Task<bool> SubmitAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(int id, string actorUserId, string comment, string sourceClient, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(int id, string actorUserId, string comment, string sourceClient, CancellationToken cancellationToken = default);
    Task<ExperimentAttachment?> RemoveAttachmentAsync(int attachmentId, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
}
