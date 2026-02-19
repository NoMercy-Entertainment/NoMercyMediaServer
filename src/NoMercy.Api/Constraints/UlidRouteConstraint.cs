using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace NoMercy.Api.Constraints;

public class UlidRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values,
        RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out object? routeValue)) return false;

        string? parameterValueString = Convert.ToString(routeValue, CultureInfo.InvariantCulture);
        return Ulid.TryParse(parameterValueString, out _);
    }
}