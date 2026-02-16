using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http; // Required for User/IP
using Serilog.Context; // Required for pushing properties
using System.Diagnostics;
using System.Text.Json; // For serializing request data

namespace Application.Common.Behaviors
{
    public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public LoggingBehavior(
            ILogger<LoggingBehavior<TRequest, TResponse>> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _logger = logger;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;

            // 1. CAPTURE ESSENTIAL CONTEXT
            // We grab these proactively so every log line below has them.
            var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
            var userName = _httpContextAccessor.HttpContext?.User?.Identity?.Name ?? "Unknown";
            var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
            var traceId = Activity.Current?.Id ?? _httpContextAccessor.HttpContext?.TraceIdentifier ?? Guid.NewGuid().ToString();

            // 2. SAFETY: Prevent logging passwords
            // We serialize the request to JSON so we can see EXACTLY what data was sent.
            // But we must be careful not to log raw passwords.
            var requestJson = JsonSerializer.Serialize(request);
            if (requestName.Contains("Login") || requestName.Contains("Password"))
            {
                requestJson = "[REDACTED SENSITIVE DATA]";
            }

            // 3. PUSH CONTEXT TO LOG STACK
            // Any log written inside this 'using' block (even deeper in your code) 
            // will automatically have these properties attached.
            using (LogContext.PushProperty("User_Id", userId))
            using (LogContext.PushProperty("User_Name", userName))
            using (LogContext.PushProperty("Client_IP", ipAddress))
            using (LogContext.PushProperty("Trace_Id", traceId))
            using (LogContext.PushProperty("Request_Data", requestJson))
            {
                _logger.LogInformation(
                    "[START] {RequestName} | User: {User_Id} | IP: {Client_IP}",
                    requestName, userId, ipAddress);

                var timer = Stopwatch.StartNew();

                try
                {
                    var response = await next(); // Execute the Command/Query

                    timer.Stop();
                    var elapsedMs = timer.ElapsedMilliseconds;

                    // 4. PERFORMANCE MONITORING
                    if (elapsedMs > 500) // If slower than 500ms, log as WARNING
                    {
                        _logger.LogWarning(
                            "[SLOW] {RequestName} took {Elapsed}ms | User: {User_Id}",
                            requestName, elapsedMs, userId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "[END] {RequestName} completed in {Elapsed}ms",
                            requestName, elapsedMs);
                    }

                    return response;
                }
                catch (Exception ex)
                {
                    timer.Stop();

                    // 5. ERROR TRACING (The "Holy Grail" of debugging)
                    // We log the Exception + The Request Data that caused it.
                    _logger.LogError(
                        ex,
                        "[ERROR] {RequestName} failed after {Elapsed}ms | User: {User_Id} | Error: {ErrorMessage} | Request: {Request_Data}",
                        requestName, timer.ElapsedMilliseconds, userId, ex.Message, requestJson);

                    throw; // Re-throw so the GlobalExceptionHandler returns the 500/400 response
                }
            }
        }
    }
}