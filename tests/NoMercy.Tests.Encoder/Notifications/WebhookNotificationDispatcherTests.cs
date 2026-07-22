// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Notifications;

namespace NoMercy.Tests.Encoder.Notifications;

public class WebhookNotificationDispatcherTests
{
    [Fact]
    public async Task Notify_NoUrlsConfigured_DoesNotSendAnyRequest()
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        await dispatcher.NotifyStartedAsync(notification: new(JobId: 1, InputPath: "/in", OutputPath: "/out", ProfileName: "HLS"), ct: CancellationToken.None);

        Assert.Empty(collection: handler.Requests);
    }

    [Fact]
    public async Task Notify_SingleUrl_Success_PostsOnce()
    {
        EncoderOptions options = BuildOptions(urls: "https://example.com/hook");
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        await dispatcher.NotifyStartedAsync(
            notification: new(JobId: 42, InputPath: "/in.mkv", OutputPath: "/out", ProfileName: "HLS 1080p"),
            ct: CancellationToken.None
        );

        Assert.Single(collection: handler.Requests);
        Assert.Equal(expected: "https://example.com/hook", actual: handler.Requests[index: 0].url);
        Assert.Contains(expectedSubstring: "encoding.started", actualString: handler.Requests[index: 0].body);
        Assert.Contains(expectedSubstring: "\"job_id\":42", actualString: handler.Requests[index: 0].body);
    }

    [Fact]
    public async Task Notify_MultipleUrls_PostsToEach()
    {
        EncoderOptions options = BuildOptions(urls: ["https://a.example/hook", "https://b.example/hook"]);
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        await dispatcher.NotifyCompletedAsync(
            notification: new(JobId: 7, OutputPath: "/out.mp4", Duration: TimeSpan.FromSeconds(seconds: 120)),
            ct: CancellationToken.None
        );

        Assert.Equal(expected: 2, actual: handler.Requests.Count);
        Assert.Contains(collection: handler.Requests, filter: r => r.url == "https://a.example/hook");
        Assert.Contains(collection: handler.Requests, filter: r => r.url == "https://b.example/hook");
    }

    [Fact]
    public async Task Notify_Completed_PayloadShapeMatchesSpec()
    {
        EncoderOptions options = BuildOptions(urls: "https://example.com/hook");
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        await dispatcher.NotifyCompletedAsync(
            notification: new(JobId: 99, OutputPath: "/out.m3u8", Duration: TimeSpan.FromSeconds(seconds: 180)),
            ct: CancellationToken.None
        );

        JsonDocument doc = JsonDocument.Parse(json: handler.Requests[index: 0].body);
        Assert.Equal(expected: "encoding.completed", actual: doc.RootElement.GetProperty(propertyName: "event").GetString());
        Assert.True(condition: doc.RootElement.TryGetProperty(propertyName: "timestamp", value: out _));
        JsonElement payload = doc.RootElement.GetProperty(propertyName: "payload");
        Assert.Equal(expected: 99, actual: payload.GetProperty(propertyName: "job_id").GetInt32());
        Assert.Equal(expected: "/out.m3u8", actual: payload.GetProperty(propertyName: "output_path").GetString());
        Assert.Equal(expected: 180.0, actual: payload.GetProperty(propertyName: "duration_seconds").GetDouble());
    }

    [Fact]
    public async Task Notify_Failed_PayloadIncludesErrorAndExceptionType()
    {
        EncoderOptions options = BuildOptions(urls: "https://example.com/hook");
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        await dispatcher.NotifyFailedAsync(
            notification: new(JobId: 5, InputPath: "/src.mkv", ErrorMessage: "ffmpeg crashed", ExceptionType: "ProcessCrashedException"),
            ct: CancellationToken.None
        );

        JsonDocument doc = JsonDocument.Parse(json: handler.Requests[index: 0].body);
        Assert.Equal(expected: "encoding.failed", actual: doc.RootElement.GetProperty(propertyName: "event").GetString());
        JsonElement payload = doc.RootElement.GetProperty(propertyName: "payload");
        Assert.Equal(expected: "ffmpeg crashed", actual: payload.GetProperty(propertyName: "error_message").GetString());
        Assert.Equal(expected: "ProcessCrashedException", actual: payload.GetProperty(propertyName: "exception_type").GetString());
    }

    [Fact]
    public async Task Notify_TransientFailure_RetriesUpToThreeTimes()
    {
        EncoderOptions options = BuildOptions(urls: "https://example.com/hook");
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.InternalServerError };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        using CancellationTokenSource cts = new();
        // Give retries enough time but cancel before the third backoff (4s) completes
        // so the test runs fast — we still see 3 request attempts.
        cts.CancelAfter(delay: TimeSpan.FromSeconds(seconds: 4));

        await dispatcher.NotifyStartedAsync(notification: new(JobId: 1, InputPath: "/in", OutputPath: "/out", ProfileName: "HLS"), ct: cts.Token);

        Assert.InRange(actual: handler.Requests.Count, low: 2, high: 3);
    }

    [Fact]
    public async Task Notify_OneUrlFails_OtherStillGetsNotified()
    {
        EncoderOptions options = BuildOptions(urls: ["https://bad.example/hook", "https://good.example/hook"]
        );
        PerUrlHandler handler = new(
            urlToStatus: new Dictionary<string, HttpStatusCode>
            {
                [key: "https://bad.example/hook"] = HttpStatusCode.InternalServerError,
                [key: "https://good.example/hook"] = HttpStatusCode.OK,
            }
        );
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        using CancellationTokenSource cts = new(delay: TimeSpan.FromSeconds(seconds: 6));
        await dispatcher.NotifyStartedAsync(notification: new(JobId: 1, InputPath: "/in", OutputPath: "/out", ProfileName: "HLS"), ct: cts.Token);

        // Good URL gets at least one request even though bad URL is failing.
        Assert.Contains(
            collection: handler.Requests,
            filter: r => r is { url: "https://good.example/hook", statusCode: HttpStatusCode.OK }
        );
    }

    [Fact]
    public async Task Notify_OperationCancelled_StopsImmediately()
    {
        EncoderOptions options = BuildOptions(urls: "https://example.com/hook");
        CapturingHandler handler = new() { StatusCode = HttpStatusCode.OK };
        WebhookNotificationDispatcher dispatcher = BuildDispatcher(options: options, handler: handler);

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await dispatcher.NotifyStartedAsync(notification: new(JobId: 1, InputPath: "/in", OutputPath: "/out", ProfileName: "HLS"), ct: cts.Token);

        // First attempt short-circuits before the request starts.
        Assert.Empty(collection: handler.Requests);
    }

    private static EncoderOptions BuildOptions(params string[] urls)
    {
        EncoderOptions options = new() { FfmpegPathOverride = "ffmpeg" };
        foreach (string url in urls)
            options.NotificationWebhookUrls.Add(item: url);
        return options;
    }

    private static WebhookNotificationDispatcher BuildDispatcher(
        EncoderOptions options,
        HttpMessageHandler handler
    )
    {
        HttpClient client = new(handler: handler);
        return new(options: options, httpClient: client, logger: NullLogger<WebhookNotificationDispatcher>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public List<(string url, string body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken: cancellationToken);
            Requests.Add(item: (request.RequestUri!.ToString(), body));
            return new(statusCode: StatusCode);
        }
    }

    private sealed class PerUrlHandler(IDictionary<string, HttpStatusCode> urlToStatus)
        : HttpMessageHandler
    {
        public List<(string url, HttpStatusCode statusCode)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            string url = request.RequestUri!.ToString();
            HttpStatusCode status = urlToStatus.TryGetValue(key: url, value: out HttpStatusCode s)
                ? s
                : HttpStatusCode.OK;
            Requests.Add(item: (url, status));
            return Task.FromResult(result: new HttpResponseMessage(statusCode: status));
        }
    }
}
