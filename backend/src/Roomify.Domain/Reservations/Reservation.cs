using Roomify.Domain.Errors;

namespace Roomify.Domain.Reservations;

/// <summary>
/// Rezerwacja chroni własne dane i cykl życia. Weryfikacja istnienia sali/konta,
/// uprawnień i konfliktów z innymi rekordami należy do wyższych warstw.
/// </summary>
public sealed class Reservation
{
    private static readonly ReservationTimePolicy TimePolicy = new();
    private DateTimeOffset _startAt;
    private DateTimeOffset _endAt;

    public Guid Id { get; }
    public Guid RoomId { get; }
    public Guid UserId { get; }
    public string Title { get; private set; }
    // Jedna para pól jest źródłem prawdy. Odtworzony value object zawsze sprawdza BR-001.
    public ReservationPeriod Period
    {
        get => new(_startAt, _endAt);
        private set
        {
            _startAt = value.Start;
            _endAt = value.End;
        }
    }
    public DateTimeOffset StartAt => _startAt;
    public DateTimeOffset EndAt => _endAt;
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Reservation(Guid roomId, Guid userId, string title, ReservationPeriod period, DateTimeOffset now)
        : this(Guid.NewGuid(), roomId, userId, title, period.Start, period.End, ReservationStatus.Active, now, null)
    {
    }

    // Konstruktor skalarny umożliwia standardowe odtworzenie także historycznych danych.
    // Nie powtarza reguły przyszłości, która dotyczy nowych operacji, a nie odczytu historii.
    private Reservation(Guid id, Guid roomId, Guid userId, string title, DateTimeOffset startAt,
        DateTimeOffset endAt, ReservationStatus status, DateTimeOffset createdAt, DateTimeOffset? updatedAt)
    {
        DomainGuard.RequireId(roomId, "reservation.roomId");
        DomainGuard.RequireId(userId, "reservation.userId");
        Id = id;
        RoomId = roomId;
        UserId = userId;
        Title = DomainGuard.RequiredText(title, 120, "BR-024");
        Period = new ReservationPeriod(startAt, endAt);
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Reservation Create(Guid roomId, Guid userId, string? title, ReservationPeriod period,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        DomainGuard.RequireId(roomId, "reservation.roomId");
        DomainGuard.RequireId(userId, "reservation.userId");
        var validTitle = DomainGuard.RequiredText(title, 120, "BR-024");
        var now = timeProvider.GetUtcNow();
        TimePolicy.Validate(period, now);
        return new Reservation(roomId, userId, validTitle, period, now);
    }

    public void ChangePeriod(ReservationPeriod period, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow();
        EnsureCanChange(now);
        TimePolicy.Validate(period, now);
        Period = period;
        UpdatedAt = now;
    }

    /// <summary>Sprawdza planowaną edycję bez zmiany stanu, przed odczytem dostępności.</summary>
    public void ValidateUpdate(string? title, ReservationPeriod period, TimeProvider timeProvider)
        => _ = ValidateDetails(title, period, timeProvider);

    public void UpdateDetails(string? title, ReservationPeriod period, TimeProvider timeProvider)
    {
        // Całość jest walidowana przed pierwszym przypisaniem; edycja nie zmienia właściciela ani sali.
        var valid = ValidateDetails(title, period, timeProvider);
        Title = valid.Title;
        Period = period;
        UpdatedAt = valid.Now;
    }

    private (string Title, DateTimeOffset Now) ValidateDetails(string? title, ReservationPeriod period, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow();
        EnsureCanChange(now);
        var validTitle = DomainGuard.RequiredText(title, 120, "BR-024");
        TimePolicy.Validate(period, now);
        return (validTitle, now);
    }

    public void Cancel(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        // Powtórzenie nie zmienia metadanych ani nie tworzy nowego przejścia (BR-027).
        if (Status == ReservationStatus.Cancelled)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        EnsureCanChange(now);
        Status = ReservationStatus.Cancelled;
        UpdatedAt = now;
    }

    /// <summary>Sprawdza zajętość tej rezerwacji; wybór sali i zbioru rekordów należy do Application.</summary>
    public bool Blocks(ReservationPeriod period)
    {
        ArgumentNullException.ThrowIfNull(period);
        return Status == ReservationStatus.Active && Period.Overlaps(period);
    }

    private void EnsureCanChange(DateTimeOffset now)
    {
        if (Status != ReservationStatus.Active || StartAt <= now)
        {
            throw new DomainException(DomainErrorKind.InvalidState, "BR-017", "Zmiana wymaga aktywnej rezerwacji, która jeszcze się nie rozpoczęła.");
        }
    }
}
