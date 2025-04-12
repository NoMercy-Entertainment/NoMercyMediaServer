using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Api.Controllers.V1.Media;
using NoMercy.NmSystem;

namespace NoMercy.Api.Controllers;

public class BaseController : Controller
{
    protected IActionResult UnauthenticatedResponse(string detail)
    {
        return Problem(
            title: "Unauthenticated.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status401Unauthorized,
            type: "/docs/errors/unauthenticated");
    }

    protected IActionResult UnauthorizedResponse(string detail)
    {
        return Problem(
            title: "Unauthorized.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status403Forbidden,
            type: "/docs/errors/forbidden");
    }

    protected IActionResult NotFoundResponse(string detail)
    {
        return Problem(
            title: "Not Found.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status404NotFound,
            type: "/docs/errors/not-found");
    }

    protected IActionResult BadRequestResponse(string detail)
    {
        return Problem(
            title: "Bad Request.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status400BadRequest,
            type: "/docs/errors/bad-request");
    }

    protected IActionResult InternalServerErrorResponse(string detail)
    {
        return Problem(
            title: "Internal Server Error.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status500InternalServerError,
            type: "/docs/errors/internal-server-error");
    }

    protected IActionResult ConflictResponse(string detail)
    {
        return Problem(
            title: "Conflict.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status409Conflict,
            type: "/docs/errors/conflict");
    }

    protected IActionResult NotImplementedResponse(string detail)
    {
        return Problem(
            title: "Not Implemented.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status501NotImplemented,
            type: "/docs/errors/not-implemented");
    }

    protected IActionResult ServiceUnavailableResponse(string detail)
    {
        return Problem(
            title: "Service Unavailable.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            type: "/docs/errors/service-unavailable");
    }

    protected IActionResult GatewayTimeoutResponse(string detail)
    {
        return Problem(
            title: "Gateway Timeout.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status504GatewayTimeout,
            type: "/docs/errors/gateway-timeout");
    }

    protected IActionResult UnprocessableEntityResponse(string detail)
    {
        return Problem(
            title: "Unprocessable Entity.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status422UnprocessableEntity,
            type: "/docs/errors/unprocessable-entity");
    }

    protected IActionResult TooManyRequestsResponse(string detail)
    {
        return Problem(
            title: "Too Many Requests.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status429TooManyRequests,
            type: "/docs/errors/too-many-requests");
    }

    protected IActionResult GoneResponse(string detail)
    {
        return Problem(
            title: "Gone.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status410Gone,
            type: "/docs/errors/gone");
    }

    protected IActionResult PaymentRequiredResponse(string detail)
    {
        return Problem(
            title: "Payment Required.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status402PaymentRequired,
            type: "/docs/errors/payment-required");
    }

    protected IActionResult LengthRequiredResponse(string detail)
    {
        return Problem(
            title: "Length Required.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status411LengthRequired,
            type: "/docs/errors/length-required");
    }

    protected IActionResult PreconditionFailedResponse(string detail)
    {
        return Problem(
            title: "Precondition Failed.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status412PreconditionFailed,
            type: "/docs/errors/precondition-failed");
    }

    protected IActionResult RequestEntityTooLargeResponse(string detail)
    {
        return Problem(
            title: "Request Entity Too Large.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status413RequestEntityTooLarge,
            type: "/docs/errors/request-entity-too-large");
    }

    protected IActionResult RequestUriTooLongResponse(string detail)
    {
        return Problem(
            title: "Request-URI Too Long.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status414RequestUriTooLong,
            type: "/docs/errors/request-uri-too-long");
    }

    protected IActionResult UnsupportedMediaTypeResponse(string detail)
    {
        return Problem(
            title: "Unsupported Media Type.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status415UnsupportedMediaType,
            type: "/docs/errors/unsupported-media-type");
    }

    protected IActionResult RequestedRangeNotSatisfiableResponse(string detail)
    {
        return Problem(
            title: "Requested Range Not Satisfiable.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status416RequestedRangeNotSatisfiable,
            type: "/docs/errors/requested-range-not-satisfiable");
    }

    protected IActionResult ExpectationFailedResponse(string detail)
    {
        return Problem(
            title: "Expectation Failed.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status417ExpectationFailed,
            type: "/docs/errors/expectation-failed");
    }

    protected IActionResult MisdirectedRequestResponse(string detail)
    {
        return Problem(
            title: "Misdirected Request.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status421MisdirectedRequest,
            type: "/docs/errors/misdirected-request");
    }

    protected IActionResult UnavailableForLegalReasonsResponse(string detail)
    {
        return Problem(
            title: "Unavailable For Legal Reasons.",
            detail: detail.Localize(),
            instance: HttpContext.Request.Path,
            statusCode: StatusCodes.Status451UnavailableForLegalReasons,
            type: "/docs/errors/unavailable-for-legal-reasons");
    }

    protected IActionResult GetPaginatedResponse<T>(IEnumerable<T> data, [FromQuery] PageRequestDto request)
    {
        List<T> newData = data.ToList();
        bool hasMore = newData.Count() >= request.Take;

        newData = newData.Take(request.Take).ToList();

        PaginatedResponse<T> response = new()
        {
            Data = newData,
            NextPage = hasMore ? request.Page + 1 : null,
            HasMore = hasMore
        };

        return Ok(response);
    }

    protected string Language()
    {
        return HttpContext.Request.Headers.AcceptLanguage.FirstOrDefault() ?? "en";
    }

    protected string Country()
    {
        return HttpContext.Request.Headers.AcceptLanguage.LastOrDefault() ?? "US";
    }
}
