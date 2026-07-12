namespace Trading.Application.Models;

/// <summary>
/// Result of a single Groww HTTP call. IsAmbiguous distinguishes "Groww definitely said no"
/// (a parsed 4xx rejection) from "we don't know what happened" (timeout/network/5xx) — an
/// order-create call in the ambiguous bucket must NOT be treated as failed, since Groww may
/// have processed it anyway; the caller reconciles later via OrderReferenceId.
/// </summary>
public class GrowwHttpResult<T>
{
    public bool IsSuccess { get; init; }
    public bool IsAmbiguous { get; init; }
    public T? Data { get; init; }
    public int? StatusCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ResponseBody { get; init; }
}
