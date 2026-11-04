using System.Collections;
using System.Reflection;
using System.Text.Json;
using TCPNoiseObservatory.Models;
using TCPNoiseObservatory.Services;
using Xunit;

namespace TCPNoiseObservatory.Tests;

public sealed class NoiseEventStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EmptyStoreHasAnEmptyQuietSnapshot()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));

        var snapshot = store.GetSnapshot();

        Assert.Equal(0, snapshot.EventsLastFiveMinutes);
        Assert.Equal(0, snapshot.EventsLastHour);
        Assert.Empty(snapshot.RecentEvents);
        Assert.Equal("quiet", snapshot.LevelCode);
        Assert.Equal(0d, snapshot.EventsPerMinute);
    }

    [Theory]
    [InlineData(0, 12_000)]
    [InlineData(1800, 0)]
    public void CountsExceedTheSampleCapacityWithoutSummingSeenCount(int ageSeconds, int expectedFiveMinutes)
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        var noiseEvent = Event(Start.AddSeconds(-ageSeconds), ulong.MaxValue);

        for (var i = 0; i < 12_000; i++)
            store.Add(noiseEvent);

        var snapshot = store.GetSnapshot();
        Assert.Equal(expectedFiveMinutes, snapshot.EventsLastFiveMinutes);
        Assert.Equal(12_000, snapshot.EventsLastHour);
        Assert.Equal(expectedFiveMinutes / 5d, snapshot.EventsPerMinute);
        Assert.Equal(100, snapshot.RecentEvents.Count);
    }

    [Fact]
    public void WindowsIncludeExactBoundariesAndExpireWithoutNewEvents()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        var noiseEvent = Event(Start);
        store.Add(noiseEvent);

        clock.SetUtcNow(Start.AddMinutes(5));
        AssertCounts(store, 1, 1);
        clock.SetUtcNow(Start.AddMinutes(5).AddTicks(1));
        AssertCounts(store, 0, 1);
        clock.SetUtcNow(Start.AddHours(1));
        AssertCounts(store, 0, 1);
        clock.SetUtcNow(Start.AddHours(1).AddTicks(1));
        AssertCounts(store, 0, 0);
        Assert.Equal(noiseEvent, Assert.Single(store.GetSnapshot().RecentEvents));
    }

    [Fact]
    public void OutOfOrderEventsUseTheirTimestampsAndExpireIndependently()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        foreach (var ageSeconds in new[] { 1, 2500, 30, 3600, 300, 1000, 301, 0 })
            store.Add(Event(Start.AddSeconds(-ageSeconds)));

        AssertCounts(store, 4, 8);
        clock.SetUtcNow(Start.AddSeconds(1));
        AssertCounts(store, 3, 7);
    }

    [Theory]
    [InlineData(-36_000_000_001L, 0, 0)] // One tick before the hour window.
    [InlineData(-36_000_000_000L, 0, 1)]
    [InlineData(-3_000_000_001L, 0, 1)] // Same second as the five-minute boundary, but expired.
    [InlineData(-3_000_000_000L, 1, 1)]
    [InlineData(0L, 1, 1)]
    [InlineData(1L, 0, 0)] // Future, even within the current bucket.
    [InlineData(36_010_000_000L, 0, 0)] // Must not overwrite a live ring slot.
    public void TimestampBoundariesAreCheckedBeforeBucketing(long offsetTicks, int fiveMinutes, int hour)
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        store.Add(Event(Start.AddTicks(offsetTicks)));

        AssertCounts(store, fiveMinutes, hour);
        Assert.Single(store.GetSnapshot().RecentEvents);
    }

    [Fact]
    public void PartialOldestSecondIsExcludedRatherThanCountingExpiredEvents()
    {
        var now = Start.AddMilliseconds(750);
        var clock = new ManualTimeProvider(now);
        var store = new NoiseEventStore(clock);
        store.Add(Event(now.AddMinutes(-5))); // In the partially overlapping second.
        store.Add(Event(Start.AddMinutes(-5).AddSeconds(1)));
        store.Add(Event(now.AddHours(-1))); // Also expires less than one second early.
        store.Add(Event(Start.AddHours(-1).AddSeconds(1)));

        AssertCounts(store, 1, 3);
        clock.SetUtcNow(Start.AddSeconds(1));
        AssertCounts(store, 1, 3);
    }

    [Fact]
    public void ExpiredAndFutureEventsCannotDisplaceLiveCountersOrBecomeDeferredCounts()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        store.Add(Event(Start));
        // Both timestamps collide with the current ring slot if accepted.
        store.Add(Event(Start.AddSeconds(-3601)));
        store.Add(Event(Start.AddSeconds(3601)));
        store.Add(Event(Start.AddTicks(1)));
        store.Add(Event(DateTimeOffset.MinValue));
        store.Add(Event(DateTimeOffset.MaxValue));

        AssertCounts(store, 1, 1);
        clock.SetUtcNow(Start.AddSeconds(1));
        AssertCounts(store, 1, 1);
        clock.SetUtcNow(Start.AddSeconds(3601));
        AssertCounts(store, 0, 0);
        Assert.Equal(6, store.GetSnapshot().RecentEvents.Count);
    }

    [Fact]
    public void TimestampsWithDifferentOffsetsRepresentTheSameInstant()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        store.Add(Event(Start.ToOffset(TimeSpan.FromHours(-5))));
        store.Add(Event(Start.AddHours(-1).ToOffset(TimeSpan.FromHours(9))));

        AssertCounts(store, 1, 2);
    }

    [Fact]
    public void LongIdlePeriodDoesNotReviveOldCountsWhenBucketsAreReused()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        store.Add(Event(Start));
        clock.SetUtcNow(Start.AddSeconds(3601 * 20));
        AssertCounts(store, 0, 0);

        // Reuse the original slot, then add a late event into another expired slot.
        store.Add(Event(clock.GetUtcNow()));
        store.Add(Event(clock.GetUtcNow().AddMinutes(-30)));
        AssertCounts(store, 1, 2);
    }

    [Fact]
    public void CountsSaturateAtThePublicIntegerLimitAndStillExpire()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        var oldEvent = Event(Start.AddMinutes(-5));
        store.Add(oldEvent);
        // Seed an extreme count without ingesting billions of test events.
        var buckets = GetStorage<Array>(store, "_buckets");
        var index = (int)(oldEvent.Timestamp.UtcTicks / TimeSpan.TicksPerSecond % buckets.Length);
        var bucket = buckets.GetValue(index)!;
        bucket.GetType().GetField("Count")!.SetValue(bucket, int.MaxValue);
        buckets.SetValue(bucket, index);
        store.Add(oldEvent); // A saturated individual bucket must not wrap.
        store.Add(Event(Start)); // Nor may the sum of multiple buckets wrap.

        AssertCounts(store, int.MaxValue, int.MaxValue);
        clock.SetUtcNow(Start.AddTicks(1));
        AssertCounts(store, 1, int.MaxValue);
        clock.SetUtcNow(Start.AddMinutes(55).AddTicks(1));
        AssertCounts(store, 0, 1);
    }

    [Fact]
    public void SustainedTrafficReusesFixedStorageAndKeepsTheRecentSampleLimit()
    {
        var clock = new ManualTimeProvider(Start);
        var store = new NoiseEventStore(clock);
        // Inspect storage without adding diagnostics to the public snapshot contract.
        var buckets = GetStorage<Array>(store, "_buckets");
        var events = GetStorage<ICollection>(store, "_events");
        Assert.Equal(3601, buckets.Length);

        for (var second = 0; second < 7200; second++)
        {
            clock.SetUtcNow(Start.AddSeconds(second));
            for (var i = 0; i < 50; i++)
                store.Add(Event(clock.GetUtcNow(), (ulong)i));

            if (second % 60 == 0)
                AssertCounts(store, Math.Min(second + 1, 301) * 50, Math.Min(second + 1, 3601) * 50);
        }

        AssertCounts(store, 15_050, 180_050);
        Assert.All(store.GetSnapshot().History!.Counts, count => Assert.Equal(3000, count));
        Assert.Same(buckets, GetStorage<Array>(store, "_buckets"));
        Assert.Equal(5000, events.Count);
        var recent = store.GetSnapshot().RecentEvents;
        Assert.Equal(100, recent.Count);
        Assert.Equal(Start.AddSeconds(7199), recent[0].Timestamp);
        Assert.Equal(49UL, recent[0].SeenCount);
    }

    [Fact]
    public void MinuteCountSaturatesWhenSummingMultipleSeconds()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start.AddSeconds(30)));
        store.Add(Event(Start));
        var buckets = GetStorage<Array>(store, "_buckets");
        var index = (int)(Start.UtcTicks / TimeSpan.TicksPerSecond % buckets.Length);
        var bucket = buckets.GetValue(index)!;
        bucket.GetType().GetField("Count")!.SetValue(bucket, int.MaxValue);
        buckets.SetValue(bucket, index);
        store.Add(Event(Start.AddSeconds(1)));

        Assert.Equal(int.MaxValue, store.GetSnapshot().History!.Counts[59]);
    }

    [Fact]
    public void RecentSampleStillUsesLast5000ArrivalsSortedByTimestampThenSeenCount()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        var evicted = Event(Start, ulong.MaxValue);
        store.Add(evicted);
        for (var i = 0; i < 5000; i++)
            store.Add(Event(Start.AddSeconds(-(i % 10)), (ulong)i));

        var snapshot = store.GetSnapshot();
        AssertCounts(store, 5001, 5001);
        Assert.Equal(100, snapshot.RecentEvents.Count);
        Assert.DoesNotContain(evicted, snapshot.RecentEvents);
        Assert.All(snapshot.RecentEvents, e => Assert.Equal(Start, e.Timestamp));
        Assert.Equal(Enumerable.Range(0, 100).Select(i => (ulong)(4990 - i * 10)),
            snapshot.RecentEvents.Select(e => e.SeenCount));
    }

    [Fact]
    public void LargeCountsRoundTripThroughTheExistingSnapshotAndMirrorContract()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        for (var i = 0; i < 6000; i++)
            store.Add(Event(Start));

        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var json = JsonSerializer.Serialize(store.GetSnapshot(), options);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "eventsLastFiveMinutes", "eventsLastHour", "eventsPerMinute", "history", "level", "levelDisplay", "recentEvents" },
            document.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));

        var cache = new RemoteSnapshotCache();
        cache.Update(JsonSerializer.Deserialize<NoiseSnapshot>(json, options)!);
        var mirrored = cache.GetSnapshot();
        Assert.Equal(6000, mirrored.EventsLastFiveMinutes);
        Assert.Equal(6000, mirrored.EventsLastHour);
        Assert.Equal(1200d, mirrored.EventsPerMinute);
        Assert.Equal("botnet_hurricane", mirrored.LevelCode);
        Assert.Equal(100, mirrored.RecentEvents.Count);
        Assert.Equal(store.GetSnapshot().RecentEvents, mirrored.RecentEvents);
        Assert.Equal(store.GetSnapshot().History!.Counts, mirrored.History!.Counts);
    }

    [Fact]
    public async Task ConcurrentWritersAndSnapshotReadersSeeConsistentCounts()
    {
        var store = new NoiseEventStore(new ManualTimeProvider(Start));
        using var ready = new CountdownEvent(6);
        using var start = new ManualResetEventSlim();
        Task Run(Action action) => Task.Factory.StartNew(() =>
        {
            ready.Signal();
            start.Wait();
            action();
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        var writers = Enumerable.Range(0, 4).Select(_ => Run(() =>
        {
            for (var i = 0; i < 10_000; i++)
                store.Add(Event(Start));
        })).ToArray();
        var readers = Enumerable.Range(0, 2).Select(_ => Run(() =>
        {
            var previous = 0;
            for (var i = 0; i < 100; i++)
            {
                var snapshot = store.GetSnapshot();
                Assert.InRange(snapshot.EventsLastHour, previous, 40_000);
                Assert.Equal(snapshot.EventsLastFiveMinutes, snapshot.EventsLastHour);
                Assert.Equal(Math.Min(snapshot.EventsLastHour, 100), snapshot.RecentEvents.Count);
                Assert.Equal(snapshot.EventsLastHour, snapshot.History!.Counts.Sum());
                previous = snapshot.EventsLastHour;
            }
        })).ToArray();
        ready.Wait();
        start.Set();
        await Task.WhenAll(writers.Concat(readers));

        AssertCounts(store, 40_000, 40_000);
        Assert.Equal(100, store.GetSnapshot().RecentEvents.Count);
    }

    private static NoiseEvent Event(DateTimeOffset timestamp, ulong seenCount = 1) =>
        new(timestamp, "public-test-sensor", "TCP", 23, "185.23.xxx.xxx", seenCount, false, null);

    private static void AssertCounts(NoiseEventStore store, int fiveMinutes, int hour)
    {
        var snapshot = store.GetSnapshot();
        Assert.Equal(fiveMinutes, snapshot.EventsLastFiveMinutes);
        Assert.Equal(hour, snapshot.EventsLastHour);
    }

    private static T GetStorage<T>(NoiseEventStore store, string field) =>
        (T)typeof(NoiseEventStore).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store)!;

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void SetUtcNow(DateTimeOffset value) => now = value;
    }
}
