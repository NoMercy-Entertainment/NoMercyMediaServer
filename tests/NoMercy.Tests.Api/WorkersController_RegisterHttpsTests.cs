using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Controllers.V1.Dashboard;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Phase 4.10 — HTTPS enforcement at worker registration. The spec lets
/// loopback (127.0.0.1, ::1, localhost) register over plain HTTP for local
/// dev; everything else MUST be HTTPS. Tests sit on top of the controller
/// directly so we exercise the validation branch without standing up Kestrel.
/// </summary>
public class WorkersController_RegisterHttpsTests
{
    private static WorkersController BuildController()
    {
        EncoderOptions opts = new() { DistributedEncodingSigningKey = "0123456789abcdef" };
        InMemoryRemoteWorkerRegistry registry = new();
        Mock<ITaskSerializer> serializer = new();
        Mock<ITaskProgressStore> progressStore = new();
        Mock<IHttpClientFactory> httpFactory = new();
        httpFactory
            .Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient { BaseAddress = new("http://example/") });

        WorkersController controller = new(
            registry: registry,
            serializer: serializer.Object,
            progressStore: progressStore.Object,
            encoderOptions: opts,
            httpClientFactory: httpFactory.Object,
            logger: NullLogger<WorkersController>.Instance,
            workerLogger: NullLogger<HttpRemoteWorker>.Instance
        );

        // Stand up an HttpContext with an "owner" claim so the IsOwner() guard
        // doesn't short-circuit the test before reaching URL validation.
        DefaultHttpContext httpContext = new();
        httpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim("server_owner", "true")],
                "test"
            )
        );
        controller.ControllerContext = new() { HttpContext = httpContext };
        return controller;
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("http://10.0.0.5:7626")]
    [InlineData("http://192.168.1.50")]
    public void Register_NonLoopback_Http_IsRejected(string baseUrl)
    {
        WorkersController controller = BuildController();

        IActionResult result = controller.Register(
            new RegisterWorkerRequest(
                WorkerId: "w-1",
                BaseUrl: baseUrl,
                CpuCores: 8,
                AvailableCpuThreads: 8,
                AvailableGpuSlots: 0,
                Gpus: null
            )
        );

        // The controller maps invalid input through BadRequestResponse, which
        // returns a BadRequestObjectResult. We just need to confirm it isn't a
        // 200/OkObjectResult — the URL got rejected.
        Assert.IsNotType<OkObjectResult>(result);
    }

    [Theory]
    [InlineData("http://127.0.0.1:7626")]
    [InlineData("http://localhost:7626")]
    [InlineData("https://worker.example.com")]
    public void Register_Loopback_Http_Or_Https_IsAccepted(string baseUrl)
    {
        WorkersController controller = BuildController();

        IActionResult result = controller.Register(
            new RegisterWorkerRequest(
                WorkerId: "w-1",
                BaseUrl: baseUrl,
                CpuCores: 8,
                AvailableCpuThreads: 8,
                AvailableGpuSlots: 0,
                Gpus: null
            )
        );

        Assert.IsType<OkObjectResult>(result);
    }
}
