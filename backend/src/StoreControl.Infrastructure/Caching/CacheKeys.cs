using System.Security.Cryptography;
using System.Text;

namespace StoreControl.Infrastructure.Caching;

internal static class CacheKeys
{
    public static string Build(string prefix, string region, Guid generation, string variant) =>
        $"{prefix}:v1:{region}:{generation:N}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(variant)))}";

    public static string Dashboard(DateOnly date, int? requestedStoreId, IEnumerable<int> storeIds) =>
        FormattableString.Invariant($"{date:yyyy-MM-dd}|{requestedStoreId}|{string.Join(',', storeIds.Distinct().Order())}");
}
