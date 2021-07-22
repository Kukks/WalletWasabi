using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WalletWasabi.Backend.Data;
using WalletWasabi.Backend.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public class SendPushService
	{
		private readonly string KeyPath = "/home/Dan/Downloads/AuthKey_4L3728R8LJ.p8";
		private WasabiBackendContext Context { get; set; }
		private string _auth_key_id = "4L3728R8LJ";
		private string _teamId = "9Z72DXKVXK"; // Chaincase LLC
		private string _bundleId = "cash.chaincase.testnet"; // APNs Development iOS
		private string _payload = @"{
				'aps': {
					'content-available': 1,
					'alert': 'Finalising CoinJoin',
					'sound': 'default'
					}
				}
			}";

		public SendPushService()
		{
		}

		private string GenerateAuthenticationHeader()
		{
			var headerBytes = JsonSerializer.SerializeToUtf8Bytes(new {
				alg = "ES256",
				kid = _auth_key_id
			});
			var header = Convert.ToBase64String(headerBytes);

			var claimsBytes = JsonSerializer.SerializeToUtf8Bytes(new
			{
				iss = _teamId,
				iat = DateTime.Now
			});
			var claims = Convert.ToBase64String(claimsBytes);

			var p8KeySpan = File.ReadAllBytes(KeyPath).AsSpan();
			var signer = ECDsa.Create();
			signer.ImportPkcs8PrivateKey(p8KeySpan, out int _);
			var dataToSign = Encoding.UTF8.GetBytes($"{header}.{claims}");
			var signatureBytes = signer.SignData(dataToSign, HashAlgorithmName.SHA256);

			var signature = Convert.ToBase64String(signatureBytes);

			return $"{header}.{claims}.{signature}";
		}

		public async Task SendNotificationsAsync(bool isDebug)
		{
			var client = new HttpClient();
			client.DefaultRequestVersion = HttpVersion.Version20;
			var content = new StringContent(_payload, Encoding.UTF8, "application/json");
			client.DefaultRequestHeaders.Add("apns-topic", _bundleId);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateAuthenticationHeader());

			//removeToken prepared statement
			var server = isDebug ? "api.development" : "api";
			// get options from config? or context from factory?
			using var context = new WasabiBackendContext(null);
			var tokens = Context.Tokens
				.Where(t => t.IsDebug == isDebug)
				.Distinct();
			foreach (var token in tokens)
			{
				var url = $"https://{server}.push.apple.com/3/device/{token}";
				var res = await client.PostAsync(url, content);

				if (!res.IsSuccessStatusCode)
				{
					Logger.LogError($"HttpPost to APNs failed: {res.Content}");
				}

				if (res.StatusCode == HttpStatusCode.BadRequest ||
					res.StatusCode == HttpStatusCode.Gone)
				{
					if(res.ReasonPhrase == "BadDeviceToken")
					{
						Context.Tokens.Remove(token);
					}
				}
			} // TODO parallelize
		}

	}
}
