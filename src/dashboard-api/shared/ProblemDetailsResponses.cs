using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DashboardApi.Shared;

/// <summary>
/// Single factory for problem-details JSON responses (<c>{"title":…,"status":…,"detail":…}</c>) returned to
/// the dashboards. A response without a detail writes only <c>title</c> and <c>status</c>, matching the
/// hand-written anonymous objects this module replaced. The nested body records pin their JSON names with
/// <see cref="JsonPropertyNameAttribute"/>, so the casing does not depend on the serializer options the
/// worker was configured with.
/// </summary>
public static class ProblemDetailsResponses
{
	public static async Task<HttpResponseData> CreateAsync(HttpRequestData request, HttpStatusCode statusCode, string title)
	{
		HttpResponseData response = request.CreateResponse(statusCode);
		await response.WriteAsJsonAsync(new Problem(title, (int)statusCode)).ConfigureAwait(false);
		return response;
	}

	public static async Task<HttpResponseData> CreateAsync(HttpRequestData request, HttpStatusCode statusCode, string title, string detail)
	{
		HttpResponseData response = request.CreateResponse(statusCode);
		await response.WriteAsJsonAsync(new ProblemDetail(title, (int)statusCode, detail)).ConfigureAwait(false);
		return response;
	}

	private sealed record Problem([property: JsonPropertyName("title")] string Title, [property: JsonPropertyName("status")] int Status);

	private sealed record ProblemDetail([property: JsonPropertyName("title")] string Title, [property: JsonPropertyName("status")] int Status, [property: JsonPropertyName("detail")] string Detail);
}
