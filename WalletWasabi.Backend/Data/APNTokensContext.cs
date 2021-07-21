using Microsoft.EntityFrameworkCore;
using WalletWasabi.Backend.Models;

namespace WalletWasabi.Backend.Data
{
	public class APNTokensContext : DbContext
	{
		public DbSet<AppleDeviceToken> Tokens { get; set; }

		protected override void OnConfiguring (DbContextOptionsBuilder builder)
		{
			builder.UseNpgsql("Host=localhost;Database=postgres;Username=dan;Password=;");
		}
	}
}
