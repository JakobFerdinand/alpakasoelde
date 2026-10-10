using System.Net;
using DashboardApi.Shared;
using DashboardApi.Tests.Fakes;
using Microsoft.Azure.Functions.Worker.Http;

namespace DashboardApi.Tests;

public sealed class ProblemDetailsResponsesTests
{
	[Fact]
	public async Task Without_a_detail_only_title_and_status_are_serialised()
	{
		FakeHttpRequestData request = new();

		var response = await ProblemDetailsResponses.CreateAsync(request, HttpStatusCode.MethodNotAllowed, "Method Not Allowed");

		Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
		Assert.Equal("""{"title":"Method Not Allowed","status":405}""", ((FakeHttpResponseData)response).BodyText());
	}

	[Fact]
	public async Task With_a_detail_title_status_and_detail_are_serialised_in_that_order()
	{
		FakeHttpRequestData request = new();

		HttpResponseData response = await ProblemDetailsResponses.CreateAsync(
			request, HttpStatusCode.BadRequest, "Bad Request", "Name exceeds 100 characters.");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
		Assert.Equal(
			"""{"title":"Bad Request","status":400,"detail":"Name exceeds 100 characters."}""",
			((FakeHttpResponseData)response).BodyText());
	}

	[Theory]
	[InlineData(HttpStatusCode.BadRequest, "Bad Request", "Ungültiger Anfrageinhalt.")]
	[InlineData(HttpStatusCode.BadRequest, "Bad Request", "Der Betrag muss größer als 0 sein.")]
	[InlineData(HttpStatusCode.NotFound, "Not Found", "Message with id 'm-1' was not found.")]
	[InlineData(HttpStatusCode.BadGateway, "Bad Gateway", "Der Assistent konnte nicht erreicht werden.")]
	public async Task The_serialised_body_is_byte_identical_to_a_hand_written_response(
		HttpStatusCode statusCode, string title, string detail)
	{
		FakeHttpRequestData request = new();

		HttpResponseData response = await ProblemDetailsResponses.CreateAsync(request, statusCode, title, detail);

		FakeHttpRequestData handWrittenRequest = new();
		HttpResponseData handWrittenResponse = handWrittenRequest.CreateResponse(statusCode);
		await handWrittenResponse.WriteAsJsonAsync(new
		{
			title,
			status = (int)statusCode,
			detail
		}, TestContext.Current.CancellationToken);

		Assert.Equal(
			((FakeHttpResponseData)handWrittenResponse).BodyText(),
			((FakeHttpResponseData)response).BodyText());
	}

	[Fact]
	public async Task Without_a_detail_the_body_is_byte_identical_to_the_two_property_hand_written_response()
	{
		FakeHttpRequestData request = new();

		HttpResponseData response = await ProblemDetailsResponses.CreateAsync(request, HttpStatusCode.MethodNotAllowed, "Method Not Allowed");

		FakeHttpRequestData handWrittenRequest = new();
		HttpResponseData handWrittenResponse = handWrittenRequest.CreateResponse(HttpStatusCode.MethodNotAllowed);
		await handWrittenResponse.WriteAsJsonAsync(new
		{
			title = "Method Not Allowed",
			status = (int)HttpStatusCode.MethodNotAllowed
		}, TestContext.Current.CancellationToken);

		Assert.Equal(
			((FakeHttpResponseData)handWrittenResponse).BodyText(),
			((FakeHttpResponseData)response).BodyText());
	}
}
