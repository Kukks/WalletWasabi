using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using NBitcoin;

namespace WalletWasabi.Helpers
{
	public static class HashCashUtils
	{
		private static string GenerateString(int length)
		{
			var r = new Random(RandomUtils.GetInt32());

			var letters = new char[length];

			for (var i = 0; i < length; i++)
			{
				letters[i] = (char) (r.Next('A', 'Z' + 1));
			}
			return new string(letters);
		}
		public static string Compute(int bits, string resource)
		{
			var counter = int.MinValue;
			var headerParts = new[]
			{
				"1",
				bits.ToString(),
				DateTime.UtcNow.ToString("yyMMddhhmmss"),
				resource,
				"",
				Convert.ToBase64String(Encoding.UTF8.GetBytes(GenerateString(10))),
				Convert.ToBase64String(BitConverter.GetBytes(counter))
			};

			var currentStamp = string.Join(":", headerParts);

			while (!AcceptableHeader(currentStamp, bits))
			{
				counter++;

				// Failed
				if (counter == int.MaxValue)
				{
					headerParts[5] = Convert.ToBase64String(Encoding.UTF8.GetBytes(GenerateString(10)));
					counter = int.MinValue;
				}

				headerParts[6] = Convert.ToBase64String(BitConverter.GetBytes(counter));
				currentStamp = string.Join(":", headerParts);

				++counter;
			}

			return currentStamp;
		}

		private  static bool AcceptableHeader(string header, int bits)
		{
			var sha = new SHA1CryptoServiceProvider();
			var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(header));
			return GetStampHashDenomination(hash) == bits;
		}

		public static bool Verify(string header, int bitsMin, TimeSpan allowedDiff, string resource, out string error, out byte[] hash )
		{
			hash = null;
			error = null;
			var parts = header.Split(':');
			if (parts[0] != "1")
			{
				error = "Only Hashcash v1 is supported";
				return false;
			}
			var zbits = int.Parse(parts[1]);
			if (zbits < bitsMin)
			{
				error = $"Insufficient pow ({zbits} instead of {bitsMin})";
				return false;
			}

			var timestamp = DateTime.ParseExact(parts[2], "yyMMddhhmmss", null);
			if (timestamp > DateTime.UtcNow)
			{
				error = $"timestamp is in the future";
				return false;
			}
			var diff = DateTime.UtcNow - timestamp;
			if (allowedDiff < diff)
			{
				error = $"timestamp too old ({diff} instead of {allowedDiff})";
				return false;
			}

			if (resource != parts[3])
			{
				error = $"resource mismatch too old ({parts[3]} instead of {resource})";
				return false;
			}

			if (!string.IsNullOrEmpty(parts[4]))
			{
				error = $"extension not used in v1, not empty";
				return false;
			}
			if (string.IsNullOrEmpty(parts[5]))
			{
				error = $"seed invalid";
				return false;
			}

			var sha = new SHA1CryptoServiceProvider();
			hash = sha.ComputeHash(Encoding.UTF8.GetBytes(header));
			var pow =  GetStampHashDenomination(hash);
			if (pow == zbits)
			{
				return true;
			}

			error = $"actual pow invalid (expected {zbits} but found {zbits})";
			return false;
		}

		private static int GetStampHashDenomination(byte[] stampHash)
		{
			var continuousBits = new BitArray(stampHash);
			var denomination = 0;
			for (var bitIndex = 0; bitIndex < continuousBits.Length; bitIndex++)
			{
				var bit = continuousBits[bitIndex];

				if (bit)
				{
					break;
				}

				denomination++;
			}

			return denomination;
		}
	}
}