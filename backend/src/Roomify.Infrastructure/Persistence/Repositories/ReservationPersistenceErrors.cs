using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace Roomify.Infrastructure.Persistence.Repositories;

internal static class ReservationPersistenceErrors
{
    internal static bool IsStorageFailure(Exception error) => error is DbException or DbUpdateException or TimeoutException
        // NpgsqlExecutionStrategy opakowuje błędy przejściowe (m.in. 40P01) także bez włączonego retry.
        || error is InvalidOperationException { InnerException: { } inner } && IsStorageFailure(inner);
}
