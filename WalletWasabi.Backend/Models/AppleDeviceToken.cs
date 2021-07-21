using System.ComponentModel.DataAnnotations;

namespace WalletWasabi.Backend.Models
{
	public class AppleDeviceToken
	{
		[Key]
		public int Id { get; set; }

		[Required]
		public string Token { get; set; }

		[Required]
		public bool IsDebug { get; set; }
	}
}
