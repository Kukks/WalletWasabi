using NBitcoin;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public class InternalDestinationProvider : IDestinationProvider
{
	public InternalDestinationProvider(KeyManager keyManager)
	{
		KeyManager = keyManager;
	}

	private KeyManager KeyManager { get; }

	public Task<IEnumerable<IDestination>> GetNextDestinationsAsync(int count, bool mixedOutputs, bool privateEnough)
	{
		// Get all locked internal keys we have and assert we have enough.
		KeyManager.AssertLockedInternalKeysIndexedAndPersist(count, false);

		var allKeys = KeyManager.GetNextCoinJoinKeys().ToList();
		var taprootKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.TaprootBIP86)
			.ToList();

		var segwitKeys = allKeys
			.Where(x => x.FullKeyPath.GetScriptTypeFromKeyPath() == ScriptPubKeyType.Segwit)
			.ToList();

		var destinations = taprootKeys.Count >= count
			? taprootKeys
			: segwitKeys;
		return Task.FromResult(destinations.Select(x => (IDestination) x.GetAddress(KeyManager.GetNetwork())));
	}

	public Task<IEnumerable<PendingPayment>> GetPendingPaymentsAsync(UtxoSelectionParameters roundParameters)
	{
		return Task.FromResult(Enumerable.Empty<PendingPayment>());
	}

	public Task<ScriptType> GetScriptTypeAsync()
	{
		return Task.FromResult(ScriptType.P2WPKH);
	}
}
