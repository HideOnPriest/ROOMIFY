using Roomify.Domain.Errors;

namespace Roomify.Domain;

internal static class DomainGuard
{
    internal static string RequiredText(string? value, int maxLength, string code)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > maxLength)
        {
            throw new DomainException(DomainErrorKind.InvalidInput, code,
                $"Wartość jest wymagana i może mieć najwyżej {maxLength} znaków po usunięciu skrajnych spacji.");
        }

        return normalized;
    }

    internal static void RequireId(Guid id, string code)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException(DomainErrorKind.InvalidInput, code, "Identyfikator nie może być pusty.");
        }
    }
}
