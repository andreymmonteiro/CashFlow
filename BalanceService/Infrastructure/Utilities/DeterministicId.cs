using System.Security.Cryptography;
using System.Text;

namespace BalanceService.Infrastructure.Utilities
{
    public static class DeterministicId
    {
        public static Guid For(string accountId, DateTime timestamp)
        {
            var bytes = Encoding.UTF8.GetBytes($"{accountId}-{timestamp.Ticks}");
            using var sha1 = SHA1.Create();
            var hash = sha1.ComputeHash(bytes);
            var guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, 16);
            return new Guid(guidBytes);
        }
    }
}
