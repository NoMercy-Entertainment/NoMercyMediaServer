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

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NoMercy.Api.DTOs.Common;
using NoMercy.Api.DTOs.Media;
using NoMercy.Authorization;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers;

public class BaseController : Controller
{
    // Media authorization policy resolved from the request's DI container.
    // Replaces the former static ClaimsPrincipal authorization extensions.
    // Resolved from the request's DI container; falls back to a policy over the
    // shared user cache when there is no HttpContext (e.g. unit-constructed
    // controllers in tests). Both paths read the same UserCache singleton.
    protected IMediaAuthorizationPolicy AuthPolicy =>
        HttpContext?.RequestServices?.GetService<IMediaAuthorizationPolicy>()
        ?? new MediaAuthorizationPolicy(UserCache.Current);

    protected IUserCache UserCacheService =>
        HttpContext?.RequestServices?.GetService<IUserCache>() ?? UserCache.Current;

    private IActionResult ProblemWithTrace(
        string title,
        string detail,
        int statusCode,
        string type,
        [System.Runtime.CompilerServices.CallerMemberName] string caller = ""
    )
    {
        // A returned 5xx never reaches GlobalExceptionHandlerMiddleware, so it would
        // otherwise be an unlogged, invisible server error.
        if (statusCode >= 500)
        {
            ILogger? log = HttpContext
                ?.RequestServices?.GetService<ILoggerFactory>()
                ?.CreateLogger("NoMercy.Api.ServerError");
            log?.LogError(
                "[{TraceId}] {Status} returned from {Caller} for {Path}: {Detail}", [HttpContext?.TraceIdentifier, statusCode, caller, HttpContext?.Request.Path.Value, detail]
            );
        }

        ProblemDetails problemDetails = new()
        {
            Type = type,
            Title = title.Localize(),
            Detail = detail.Localize(),
            Instance = HttpContext?.Request.Path.Value,
            Status = statusCode,
            Extensions = { { "traceId", HttpContext?.TraceIdentifier } },
        };

        return StatusCode(statusCode, problemDetails);
    }

    protected IActionResult UnauthenticatedResponse(string detail)
    {
        return ProblemWithTrace(
            "Unauthenticated.",
            detail,
            StatusCodes.Status401Unauthorized,
            "/docs/errors/unauthenticated"
        );
    }

    protected IActionResult UnauthorizedResponse(string detail)
    {
        return ProblemWithTrace(
            "Unauthorized.",
            detail,
            StatusCodes.Status403Forbidden,
            "/docs/errors/forbidden"
        );
    }

    protected IActionResult NotFoundResponse(string detail)
    {
        return ProblemWithTrace(
            "Not Found.",
            detail,
            StatusCodes.Status404NotFound,
            "/docs/errors/not-found"
        );
    }

    protected IActionResult BadRequestResponse(string detail)
    {
        return ProblemWithTrace(
            "Bad Request.",
            detail,
            StatusCodes.Status400BadRequest,
            "/docs/errors/bad-request"
        );
    }

    protected IActionResult InternalServerErrorResponse(string detail)
    {
        return ProblemWithTrace(
            "Internal Server Error.",
            detail,
            StatusCodes.Status500InternalServerError,
            "/docs/errors/internal-server-error"
        );
    }

    protected IActionResult ConflictResponse(string detail)
    {
        return ProblemWithTrace(
            "Conflict.",
            detail,
            StatusCodes.Status409Conflict,
            "/docs/errors/conflict"
        );
    }

    protected IActionResult NotImplementedResponse(string detail)
    {
        return ProblemWithTrace(
            "Not Implemented.",
            detail,
            StatusCodes.Status501NotImplemented,
            "/docs/errors/not-implemented"
        );
    }

    protected IActionResult ServiceUnavailableResponse(string detail)
    {
        return ProblemWithTrace(
            "Service Unavailable.",
            detail,
            StatusCodes.Status503ServiceUnavailable,
            "/docs/errors/service-unavailable"
        );
    }

    protected IActionResult GatewayTimeoutResponse(string detail)
    {
        return ProblemWithTrace(
            "Gateway Timeout.",
            detail,
            StatusCodes.Status504GatewayTimeout,
            "/docs/errors/gateway-timeout"
        );
    }

    protected IActionResult UnprocessableEntityResponse(string detail)
    {
        return ProblemWithTrace(
            "Unprocessable Entity.",
            detail,
            StatusCodes.Status422UnprocessableEntity,
            "/docs/errors/unprocessable-entity"
        );
    }

    protected IActionResult TooManyRequestsResponse(string detail)
    {
        return ProblemWithTrace(
            "Too Many Requests.",
            detail,
            StatusCodes.Status429TooManyRequests,
            "/docs/errors/too-many-requests"
        );
    }

    protected IActionResult GoneResponse(string detail)
    {
        return ProblemWithTrace(
            "Gone.",
            detail,
            StatusCodes.Status410Gone,
            "/docs/errors/gone"
        );
    }

    protected IActionResult PaymentRequiredResponse(string detail)
    {
        return ProblemWithTrace(
            "Payment Required.",
            detail,
            StatusCodes.Status402PaymentRequired,
            "/docs/errors/payment-required"
        );
    }

    protected IActionResult LengthRequiredResponse(string detail)
    {
        return ProblemWithTrace(
            "Length Required.",
            detail,
            StatusCodes.Status411LengthRequired,
            "/docs/errors/length-required"
        );
    }

    protected IActionResult PreconditionFailedResponse(string detail)
    {
        return ProblemWithTrace(
            "Precondition Failed.",
            detail,
            StatusCodes.Status412PreconditionFailed,
            "/docs/errors/precondition-failed"
        );
    }

    protected IActionResult RequestEntityTooLargeResponse(string detail)
    {
        return ProblemWithTrace(
            "Request Entity Too Large.",
            detail,
            StatusCodes.Status413RequestEntityTooLarge,
            "/docs/errors/request-entity-too-large"
        );
    }

    protected IActionResult RequestUriTooLongResponse(string detail)
    {
        return ProblemWithTrace(
            "Request-URI Too Long.",
            detail,
            StatusCodes.Status414RequestUriTooLong,
            "/docs/errors/request-uri-too-long"
        );
    }

    protected IActionResult UnsupportedMediaTypeResponse(string detail)
    {
        return ProblemWithTrace(
            "Unsupported Media Type.",
            detail,
            StatusCodes.Status415UnsupportedMediaType,
            "/docs/errors/unsupported-media-libraryType"
        );
    }

    protected IActionResult RequestedRangeNotSatisfiableResponse(string detail)
    {
        return ProblemWithTrace(
            "Requested Range Not Satisfiable.",
            detail,
            StatusCodes.Status416RequestedRangeNotSatisfiable,
            "/docs/errors/requested-range-not-satisfiable"
        );
    }

    protected IActionResult ExpectationFailedResponse(string detail)
    {
        return ProblemWithTrace(
            "Expectation Failed.",
            detail,
            StatusCodes.Status417ExpectationFailed,
            "/docs/errors/expectation-failed"
        );
    }

    protected IActionResult MisdirectedRequestResponse(string detail)
    {
        return ProblemWithTrace(
            "Misdirected Request.",
            detail,
            StatusCodes.Status421MisdirectedRequest,
            "/docs/errors/misdirected-request"
        );
    }

    protected IActionResult UnavailableForLegalReasonsResponse(string detail)
    {
        return ProblemWithTrace(
            "Unavailable For Legal Reasons.",
            detail,
            StatusCodes.Status451UnavailableForLegalReasons,
            "/docs/errors/unavailable-for-legal-reasons"
        );
    }

    protected IActionResult GetPaginatedResponse<T>(
        IEnumerable<T> data,
        [FromQuery] PageRequestDto request
    )
    {
        List<T> newData = data.ToList();
        bool hasMore = newData.Count >= request.Take;

        newData = newData.Take(request.Take).ToList();

        PaginatedResponse<T> response = new()
        {
            Data = newData,
            NextPage = hasMore ? request.Page + 1 : null,
            HasMore = hasMore,
        };

        return Ok(response);
    }

    protected string Language()
    {
        return HttpContext
                .Request.Headers.AcceptLanguage.FirstOrDefault()
                ?.Split("_")
                .FirstOrDefault()
            ?? LocalizationHelper.GlobalLocalizer.TargetLanguage;
    }

    protected string Country()
    {
        return HttpContext.Request.Headers["country"].FirstOrDefault()
            ?? RegionInfo.CurrentRegion.TwoLetterISORegionName;
    }

    protected static readonly string[] Numbers =
    [
        "*",
        "#",
        "'",
        "\"",
        "1",
        "2",
        "3",
        "4",
        "5",
        "6",
        "7",
        "8",
        "9",
        "0",
    ];

    protected static readonly string[] Letters =
    [
        "#",
        "A",
        "B",
        "C",
        "D",
        "E",
        "F",
        "G",
        "H",
        "I",
        "J",
        "K",
        "L",
        "M",
        "N",
        "O",
        "P",
        "Q",
        "R",
        "S",
        "T",
        "U",
        "V",
        "W",
        "X",
        "Y",
        "Z",
    ];
}
