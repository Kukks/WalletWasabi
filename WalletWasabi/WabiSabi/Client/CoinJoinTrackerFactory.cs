using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.PostRequests;
using WalletWasabi.WabiSabi.Client.RoundStateAwaiters;
using WalletWasabi.Wallets;
using WalletWasabi.WebClients.Wasabi;

namespace WalletWasabi.WabiSabi.Client;

public class CoinJoinTrackerFactory
{
	private readonly IWabiSabiApiRequestHandler _wabiSabiApiRequestHandler;
	private readonly Action<BannedCoinEventArgs> _onCoinBan;
	private readonly string _coordinatorName;

	public CoinJoinTrackerFactory(IWasabiHttpClientFactory httpClientFactory,
		IWabiSabiApiRequestHandler wabiSabiApiRequestHandler,
		RoundStateUpdater roundStatusUpdater,
		string coordinatorIdentifier,
		CancellationToken cancellationToken,
		Action<BannedCoinEventArgs> onCoinBan, string coordinatorName)
	{
		_wabiSabiApiRequestHandler = wabiSabiApiRequestHandler;
		HttpClientFactory = httpClientFactory;
		_onCoinBan = onCoinBan;
		_coordinatorName = coordinatorName;
		RoundStatusUpdater = roundStatusUpdater;
		CoordinatorIdentifier = coordinatorIdentifier;
		CancellationToken = cancellationToken;
		LiquidityClueProvider = new LiquidityClueProvider();
	}

	private IWasabiHttpClientFactory HttpClientFactory { get; }
	private RoundStateUpdater RoundStatusUpdater { get; }
	private CancellationToken CancellationToken { get; }
	private string CoordinatorIdentifier { get; }
	private LiquidityClueProvider LiquidityClueProvider { get; }

	public async Task<CoinJoinTracker> CreateAndStartAsync(IWallet wallet, Func<Task<IEnumerable<SmartCoin>>> coinCandidatesFunc, bool stopWhenAllMixed, bool overridePlebStop)
	{
		await LiquidityClueProvider.InitLiquidityClueAsync(wallet).ConfigureAwait(false);

		if (wallet.KeyChain is null)
		{
			throw new NotSupportedException("Wallet has no key chain.");
		}

		var coinSelector = CoinJoinCoinSelector.FromWallet(wallet);
		var outputProvider = new OutputProvider(wallet, _coordinatorName);
		var coinJoinClient = new CoinJoinClient(
			_onCoinBan,
			HttpClientFactory,
			_wabiSabiApiRequestHandler,
			wallet,
			wallet.KeyChain,
			outputProvider,
			RoundStatusUpdater,
			CoordinatorIdentifier,
			coinSelector,
			LiquidityClueProvider,
			feeRateMedianTimeFrame: wallet.FeeRateMedianTimeFrame,
			doNotRegisterInLastMinuteTimeLimit: TimeSpan.FromMinutes(1),
			wallet.GetCoinSelector(),
			_coordinatorName);

		return new CoinJoinTracker(wallet, coinJoinClient, coinCandidatesFunc, stopWhenAllMixed, overridePlebStop, CancellationToken);
	}
}
