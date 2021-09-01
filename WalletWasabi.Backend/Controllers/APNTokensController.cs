using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
		private readonly IDbContextFactory<WasabiBackendContext> ContextFactory;

		public APNTokensController(IDbContextFactory<WasabiBackendContext> contextFactory)
		{
			ContextFactory = contextFactory;
		}

		[HttpPut]
		[ProducesResponseType(200)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> StoreTokenAsync([FromBody] DeviceToken token)
		{
			if (!ModelState.IsValid)
			{
				return BadRequest("Invalid device token.");
			}

			await using var context = ContextFactory.CreateDbContext();
			var existingToken = await context.Tokens.FindAsync(token.Token);
			if(existingToken != null)
			{
				existingToken.Status = token.Status;
				existingToken.Type = token.Type;
				return Ok("Device token stored.");
			}
			await context.Tokens.AddAsync(token);
			await context.SaveChangesAsync();
			return Ok("Device token stored.");
		}

		/// <summary>
		/// Removes a device token so that device stops receiving notifications.
		/// </summary>
		/// <param name="tokenString">An Apple device token</param>
		/// <response code="200">Always return Ok, we should not confirm whether a token is in the db or not here</response>
		[HttpDelete("{tokenString}")]
		[ProducesResponseType(200)]
		public async Task<IActionResult> DeleteTokenAsync([FromRoute] string tokenString)
		{
			await using var context = ContextFactory.CreateDbContext();
			var token = await context.Tokens.FindAsync(tokenString);
			if (token != null)
			{
				context.Tokens.Remove(token);
				await context.SaveChangesAsync();
			}
			return Ok();
		}
	}
}
