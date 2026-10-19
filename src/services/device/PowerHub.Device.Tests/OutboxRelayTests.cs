using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PowerHub.Device.Data;
using PowerHub.Device.Features;
using PowerHub.ServiceDefaults;

namespace PowerHub.Device.Tests;

/// <summary>
/// Drives the relay directly against the real database, with the network and the clock
/// replaced. Each test uses its own event type and destinations, so the events other tests
/// write to the shared database are never in scope.
/// </summary>
public sealed class OutboxRelayTests(DeviceApp app)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Each_event_reaches_every_subscribed_destination_once()
    {
        string registered = NewEventType(), removed = NewEventType();
        var registrations = await AddEventsAsync(registered, 2);
        var removals = await AddEventsAsync(removed, 1);
        string telemetry = NewDestination(), notification = NewDestination();

        using var harness = new Harness(app, options =>
        {
            Subscribe(options, telemetry, registered);
            Subscribe(options, notification, registered, removed);
        });
        await harness.Relay.RunOnceAsync(Ct);
        await harness.Relay.RunOnceAsync(Ct);

        Assert.Equal(registrations.Order(), harness.Network.EventsAt(telemetry).Order());
        Assert.Equal(registrations.Concat(removals).Order(), harness.Network.EventsAt(notification).Order());
        Assert.All(harness.Network.Received, delivery => Assert.Equal($"token-for:{delivery.Destination}-service", delivery.Token));

        var rows = await DeliveriesAsync(telemetry, notification);
        Assert.Equal(5, rows.Count);
        Assert.All(rows, row =>
        {
            Assert.NotNull(row.DeliveredAt);
            Assert.Equal(0, row.Attempts);
            Assert.Null(row.LockedUntil);
        });
    }

    [Fact]
    public async Task A_failed_delivery_is_retried_after_a_backoff()
    {
        var eventType = NewEventType();
        var eventId = Assert.Single(await AddEventsAsync(eventType, 1));
        var destination = NewDestination();
        using var harness = new Harness(app, options => Subscribe(options, destination, eventType));
        harness.Network.Respond = attempt => attempt == 1 ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Accepted;

        await harness.Relay.RunOnceAsync(Ct);
        var failed = Assert.Single(await DeliveriesAsync(destination));
        Assert.Equal(1, failed.Attempts);
        Assert.Equal("HTTP 503", failed.LastError);
        Assert.Null(failed.DeliveredAt);
        Assert.Null(failed.LockedUntil);
        Assert.InRange(failed.NextAttemptAt - harness.Time.Now, TimeSpan.FromSeconds(0.79), TimeSpan.FromSeconds(1.21));

        // Not due yet: nothing is sent.
        await harness.Relay.RunOnceAsync(Ct);
        Assert.Single(harness.Network.Received);

        harness.Time.Now += TimeSpan.FromSeconds(2);
        await harness.Relay.RunOnceAsync(Ct);

        Assert.Equal([eventId, eventId], harness.Network.EventsAt(destination));
        Assert.NotNull(Assert.Single(await DeliveriesAsync(destination)).DeliveredAt);
    }

    [Fact]
    public async Task A_rejected_event_is_dead_lettered_at_once()
    {
        var eventType = NewEventType();
        await AddEventsAsync(eventType, 1);
        var destination = NewDestination();
        using var harness = new Harness(app, options => Subscribe(options, destination, eventType));
        harness.Network.Respond = _ => HttpStatusCode.BadRequest;

        await harness.Relay.RunOnceAsync(Ct);
        harness.Time.Now += TimeSpan.FromHours(1);
        await harness.Relay.RunOnceAsync(Ct);

        Assert.Single(harness.Network.Received);
        var row = Assert.Single(await DeliveriesAsync(destination));
        Assert.NotNull(row.DeadLetteredAt);
        Assert.Equal(1, row.Attempts);
        Assert.Equal("HTTP 400", row.LastError);
    }

    [Fact]
    public async Task Retrying_stops_at_the_attempt_limit()
    {
        var eventType = NewEventType();
        await AddEventsAsync(eventType, 1);
        var destination = NewDestination();
        using var harness = new Harness(app, options =>
        {
            options.MaxAttempts = 3;
            Subscribe(options, destination, eventType);
        });
        harness.Network.Respond = _ => HttpStatusCode.InternalServerError;

        for (var pass = 0; pass < 5; pass++)
        {
            await harness.Relay.RunOnceAsync(Ct);
            harness.Time.Now += TimeSpan.FromMinutes(15);
        }

        Assert.Equal(3, harness.Network.Received.Count);
        var row = Assert.Single(await DeliveriesAsync(destination));
        Assert.Equal(3, row.Attempts);
        Assert.NotNull(row.DeadLetteredAt);
    }

    [Fact]
    public async Task Concurrent_relays_deliver_each_event_once()
    {
        var eventType = NewEventType();
        var eventIds = await AddEventsAsync(eventType, 30);
        var destination = NewDestination();
        var network = new FakeNetwork();
        var time = new ManualTime();
        using var first = new Harness(app, Configure, network, time);
        using var second = new Harness(app, Configure, network, time);

        for (var pass = 0; pass < 4; pass++)
        {
            await Task.WhenAll(first.Relay.RunOnceAsync(Ct), second.Relay.RunOnceAsync(Ct));
        }

        Assert.Equal(eventIds.Order(), network.EventsAt(destination).Order());

        void Configure(OutboxOptions options)
        {
            options.BatchSize = 10;
            Subscribe(options, destination, eventType);
        }
    }

    [Fact]
    public async Task A_held_delivery_is_left_alone_until_its_lease_expires()
    {
        var eventType = NewEventType();
        var eventId = Assert.Single(await AddEventsAsync(eventType, 1));
        var destination = NewDestination();
        using var harness = new Harness(app, options => Subscribe(options, destination, eventType));

        // Another replica claimed it and then stopped responding.
        await app.WithDbAsync(db =>
        {
            db.Deliveries.Add(new OutboxDelivery
            {
                Destination = destination,
                MessageId = eventId,
                NextAttemptAt = harness.Time.Now,
                LockedUntil = harness.Time.Now + TimeSpan.FromSeconds(60),
            });
            return db.SaveChangesAsync(Ct);
        });

        await harness.Relay.RunOnceAsync(Ct);
        Assert.Empty(harness.Network.Received);

        harness.Time.Now += TimeSpan.FromSeconds(61);
        await harness.Relay.RunOnceAsync(Ct);
        Assert.Equal([eventId], harness.Network.EventsAt(destination));
    }

    [Fact]
    public async Task A_refused_token_is_replaced_before_the_retry()
    {
        var eventType = NewEventType();
        await AddEventsAsync(eventType, 1);
        var destination = NewDestination();
        using var harness = new Harness(app, options => Subscribe(options, destination, eventType));
        harness.Network.Respond = attempt => attempt == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.Accepted;

        await harness.Relay.RunOnceAsync(Ct);
        harness.Time.Now += TimeSpan.FromSeconds(2);
        await harness.Relay.RunOnceAsync(Ct);

        Assert.Equal(2, harness.Network.TokenRequests);
        Assert.NotNull(Assert.Single(await DeliveriesAsync(destination)).DeliveredAt);
    }

    [Fact]
    public void Backoff_grows_exponentially_up_to_ten_minutes_with_jitter()
    {
        Assert.InRange(OutboxRelay.Backoff(1), TimeSpan.FromSeconds(0.8), TimeSpan.FromSeconds(1.2));
        Assert.InRange(OutboxRelay.Backoff(4), TimeSpan.FromSeconds(6.4), TimeSpan.FromSeconds(9.6));
        Assert.InRange(OutboxRelay.Backoff(20), TimeSpan.FromMinutes(8), TimeSpan.FromMinutes(12));
    }

    private static string NewEventType() => $"test.{Guid.NewGuid():N}.v1";

    private static string NewDestination() => $"d{Guid.NewGuid():N}";

    private static void Subscribe(OutboxOptions options, string destination, params string[] eventTypes)
    {
        var target = new OutboxDestination { BaseUrl = $"http://{destination}.test", Audience = $"{destination}-service" };
        foreach (var eventType in eventTypes)
        {
            target.EventTypes.Add(eventType);
        }

        options.Destinations[destination] = target;
    }

    private async Task<List<Guid>> AddEventsAsync(string eventType, int count)
    {
        var ids = Enumerable.Range(0, count).Select(_ => Guid.CreateVersion7()).ToList();
        await app.WithDbAsync(db =>
        {
            foreach (var id in ids)
            {
                db.Outbox.Add(new OutboxMessage
                {
                    Id = id,
                    EventType = eventType,
                    Subject = $"test/{id}",
                    OccurredAt = DateTimeOffset.UtcNow,
                    Payload = JsonSerializer.Serialize(new { eventId = id, eventType }),
                });
            }

            return db.SaveChangesAsync(Ct);
        });
        return ids;
    }

    private Task<List<OutboxDelivery>> DeliveriesAsync(params string[] destinations) =>
        app.WithDbAsync(db => db.Deliveries.Where(row => destinations.Contains(row.Destination)).ToListAsync(Ct));

    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed record Received(string Destination, Guid EventId, string? Token);

    /// <summary>Stands in for Identity's token endpoint and for every destination.</summary>
    private sealed class FakeNetwork : HttpMessageHandler, IHttpClientFactory
    {
        private readonly ConcurrentQueue<Received> _received = new();
        private int _tokenRequests;

        /// <summary>The response for the nth delivery attempt across all destinations.</summary>
        public Func<int, HttpStatusCode> Respond { get; set; } = _ => HttpStatusCode.Accepted;

        public ConcurrentQueue<Received> Received => _received;

        public int TokenRequests => _tokenRequests;

        public List<Guid> EventsAt(string destination) =>
            [.. _received.Where(delivery => delivery.Destination == destination).Select(delivery => delivery.EventId)];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (request.RequestUri!.AbsolutePath == "/internal/v1/oauth/token")
            {
                Interlocked.Increment(ref _tokenRequests);
                var audience = body.Split('&').Single(pair => pair.StartsWith("audience=", StringComparison.Ordinal))["audience=".Length..];
                var token = JsonSerializer.Serialize(new { access_token = $"token-for:{audience}", token_type = "Bearer", expires_in = 300 });
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(token, Encoding.UTF8, "application/json") };
            }

            Assert.Equal("/internal/v1/events", request.RequestUri.AbsolutePath);
            var eventId = JsonDocument.Parse(body).RootElement.GetProperty("eventId").GetGuid();
            var destination = request.RequestUri.Host[..^".test".Length];
            _received.Enqueue(new Received(destination, eventId, request.Headers.Authorization?.Parameter));
            return new HttpResponseMessage(Respond(_received.Count));
        }
    }

    private sealed class Harness : IDisposable
    {
        private readonly ServiceTokenClient _tokens;
        private readonly OutboxMetrics _metrics = new();

        public Harness(DeviceApp app, Action<OutboxOptions> configure, FakeNetwork? network = null, ManualTime? time = null)
        {
            Network = network ?? new FakeNetwork();
            Time = time ?? new ManualTime();
            var options = new OutboxOptions { RelayEnabled = false };
            configure(options);
            var auth = new ServiceAuthOptions
            {
                TokenUrl = "http://identity.test/internal/v1/oauth/token",
                ClientId = "device-service",
                ClientSecret = "test-secret",
            };
            _tokens = new ServiceTokenClient(Network, Options.Create(auth), Time);
            Relay = new OutboxRelay(
                app.Services.GetRequiredService<IServiceScopeFactory>(),
                Network,
                _tokens,
                Options.Create(options),
                _metrics,
                Time,
                NullLogger<OutboxRelay>.Instance);
        }

        public FakeNetwork Network { get; }

        public ManualTime Time { get; }

        public OutboxRelay Relay { get; }

        public void Dispose()
        {
            _tokens.Dispose();
            _metrics.Dispose();
        }
    }
}
