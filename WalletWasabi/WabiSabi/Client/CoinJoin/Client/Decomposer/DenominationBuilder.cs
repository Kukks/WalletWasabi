using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using WabiSabi.Crypto.Randomness;
using WalletWasabi.Extensions;

namespace WalletWasabi.WabiSabi.Client.CoinJoin.Client.Decomposer;

public static class DenominationBuilder
{
	public static IOrderedEnumerable<Output> CreateDenominations(Money minAllowedOutputAmount,
		Money maxAllowedOutputAmount, FeeRate feeRate, IEnumerable<ScriptType> allowedOutputTypes,
		long? minimumDenominationAmount,
		WasabiRandom random, long[]? walletAllowedDenominations)
	{
		var denominations = new HashSet<Output?>();

		Output? CreateDenom(double sats, out bool stop)
		{
			stop = false;
			try
			{
				var scriptType = allowedOutputTypes.RandomElement(random);
				var result =  Output.FromDenomination(Money.Satoshis((ulong)sats), scriptType, feeRate);
				if (result.Amount > maxAllowedOutputAmount)
				{
					stop = true;
				}
				if ( (minimumDenominationAmount is not null && result.Amount.Satoshi < minimumDenominationAmount.Value) || result.Amount < minAllowedOutputAmount || result.Amount > maxAllowedOutputAmount)
				{
					return null;
				}

				if(walletAllowedDenominations is not null && !walletAllowedDenominations.Contains(result.Amount.Satoshi))
				{
					return null;
				}
				return result;
			}
			catch (Exception e)
			{
				return null;
			}
		}

		// Powers of 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(2, i), out var stop);

			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}

			denominations.Add(denom);
		}

		// Powers of 3
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i), out var stop);
			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 3 * 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i) * 2, out var stop);
			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i), out var stop);
			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 * 2 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 2, out var stop);
			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 * 5 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 5, out var stop);
			if (denom is null)
			{
				if(stop)
				{
					break;
				}
				continue;
			}
			denominations.Add(denom);
		}

		// Greedy decomposer will take the higher values first. Order in a way to prioritize cheaper denominations, this only matters in case of equality.
		return denominations.OrderByDescending(x => x.EffectiveAmount);
	}
}
