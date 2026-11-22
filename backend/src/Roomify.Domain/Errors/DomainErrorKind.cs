namespace Roomify.Domain.Errors;

/// <summary>Kategorie naruszeń domeny niezależne od protokołu komunikacji.</summary>
public enum DomainErrorKind
{
    InvalidInput,
    InvalidState,
    InvalidPeriod,
}
