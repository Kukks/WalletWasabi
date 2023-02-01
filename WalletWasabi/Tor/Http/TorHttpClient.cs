using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.Tor.Socks5.Exceptions;
using WalletWasabi.Tor.Socks5.Pool;
using WalletWasabi.Tor.Socks5.Pool.Circuits;

namespace WalletWasabi.Tor.Http;

public class TorHttpClient : IHttpClient
{
	/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
	public TorHttpClient(Uri baseUri, TorHttpPool torHttpPool, Mode mode = Mode.DefaultCircuit) :
		this(() => baseUri, torHttpPool, mode)
	{
	}

	/// <summary>Use this constructor when you want to issue relative or absolute HTTP requests.</summary>
	public TorHttpClient(Func<Uri>? baseUriGetter, TorHttpPool torHttpPool, Mode mode = Mode.DefaultCircuit, ICircuit? circuit = null)
	{
		BaseUriGetter = baseUriGetter;
		TorHttpPool = torHttpPool;
		Mode = mode;

		if (mode == Mode.SingleCircuitPerLifetime && circuit is null)
		{
			throw new NotSupportedException("Circuit is required in this case.");
		}

		PredefinedCircuit = mode switch
		{
			Mode.DefaultCircuit => DefaultCircuit.Instance,
			Mode.SingleCircuitPerLifetime => circuit,
			Mode.NewCircuitPerRequest => null,
			_ => throw new NotSupportedException(),
		};
	}

	public Func<Uri>? BaseUriGetter { get; }

	/// <summary>Whether each HTTP(s) request should use a separate Tor circuit or not to increase privacy.</summary>
	public Mode Mode { get; }

	/// <summary>Non-null for <see cref="Mode.DefaultCircuit"/> and <see cref="Mode.SingleCircuitPerLifetime"/>.</summary>
	private ICircuit? PredefinedCircuit { get; }

	private TorHttpPool TorHttpPool { get; }

	/// <exception cref="HttpRequestException">When HTTP request fails to be processed. Inner exception may be an instance of <see cref="TorException"/>.</exception>
	/// <exception cref="OperationCanceledException">When <paramref name="cancellationToken"/> is canceled by the user.</exception>
	/// <inheritdoc cref="SendAsync(HttpRequestMessage, CancellationToken)"/>
	public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string relativeUri, HttpContent? content = null, CancellationToken cancellationToken = default)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		Uri requestUri = new(BaseUriGetter(), relativeUri);
		using HttpRequestMessage request = new(method, requestUri);

		if (content is { })
		{
			request.Content = content;
		}

		return await SendAsync(request, cancellationToken).ConfigureAwait(false);
	}

	/// <exception cref="HttpRequestException">When <paramref name="request"/> fails to be processed.</exception>
	/// <exception cref="OperationCanceledException">If <paramref name="cancellationToken"/> is set.</exception>
	/// <remarks>
	/// No exception is thrown when the status code of the <see cref="HttpResponseMessage">response</see>
	/// is, for example, <see cref="HttpStatusCode.NotFound"/>.
	/// </remarks>
	public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
	{
		HttpResponseMessage result = null;
		if (Mode is Mode.NewCircuitPerRequest)
		{
			result = await TorHttpPool.SendAsync(request, AnyOneOffCircuit.Instance, cancellationToken).ConfigureAwait(false);
		}
		else
		{
			result = await TorHttpPool.SendAsync(request, PredefinedCircuit!, cancellationToken).ConfigureAwait(false);
		}

		var redirectUri = GetUriForRedirect(request.RequestUri, result);
		if (redirectUri is null)
		{
			return result;
		}

		request.RequestUri = redirectUri;
		request.Headers.Remove("Host");
		return await SendAsync(request, cancellationToken).ConfigureAwait(false);
	}
	
	private Uri? GetUriForRedirect(Uri requestUri, HttpResponseMessage response)
	{
		switch (response.StatusCode)
		{
			case HttpStatusCode.Moved:
			case HttpStatusCode.Found:
			case HttpStatusCode.SeeOther:
			case HttpStatusCode.TemporaryRedirect:
			case HttpStatusCode.MultipleChoices:
			case HttpStatusCode.PermanentRedirect:
				break;

			default:
				return null;
		}

		Uri? location = response.Headers.Location;
		if (location == null)
		{
			return null;
		}

		// Ensure the redirect location is an absolute URI.
		if (!location.IsAbsoluteUri)
		{
			location = new Uri(requestUri, location);
		}

		// Per https://tools.ietf.org/html/rfc7231#section-7.1.2, a redirect location without a
		// fragment should inherit the fragment from the original URI.
		string requestFragment = requestUri.Fragment;
		if (!string.IsNullOrEmpty(requestFragment))
		{
			string redirectFragment = location.Fragment;
			if (string.IsNullOrEmpty(redirectFragment))
			{
				location = new UriBuilder(location) { Fragment = requestFragment }.Uri;
			}
		}

		return location;
	}

	/// <inheritdoc cref="TorHttpPool.PrebuildCircuitsUpfront(Uri, int, TimeSpan)"/>
	/// <exception cref="InvalidOperationException">When no <see cref="BaseUriGetter"/> is set.</exception>
	public void PrebuildCircuitsUpfront(int count, TimeSpan deadline)
	{
		if (BaseUriGetter is null)
		{
			throw new InvalidOperationException($"{nameof(BaseUriGetter)} is not set.");
		}

		TorHttpPool.PrebuildCircuitsUpfront(BaseUriGetter(), count, deadline);
	}

	public Task<bool> IsTorRunningAsync(CancellationToken cancellationToken)
	{
		return TorHttpPool.IsTorRunningAsync(cancellationToken);
	}
}
