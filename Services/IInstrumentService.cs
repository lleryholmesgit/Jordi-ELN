using ElectronicLabNotebook.Models;

namespace ElectronicLabNotebook.Services;

public interface IInstrumentService
{
    Task<IReadOnlyList<Instrument>> SearchAsync(InstrumentSearchRequest request, CancellationToken cancellationToken = default);
    Task<Instrument?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<Instrument?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Instrument?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<Instrument> CreateAsync(InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
    Task<Instrument?> UpdateAsync(int id, InstrumentSaveRequest request, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
    Task<Instrument?> RefreshQrAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, string actorUserId, string sourceClient, CancellationToken cancellationToken = default);
}
