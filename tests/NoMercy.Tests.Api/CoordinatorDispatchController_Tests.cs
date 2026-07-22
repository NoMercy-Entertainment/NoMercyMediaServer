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

using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using NoMercy.Api.Controllers.V1.Dashboard.Encoder;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Distribution;
using NoMercy.Authorization;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Route-shape and functional tests for <see cref="CoordinatorDispatchController"/>.
///
/// Route tests verify the spec paths are declared. Functional tests exercise
/// controller logic against mocked dispatcher / progress-store — no HTTP
/// server is required.
/// </summary>
[Trait(name: "Category", value: "CoordinatorDispatch")]
public class CoordinatorDispatchController_Tests
{
    // Shared owner GUID across all Tests.Api fixtures that seed
    // ClaimsPrincipalExtensions._users. Owner property returns the FIRST
    // u.Owner==true user, so all fixtures must agree on the same id —
    // otherwise test execution order can desync principal NameIdentifier
    // from the resolved Owner.Id and IsOwner() flips to false.
    private static readonly Guid OwnerUserId = Guid.Parse(input: "11111111-1111-1111-1111-111111111111");

    // ── Owner seeding — reset+reseed unconditionally for order-independence ──

    private static void SeedOwnerInClaimsExtensions()
    {
        // Reset wipes whatever a prior test left in the process-wide static.
        // NoMercyApiFactory.InitializeAsync() replaces _users with DB users
        // whose Id differs from OwnerUserId; ClaimsPrincipalExtensionsTests
        // calls Reset() in Dispose(). Either leaves IsOwner() returning false.
        UserCache.Current.Reset();

        UserCache.Current.AddUser(
            user: new()
            {
                Id = OwnerUserId,
                Owner = true,
                Email = "dispatch-owner@local",
            }
        );
    }

    // ── Route shape ─────────────────────────────────────────────────────────

    private static IEnumerable<string> ControllerRoutes(Type controller) =>
        controller.GetCustomAttributes<RouteAttribute>().Select(selector: a => a.Template);

    private static IEnumerable<string> ActionRoutes(Type controller, Type httpVerb) =>
        controller
            .GetMethods(bindingAttr: BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(selector: m => m.GetCustomAttributes(attributeType: httpVerb, inherit: false))
            .Cast<HttpMethodAttribute>()
            .Select(selector: a => a.Template ?? string.Empty);

    [Fact]
    public void CoordinatorDispatch_Exposes_Distribution_Route()
    {
        IEnumerable<string> routes = ControllerRoutes(controller: typeof(CoordinatorDispatchController));
        Assert.Contains(
            expected: "api/v{version:apiVersion}/distribution/workers/dispatch",
            collection: routes,
            comparer: StringComparer.OrdinalIgnoreCase
        );
    }

    [Fact]
    public void CoordinatorDispatch_Post_Action_Exists()
    {
        // The root POST has a null/empty template (no sub-path).
        IEnumerable<string> posts = ActionRoutes(
            controller: typeof(CoordinatorDispatchController),
            httpVerb: typeof(HttpPostAttribute)
        );
        Assert.True(
            condition: posts.Any(predicate: t => string.IsNullOrEmpty(value: t)),
            userMessage: "POST / (root) action must exist on the dispatch controller"
        );
    }

    [Fact]
    public void CoordinatorDispatch_GetStatus_Action_Exists()
    {
        IEnumerable<string> gets = ActionRoutes(
            controller: typeof(CoordinatorDispatchController),
            httpVerb: typeof(HttpGetAttribute)
        );
        Assert.Contains(expected: "{taskId}/status", collection: gets, comparer: StringComparer.OrdinalIgnoreCase);
    }

    // ── Controller factory ──────────────────────────────────────────────────

    private static CoordinatorDispatchController BuildController(
        Mock<IWorkerDispatcher>? dispatcherMock = null,
        Mock<ITaskProgressStore>? storeMock = null,
        EncoderOptions? options = null
    )
    {
        SeedOwnerInClaimsExtensions();

        Mock<IWorkerDispatcher> d = dispatcherMock ?? new Mock<IWorkerDispatcher>();
        Mock<ITaskProgressStore> s = storeMock ?? new Mock<ITaskProgressStore>();

        if (dispatcherMock is null)
        {
            d.SetupGet(expression: x => x.AvailableWorkerCount).Returns(value: 1);
            d.Setup(expression: x => x.DispatchAsync(It.IsAny<EncodeTask[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    valueFunction: (EncodeTask[] tasks, CancellationToken _) =>
                        tasks
                            .Select(selector: t => new DispatchResult(
                                TaskId: t.TaskId,
                                Success: true,
                                OutputPath: t.OutputPath,
                                Duration: TimeSpan.FromSeconds(seconds: 1),
                                WorkerId: "mock-worker"
                            ))
                            .ToArray()
                );
        }

        if (storeMock is null)
            s.Setup(expression: x => x.GetAll()).Returns(value: []);

        CoordinatorDispatchController controller = new(
            dispatcher: d.Object,
            progressStore: s.Object,
            encoderOptions: options ?? new EncoderOptions()
        );

        DefaultHttpContext ctx = new()
        {
            User = new(
                identity: new ClaimsIdentity(
                    claims: [new(type: ClaimTypes.NameIdentifier, value: OwnerUserId.ToString())],
                    authenticationType: "test"
                )
            ),
        };
        controller.ControllerContext = new() { HttpContext = ctx };
        return controller;
    }

    // ── Dispatch: validation ────────────────────────────────────────────────

    [Fact]
    public async Task Dispatch_EmptyTasks_ReturnsBadRequest()
    {
        CoordinatorDispatchController sut = BuildController();

        IActionResult result = await sut.Dispatch(
            request: new(Tasks: []),
            ct: CancellationToken.None
        );

        ObjectResult obj = Assert.IsType<ObjectResult>(@object: result);
        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: obj.StatusCode);
    }

    [Fact]
    public async Task Dispatch_NullTasks_ReturnsBadRequest()
    {
        CoordinatorDispatchController sut = BuildController();

        IActionResult result = await sut.Dispatch(
            request: new(Tasks: null),
            ct: CancellationToken.None
        );

        ObjectResult obj = Assert.IsType<ObjectResult>(@object: result);
        Assert.Equal(expected: StatusCodes.Status400BadRequest, actual: obj.StatusCode);
    }

    [Fact]
    public async Task Dispatch_ValidTasks_CallsDispatcher_ReturnsOk()
    {
        Mock<IWorkerDispatcher> mock = new();
        mock.SetupGet(expression: d => d.AvailableWorkerCount).Returns(value: 2);
        mock.Setup(expression: d => d.DispatchAsync(It.IsAny<EncodeTask[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(value:
            [
                new(
                    TaskId: "t1",
                    Success: true,
                    OutputPath: "/out/t1",
                    Duration: TimeSpan.FromSeconds(seconds: 2),
                    WorkerId: "worker-a"
                ),
            ]);

        CoordinatorDispatchController sut = BuildController(dispatcherMock: mock);

        IActionResult result = await sut.Dispatch(
            request: new(
                Tasks: [new(OutputPath: "/out/t1", TaskId: "t1")]
            ),
            ct: CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(@object: result);
        mock.Verify(
            expression: d =>
                d.DispatchAsync(
                    It.Is<EncodeTask[]>(arr => arr.Length == 1),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    [Fact]
    public async Task Dispatch_AutoAssignsTaskId_WhenNotProvided()
    {
        List<EncodeTask[]> captured = [];
        Mock<IWorkerDispatcher> mock = new();
        mock.SetupGet(expression: d => d.AvailableWorkerCount).Returns(value: 1);
        mock.Setup(expression: d => d.DispatchAsync(It.IsAny<EncodeTask[]>(), It.IsAny<CancellationToken>()))
            .Callback<EncodeTask[], CancellationToken>(action: (tasks, _) => captured.Add(item: tasks))
            .ReturnsAsync(
                valueFunction: (EncodeTask[] tasks, CancellationToken _) =>
                    tasks
                        .Select(selector: t => new DispatchResult(
                            TaskId: t.TaskId,
                            Success: true,
                            OutputPath: t.OutputPath,
                            Duration: TimeSpan.Zero
                        ))
                        .ToArray()
            );

        CoordinatorDispatchController sut = BuildController(dispatcherMock: mock);

        await sut.Dispatch(
            request: new(
                Tasks: [new(OutputPath: "/out/auto", TaskId: null)]
            ),
            ct: CancellationToken.None
        );

        Assert.Single(collection: captured);
        Assert.False(
            condition: string.IsNullOrWhiteSpace(value: captured[index: 0][0].TaskId),
            userMessage: "A task ID must be auto-generated when the caller omits it"
        );
    }

    [Fact]
    public async Task Dispatch_MultipleTasks_AllPassedToDispatcher()
    {
        Mock<IWorkerDispatcher> mock = new();
        mock.SetupGet(expression: d => d.AvailableWorkerCount).Returns(value: 3);
        mock.Setup(expression: d => d.DispatchAsync(It.IsAny<EncodeTask[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                valueFunction: (EncodeTask[] tasks, CancellationToken _) =>
                    tasks
                        .Select(selector: t => new DispatchResult(
                            TaskId: t.TaskId,
                            Success: true,
                            OutputPath: t.OutputPath,
                            Duration: TimeSpan.Zero
                        ))
                        .ToArray()
            );

        CoordinatorDispatchController sut = BuildController(dispatcherMock: mock);

        await sut.Dispatch(
            request: new(
                Tasks:
                [
                    new(OutputPath: "/out/a", TaskId: "a"),
                    new(OutputPath: "/out/b", TaskId: "b"),
                    new(OutputPath: "/out/c", TaskId: "c"),
                ]
            ),
            ct: CancellationToken.None
        );

        mock.Verify(
            expression: d =>
                d.DispatchAsync(
                    It.Is<EncodeTask[]>(arr => arr.Length == 3),
                    It.IsAny<CancellationToken>()
                ),
            times: Times.Once
        );
    }

    // ── GetTaskStatus ─────────────────────────────────────────────────────────

    [Fact]
    public void GetTaskStatus_UnknownTask_ReturnsNotFound()
    {
        Mock<ITaskProgressStore> store = new();
        store.Setup(expression: s => s.GetAll()).Returns(value: []);

        CoordinatorDispatchController sut = BuildController(storeMock: store);

        IActionResult result = sut.GetTaskStatus(taskId: "does-not-exist");

        ObjectResult obj = Assert.IsType<ObjectResult>(@object: result);
        Assert.Equal(expected: StatusCodes.Status404NotFound, actual: obj.StatusCode);
    }

    [Fact]
    public void GetTaskStatus_KnownTask_ReturnsOk()
    {
        TaskProgressSnapshot snapshot = new(
            TaskId: "t-known",
            WorkerId: "w1",
            PercentComplete: 55.0,
            CurrentFps: 30,
            CurrentSpeed: 1.2,
            CurrentStage: "encoding",
            ElapsedSeconds: 60,
            EstimatedRemainingSeconds: 45,
            CurrentTimeSeconds: 120,
            DurationSeconds: 300,
            ReceivedAtUtc: DateTime.UtcNow
        );

        Mock<ITaskProgressStore> store = new();
        store.Setup(expression: s => s.GetAll()).Returns(value: [snapshot]);

        CoordinatorDispatchController sut = BuildController(storeMock: store);

        IActionResult result = sut.GetTaskStatus(taskId: "t-known");

        Assert.IsType<OkObjectResult>(@object: result);
    }
}
