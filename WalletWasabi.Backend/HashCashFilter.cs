using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
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
		public virtual int MinPow { get; }

		protected HashCashFilter()
		{
		}

		public HashCashFilter(string resource, TimeSpan maxDifference, int minPow)
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
			if (!context.HttpContext.Request.Headers.TryGetValue("X-Hashcash", out var xhashcash))
			{
				context.Result = new BadRequestObjectResult("Missing X-Hashcash header");
			}

			var resource = GetResource(context);
			if (resource is null)
			{
				return;
			}

			var memoryCache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();

			if (!HashCashUtils.Verify(xhashcash, MinPow, MaxDifference, resource, s =>
			{
				var cacheKey = $"{nameof(HashCashFilter)}_seed_{s}";
				if(memoryCache.TryGetValue(cacheKey, out _))
				{
					return false;
				}

				memoryCache.CreateEntry(cacheKey).SlidingExpiration = TimeSpan.FromHours(1);
				return true;
			}))
			{
				context.Result = new BadRequestObjectResult(
					$"Invalid hashcash computation ({MinPow} pow min with utc datetime less than {MaxDifference.ToString()} ago)");
			}
		}
	}
}