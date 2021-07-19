using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace WalletWasabi.Standard.Backend.Models
{
	[Index(nameof(Token), IsUnique = true)]
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
