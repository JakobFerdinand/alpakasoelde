using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DashboardApi.Tests.Fakes;

/// <summary>
/// Smallest hand-rolled <see cref="FunctionContext"/> that lets <c>req.CreateResponse()</c> and
/// <c>WriteAsJsonAsync</c> run: it only exists so the real worker serializer can be resolved.
/// The serializer mirrors what the worker registers by default — a <see cref="JsonObjectSerializer"/> over
/// options without a naming policy.
/// </summary>
internal sealed class FakeFunctionContext : FunctionContext
{
	public FakeFunctionContext()
	{
		IServiceCollection services = new ServiceCollection();
		services.AddSingleton(Options.Create(new WorkerOptions
		{
			Serializer = new JsonObjectSerializer(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
		}));
		InstanceServices = services.BuildServiceProvider();
	}

	public override string InvocationId => Guid.NewGuid().ToString();

	public override string FunctionId => "test-function";

	public override TraceContext TraceContext { get; } = new FakeTraceContext();

	public override BindingContext BindingContext { get; } = new FakeBindingContext();

	public override RetryContext RetryContext { get; } = new FakeRetryContext();

	public override FunctionDefinition FunctionDefinition { get; } = new FakeFunctionDefinition();

	public override IDictionary<object, object> Items { get; set; } = new Dictionary<object, object>();

	public override IInvocationFeatures Features { get; } = new FakeInvocationFeatures();

	public override IServiceProvider InstanceServices { get; set; }

	private sealed class FakeTraceContext : TraceContext
	{
		public override string TraceParent => string.Empty;

		public override string TraceState => string.Empty;
	}

	private sealed class FakeBindingContext : BindingContext
	{
		public override IReadOnlyDictionary<string, object?> BindingData => new Dictionary<string, object?>();
	}

	private sealed class FakeRetryContext : RetryContext
	{
		public override int RetryCount => 0;

		public override int MaxRetryCount => 0;
	}

	private sealed class FakeFunctionDefinition : FunctionDefinition
	{
		public override ImmutableArray<FunctionParameter> Parameters => [];

		public override string PathToAssembly => string.Empty;

		public override string EntryPoint => string.Empty;

		public override string Id => "test-function";

		public override string Name => "test-function";

		public override ImmutableDictionary<string, BindingMetadata> InputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;

		public override ImmutableDictionary<string, BindingMetadata> OutputBindings => ImmutableDictionary<string, BindingMetadata>.Empty;
	}

	private sealed class FakeInvocationFeatures : IInvocationFeatures
	{
		private readonly Dictionary<Type, object> _features = [];

		public void Set<T>(T instance) => _features[typeof(T)] = instance!;

		public T? Get<T>() => _features.TryGetValue(typeof(T), out object? instance) ? (T?)instance : default;

		public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
