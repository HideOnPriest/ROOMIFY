using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata;
using Roomify.Domain.Reservations;
using Roomify.Domain.Rooms;
using Roomify.Infrastructure.Identity;

namespace Roomify.Api.IntegrationTests;

internal static class IdentityModelAssertions
{
    internal static void AssertExpectedEntityTypes(IModel model)
    {
        var expected = new[] { typeof(Room), typeof(Reservation), typeof(ApplicationUser), typeof(IdentityRole<Guid>),
            typeof(IdentityUserRole<Guid>), typeof(IdentityUserClaim<Guid>), typeof(IdentityUserLogin<Guid>),
            typeof(IdentityUserToken<Guid>), typeof(IdentityRoleClaim<Guid>) };
        Assert.Equal(expected.OrderBy(x => x.FullName), model.GetEntityTypes().Select(x => x.ClrType).OrderBy(x => x.FullName));
    }
}
