using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.Azure.Functions.Worker.Http;

namespace DashboardApi.Tests.Fakes;

/// <summary>
/// Minimal <see cref="HttpRequestData"/> creating <see cref="FakeHttpResponseData"/> instances whose body can be
/// read back. That is the seam for asserting on real HTTP responses.
/// </summary>
internal sealed class FakeHttpRequestData : HttpRequestData
{
	public FakeHttpRequestData(string method = "POST") : base(new FakeFunctionContext())
	{
		Method = method;
		Url = new Uri("http://localhost/api/test");
	}

	public override string Method { get; }

	public override Uri Url { get; }

	public override Stream Body { get; } = new MemoryStream();

	public override HttpHeadersCollection Headers { get; } = new();

	public override IReadOnlyCollection<IHttpCookie> Cookies { get; } = [];

	public override IEnumerable<ClaimsIdentity> Identities { get; } = [];

	public override Microsoft.Azure.Functions.Worker.Http.HttpResponseData CreateResponse()
		=> new FakeHttpResponseData(FunctionContext);
}

/// <summary>
/// Small <see cref="HttpResponseData"/> buffering the body as UTF-8 text for assertions.
/// </summary>
internal sealed class FakeHttpResponseData : HttpResponseData
{
	public FakeHttpResponseData(Microsoft.Azure.Functions.Worker.FunctionContext functionContext) : base(functionContext)
	{
	}

	public override HttpStatusCode StatusCode { get; set; }

	public override HttpHeadersCollection Headers { get; set; } = new();

	public override Stream Body { get; set; } = new MemoryStream();

	public override HttpCookies Cookies { get; } = new FakeCookies();

	public string BodyText()
	{
		MemoryStream stream = (MemoryStream)Body;
		return Encoding.UTF8.GetString(stream.ToArray());
	}

	private sealed class FakeCookies : HttpCookies
	{
		public override void Append(string name, string value) => throw new NotSupportedException("Cookies are not needed by any test.");

		public override void Append(IHttpCookie cookie) => throw new NotSupportedException("Cookies are not needed by any test.");

		public override IHttpCookie CreateNew() => throw new NotSupportedException("Cookies are not needed by any test.");
	}
}
