using TCPNoiseObservatory.Services;

namespace TCPNoiseObservatory.Endpoints;

public static class ObservatoryEndpoints
{
    public static IEndpointRouteBuilder MapObservatoryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (
            FakeNoiseData fakeNoiseData,
            ObservatoryHtmlRenderer renderer) =>
        {
            var snapshot = fakeNoiseData.GetSnapshot();
            var html = renderer.Render(snapshot);

            return Results.Content(
                html,
                contentType: "text/html; charset=utf-8");
        });

        return endpoints;
    }
}