using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletWasabi.Blockchain.Keys;
using WalletWasabi.Exceptions;
using WalletWasabi.Helpers;
using WalletWasabi.Logging;
using WalletWasabi.TorSocks5;

namespace WalletWasabi.WebClients.PayJoin
{
	public class PayjoinClient : IPayjoinClient
	{
		public PayjoinClient(Uri paymentUrl, EndPoint torSocks5EndPoint)
		{
			TorSocks5EndPoint = Guard.NotNull(nameof(torSocks5EndPoint), torSocks5EndPoint);
			PaymentUrl = Guard.NotNull(nameof(paymentUrl), paymentUrl);
			TorHttpClient = new TorHttpClient(PaymentUrl, TorSocks5EndPoint);
		}
		public PayjoinClient(Uri paymentUrl, HttpClient httpClient)
		{
			PaymentUrl = Guard.NotNull(nameof(paymentUrl), paymentUrl);
			ClearnetHttpClient = Guard.NotNull(nameof(httpClient), httpClient);
		}

		// For testing only
		internal PayjoinClient(ITorHttpClient httpClient)
		{
			TorHttpClient = Guard.NotNull(nameof(httpClient), httpClient);
		}

		public Uri PaymentUrl { get; }
		private EndPoint TorSocks5EndPoint { get; }
		private ITorHttpClient TorHttpClient { get; }
		private HttpClient ClearnetHttpClient { get; }

		private async Task<PSBT> RequestPayjoin(PSBT originalTx, IHDKey accountKey, RootedKeyPath rootedKeyPath, CancellationToken cancellationToken)
		{
			Guard.NotNull(nameof(originalTx), originalTx);
			if (originalTx.IsAllFinalized())
			{
				throw new InvalidOperationException("The original PSBT should not be finalized.");
			}

			var sentBefore = -originalTx.GetBalance(ScriptPubKeyType.Segwit, accountKey, rootedKeyPath);
			var oldGlobalTx = originalTx.GetGlobalTransaction();

			if (!originalTx.TryGetEstimatedFeeRate(out var originalFeeRate) || !originalTx.TryGetVirtualSize(out var oldVirtualSize))
			{
				throw new ArgumentException("originalTx should have utxo information", nameof(originalTx));
			}

			var originalFee = originalTx.GetFee();
			var cloned = originalTx.Clone();
			if (!cloned.TryFinalize(out var _))
			{
				return null;
			}

			// We make sure we don't send unnecessary information to the receiver
			foreach (var finalized in cloned.Inputs.Where(i => i.IsFinalized()))
			{
				finalized.ClearForFinalize();
			}

			foreach (var output in cloned.Outputs)
			{
				output.HDKeyPaths.Clear();
			}

			cloned.GlobalXPubs.Clear();
			var request = new HttpRequestMessage(HttpMethod.Post, PaymentUrl)
			{
				Content = new StringContent(cloned.ToHex(), Encoding.UTF8, "text/plain")
			};
			HttpResponseMessage bpuResponse = null;
			if (TorHttpClient == null)
			{
				bpuResponse = await new HttpClient().SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				bpuResponse = await TorHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			}
			if (!bpuResponse.IsSuccessStatusCode)
			{
				var errorStr = await bpuResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
				try
				{
					var error = JObject.Parse(errorStr);
					throw new PayjoinReceiverException((int)bpuResponse.StatusCode,
						error["errorCode"].Value<string>(),
						error["message"].Value<string>());
				}
				catch (JsonReaderException)
				{
					// will throw
					bpuResponse.EnsureSuccessStatusCode();
					throw;
				}
			}

			var hexOrBase64 = await bpuResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
			var newPSBT = PSBT.Parse(hexOrBase64, originalTx.Network);

			// Checking that the PSBT of the receiver is clean
			if (newPSBT.GlobalXPubs.Any())
			{
				throw new PayjoinSenderException("GlobalXPubs should not be included in the receiver's PSBT");
			}

			if (newPSBT.Outputs.Any(o => o.HDKeyPaths.Count != 0) || newPSBT.Inputs.Any(o => o.HDKeyPaths.Count != 0))
			{
				throw new PayjoinSenderException("Keypath information should not be included in the receiver's PSBT");
			}

			if (newPSBT.CheckSanity() is IList<PSBTError> errors2 && errors2.Count != 0)
			{
				throw new PayjoinSenderException($"The PSBT of the receiver is insane ({errors2[0]})");
			}

			// Do not trust on inputs order because the payjoin server should shuffle them.
			foreach (var input in originalTx.Inputs)
			{
				var newInput = newPSBT.Inputs.FindIndexedInput(input.PrevOut);
				if (newInput is { })
				{
					newInput.UpdateFrom(input);
					newInput.PartialSigs.Clear();
				}
			}

			// We make sure we don't sign things that should not be signed.
			foreach (var finalized in newPSBT.Inputs.Where(i => i.IsFinalized()))
			{
				finalized.ClearForFinalize();
			}

			// We make sure we don't sign things that should not be signed.
			foreach (var output in newPSBT.Outputs)
			{
				output.HDKeyPaths.Clear();
				foreach (var originalOutput in originalTx.Outputs)
				{
					if (output.ScriptPubKey == originalOutput.ScriptPubKey)
					{
						output.UpdateFrom(originalOutput);
					}
				}
			}

			var newGlobalTx = newPSBT.GetGlobalTransaction();
			if (newGlobalTx.Version != oldGlobalTx.Version)
			{
				throw new PayjoinSenderException("The version field of the transaction has been modified");
			}

			if (newGlobalTx.LockTime != oldGlobalTx.LockTime)
			{
				throw new PayjoinSenderException("The LockTime field of the transaction has been modified");
			}

			// Making sure that our inputs are finalized, and that some of our inputs have not been added.
			int ourInputCount = 0;
			var accountHDScriptPubkey = new HDKeyScriptPubKey(accountKey, ScriptPubKeyType.Segwit);
			foreach (var input in newPSBT.Inputs.CoinsFor(accountHDScriptPubkey, accountKey, rootedKeyPath))
			{
				if (oldGlobalTx.Inputs.FindIndexedInput(input.PrevOut) is IndexedTxIn ourInput)
				{
					ourInputCount++;
					if (input.IsFinalized())
					{
						throw new PayjoinSenderException("A PSBT input from us should not be finalized");
					}

					if (newGlobalTx.Inputs[input.Index].Sequence != ourInput.TxIn.Sequence)
					{
						throw new PayjoinSenderException("The sequence of one of our input has been modified");
					}
				}
				else
				{
					throw new PayjoinSenderException("The payjoin receiver added some of our own inputs in the proposal");
				}
			}

			foreach (var input in newPSBT.Inputs)
			{
				if (originalTx.Inputs.FindIndexedInput(input.PrevOut) is null)
				{
					if (!input.IsFinalized())
					{
						throw new PayjoinSenderException("The payjoin receiver included a non finalized input");
					}
					// Making sure that the receiver's inputs are finalized and match format
					var payjoinInputType = input.GetInputScriptPubKeyType();
					if (payjoinInputType is null || payjoinInputType.Value != ScriptPubKeyType.Segwit)
					{
						throw new PayjoinSenderException("The payjoin receiver included an input that is not the same segwit input type");
					}
				}
			}

			// Making sure that the receiver's inputs are finalized
			foreach (var input in newPSBT.Inputs)
			{
				if (originalTx.Inputs.FindIndexedInput(input.PrevOut) is null && !input.IsFinalized())
				{
					throw new PayjoinSenderException("The payjoin receiver included a non finalized input");
				}
			}

			if (ourInputCount < originalTx.Inputs.Count)
			{
				throw new PayjoinSenderException("The payjoin receiver removed some of our inputs");
			}

			// We limit the number of inputs the receiver can add
			var addedInputs = newPSBT.Inputs.Count - originalTx.Inputs.Count;
			if (originalTx.Inputs.Count < addedInputs)
			{
				throw new PayjoinSenderException("The payjoin receiver added too much inputs");
			}

			var sentAfter = -newPSBT.GetBalance(ScriptPubKeyType.Segwit, accountKey, rootedKeyPath);

			if (sentAfter > sentBefore)
			{
				var overPaying = sentAfter - sentBefore;
				if (!newPSBT.TryGetEstimatedFeeRate(out var newFeeRate) || !newPSBT.TryGetVirtualSize(out var newVirtualSize))
				{
					throw new PayjoinSenderException("The payjoin receiver did not include UTXO information to calculate fee correctly");
				}

				var additionalFee = newPSBT.GetFee() - originalFee;
				if (overPaying > additionalFee)
				{
					throw new PayjoinSenderException("The payjoin receiver is sending more money to himself");
				}

				if (overPaying > originalFee)
				{
					throw new PayjoinSenderException("The payjoin receiver is making us pay more than twice the original fee");
				}

				// Let's check the difference is only for the fee and that feerate
				// did not change that much
				var expectedFee = originalFeeRate.GetFee(newVirtualSize);
				// Signing precisely is hard science, give some breathing room for error.
				expectedFee += originalFeeRate.GetFee(newPSBT.Inputs.Count * 2);
				if (overPaying > (expectedFee - originalFee))
				{
					throw new PayjoinSenderException("The payjoin receiver increased the fee rate we are paying too much");
				}
			}

			return newPSBT;
		}
		
		public async Task<PSBT> TryNegotiatePayjoin(Func<PSBT, CancellationToken, Task<(PSBT PSBT, bool Signed)>> sign, PSBT psbt, KeyManager keyManager, CancellationToken cancellationToken)
		{
			try
			{
				Logger.LogInfo($"Negotiating payjoin payment with `{PaymentUrl}`.");

				psbt = await RequestPayjoin(psbt,
					keyManager.ExtPubKey,
					new RootedKeyPath(keyManager.MasterFingerprint.Value, keyManager.AccountKeyPath),
					cancellationToken);
				if (psbt == null)
				{
					return null;
				}

				var signedPayjoinPsbt = await sign.Invoke(psbt, cancellationToken);
				if (!signedPayjoinPsbt.Signed)
				{
					Logger.LogWarning($"Payjoin PSBT could not be signed. Ignoring...");
					return null;
				}
				Logger.LogInfo($"Payjoin payment was negotiated successfully.");
				return signedPayjoinPsbt.PSBT;
			}
			catch (TorSocks5FailureResponseException e)
			{
				if (e.Message.Contains("HostUnreachable"))
				{
					Logger.LogWarning($"Payjoin server is not reachable. Ignoring...");
				}

				// ignore
			}
			catch (HttpRequestException e)
			{
				Logger.LogWarning($"Payjoin server responded with {e.ToTypeMessageString()}. Ignoring...");
			}
			catch (PayjoinException e)
			{
				Logger.LogWarning($"Payjoin server responded with {e.Message}. Ignoring...");
			}
			catch (Exception e)
			{
				Logger.LogError($"Payjoin payment ran into an issue {e.Message}. Ignoring...");
				//the show must go on. There is a chance that the transaction was received or exposed to the receiver or listening third-party. Not attempting to broadcast the original now leave you vulnerable to the tx being broadcasted eventually.
			}

			return null;
		}
	}
}
