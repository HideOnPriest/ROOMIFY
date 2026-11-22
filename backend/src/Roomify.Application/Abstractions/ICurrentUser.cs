namespace Roomify.Application.Abstractions;

/// <summary>Zaufany adapter tożsamości; identyfikator nie pochodzi z danych rezerwacji.</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    // Domyślnie brak uprawnień, także dla adapterów WP-03.1, które nie obsługują jeszcze ról.
    bool IsAdministrator => false;
    // Adapter musi jawnie potwierdzić aktywność. Starszy adapter bez tej informacji odmawia dostępu.
    bool IsActive => false;
}

/// <summary>Bez podłączonego adaptera tożsamości operacja pozostaje niedostępna.</summary>
internal sealed class UnauthenticatedCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
}
