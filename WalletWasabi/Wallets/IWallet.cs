using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NBitcoin;
using WalletWasabi.Blockchain.TransactionOutputs;
using WalletWasabi.Blockchain.Transactions;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Client;
using LogLevel = WalletWasabi.Logging.LogLevel;

namespace WalletWasabi.Wallets;

public enum ConsolidationModeType
{
	Always,
	Never,
	WhenLowFee,
	WhenLowFeeAndManyUTXO
}

public interface IWallet
{
	void Log(LogLevel logLevel, string logMessage, [CallerFilePath] string callerFilePath = "",
		[CallerMemberName] string callerMemberName = "", [CallerLineNumber] int callerLineNumber = -1);
	string WalletName { get; }
	bool IsUnderPlebStop { get; }
	bool IsMixable(string coordinator);

	/// <summary>
	/// Watch only wallets have no key chains.
	/// </summary>
	IKeyChain? KeyChain { get; }

	IDestinationProvider DestinationProvider { get; }
	int AnonScoreTarget { get; }
	ConsolidationModeType ConsolidationMode { get; }
	TimeSpan FeeRateMedianTimeFrame { get; }
	int ExplicitHighestFeeTarget { get; }
	int LowFeeTarget { get; }

	bool RedCoinIsolation { get; }
	bool BatchPayments { get; }
	long? MinimumDenominationAmount { get; }

	Task<bool> IsWalletPrivateAsync();

	Task<IEnumerable<SmartCoin>> GetCoinjoinCoinCandidatesAsync(string coordinatorname);

	Task<IEnumerable<SmartTransaction>> GetTransactionsAsync();

	IRoundCoinSelector? GetCoinSelector()
	{
		return null;
	}

	bool IsRoundOk(RoundParameters coinjoinStateParameters, string coordinatorName);
	Task CompletedCoinjoin(CoinJoinTracker finishedCoinJoin);
}


public interface IRoundCoinSelector
{
	Task<ImmutableList<SmartCoin>> SelectCoinsAsync(IEnumerable<SmartCoin> coinCandidates,
		UtxoSelectionParameters utxoSelectionParameters, Money liquidityClue, SecureRandom secureRandom);
}
