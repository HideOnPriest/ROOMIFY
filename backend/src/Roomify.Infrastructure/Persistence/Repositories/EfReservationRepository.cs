using Microsoft.EntityFrameworkCore;
using Npgsql;
using Roomify.Application.Reservations;
using Roomify.Domain.Reservations;

namespace Roomify.Infrastructure.Persistence.Repositories;

public sealed class EfReservationRepository(RoomifyDbContext context) : IReservationRepository
{
    public async Task<bool> HasActiveConflictAsync(Guid roomId, ReservationPeriod period, CancellationToken cancellationToken)
    {
        try
        {
            // AnyAsync daje EXISTS w SQL. Nie materializujemy listy ani nie ograniczamy po dacie kalendarzowej.
            return await context.Reservations.AsNoTracking().AnyAsync(reservation => reservation.RoomId == roomId
                && reservation.Status == ReservationStatus.Active
                && reservation.StartAt < period.End && reservation.EndAt > period.Start, cancellationToken);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            throw new ReservationStorageException();
        }
    }

    public async Task<ReservationWriteOutcome> AddAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        context.Reservations.Add(reservation);
        return await PersistAsync(reservation, cancellationToken);
    }

    public async Task<Reservation?> FindByIdAsync(Guid reservationId, CancellationToken cancellationToken)
    {
        try
        {
            // Odczyt do modyfikacji: tracking zachowuje oryginalne xmin dla warunku UPDATE.
            return await context.Reservations.SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            throw new ReservationStorageException();
        }
    }

    public async Task<bool> HasActiveConflictExcludingAsync(Guid roomId, ReservationPeriod period,
        Guid excludedReservationId, CancellationToken cancellationToken)
    {
        try
        {
            return await context.Reservations.AsNoTracking().AnyAsync(x => x.RoomId == roomId
                && x.Id != excludedReservationId && x.Status == ReservationStatus.Active
                && x.StartAt < period.End && x.EndAt > period.Start, cancellationToken);
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            throw new ReservationStorageException();
        }
    }

    public Task<ReservationWriteOutcome> SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        if (context.Entry(reservation).State == EntityState.Detached)
            throw new InvalidOperationException("Zapis wymaga encji odczytanej ze śledzeniem w tym zakresie.");
        return PersistAsync(reservation, cancellationToken);
    }

    private async Task<ReservationWriteOutcome> PersistAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return ReservationWriteOutcome.Saved;
        }
        catch (DbUpdateConcurrencyException)
        {
            context.Entry(reservation).State = EntityState.Detached;
            return ReservationWriteOutcome.ConcurrentModification;
        }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.ExclusionViolation, ConstraintName: "ex_reservations_active_period" })
        {
            context.Entry(reservation).State = EntityState.Detached;
            return ReservationWriteOutcome.Conflict;
        }
        catch (Exception error) when (ReservationPersistenceErrors.IsStorageFailure(error))
        {
            // FK, UNIQUE, inny exclusion i deadlock nie stają się konfliktem terminów.
            context.Entry(reservation).State = EntityState.Detached;
            throw new ReservationStorageException();
        }
        catch (OperationCanceledException)
        {
            context.Entry(reservation).State = EntityState.Detached;
            throw;
        }
    }
}
