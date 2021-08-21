using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

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

		public StringContent ToHttpStringContent()
		{
			string jsonString = JsonConvert.SerializeObject(this, Formatting.None);
			return new StringContent(jsonString, Encoding.UTF8, "application/json");
		}
	}
}
