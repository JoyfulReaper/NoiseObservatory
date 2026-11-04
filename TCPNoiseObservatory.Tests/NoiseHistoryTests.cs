using System.Text.Json;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;
using Xunit;

namespace TCPNoiseObservatory.Tests;

public sealed class NoiseHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 30, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void MinutesAreChronologicalWithZerosBetweenDistributedEvents()
    {
        var store = new NoiseEventStore(new Clock(Now));
        var first = Now.AddSeconds(-30).AddMinutes(-59);
        foreach (var index in new[] { 59, 0, 10, 10, 0, 59, 59 })
            store.Add(Event(first.AddMinutes(index).AddSeconds(5)));

        var history = store.GetSnapshot().History!;
        Assert.Equal(Now, history.AsOf);
        Assert.Equal(first, history.FirstMinute);
        Assert.Equal(60, history.Counts.Count);
        Assert.Equal(2, history.Counts[0]);
        Assert.Equal(2, history.Counts[10]);
        Assert.Equal(3, history.Counts[59]);
        Assert.Equal(57, history.Counts.Count(count => count == 0));
    }

    [Fact]
    public void OneMinuteCanExceed5000WithoutSummingSeenCount()
    {
        var store = new NoiseEventStore(new Clock(Now));
        for (var i = 0; i < 12_000; i++)
            store.Add(Event(Now));

        var snapshot = store.GetSnapshot();
        Assert.Equal(12_000, snapshot.History!.Counts[59]);
        Assert.Equal(12_000, snapshot.History.Counts.Sum());
        Assert.Equal(12_000, snapshot.EventsLastFiveMinutes);
        Assert.Equal(100, snapshot.RecentEvents.Count);
    }

    [Fact]
    public void EmptyStoreProducesSixtyZeroMinutes()
    {
        var history = new NoiseEventStore(new Clock(Now)).GetSnapshot().History!;

        Assert.Equal(60, history.Counts.Count);
        Assert.All(history.Counts, count => Assert.Equal(0, count));
    }

    [Fact]
    public void MinuteBoundaryMovesPreviousCurrentBarAndDropsOldestMinute()
    {
        var clock = new Clock(Now.AddSeconds(30).AddTicks(-1));
        var store = new NoiseEventStore(clock);
        store.Add(Event(Now.AddSeconds(-30).AddMinutes(-59)));
        store.Add(Event(clock.GetUtcNow()));
        var before = store.GetSnapshot().History!;
        Assert.Equal(1, before.Counts[0]);
        Assert.Equal(1, before.Counts[59]);

        clock.Now = Now.AddSeconds(30);
        var after = store.GetSnapshot().History!;
        Assert.Equal(before.FirstMinute.AddMinutes(1), after.FirstMinute);
        Assert.Equal(1, after.Counts[58]);
        Assert.Equal(0, after.Counts[59]);
        store.Add(Event(clock.GetUtcNow()));
        Assert.Equal(1, store.GetSnapshot().History!.Counts[59]);
        Assert.Equal(0, after.Counts[59]); // Earlier snapshots remain immutable.
    }

    [Fact]
    public void OutOfOrderExpiredAndFutureEventsRespectCalendarMinuteBoundaries()
    {
        var store = new NoiseEventStore(new Clock(Now));
        var first = Now.AddSeconds(-30).AddMinutes(-59);
        store.Add(Event(Now));
        store.Add(Event(first.AddSeconds(-1))); // Inside rolling hour, outside chart.
        store.Add(Event(first));
        store.Add(Event(Now.AddHours(-1).AddTicks(-1))); // Expired at ingestion.
        store.Add(Event(Now.AddTicks(1))); // Future even in the current second.
        store.Add(Event(Now.AddMinutes(-5)));

        var snapshot = store.GetSnapshot();
        Assert.Equal(4, snapshot.EventsLastHour);
        Assert.Equal(3, snapshot.History!.Counts.Sum());
        Assert.Equal(1, snapshot.History.Counts[0]);
        Assert.Equal(1, snapshot.History.Counts[54]);
        Assert.Equal(1, snapshot.History.Counts[59]);
    }

    [Fact]
    public void LongIdlePeriodAndRingReuseDoNotReviveOldHistory()
    {
        var clock = new Clock(Now);
        var store = new NoiseEventStore(clock);
        store.Add(Event(Now));
        clock.Now = Now.AddSeconds(3601 * 20);
        Assert.All(store.GetSnapshot().History!.Counts, count => Assert.Equal(0, count));

        store.Add(Event(clock.Now)); // Reuses the original ring slot.
        store.Add(Event(clock.Now.AddMinutes(-30)));
        var history = store.GetSnapshot().History!;
        Assert.Equal(1, history.Counts[29]);
        Assert.Equal(1, history.Counts[59]);
        Assert.Equal(2, history.Counts.Sum());
    }

    [Fact]
    public void CalendarMinutesAcrossMidnightUseUtcRegardlessOfTimestampOffset()
    {
        var clock = new Clock(new DateTimeOffset(2026, 11, 4, 0, 0, 0, TimeSpan.Zero));
        var store = new NoiseEventStore(clock);
        store.Add(Event(clock.Now.AddTicks(-1).ToOffset(TimeSpan.FromHours(-5))));
        store.Add(Event(clock.Now));

        var history = store.GetSnapshot().History!;
        Assert.Equal(new DateTimeOffset(2026, 11, 3, 23, 1, 0, TimeSpan.Zero), history.FirstMinute);
        Assert.Equal(1, history.Counts[58]);
        Assert.Equal(1, history.Counts[59]);
    }

    [Fact]
    public void SnapshotHistoryRoundTripsThroughMirrorWithoutChangingItsSourceTime()
    {
        var store = new NoiseEventStore(new Clock(Now));
        store.Add(Event(Now));
        var snapshot = store.GetSnapshot();
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "asOf", "counts" }, document.RootElement.GetProperty("history")
            .EnumerateObject().Select(property => property.Name));

        var cache = new RemoteSnapshotCache();
        cache.Update(JsonSerializer.Deserialize<NoiseSnapshot>(json, JsonOptions)!);
        var mirrored = cache.GetSnapshot();
        Assert.Equal(Now, mirrored.History!.AsOf);
        Assert.Equal(snapshot.History!.Counts, mirrored.History.Counts);
        Assert.Equal(json, JsonSerializer.Serialize(mirrored, JsonOptions));
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"history\":null")]
    public void OlderSnapshotsWithoutHistoryStillDeserializeAndMirror(string historyField)
    {
        var json = "{\"eventsLastFiveMinutes\":6000,\"eventsLastHour\":9000,\"recentEvents\":[]" + historyField + "}";
        var cache = new RemoteSnapshotCache();
        cache.Update(JsonSerializer.Deserialize<NoiseSnapshot>(json, JsonOptions)!);

        Assert.Null(cache.GetSnapshot().History);
        Assert.Equal(6000, cache.GetSnapshot().EventsLastFiveMinutes);
        Assert.Equal(9000, cache.GetSnapshot().EventsLastHour);
        Assert.DoesNotContain("history", JsonSerializer.Serialize(cache.GetSnapshot(), JsonOptions));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(59)]
    [InlineData(61)]
    [InlineData(6000)]
    public void HistoryRejectsInvalidLengthsSoRetainedMirrorHistoryStaysBounded(int count)
    {
        var json = JsonSerializer.Serialize(new { asOf = Now, counts = new int[count] }, JsonOptions);
        Assert.Throws<ArgumentException>(() => JsonSerializer.Deserialize<NoiseHistory>(json, JsonOptions));
    }

    [Fact]
    public void HistoryRejectsNegativeCountsAndOwnsItsBoundedCopy()
    {
        var counts = new int[60];
        var history = new NoiseHistory(Now, counts);
        counts[0] = -1;

        Assert.Equal(0, history.Counts[0]);
        Assert.Throws<ArgumentException>(() => new NoiseHistory(Now, counts));
        Assert.Throws<ArgumentOutOfRangeException>(() => new NoiseHistory(DateTimeOffset.MinValue, new int[60]));
    }

    private static NoiseEvent Event(DateTimeOffset timestamp) =>
        new(timestamp, "public-test-sensor", "TCP", 23, "185.23.xxx.xxx", ulong.MaxValue, false, null);

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
