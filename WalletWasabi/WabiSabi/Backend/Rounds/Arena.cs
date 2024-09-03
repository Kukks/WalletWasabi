using System.Collections.Concurrent;
using NBitcoin;
using NBitcoin.RPC;
using Nito.AsyncEx;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Bases;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Crypto.Randomness;
using WalletWasabi.WabiSabi.Backend.Models;
using WalletWasabi.WabiSabi.Models.MultipartyTransaction;
using WalletWasabi.WabiSabi.Backend.Statistics;
using System.Collections.Immutable;
using System.Net.Http;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Extensions;
using WalletWasabi.Logging;
using WalletWasabi.WabiSabi.Backend.DoSPrevention;

namespace WalletWasabi.WabiSabi.Backend.Rounds;

public partial class Arena : PeriodicRunner
{
	private readonly IHttpClientFactory _httpClientFactory;
	private readonly WabiSabiConfig.CoordinatorScriptResolver _coordinatorScriptResolver;

	public Arena(
		TimeSpan period,
		WabiSabiConfig config,
		IRPCClient rpc,
		Prison prison,
		RoundParameterFactory roundParameterFactory,
		IHttpClientFactory httpClientFactory,
		WabiSabiConfig.CoordinatorScriptResolver coordinatorScriptResolver,
		CoinJoinScriptStore? coinJoinScriptStore = null) : base(period)
	{
		_httpClientFactory = httpClientFactory;
		_coordinatorScriptResolver = coordinatorScriptResolver;
		Config = config;
		Rpc = rpc;
		Prison = prison;
		CoinJoinScriptStore = coinJoinScriptStore;
		RoundParameterFactory = roundParameterFactory;
		MaxSuggestedAmountProvider = new(Config);
	}

	public event EventHandler<Transaction>? CoinJoinBroadcast;

	public HashSet<Round> Rounds { get; } = new();
	public ImmutableList<RoundState> RoundStates { get; private set; } = ImmutableList<RoundState>.Empty;
	internal ConcurrentQueue<uint256> DisruptedRounds { get; } = new();
	private AsyncLock AsyncLock { get; } = new();
	private WabiSabiConfig Config { get; }
	internal IRPCClient Rpc { get; }
	private Prison Prison { get; }
	public CoinJoinScriptStore? CoinJoinScriptStore { get; }
	private RoundParameterFactory RoundParameterFactory { get; }
	public MaxSuggestedAmountProvider MaxSuggestedAmountProvider { get; }

	protected override async Task ActionAsync(CancellationToken cancel)
	{
		var before = DateTimeOffset.UtcNow;
		using (await AsyncLock.LockAsync(cancel).ConfigureAwait(false))
		{
			var beforeInside = DateTimeOffset.UtcNow;
			TimeoutRounds();

			TimeoutAlices();

			await StepTransactionSigningPhaseAsync(cancel).ConfigureAwait(false);

			await StepOutputRegistrationPhase(cancel).ConfigureAwait(false);

			await StepConnectionConfirmationPhaseAsync(cancel).ConfigureAwait(false);

			await StepInputRegistrationPhaseAsync(cancel).ConfigureAwait(false);

			cancel.ThrowIfCancellationRequested();

			// Ensure there's at least one non-blame round in input registration.
			await CreateRoundsAsync(cancel).ConfigureAwait(false);

			AbortDisruptedRounds();

			// RoundStates have to contain all states. Do not change stateId=0.
			SetRoundStates();

		}
	}

	private void SetRoundStates()
	{
		// Order rounds ascending by max suggested amount, then ascending by input count.
		// This will make sure WW2.0.1 clients register according to our desired order.
		var rounds = Rounds
						.OrderBy(x => x.Parameters.MaxSuggestedAmount)
						.ThenBy(x => x.InputCount)
						.ToList();

		RoundStates = rounds.Select(r => RoundState.FromRound(r, stateId: 0)).ToImmutableList();
	}

	private async Task StepInputRegistrationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x =>
			x.Phase == Phase.InputRegistration
			&& x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
			.ToArray())
		{
			try
			{
				await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
				{
					if (offendingAlices.Length != 0)
					{
						round.Alices.RemoveAll(x => offendingAlices.Contains(x));
					}
				}

				if (round.InputCount < round.Parameters.MinInputCountByRound)
				{
					if (!round.InputRegistrationTimeFrame.HasExpired)
					{
						continue;
					}

					MaxSuggestedAmountProvider.StepMaxSuggested(round, false);
					EndRound(round, EndRoundState.AbortedNotEnoughAlices);
					round.LogInfo(null, null, $"Not enough inputs ({round.InputCount}) in {nameof(Phase.InputRegistration)} phase. The minimum is ({round.Parameters.MinInputCountByRound}). {nameof(round.Parameters.MaxSuggestedAmount)} was '{round.Parameters.MaxSuggestedAmount}' BTC.");
				}
				else if (round.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound))
				{
					MaxSuggestedAmountProvider.StepMaxSuggested(round, true);
					SetRoundPhase(round, Phase.ConnectionConfirmation);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(null,null,ex.Message);
			}
		}
	}

	private async Task StepConnectionConfirmationPhaseAsync(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.ConnectionConfirmation).ToArray())
		{
			try
			{
				if (round.Alices.All(x => x.ConfirmedConnection))
				{
					SetRoundPhase(round, Phase.OutputRegistration);
				}
				else if (round.ConnectionConfirmationTimeFrame.HasExpired)
				{
					var alicesDidNotConfirm = round.Alices.Where(x => !x.ConfirmedConnection).ToArray();
					if (ReasonableOffendersCount(alicesDidNotConfirm.Length, round.Parameters.MinInputCountByRound))
					{
						foreach (var alice in alicesDidNotConfirm)
						{
							Prison.FailedToConfirm(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
						}
					}
					else
					{
						Logger.LogWarning($"{round.Id}: Tried to ban {alicesDidNotConfirm.Length} inputs for FailedToConfirm - ban was skipped.");
						foreach (var alice in alicesDidNotConfirm)
						{
							Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
						}
					}
					var removedAliceCount = round.Alices.RemoveAll(x => alicesDidNotConfirm.Contains(x));
					round.LogInfo(null, null, $"{removedAliceCount} alices removed because they didn't confirm.");

					// Once an input is confirmed and non-zero credentials are issued, it is too late to do any
					if (round.InputCount >= round.Parameters.MinInputCountByRound)
					{
						var allOffendingAlices = new List<Alice>();
						await foreach (var offendingAlices in CheckTxoSpendStatusAsync(round, cancel).ConfigureAwait(false))
						{
							allOffendingAlices.AddRange(offendingAlices);
						}

						if (ReasonableOffendersCount(allOffendingAlices.Count, round.Parameters.MinInputCountByRound))
						{
							foreach (var offender in allOffendingAlices)
							{
								Prison.DoubleSpent(offender.Coin.Outpoint, offender.Coin.Amount, round.Id);
							}
						}
						else
						{
							Logger.LogWarning($"{round.Id}: Tried to ban {allOffendingAlices.Count} inputs for FailedToConfirm - ban was skipped.");
							foreach (var alice in allOffendingAlices)
							{
								Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
							}
						}
						if (allOffendingAlices.Count > 0)
						{
							round.LogInfo(null,null,$"There were {allOffendingAlices.Count} alices that spent the registered UTXO. Aborting...");

							await EndRoundAndTryCreateBlameRoundAsync(round, cancel).ConfigureAwait(false);
							return;
						}
					}

					if (round.InputCount < round.Parameters.MinInputCountByRound)
					{
						EndRound(round, EndRoundState.AbortedNotEnoughAlices);
						round.LogInfo(null, null, $"Not enough inputs ({round.InputCount}) in {nameof(Phase.ConnectionConfirmation)} phase. The minimum is ({round.Parameters.MinInputCountByRound}).");
					}
					else
					{
						round.OutputRegistrationTimeFrame = TimeFrame.Create(Config.FailFastOutputRegistrationTimeout);
						SetRoundPhase(round, Phase.OutputRegistration);
					}
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(null,null,ex.Message);
			}
		}
	}

	private async Task StepOutputRegistrationPhase(CancellationToken cancel)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.OutputRegistration).ToArray())
		{
			try
			{
				var allReady = round.Alices.All(a => a.ReadyToSign);
				bool phaseExpired = round.OutputRegistrationTimeFrame.HasExpired;

				if (allReady || phaseExpired)
				{
					var coinjoin = round.Assert<ConstructionState>();

					round.LogInfo(null, null, $"{coinjoin.Inputs.Count()} inputs were added.");
					round.LogInfo(null, null, $"{coinjoin.Outputs.Count()} outputs were added.");

					coinjoin = await AddCoordinationFee(round, coinjoin, cancel).ConfigureAwait(false);
					round.CoinjoinState = FinalizeTransaction(round.Id, coinjoin);

					if (!allReady && phaseExpired)
					{
						// It would be better to end the round and create a blame round here, but older client would not support it.
						// See https://github.com/zkSNACKs/WalletWasabi/pull/11028.
						round.TransactionSigningTimeFrame = TimeFrame.Create(Config.FailFastTransactionSigningTimeout);
						round.FastSigningPhase = true;
					}

					SetRoundPhase(round, Phase.TransactionSigning);
				}
			}
			catch (Exception ex)
			{
				EndRound(round, EndRoundState.AbortedWithError);
				round.LogError(null,null,ex.Message);
			}
		}
	}

	private async Task StepTransactionSigningPhaseAsync(CancellationToken cancellationToken)
	{
		foreach (var round in Rounds.Where(x => x.Phase == Phase.TransactionSigning).ToArray())
		{
			var state = round.Assert<SigningState>();

			try
			{
				if (state.IsFullySigned)
				{
					Transaction coinjoin = state.CreateTransaction();

					// Logging.
					round.LogInfo(null, null, "Trying to broadcast coinjoin.");
					Coin[] spentCoins = round.CoinjoinState.Inputs.ToArray();
					Money networkFee = coinjoin.GetFee(spentCoins);
					round.LogInfo(null, null, $"Network Fee: {networkFee.ToString(false, false)} BTC.");
					uint256 roundId = round.Id;
					FeeRate feeRate = coinjoin.GetFeeRate(spentCoins);
					round.LogInfo(null, null, $"Network Fee Rate: {feeRate.SatoshiPerByte} sat/vByte.");
					round.LogInfo(null, null, $"Desired Fee Rate: {round.Parameters.MiningFeeRate.SatoshiPerByte} sat/vByte.");

					// Added for monitoring reasons.
					try
					{
						FeeRate targetFeeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
						round.LogInfo(null, null, $"Current Fee Rate on the Network: {targetFeeRate.SatoshiPerByte} sat/vByte. Confirmation target is: {(int)Config.ConfirmationTarget} blocks.");
					}
					catch (Exception ex)
					{
						Logger.LogDebug($"Could not log fee rate monitoring: '{ex.Message}'.");
					}

					round.LogInfo(null, null, $"Number of inputs: {coinjoin.Inputs.Count}.");
					round.LogInfo(null, null, $"Number of outputs: {coinjoin.Outputs.Count}.");
					round.LogInfo(null, null, $"Serialized Size: {coinjoin.GetSerializedSize() / 1024.0} KB.");
					round.LogInfo(null, null, $"VSize: {coinjoin.GetVirtualSize() / 1024.0} KB.");
					var indistinguishableOutputs = coinjoin.GetIndistinguishableOutputs(includeSingle: true);
					foreach (var (value, count) in indistinguishableOutputs.Where(x => x.count > 1))
					{
						round.LogInfo(null, null, $"There are {count} occurrences of {value.ToString(true, false)} outputs.");
					}

					round.LogInfo(
						null,null,$"There are {indistinguishableOutputs.Count(x => x.count == 1)} occurrences of unique outputs.");

					// Broadcasting.
					await Rpc.SendRawTransactionAsync(coinjoin, cancellationToken).ConfigureAwait(false);
					EndRound(round, EndRoundState.TransactionBroadcasted);
					round.LogInfo(null, null,  $"Successfully broadcast the coinjoin: {coinjoin.GetHash()}.");


					foreach (var address in coinjoin.Outputs
						.Select(x => x.ScriptPubKey)
						.Where(script => CoinJoinScriptStore?.Contains(script) is true))
					{
						if ( round.FeeTxOuts.Any(@out => @out.ScriptPubKey == address))
						{
							round.LogError(
								null,null,$"Coordinator script pub key reuse detected: {address.ToHex()}");
						}
						else
						{
							round.LogError(null,null,$"Output script pub key reuse detected: {address.ToHex()}");
						}
					}

					CoinJoinScriptStore?.AddRange(coinjoin.Outputs.Select(x => x.ScriptPubKey));
					CoinJoinBroadcast?.Invoke(this, coinjoin);
				}
				else if (round.TransactionSigningTimeFrame.HasExpired)
				{
					round.LogWarning(null,null,$"Signing phase failed with timed out after {round.TransactionSigningTimeFrame.Duration.TotalSeconds} seconds.");
					if (round.FastSigningPhase)
					{
						await FailFastTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
					else
					{
						await FailTransactionSigningPhaseAsync(round, cancellationToken).ConfigureAwait(false);
					}
				}
			}
			catch (RPCException ex)
			{
				round.LogError(null,null,$"Transaction broadcasting failed: '{ex}'.");
				EndRound(round, EndRoundState.TransactionBroadcastFailed);
			}
			catch (Exception ex)
			{
				round.LogWarning(null,null,$"Signing phase failed, reason: '{ex}'.");
				EndRound(round, EndRoundState.AbortedWithError);
			}
		}
	}

	private async IAsyncEnumerable<Alice[]> CheckTxoSpendStatusAsync(Round round, [EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var chunkOfAlices in round.Alices.ToList().ChunkBy(16))
		{
			var batchedRpc = Rpc.PrepareBatch();

			var aliceCheckingTaskPairs = chunkOfAlices
				.Select(x => (Alice: x, StatusTask: Rpc.GetTxOutAsync(x.Coin.Outpoint.Hash, (int)x.Coin.Outpoint.N, includeMempool: true, cancellationToken)))
				.ToList();

			await batchedRpc.SendBatchAsync(cancellationToken).ConfigureAwait(false);

			var spendStatusCheckingTasks = aliceCheckingTaskPairs.Select(async x => (x.Alice, Status: await x.StatusTask.ConfigureAwait(false)));
			var alices = await Task.WhenAll(spendStatusCheckingTasks).ConfigureAwait(false);
			yield return alices.Where(x => x.Status is null).Select(x => x.Alice).ToArray();
		}
	}

	private async Task FailTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var state = round.Assert<SigningState>();

		var unsignedOutpoints = state.UnsignedInputs.Select(c => c.Outpoint).ToHashSet();

		var alicesWhoDidNotSign = round.Alices
			.Where(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint))
			.ToHashSet();

		if (ReasonableOffendersCount(alicesWhoDidNotSign.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesWhoDidNotSign)
			{
				Prison.FailedToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			round.LogWarning(null,null, $"{round.Id}: Tried to ban {alicesWhoDidNotSign.Count} inputs for FailedToConfirm - ban was skipped.");
			foreach (var alice in alicesWhoDidNotSign)
			{
				Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var cnt = round.Alices.RemoveAll(alice => unsignedOutpoints.Contains(alice.Coin.Outpoint));

		round.LogInfo(null, null, $"Removed {cnt} alices, because they didn't sign. Remaining: {round.InputCount}");

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task FailFastTransactionSigningPhaseAsync(Round round, CancellationToken cancellationToken)
	{
		var alicesToRemove = round.Alices.Where(alice => !alice.ReadyToSign).ToHashSet();

		if (ReasonableOffendersCount(alicesToRemove.Count, round.Parameters.MinInputCountByRound))
		{
			foreach (var alice in alicesToRemove)
			{
				// Intentionally, do not ban Alices who have not signed, as clients using hardware wallets may not be able to sign in time.
				Prison.FailedToSignalReadyToSign(alice.Coin.Outpoint, alice.Coin.Amount, round.Id);
			}
		}
		else
		{
			Logger.LogWarning($"{round.Id}: Tried to ban {alicesToRemove.Count} inputs for FailedToConfirm - ban was skipped.");
			foreach (var alice in alicesToRemove)
			{
				Prison.BackendStabilitySafetyBan(alice.Coin.Outpoint, round.Id);
			}
		}

		var removedAlices = round.Alices.RemoveAll(alice => alicesToRemove.Contains(alice));

		round.LogInfo(null, null, $"Removed {removedAlices} alices, because they weren't ready. Remaining: {round.InputCount}");

		await EndRoundAndTryCreateBlameRoundAsync(round, cancellationToken).ConfigureAwait(false);
	}

	private async Task EndRoundAndTryCreateBlameRoundAsync(Round round, CancellationToken cancellationToken)
	{
		if (round.InputCount < Config.MinInputCountByBlameRound)
		{
			// There are not enough inputs, makes no sense to create the blame round.
			EndRound(round, EndRoundState.AbortedNotEnoughAlicesSigned);
			return;
		}

		// This indicates to the client that there will be a blame round.
		EndRound(round, EndRoundState.NotAllAlicesSign);

		var feeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
		var blameWhitelist = round.Alices
			.Select(x => x.Coin.Outpoint)
			.Where(x => !Prison.IsBanned(x, Config.GetDoSConfiguration(), DateTimeOffset.UtcNow))
			.ToHashSet();

		RoundParameters parameters = RoundParameterFactory.CreateBlameRoundParameter(feeRate, round) with
		{
			MinInputCountByRound = Config.MinInputCountByBlameRound
		};

		BlameRound blameRound = new(parameters, round, blameWhitelist, SecureRandom.Instance);
		AddRound(blameRound);
		blameRound.LogInfo(null, null, $"Blame round created from round '{round.Id}'.");
	}

	private async Task CreateRoundsAsync(CancellationToken cancellationToken)
	{
		FeeRate? feeRate = null;

		// Have rounds to split the volume around minimum input counts if load balance is required.
		// Only do things if the load balancer compatibility is configured.
		if (Config.WW200CompatibleLoadBalancing)
		{
			foreach (var round in Rounds.Where(x =>
				x.Phase == Phase.InputRegistration
				&& x is not BlameRound
				&& !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)
				&& x.InputCount >= Config.RoundDestroyerThreshold).ToArray())
			{
				feeRate = (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;

				var allInputs = round.Alices.Select(y => y.Coin.Amount).OrderBy(x => x).ToArray();

				// 0.75 to bias towards larger numbers as larger input owners often have many smaller inputs too.
				var smallSuggestion = allInputs.Skip((int)(allInputs.Length * Config.WW200CompatibleLoadBalancingInputSplit)).First();
				var largeSuggestion = MaxSuggestedAmountProvider.AbsoluteMaximumInput;

				var roundWithoutThis = Rounds.Except(new[] { round });
				RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, largeSuggestion);
				Round? foundLargeRound = roundWithoutThis
					.FirstOrDefault(x =>
									x.Phase == Phase.InputRegistration
									&& x is not BlameRound
									&& !x.IsInputRegistrationEnded(round.Parameters.MaxInputCountByRound)
									&& x.Parameters.MaxSuggestedAmount >= allInputs.Max()
									&& x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromSeconds(60));
				var largeRound = foundLargeRound ?? TryMineRound(parameters, roundWithoutThis.ToArray());

				if (largeRound is not null)
				{
					parameters = RoundParameterFactory.CreateRoundParameter(feeRate, smallSuggestion);
					var smallRound = TryMineRound(parameters, roundWithoutThis.Concat(new[] { largeRound }).ToArray());

					// If creation is successful, only then destroy the round.
					if (smallRound is not null)
					{
						AddRound(largeRound);
						AddRound(smallRound);

						if (foundLargeRound is null)
						{
							largeRound.LogInfo(null, null, $"Mined round with parameters: {nameof(largeRound.Parameters.MaxSuggestedAmount)}:'{largeRound.Parameters.MaxSuggestedAmount}' BTC.");
						}
						smallRound.LogInfo(null, null, $"Mined round with parameters: {nameof(smallRound.Parameters.MaxSuggestedAmount)}:'{smallRound.Parameters.MaxSuggestedAmount}' BTC.");

						// If it can't create the large round, then don't abort.
						EndRound(round, EndRoundState.AbortedLoadBalancing);
						Logger.LogInfo($"Destroyed round with {allInputs.Length} inputs. Threshold: {Config.RoundDestroyerThreshold}");
					}
				}
			}
		}

		// Add more rounds if not enough.
		var registrableRoundCount = Rounds.Count(x => x is not BlameRound && x.Phase == Phase.InputRegistration && x.InputRegistrationTimeFrame.Remaining > TimeSpan.FromMinutes(1));
		int roundsToCreate = Config.RoundParallelization - registrableRoundCount;
		for (int i = 0; i < roundsToCreate; i++)
		{
			feeRate ??= (await Rpc.EstimateConservativeSmartFeeAsync((int)Config.ConfirmationTarget, cancellationToken).ConfigureAwait(false)).FeeRate;
			RoundParameters parameters = RoundParameterFactory.CreateRoundParameter(feeRate, MaxSuggestedAmountProvider.MaxSuggestedAmount);

			var r = new Round(parameters, SecureRandom.Instance);
			AddRound(r);
			r.LogInfo(null, null, $"Created round with parameters: {r.Parameters.MiningFeeRate} mining fee, {r.Parameters.MinInputCountByRound} min inputs");
		}
	}

	private Round? TryMineRound(RoundParameters parameters, Round[] rounds)
	{
		// Huge HACK to keep it compatible with WW2.0.0 client version, which's
		// round preference is based on the ordering of ToImmutableDictionary.
		// Add round until ToImmutableDictionary orders it to be the first round
		// so old clients will prefer that one.
		IOrderedEnumerable<Round>? orderedRounds;
		Round r;
		var before = DateTimeOffset.UtcNow;
		var times = 0;
		var maxCycleTimes = 300;
		do
		{
			var roundsCopy = rounds.ToList();
			r = new Round(parameters, SecureRandom.Instance);
			roundsCopy.Add(r);
			orderedRounds = roundsCopy
				.Where(x => x.Phase == Phase.InputRegistration && x is not BlameRound && !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound))
				.OrderBy(x => x.Parameters.MaxSuggestedAmount)
				.ThenBy(x => x.InputCount);
			times++;
		}
		while (times <= maxCycleTimes && orderedRounds.ToImmutableDictionary(x => x.Id, x => x).First().Key != r.Id);

		Logger.LogDebug($"First ordered round creator did {times} cycles.");

		if (times > maxCycleTimes)
		{
			r.LogInfo(null, null, "First ordered round creation too expensive. Skipping...");
			return null;
		}
		else
		{
			return r;
		}
	}

	private void TimeoutRounds()
	{
		foreach (var expiredRound in Rounds.Where(
			x =>
			x.Phase == Phase.Ended
			&& x.End + Config.RoundExpiryTimeout < DateTimeOffset.UtcNow).ToArray())
		{
			Rounds.Remove(expiredRound);
		}
	}

	private void TimeoutAlices()
	{
		var now = DateTimeOffset.UtcNow;
		foreach (var round in Rounds.Where(x => !x.IsInputRegistrationEnded(x.Parameters.MaxInputCountByRound)).ToArray())
		{
			var alicesToRemove = round.Alices.Where(x => x.Deadline < now && !x.ConfirmedConnection).ToArray();
			foreach (var alice in alicesToRemove)
			{
				round.Alices.Remove(alice);
			}

			var removedAliceCount = alicesToRemove.Length;
			if (removedAliceCount > 0)
			{
				round.LogInfo(null, null, $"{removedAliceCount} alices timed out and removed.");
			}
		}
	}

	private async Task<ConstructionState> AddCoordinationFee(Round round,
		ConstructionState coinjoin, CancellationToken cancellationToken)
	{

		var collectedCoordinationFee = round.Alices.Sum(x => round.Parameters.CoordinationFeeRate.GetFee(x.Coin.Amount));

		if (collectedCoordinationFee == 0)
		{
			round.FeeTxOuts = [];
			round.LogInfo(null, null, $"Coordination fee wasn't taken, because it was free for everyone. Hurray!");
		}
		if(Config.CoordinatorSplits?.Any() is true)
		{
			var splits = await Config.GetNextCleanCoordinatorScripts(_coordinatorScriptResolver,_httpClientFactory, round, cancellationToken).ConfigureAwait(false);
			var splitsVSize = splits.Sum(tuple =>
				tuple.Item2 is null
					? 0
					: new TxOut(Money.Zero, tuple.Item2).GetSerializedSize());
			var coinjoinVSize = coinjoin.EstimatedVsize + splitsVSize;
			var expectedCost = coinjoin.Parameters.MiningFeeRate.GetFee(coinjoinVSize)!;
			var coordinationFee = coinjoin.Balance - expectedCost;
			if (coordinationFee > 0)
			{
				round.LogInfo(null, null, $"Coordination fee was originally {Money.Satoshis(collectedCoordinationFee).ToDecimal(MoneyUnit.BTC)}, " +
				                   $"but we're now at {coordinationFee.ToDecimal(MoneyUnit.BTC)} after computing advertised feerate and splitting it among the coordinator scripts.");
				var txOuts = ComputeSplitAmounts(coordinationFee, splits, round);
				coinjoin = txOuts.Aggregate(coinjoin, (current, txOut) => current.AddOutput(txOut)).AsPayingForSharedOverhead();
				round.FeeTxOuts = txOuts;
			}
			if(coinjoin.EffectiveFeeRate > coinjoin.Parameters.MiningFeeRate)
			{
				round.LogWarning(null,null,$"Effective fee rate is higher than the advertised fee rate. Effective: {coinjoin.EffectiveFeeRate.SatoshiPerByte}, Advertised: {coinjoin.Parameters.MiningFeeRate.SatoshiPerByte}");
			}
		}

		return coinjoin;
	}
	// Compute splits of the coordinator fee based on ratios
	private TxOut[] ComputeSplitAmounts(Money amountToSplit, (WabiSabiConfig.CoordinatorSplit split, Script? script)[] splits, Round round)
	{

		// If there is a split with a null script, then distribute the ratio evenly among the other splits
		var failedSplits = splits.Where(tuple => tuple.Item2 is null);
		var workingSplits = splits.Except(failedSplits).ToArray();
		if (failedSplits.Any() && workingSplits.Any())
		{
			// Take the sum of the ratios of the failed splits and add them to the other splits so that they may claim the amount evenly
			var toDistribute = failedSplits.Sum(tuple => tuple.split.Ratio)/  workingSplits.Length;;
			foreach ((WabiSabiConfig.CoordinatorSplit split, Script) valueTuple in workingSplits)
			{
				valueTuple.split.Ratio += toDistribute;
			}
		}

		var totalRatio = Math.Max(1, workingSplits.Sum(split => split.split.Ratio));
		var satsPerShare = amountToSplit.ToDecimal(MoneyUnit.BTC) / totalRatio;

		var result = workingSplits.Select(tuple =>
			new TxOut(new Money(tuple.split.Ratio * satsPerShare, MoneyUnit.BTC), tuple.Item2)).ToList();
		var invalid = new Func<TxOut, bool>(@out =>
			@out.IsDust() ||
			@out.Value.Satoshi < round.Parameters.AllowedOutputAmounts.Min);


		var left = result.FirstOrDefault(invalid)?.Value?.ToDecimal(MoneyUnit.BTC) ?? 0m;
		while (left > 0)
		{
			if (result.Count == 1)
			{
				return [];
			}
			result.RemoveAll(@out => invalid(@out));
			satsPerShare = left / totalRatio;
			result.ForEach(@out => @out.Value=  new Money(@out.Value.ToDecimal(MoneyUnit.BTC) + satsPerShare, MoneyUnit.BTC));
			left = result.FirstOrDefault(invalid)?.Value?.ToDecimal(MoneyUnit.BTC) ?? 0m;
		}

		return result.ToArray();
	}

	private void AddRound(Round round)
	{
		Rounds.Add(round);
	}

	public void AbortRound(uint256 roundId)
	{
		DisruptedRounds.Enqueue(roundId);
	}

	private void AbortDisruptedRounds()
	{
		while (DisruptedRounds.TryDequeue(out var disruptedRoundId))
		{
			var roundOrNull = Rounds.FirstOrDefault(x => x.Id == disruptedRoundId);
			if (roundOrNull is { } nonNullRound)
			{
				nonNullRound.LogInfo(null, null, "Round aborted because it was disrupted by double spenders.");
				nonNullRound.EndRound(EndRoundState.AbortedDoubleSpendingDetected);
			}
		}
	}

	private void SetRoundPhase(Round round, Phase phase)
	{
		round.SetPhase(phase);
	}

	internal void EndRound(Round round, EndRoundState endRoundState)
	{
		round.EndRound(endRoundState);
	}

	private SigningState FinalizeTransaction(uint256 roundId, ConstructionState constructionState)
	{
		SigningState signingState = constructionState.Finalize();
		return signingState;
	}

	/// <summary>
	/// If too many inputs seem to misbehave, problem is probably on coordinator's side.
	/// Don't ban in that case to avoid huge amount of false-positives.
	/// </summary>
	private static bool ReasonableOffendersCount(int offendersCount, int minInputCount) => offendersCount <= minInputCount;
}
