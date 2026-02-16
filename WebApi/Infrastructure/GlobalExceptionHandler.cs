using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FluentValidation; // 👈 1. Add this namespace

namespace WebApi.Infrastructure
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetailsService;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger,
            IProblemDetailsService problemDetailsService)
        {
            _logger = logger;
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            _logger.LogError(exception, "Unhandled exception occurred");

            // 2. Create the base ProblemDetails object
            var problemDetails = new ProblemDetails
            {
                Type = exception.GetType().Name,
                Title = "An error occurred",
                Detail = exception.Message,
                Status = StatusCodes.Status500InternalServerError
            };

            // 3. Handle Specific Exception Types
            switch (exception)
            {
                // 👇 HANDLE VALIDATION ERRORS HERE
                case ValidationException validationException:
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Title = "Validation Failed";
                    problemDetails.Detail = "Invalid Inputs.";

                    // Group errors by Field Name to create the array structure
                    var errors = validationException.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()
                        );

                    // Add them to the "errors" field in JSON
                    problemDetails.Extensions["errors"] = errors;
                    break;

                case ArgumentException:
                case InvalidOperationException:
                    problemDetails.Status = StatusCodes.Status400BadRequest;
                    problemDetails.Title = "Bad Request";
                    break;

                default:
                    problemDetails.Status = StatusCodes.Status500InternalServerError;
                    break;
            }

            httpContext.Response.StatusCode = problemDetails.Status.Value;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails,
                Exception = exception
            });
        }
    }
}