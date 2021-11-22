using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WalletWasabi.Backend.Filters;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.Backend.Controllers
{
	[ApiController]
	[ExceptionTranslate]
	[Route("[controller]")]
	[Produces("application/json")]
	public class WabiSabiController : ControllerBase, IWabiSabiApiRequestHandler
	{
		public WabiSabiController(Arena arena)
		{
			Arena = arena;
		}

		private Arena Arena { get; }

		[HttpGet("status")]
		public Task<RoundState[]> GetStatusAsync(CancellationToken cancellationToken)
		{
			return Arena.GetStatusAsync(cancellationToken);
		}

		[HttpPost("connection-confirmation")]
		public Task<ConnectionConfirmationResponse> ConfirmConnectionAsync(ConnectionConfirmationRequest request, CancellationToken cancellableToken)
		{
			// TODO: Add idempotent.
			return Arena.ConfirmConnectionAsync(request, cancellableToken);
		}

		[HttpPost("input-registration")]
		public Task<InputRegistrationResponse> RegisterInputAsync(InputRegistrationRequest request, CancellationToken cancellableToken)
		{
			// TODO: Add idempotent.
			return Arena.RegisterInputAsync(request, cancellableToken);
		}

		[HttpPost("output-registration")]
		public Task RegisterOutputAsync(OutputRegistrationRequest request, CancellationToken cancellableToken)
		{
			// TODO: Add idempotent.
			return Arena.RegisterOutputAsync(request, cancellableToken);
		}

		[HttpPost("credential-issuance")]
		public Task<ReissueCredentialResponse> ReissuanceAsync(ReissueCredentialRequest request, CancellationToken cancellableToken)
		{
			// TODO: Add idempotent.
			return Arena.ReissuanceAsync(request, cancellableToken);
		}

		[HttpPost("input-unregistration")]
		public Task RemoveInputAsync(InputsRemovalRequest request, CancellationToken cancellableToken)
		{
			return Arena.RemoveInputAsync(request, cancellableToken);
		}

		[HttpPost("transaction-signature")]
		public Task SignTransactionAsync(TransactionSignaturesRequest request, CancellationToken cancellableToken)
		{
			return Arena.SignTransactionAsync(request, cancellableToken);
		}

		[HttpPost("ready-to-sign")]
		public Task ReadyToSignAsync(ReadyToSignRequestRequest request, CancellationToken cancellableToken)
		{
			return Arena.ReadyToSignAsync(request, cancellableToken);
		}
	}
}
