using System.Net;
using System.Text;
using TCPNoiseObservatory.Models;

namespace TCPNoiseObservatory.Services;

public sealed class ObservatoryHtmlRenderer
{
    public string Render(NoiseSnapshot snapshot)
    {
        var html = new StringBuilder();

        html.Append("""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>TCPNoise Observatory</title>
            </head>
            <body>
                <h1>TCPNoise Observatory</h1>
            """);

        html.Append($"<p>Last five minutes: {snapshot.EventsLastFiveMinutes}</p>");
        html.Append($"<p>Last hour: {snapshot.EventsLastHour}</p>");

        html.Append("""
            <table>
                <thead>
                    <tr>
                        <th>Time</th>
                        <th>Sensor</th>
                        <th>Protocol</th>
                        <th>Port</th>
                        <th>Source</th>
                    </tr>
                </thead>
                <tbody>
            """);

        foreach (var noiseEvent in snapshot.RecentEvents
                     .OrderByDescending(x => x.Timestamp))
        {
            html.Append("<tr>");
            html.Append($"<td>{noiseEvent.Timestamp:HH:mm:ss}</td>");
            html.Append($"<td>{Encode(noiseEvent.Sensor)}</td>");
            html.Append($"<td>{Encode(noiseEvent.Protocol)}</td>");
            html.Append($"<td>{noiseEvent.DestinationPort}</td>");
            html.Append($"<td>{Encode(noiseEvent.Source)}</td>");
            html.Append("</tr>");
        }

        html.Append("""
                </tbody>
            </table>
            </body>
            </html>
            """);

        return html.ToString();
    }

    private static string Encode(string value)
        => WebUtility.HtmlEncode(value);
}