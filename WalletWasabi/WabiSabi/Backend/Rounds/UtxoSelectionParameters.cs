using NBitcoin;
using System.Collections.Immutable;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Models;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public record UtxoSelectionParameters(
	MoneyRange AllowedInputAmounts,
	MoneyRange AllowedOutputAmounts,
	CoordinationFeeRate CoordinationFeeRate,
	FeeRate MiningFeeRate,
	ImmutableSortedSet<ScriptType> AllowedInputScriptTypes,
	ImmutableSortedSet<ScriptType> AllowedOutputScriptTypes,
	string CoordinatorName)
{
	public static UtxoSelectionParameters FromRoundParameters(RoundParameters roundParameters, string coordinatorName, WasabiRandom? random = null) =>
		new(
			roundParameters.AllowedInputAmounts,
			roundParameters.CalculateReasonableOutputAmountRange(random),
			roundParameters.CoordinationFeeRate,
			roundParameters.MiningFeeRate,
			roundParameters.AllowedInputTypes,
			roundParameters.AllowedOutputTypes, 
			coordinatorName);
}
