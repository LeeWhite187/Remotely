using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Remotely.Server.Data;

public class TestingDbContext : AppDb
{
    public TestingDbContext(IWebHostEnvironment hostEnvironment)
        : base(hostEnvironment)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseInMemoryDatabase("Remotely");
        // The in-memory provider doesn't support transactions. The multi-tenant
        // DataService methods (RemoveUserFromOrganization, SetMemberIsAdmin) wrap
        // their FR-15 / FR-24 lockout checks in BeginTransactionAsync per NFR-03;
        // those become no-ops here, which is acceptable for single-threaded tests.
        options.ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        base.OnConfiguring(options);
    }
}
