using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WalletWasabi.Extensions;
using WalletWasabi.WabiSabi.Backend.Rounds;

namespace WalletWasabi.WabiSabi.Client;

public static class DenominationBuilder
{
	public static IOrderedEnumerable<Output> CreateDenominations(Money minAllowedOutputAmount,
		Money maxAllowedOutputAmount, FeeRate feeRate, IEnumerable<ScriptType> allowedOutputTypes,
		long? minimumDenominationAmount)
	{
		var denominations = new HashSet<Output?>();

		Output? CreateDenom(double sats)
		{
			var scriptType = allowedOutputTypes.RandomElement();
			var result =  Output.FromDenomination(Money.Satoshis((ulong)sats), scriptType, feeRate);
			if ( (minimumDenominationAmount is not null && result.Amount.Satoshi < minimumDenominationAmount.Value) || result.Amount < minAllowedOutputAmount || result.Amount > maxAllowedOutputAmount)
			{
				return null;
			}
			return result;
		}

		// Powers of 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(2, i));

			if (denom is null)
			{
				continue;
			}

			denominations.Add(denom);
		}

		// Powers of 3
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i));
			if (denom is null)
			{
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 3 * 2
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(3, i) * 2);

			if (denom is null)
			{
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i));

			if (denom is null)
			{
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 * 2 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 2);

			if (denom is null)
			{
				continue;
			}
			denominations.Add(denom);
		}

		// Powers of 10 * 5 (1-2-5 series)
		for (int i = 0; i < int.MaxValue; i++)
		{
			var denom = CreateDenom(Math.Pow(10, i) * 5);

			if (denom is null)
			{
				continue;
			}
			denominations.Add(denom);
		}

		// Greedy decomposer will take the higher values first. Order in a way to prioritize cheaper denominations, this only matters in case of equality.
		return denominations.OrderByDescending(x => x.EffectiveAmount);
	}
}
