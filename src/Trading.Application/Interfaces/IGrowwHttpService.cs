using Trading.Application.Models;

namespace Trading.Application.Interfaces;

/// <summary>
/// The single entry point for every Groww API call — no service should call
/// HttpClient.GetAsync/PostAsJsonAsync directly. Attaches auth, sends, deserializes safely,
/// and on failure logs one rich AppError row (masked curl + request/response + signal
/// context) instead of each caller doing its own error handling.
/// </summary>
public interface IGrowwHttpService
{
    Task<GrowwHttpResult<T>> GetAsync<T>(string url, string? signalId = null, string? orderReferenceId = null);
    Task<GrowwHttpResult<T>> PostAsync<T>(string url, object body, string? signalId = null, string? orderReferenceId = null);
    Task<GrowwHttpResult<T>> PutAsync<T>(string url, object body, string? signalId = null, string? orderReferenceId = null);
}
