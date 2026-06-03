using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Api.Controllers.V1.Dashboard.Admin;
using NoMercy.Database.Models.Users;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Helpers.Extensions;
using Xunit;

namespace NoMercy.Tests.Api;

#pragma warning disable CS0618 // intentionally testing the backwards-compat controller
/// <summary>
/// Phase 4.10 — HTTPS enforcement at worker registration. The spec lets
/// loopback (127.0.0.1, ::1, localhost) register over plain HTTP for local
/// dev; everything else MUST be HTTPS. Tests sit on top of the controller
/// directly so we exercise the validation branch without standing up Kestrel.
/// </summary>
public class WorkersController_RegisterHttpsTests
{
    private static readonly Guid OwnerUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static WorkersController BuildController()
    {
        SeedOwnerInClaimsExtensions();

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

        // Stand up an HttpContext whose principal carries the owner's user id
        // as ClaimTypes.NameIdentifier — that's what IsOwner() actually checks
        // against the seeded static _users list.
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, OwnerUserId.ToString())],
                    "test"
                )
            ),
        };
        controller.ControllerContext = new() { HttpContext = httpContext };
        return controller;
    }

    /// <summary>
    /// IsOwner() checks the private static <c>_users</c> list inside
    /// <see cref="ClaimsPrincipleExtensions"/>. Because the list is process-wide,
    /// another test fixture (e.g. NoMercyApiFactory) may have replaced it via
    /// InitializeAsync() or cleared it via Reset() before these tests run.
    /// We therefore reset+reseed unconditionally on every BuildController() call
    /// so the owner check is order-independent.
    /// </summary>
    private static void SeedOwnerInClaimsExtensions()
    {
        // Reset wipes whatever a prior test left in the static — InitializeAsync
        // from integration fixtures or Reset() from ClaimsPrincipleExtensionsTests.
        ClaimsPrincipleExtensions.Reset();

        ClaimsPrincipleExtensions.AddUser(
            new()
            {
                Id = OwnerUserId,
                Owner = true,
                Email = "test-owner@local",
            }
        );
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
#pragma warning restore CS0618
