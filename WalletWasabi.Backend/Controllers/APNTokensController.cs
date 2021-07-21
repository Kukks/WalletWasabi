using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Data;
using WalletWasabi.Backend.Models;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend.Controllers
{
	[Route("api/v" + Constants.BackendMajorVersion + "/btc/[controller]")]
	[ApiController]
	[Produces("application/json")]
	public class APNTokensController : ControllerBase
	{
		private readonly APNTokensContext Db;

		public APNTokensController(APNTokensContext db)
		{
			Db = db;
		}

		[HttpPost]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> StoreTokenAsync([FromBody] AppleDeviceToken token)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest("Invalid Apple device token.");
			}
			Db.Tokens.Add(token);
			await Db.SaveChangesAsync();
			return Ok("Device token stored.");
		}

		/// <summary>
		/// Removes a device token so that device stops receiving notifications.
		/// </summary>
		/// <param name="tokenString">An Apple device token</param>
		/// <response code="200">Always return Ok, we should not confirm whether a token is in the db or not here</response>
		[HttpDelete]
		[ProducesResponseType(200)]
		public async Task<IActionResult> DeleteTokenAsync([FromRoute] string tokenString)
		{
			var token = await Db.Tokens.FindAsync(tokenString);
			if (token != null)
			{
				Db.Tokens.Remove(token);
				await Db.SaveChangesAsync();
			}
			return Ok();
		}
	}
}
