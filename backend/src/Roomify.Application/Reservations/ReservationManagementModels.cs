namespace Roomify.Application.Reservations;

public sealed record UpdateReservationRequest(Guid ReservationId, string? Title, DateTimeOffset StartAt, DateTimeOffset EndAt);
public sealed record CancelReservationRequest(Guid ReservationId);
