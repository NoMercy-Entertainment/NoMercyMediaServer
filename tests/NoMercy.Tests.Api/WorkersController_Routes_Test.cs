using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using NoMercy.Api.Controllers.V1.Dashboard.Admin;
using NoMercy.Api.Controllers.V1.Worker;
using Xunit;

namespace NoMercy.Tests.Api;

/// <summary>
/// Phase 4.10 — distribution route alignment. Verifies the spec routes are
/// exposed and the legacy aliases stay in place so older self-hosted workers
/// keep functioning.
/// </summary>
[Trait("Category", "Routes")]
public class WorkersController_Routes_Test
{
    private static IEnumerable<string> ControllerRoutes(Type controller) =>
        controller.GetCustomAttributes<RouteAttribute>().Select(a => a.Template);

    private static IEnumerable<string> ActionRoutes(Type controller, Type httpVerb) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .SelectMany(m => m.GetCustomAttributes(httpVerb, inherit: false))
            .Cast<HttpMethodAttribute>()
            .Select(a => a.Template ?? string.Empty);

    [Fact]
    public void WorkersController_Exposes_Distribution_PrimaryRoute()
    {
        IEnumerable<string> routes = ControllerRoutes(typeof(WorkersController));
        Assert.Contains("api/v{version:apiVersion}/distribution/workers", routes);
    }

    [Fact]
    public void WorkersController_Keeps_Dashboard_Alias_For_Compat()
    {
        IEnumerable<string> routes = ControllerRoutes(typeof(WorkersController));
        Assert.Contains("api/v{version:apiVersion}/dashboard/workers", routes);
    }

    [Fact]
    public void WorkerExecution_Exposes_Tasks_PrimaryRoute()
    {
        IEnumerable<string> routes = ActionRoutes(
            typeof(WorkerExecutionController),
            typeof(HttpPostAttribute)
        );
        Assert.Contains("tasks", routes);
    }

    [Fact]
    public void WorkerExecution_Keeps_ExecuteTask_Alias_For_Compat()
    {
        IEnumerable<string> routes = ActionRoutes(
            typeof(WorkerExecutionController),
            typeof(HttpPostAttribute)
        );
        Assert.Contains("execute-task", routes);
    }

    [Fact]
    public void WorkerSource_Exposes_PrimaryRoute()
    {
        IEnumerable<string> routes = ControllerRoutes(typeof(WorkerSourceController));
        Assert.Contains("api/v{version:apiVersion}/worker/source", routes);
    }

    [Fact]
    public void WorkerSource_Keeps_LegacyAlias_For_Compat()
    {
        IEnumerable<string> routes = ControllerRoutes(typeof(WorkerSourceController));
        Assert.Contains("api/v{version:apiVersion}/worker-source", routes);
    }
}
