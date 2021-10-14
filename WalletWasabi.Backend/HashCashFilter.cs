using System;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using NBitcoin.DataEncoders;
using WalletWasabi.Helpers;

namespace WalletWasabi.Backend
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
	public class HashCashFilter : Attribute, IActionFilter
	{
		private readonly string _resource;

		public virtual string GetResource(ActionExecutingContext context)
		{
			return _resource;
		}

		public virtual TimeSpan MaxDifference { get; }
		public virtual int? MinPow { get; }

		protected HashCashFilter()
		{
		}

		public HashCashFilter(string resource, TimeSpan maxDifference, int? minPow)
		{
			_resource = resource;
			MaxDifference = maxDifference;
			MinPow = minPow;
		}

		public void OnActionExecuted(ActionExecutedContext context)
		{
		}

		public void OnActionExecuting(ActionExecutingContext context)
		{

			var config = context.HttpContext.RequestServices.GetRequiredService<Config>();
			var pow = MinPow?? config.HashCashDifficulty;
			var resource = GetResource(context);
			if (resource is null || pow <= 0)
			{
				return;
			}
			context.HttpContext.Response.Headers.Add("X-Hashcash-Challenge", new StringValues($"1:{pow}:{DateTime.UtcNow:yyMMddhhmmss}:{resource}:whatever:0"));
			if (!context.HttpContext.Request.Headers.TryGetValue("X-Hashcash", out var xhashcash))
			{
				context.Result = new BadRequestObjectResult("Missing X-Hashcash header");
			}

			var memoryCache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

			if (!HashCashUtils.Verify(xhashcash, MinPow, MaxDifference, resource, out var error, out var hash))
			{
				context.Result = new BadRequestObjectResult(
					$"Invalid hashcash: {error}");
			}
			var cacheKey = $"{nameof(HashCashFilter)}_hash_{Encoders.Hex.EncodeData(hash)}";
			if(memoryCache.TryGetValue(cacheKey, out _))
			{
				context.Result = new BadRequestObjectResult(
					$"Invalid hashcash: hash seen before");
			}

			memoryCache.CreateEntry(cacheKey).SlidingExpiration = MaxDifference;
		}
	}
}