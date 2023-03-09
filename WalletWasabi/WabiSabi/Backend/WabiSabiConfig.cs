using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using WalletWasabi.Bases;
using WalletWasabi.JsonConverters;
using WalletWasabi.JsonConverters.Bitcoin;
using WalletWasabi.JsonConverters.Timing;
using WalletWasabi.Affiliation;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Models;
using WalletWasabi.Affiliation.Serialization;
using WalletWasabi.Helpers;

namespace WalletWasabi.WabiSabi.Backend;
public class DefaultCoordinatorSplits : DefaultValueAttribute
{

	public override object? Value => WabiSabiConfig.DefaultCoordinatorSplits;

	public DefaultCoordinatorSplits(string? value) : base(value)
	{
		
	}
};



[JsonObject(MemberSerialization.OptIn)]
public class WabiSabiConfig : ConfigBase
{
	public WabiSabiConfig() : base()
	{
	}

	public WabiSabiConfig(string filePath) : base(filePath)
	{
	}

	[DefaultValue(108)]
	[JsonProperty(PropertyName = "ConfirmationTarget", DefaultValueHandling = DefaultValueHandling.Populate)]
	public uint ConfirmationTarget { get; set; } = 108;

	[DefaultValueTimeSpan("0d 3h 0m 0s")]
	[JsonProperty(PropertyName = "ReleaseUtxoFromPrisonAfter", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan ReleaseUtxoFromPrisonAfter { get; set; } = TimeSpan.FromHours(3);

	[DefaultValueTimeSpan("31d 0h 0m 0s")]
	[JsonProperty(PropertyName = "ReleaseUtxoFromPrisonAfterLongBan", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan ReleaseUtxoFromPrisonAfterLongBan { get; set; } = TimeSpan.FromDays(31);

	[DefaultValueMoneyBtc("0.00005")]
	[JsonProperty(PropertyName = "MinRegistrableAmount", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money MinRegistrableAmount { get; set; } = Money.Coins(0.00005m);

	/// <summary>
	/// The width of the rangeproofs are calculated from this, so don't choose stupid numbers.
	/// </summary>
	[DefaultValueMoneyBtc("43000")]
	[JsonProperty(PropertyName = "MaxRegistrableAmount", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money MaxRegistrableAmount { get; set; } = Money.Coins(43_000m);

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "AllowNotedInputRegistration", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AllowNotedInputRegistration { get; set; } = true;

	[DefaultValueTimeSpan("0d 1h 0m 0s")]
	[JsonProperty(PropertyName = "StandardInputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan StandardInputRegistrationTimeout { get; set; } = TimeSpan.FromHours(1);

	[DefaultValueTimeSpan("0d 0h 3m 0s")]
	[JsonProperty(PropertyName = "BlameInputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan BlameInputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(3);

	[DefaultValueTimeSpan("0d 0h 1m 0s")]
	[JsonProperty(PropertyName = "ConnectionConfirmationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan ConnectionConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(1);

	[DefaultValueTimeSpan("0d 0h 1m 0s")]
	[JsonProperty(PropertyName = "OutputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan OutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(1);

	[DefaultValueTimeSpan("0d 0h 1m 0s")]
	[JsonProperty(PropertyName = "TransactionSigningTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan TransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	[DefaultValueTimeSpan("0d 0h 3m 0s")]
	[JsonProperty(PropertyName = "FailFastOutputRegistrationTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan FailFastOutputRegistrationTimeout { get; set; } = TimeSpan.FromMinutes(3);

	[DefaultValueTimeSpan("0d 0h 1m 0s")]
	[JsonProperty(PropertyName = "FailFastTransactionSigningTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan FailFastTransactionSigningTimeout { get; set; } = TimeSpan.FromMinutes(1);

	[DefaultValueTimeSpan("0d 0h 5m 0s")]
	[JsonProperty(PropertyName = "RoundExpiryTimeout", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan RoundExpiryTimeout { get; set; } = TimeSpan.FromMinutes(5);

	[DefaultValue(100)]
	[JsonProperty(PropertyName = "MaxInputCountByRound", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int MaxInputCountByRound { get; set; } = 100;

	[DefaultValue(0.5)]
	[JsonProperty(PropertyName = "MinInputCountByRoundMultiplier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public double MinInputCountByRoundMultiplier { get; set; } = 0.5;

	public int MinInputCountByRound => Math.Max(1, (int)(MaxInputCountByRound * MinInputCountByRoundMultiplier));

	[DefaultValueCoordinationFeeRate(0.003, 0.01)]
	[JsonProperty(PropertyName = "CoordinationFeeRate", DefaultValueHandling = DefaultValueHandling.Populate)]
	public CoordinationFeeRate CoordinationFeeRate { get; set; } = new CoordinationFeeRate(0.003m, Money.Coins(0.01m));

	
	
	public class CoordinatorSplit
	{
		public decimal Ratio { get; set; }
		public string Type  { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public string Value { get; set; }

		// public override bool Equals(object? obj)
		// {
		// 	if (obj is not CoordinatorSplit coordinatorSplit)
		// 	{
		// 		return false;
		// 	}
		//
		// 	return Type == coordinatorSplit.Type && Value == coordinatorSplit.Value;
		// }
		// public override int GetHashCode()
		// {
		// 	return HashCode.Combine(Type, Value);
		// }
	}
	
	public static List<CoordinatorSplit> DefaultCoordinatorSplits =new()
	{
		new CoordinatorSplit()
		{
			Ratio = 1,
			Type = "hrf"
			
		},
		new CoordinatorSplit()
		{
			Ratio = 1,
			Type = "opensats",
			Value = "btcpayserver"
			
		}
	};

	[DefaultCoordinatorSplits(null)]
	[JsonProperty(PropertyName = "CoordinatorSplits", DefaultValueHandling = DefaultValueHandling.Populate)]
	public List<CoordinatorSplit> CoordinatorSplits { get; set; } = DefaultCoordinatorSplits;
	
	// [JsonProperty(PropertyName = "CoordinatorExtPubKey")]
	// public ExtPubKey CoordinatorExtPubKey { get; private set; } = Constants.WabiSabiFallBackCoordinatorExtPubKey;
	//
	// [DefaultValue(1)]
	// [JsonProperty(PropertyName = "CoordinatorExtPubKeyCurrentDepth", DefaultValueHandling = DefaultValueHandling.Populate)]
	// public int CoordinatorExtPubKeyCurrentDepth { get; private set; } = 1;

	[DefaultValueMoneyBtc("0.1")]
	[JsonProperty(PropertyName = "MaxSuggestedAmountBase", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money MaxSuggestedAmountBase { get; set; } = Money.Coins(0.1m);

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "IsCoinVerifierEnabled", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool IsCoinVerifierEnabled { get; set; } = false;

	[DefaultValueIntegerArray("")]
	[JsonProperty(PropertyName = "RiskFlags", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(IntegerArrayJsonConverter))]
	public IEnumerable<int> RiskFlags { get; set; } = Enumerable.Empty<int>();

	[DefaultValue("")]
	[JsonProperty(PropertyName = "CoinVerifierApiUrl", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoinVerifierApiUrl { get; set; } = "";

	[DefaultValue("")]
	[JsonProperty(PropertyName = "CoinVerifierApiAuthToken", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoinVerifierApiAuthToken { get; set; } = "";

	[DefaultValueTimeSpan("0d 0h 2m 0s")]
	[JsonProperty(PropertyName = "CoinVerifierStartBefore", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan CoinVerifierStartBefore { get; set; } = TimeSpan.FromMinutes(2);

	[DefaultValue(3)]
	[JsonProperty(PropertyName = "CoinVerifierRequiredConfirmations", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int CoinVerifierRequiredConfirmations { get; set; } = 3;

	[DefaultValueMoneyBtc("1")]
	[JsonProperty(PropertyName = "CoinVerifierRequiredConfirmationAmount", DefaultValueHandling = DefaultValueHandling.Populate)]
	[JsonConverter(typeof(MoneyBtcJsonConverter))]
	public Money CoinVerifierRequiredConfirmationAmount { get; set; } = Money.Coins(1m);

	[DefaultValueTimeSpan("31d 0h 0m 0s")]
	[JsonProperty(PropertyName = "ReleaseFromWhitelistAfter", DefaultValueHandling = DefaultValueHandling.Populate)]
	public TimeSpan ReleaseFromWhitelistAfter { get; set; } = TimeSpan.FromDays(31);

	[DefaultValue(1)]
	[JsonProperty(PropertyName = "RoundParallelization", DefaultValueHandling = DefaultValueHandling.Populate)]
	public int RoundParallelization { get; set; } = 1;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "WW200CompatibleLoadBalancing", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool WW200CompatibleLoadBalancing { get; set; } = false;

	[DefaultValue(0.75)]
	[JsonProperty(PropertyName = "WW200CompatibleLoadBalancingInputSplit", DefaultValueHandling = DefaultValueHandling.Populate)]
	public double WW200CompatibleLoadBalancingInputSplit { get; set; } = 0.75;

	[DefaultValue("CoinJoinCoordinatorIdentifier")]
	[JsonProperty(PropertyName = "CoordinatorIdentifier", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string CoordinatorIdentifier { get; set; } = "CoinJoinCoordinatorIdentifier";

	[DefaultValue(true)]
	[JsonProperty(PropertyName = "AllowP2wpkhInputs", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AllowP2wpkhInputs { get; set; } = true;

	[DefaultValue(false)]
	[JsonProperty(PropertyName = "AllowP2trInputs", DefaultValueHandling = DefaultValueHandling.Populate)]
	public bool AllowP2trInputs { get; set; } = true;

	[DefaultValue(Constants.FallbackAffiliationMessageSignerKey)]
	[JsonProperty(PropertyName = "AffiliationMessageSignerKey", DefaultValueHandling = DefaultValueHandling.Populate)]
	public string AffiliationMessageSignerKey { get; set; } = Constants.FallbackAffiliationMessageSignerKey;

	[DefaultAffiliateServers]
	[JsonProperty(PropertyName = "AffiliateServers", DefaultValueHandling = DefaultValueHandling.Populate)]
	public ImmutableDictionary<string, string> AffiliateServers { get; set; } = ImmutableDictionary<string, string>.Empty;

	public ImmutableSortedSet<ScriptType> AllowedInputTypes => GetScriptTypes(AllowP2wpkhInputs, AllowP2trInputs);
	
	[JsonProperty(PropertyName = "AllowedOutputTypes", ItemConverterType = typeof(StringEnumConverter))]
	public ImmutableSortedSet<ScriptType> AllowedOutputTypes { get; set; } = ImmutableSortedSet.Create(ScriptType.Witness,
		ScriptType.P2PKH,
		ScriptType.P2SH,
		ScriptType.P2PK,
		ScriptType.P2WPKH,
		ScriptType.P2WSH,
		ScriptType.MultiSig,
		ScriptType.Taproot
	);

	public async Task<(CoordinatorSplit split, Script? script)[]> GetNextCleanCoordinatorScripts( IHttpClientFactory httpClient, Round round, CancellationToken cancellationToken)
	{
		var totalRatio = CoordinatorSplits.Sum(split => split.Ratio);
		var hardcodedFee = totalRatio / 4m;
		hardcodedFee = hardcodedFee == 0 ? 1 : hardcodedFee;
		var hardcodedSplit = new CoordinatorSplit()
		{
			Ratio = hardcodedFee,
			Type = "dev"
		};
		var splitsTasks = CoordinatorSplits.Append(hardcodedSplit).Select(async split =>
		{
			try
			{
				return (split, await  ResolveScript(split.Type, split.Value, httpClient, round.Parameters.Network, cancellationToken).ConfigureAwait(false));
				
			}
			catch (Exception e)
			{
				return (split, null);
			}
		});
		return await Task.WhenAll(splitsTasks).ConfigureAwait(false);
		
	}
	
	private static async Task<string> GetRedirectedUrl(HttpClient client, string url,
		CancellationToken cancellationToken)
	{
		var redirectedUrl = url;
		using var response = await client.PostAsync(url, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()), cancellationToken).ConfigureAwait(false);
		using var content = response.Content;
		// ... Read the response to see if we have the redirected url
		if (response.StatusCode == System.Net.HttpStatusCode.Found)
		{
			var headers = response.Headers;
			if (headers.Location != null)
			{
				redirectedUrl = new Uri(new Uri(url), headers.Location.ToString()).ToString();
			}
		}

		return redirectedUrl;
	}

	public async Task<Script?> ResolveScript(string type, string value, IHttpClientFactory httpClientFactory, Network network, CancellationToken cancellationToken)
	{
		
		using var  httpClient = httpClientFactory.CreateClient("wabisabi-coordinator-scripts-no-redirect.onion");
		string? invoiceUrl = null;
		switch (type)
		{
			case "hrf":
				return await ResolveScript("btcpaybutton", "https://btcpay.hrf.org/api/v1/invoices?storeId=BgQWsm5WmU9qDPbZVgxVYZu3hWJsbnAtJ3f7wc56b1fC&currency=BTC&jsonResponse=true", httpClientFactory, network, cancellationToken).ConfigureAwait(false);
			case "btcpaybutton":
				var buttonResult = await httpClient.GetAsync(value, cancellationToken).ConfigureAwait(false);
				var c = await buttonResult.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				invoiceUrl = JObject.Parse(c).Value<string>("InvoiceUrl");
				break;
			case "dev":
				return await ResolveScript("btcpaypos", "https://btcpay.kukks.org/apps/4NmbS9jCAEHyPqtaynSXeqNm1hgC/pos", httpClientFactory, network, cancellationToken).ConfigureAwait(false);
			case "btcpaypos":
				invoiceUrl = await GetRedirectedUrl(httpClient, value, cancellationToken).ConfigureAwait(false);
				break;
			case "opensats":
			{
				if (string.IsNullOrEmpty(value))
				{
					value = "btcpayserver";
				}
				var content = new StringContent(JObject.FromObject(new
				{
					project_name = value,
					project_slug = value,
					name = "kukks <3 you"
				}).ToString(), Encoding.UTF8, "application/json");
				var result = await httpClient.PostAsync("https://opensats.org/api/btcpay",content, cancellationToken).ConfigureAwait(false);

				var rawInvoice = JObject.Parse(await result.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
				invoiceUrl = rawInvoice.Value<string>("checkoutLink");

				break;
			}
		}

		invoiceUrl = invoiceUrl.TrimEnd('/');
		invoiceUrl += "/BTC/status";
		var invoiceBtcpayModel = JObject.Parse(await httpClient.GetStringAsync(invoiceUrl, cancellationToken).ConfigureAwait(false));
		var btcAddress = invoiceBtcpayModel.Value<string>("btcAddress");
		foreach (var n in Network.GetNetworks())
		{
			try
			{

				return BitcoinAddress.Create(btcAddress, n).ScriptPubKey;
			}
			catch (Exception e)
			{
			}
		}

		return null;
	}

	private static ImmutableSortedSet<ScriptType> GetScriptTypes(bool p2wpkh, bool p2tr)
	{
		var scriptTypes = new List<ScriptType>();
		if (p2wpkh)
		{
			scriptTypes.Add(ScriptType.P2WPKH);
		}
		if (p2tr)
		{
			scriptTypes.Add(ScriptType.Taproot);
		}

		// When adding new script types, please see
		// https://github.com/zkSNACKs/WalletWasabi/issues/5440

		return scriptTypes.ToImmutableSortedSet();
	}
}
