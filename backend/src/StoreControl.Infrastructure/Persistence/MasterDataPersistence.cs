using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StoreControl.Application.Common.Exceptions;

namespace StoreControl.Infrastructure.Persistence;

internal static class MasterDataPersistence
{
    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    public static byte[] ParseRowVersion(string rowVersion)
    {
        try
        {
            var bytes = Convert.FromBase64String(rowVersion);
            if (bytes.Length != 8)
            {
                throw new FormatException();
            }

            return bytes;
        }
        catch (FormatException)
        {
            throw new RequestValidationException("rowVersion", "A versão de concorrência é inválida.");
        }
    }

    public static bool ValidateQuery(
        int page,
        int pageSize,
        string sortBy,
        IReadOnlyCollection<string> allowedSorts,
        string sortDirection)
    {
        if (page < 1)
        {
            throw new RequestValidationException("page", "A página deve ser maior ou igual a 1.");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new RequestValidationException("pageSize", "O tamanho da página deve estar entre 1 e 100.");
        }

        if (!allowedSorts.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
        {
            throw new RequestValidationException(
                "sortBy",
                $"Ordenação inválida. Valores aceitos: {string.Join(", ", allowedSorts)}.");
        }

        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("sortDirection", "Use asc ou desc para a direção da ordenação.");
        }

        return string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
    }
}
