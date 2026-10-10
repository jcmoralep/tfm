using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace BmadPlatform.Infrastructure.Persistence;

/// <summary>Recognises provider errors in one place, so repositories do not depend on driver details.</summary>
internal static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is MySqlException { ErrorCode: MySqlErrorCode.DuplicateKeyEntry })
            {
                return true;
            }
        }

        return false;
    }
}
