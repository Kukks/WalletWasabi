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
using Microsoft.EntityFrameworkCore;
using WalletWasabi.Backend.Data;
using WalletWasabi.Backend.Models;
using WalletWasabi.Logging;

namespace WalletWasabi.Backend
{
	public class SendPushService
	{
		private readonly IDbContextFactory<WasabiBackendContext> ContextFactory;
		private readonly Config Config;

		private string KeyPath => Config.APNsAuthKeyFile;
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

		public SendPushService(IDbContextFactory<WasabiBackendContext> contextFactory, Config config)
		{
			ContextFactory = contextFactory;
			Config = config;
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
			await using var context = ContextFactory.CreateDbContext();
			var client = new HttpClient();
			client.DefaultRequestVersion = HttpVersion.Version20;
			var content = new StringContent(_payload, Encoding.UTF8, "application/json");
			client.DefaultRequestHeaders.Add("apns-topic", _bundleId);
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateAuthenticationHeader());

			//removeToken prepared statement
			var server = isDebug ? "api.development" : "api";
			var tokens = context.Tokens
				.Where(t => t.IsDebug == isDebug)
				.Distinct();
			await Task.WhenAll(tokens.Select(async token =>
			{
				var url = $"https://{server}.push.apple.com/3/device/{token}";
				var res = await client.PostAsync(url, content);

				if (!res.IsSuccessStatusCode)
				{
					Logger.LogError($"HttpPost to APNs failed: {res.Content}");
				}

				if (res.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Gone)
				{
					if (res.ReasonPhrase == "BadDeviceToken")
					{
						context.Tokens.Remove(token);
					}
				}
			}));
			await context.SaveChangesAsync();
		}

	}
}
