using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.Api.Constraints;

public class Program
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap.Add("ulid", typeof(UlidRouteConstraint));
        });
    }
}