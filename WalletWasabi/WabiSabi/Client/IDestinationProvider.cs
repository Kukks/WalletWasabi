using NBitcoin;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public interface IDestinationProvider
{
	Task<IEnumerable<IDestination>> GetNextDestinationsAsync(int count, bool mixedOutputs, bool privateEnough);

	Task<IEnumerable<PendingPayment>> GetPendingPaymentsAsync(UtxoSelectionParameters roundParameters);

	Task<ScriptType> GetScriptTypeAsync();
}


