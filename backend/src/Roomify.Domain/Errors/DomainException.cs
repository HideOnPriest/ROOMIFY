namespace Roomify.Domain.Errors;

/// <summary>Kod pozwala rozróżniać reguły bez analizowania treści komunikatu.</summary>
public sealed class DomainException(DomainErrorKind kind, string code, string message) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
    public string Code { get; } = code;
}
