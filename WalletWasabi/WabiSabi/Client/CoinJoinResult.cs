using NBitcoin;
using System.Collections.Immutable;
using WalletWasabi.Blockchain.TransactionOutputs;

namespace WalletWasabi.WabiSabi.Client;

public abstract record CoinJoinResult;

public record SuccessfulCoinJoinResult(
	ImmutableList<SmartCoin> Coins,
	ImmutableList<Script> OutputScripts,
	ImmutableDictionary<TxOut, PendingPayment> HandledPayments,
	Transaction UnsignedCoinJoin, uint256 RoundId) : CoinJoinResult;

public record FailedCoinJoinResult : CoinJoinResult;

public record DisruptedCoinJoinResult(ImmutableList<SmartCoin> SignedCoins, bool abandonAndAllSubsequentBlames) : CoinJoinResult; 
