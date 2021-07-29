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

		private string _keyPath = "/Users/Dan/Downloads/AuthKey_4L3728R8LJ.p8";
		private string _auth_key_id = "4L3728R8LJ";
		private string _teamId = "9Z72DXKVXK"; // Chaincase LLC
		private string _bundleId = "cash.chaincase.testnet"; // APNs Development iOS
		private string _payload = @"{
				""aps"": {
					""content-available"": 1,
					""alert"": ""Doing CoinJoin Crypto..."",
					""sound"": ""default"",
				}
			}";
		private int _spam = 0;

		public SendPushService(IDbContextFactory<WasabiBackendContext> contextFactory)
		{
			ContextFactory = contextFactory;
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
				iat = DateTimeOffset.Now.ToUnixTimeSeconds()
			});
			var claims = Convert.ToBase64String(claimsBytes);

			var p8KeySpan = GetBytesFromPem(_keyPath);
			var signer = ECDsa.Create();
			signer.ImportPkcs8PrivateKey(p8KeySpan, out _);
			var dataToSign = Encoding.UTF8.GetBytes($"{header}.{claims}");
			var signatureBytes = signer.SignData(dataToSign, HashAlgorithmName.SHA256);

			var signature = Convert.ToBase64String(signatureBytes);

			return $"{header}.{claims}.{signature}";
		}

		/// <summary>
		/// Apple gives us a APNs Auth Key to sign jwts with in a p8 pem file.
		/// This method reads that
		/// </summary>
		public static byte[] GetBytesFromPem(string pemFile)
		{
			var p8File = File.ReadAllLines(pemFile);
			var p8Key = p8File.Skip(1).SkipLast(1); // Remove PEM bookends
			var base64Key = string.Join("", p8Key);
			return Convert.FromBase64String(base64Key);
		}

		public async Task SendNotificationsAsync(bool isDebug)
		{
			await using var context = ContextFactory.CreateDbContext();
			var client = new HttpClient();
			client.DefaultRequestVersion = HttpVersion.Version20;
			var content = new StringContent(_payload, Encoding.UTF8, "application/json");
			client.DefaultRequestHeaders.Add("apns-topic", _bundleId);
			client.DefaultRequestHeaders.Add("apns-push-type", "alert");
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GenerateAuthenticationHeader());

			//removeToken prepared statement
			var server = isDebug ? "api.sandbox" : "api";
			var tokens = context.Tokens
				.Where(t => t.IsDebug == isDebug)
				.Distinct();

			Console.WriteLine(tokens.Count());
			foreach(var token in tokens)
			{
				await SendNotificationAsync(token, server, context, content, client);
			}
			//await Task.WhenAll(tokens.Select(token => SendNotificationAsync(token, server, context, content, client)));
			await context.SaveChangesAsync();
		}

		public async Task SendNotificationAsync(AppleDeviceToken token, string server, WasabiBackendContext context, StringContent content, HttpClient client)
		{
			if (_spam++ % 2 == 1)
			{
				return;
			}

			var url = $"https://{server}.push.apple.com/3/device/{token.Token}";
			var guid = Guid.NewGuid().ToString();
			content.Headers.Add("apns-id", guid);
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
		}
	}
}

//{ StatusCode: 403, ReasonPhrase: 'Forbidden', Version: 2.0, Content: System.Net.Http.HttpConnectionResponseContent, Headers: { apns - id: 79FFDC32 - BAA9 - 17E3 - B5A7 - F67C24157B52 } }
