# CQRS & Controller Patterns — Implementation Guide

> Strict conventions used throughout this project. Follow these exactly when adding any new feature. Every pattern here has a real file reference you can look at.

---

## Directory Structure

```
Application/Services/{Domain}/
  Command/        ← write operations (Command + Handler in the same file)
  Query/          ← read operations (Query + Handler in the same file)
  Request/        ← input DTOs (used by Command/Query constructors)
  Response/       ← output DTOs (returned by handlers)
  Validator/      ← one validator file per Command or Query

WebApi/Controllers/
  {Domain}Controller.cs
```

One file = one Command/Query + its Handler. Never split them.

---

## 1. Commands
W
### File: `Application/Services/{Domain}/Command/{Name}Command.cs`

**Structure rules:**

1. The class name is the operation name + `Command` (e.g., `ProcessWalkInTimeInCommand`)
2. The handler class is `{Name}CommandHandler`, in the **same file**
3. The Command constructor takes a **Request DTO** (not raw primitives for multi-field inputs) plus any server-resolved values (e.g., `frontDeskUserId` from the JWT claim)
4. `IRequest<TResponse>` — always specify the response type
5. Store the request in a read-only property, not a field

```csharp
// Command — holds inputs only, no logic
public class ProcessWalkInTimeInCommand : IRequest<WalkInTimeInResponse>
{
    public WalkInTimeInRequest Request { get; }
    public string FrontDeskUserId { get; }

    public ProcessWalkInTimeInCommand(WalkInTimeInRequest request, string frontDeskUserId)
    {
        Request = request;
        FrontDeskUserId = frontDeskUserId;
    }
}

// Handler — all logic lives here
public class ProcessWalkInTimeInCommandHandler
    : IRequestHandler<ProcessWalkInTimeInCommand, WalkInTimeInResponse>
{
    private readonly ApplicationDbContext _dbContext;

    public ProcessWalkInTimeInCommandHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<WalkInTimeInResponse> Handle(
        ProcessWalkInTimeInCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request; // alias for brevity
        // ...
    }
}
```

### Handler body — standard execution order:

1. **Load and validate dependencies** (DB lookups) — throw `NotFoundException` or `ConflictException` immediately when something is wrong. Do not continue.
2. **Business rule checks** — reject invalid states before touching the DB.
3. **Begin transaction** — only when writing multiple related records.
4. **Re-check constraints inside the transaction** (race condition prevention) when relevant (e.g., availability).
5. **Compute derived values** (pricing, reference numbers, etc.)
6. **Create and persist entities**
7. **Commit and return response DTO**
8. **Catch block** — rollback transaction and re-throw. Never swallow exceptions.

```csharp
// Transaction pattern
await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
try
{
    // ... all writes here
    await _dbContext.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);
    return response;
}
catch
{
    await tx.RollbackAsync(cancellationToken);
    throw;
}
```

### Exception types — use consistently:

| Exception | When |
|---|---|
| `NotFoundException` | Record does not exist or is inactive |
| `ConflictException` | Business rule violated, state conflict, duplicate |
| `UnauthorizedException` | Auth failed, token invalid, access denied |

Never throw `Exception` directly. Never return null to indicate failure — always throw.

### Private helpers in the handler:

Extract complex logic into `private static` methods on the handler class. Use `/// <summary>` doc comments explaining the **why**, not the what.

```csharp
private static DateTime ResolveScheduledEnd(
    ServicePackage package, decimal? estimatedHours, DateTime now)
{
    // ... pure computation, no DB access
}
```

---

## 2. Queries

### File: `Application/Services/{Domain}/Query/{Name}Query.cs`

**Structure rules — same as commands, with these differences:**

1. Always use `AsNoTracking()` — queries never modify data
2. Use `.Select()` projection directly in the query — never `.Include()` followed by AutoMapper. Project only the columns you need.
3. Count before pagination, always as a separate call on the same `IQueryable`
4. Extract filters into `private static` helper methods — keep `Handle` readable
5. Return `PagedResult<T>` for lists, the DTO directly for single records

```csharp
public class GetBookingsQuery : IRequest<PagedResult<BookingListItemResponse>>
{
    public BookingFilterRequest Filter { get; }
    public GetBookingsQuery(BookingFilterRequest filter) => Filter = filter;
}

public class GetBookingsQueryHandler
    : IRequestHandler<GetBookingsQuery, PagedResult<BookingListItemResponse>>
{
    private readonly ApplicationDbContext _dbContext;

    public GetBookingsQueryHandler(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<BookingListItemResponse>> Handle(
        GetBookingsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Booking> query = _dbContext.Bookings.AsNoTracking();

        // Compose filters — each helper appends a WHERE clause or returns unchanged
        query = ApplySearch(query, request.Filter.Search);
        query = ApplyStatusFilter(query, request.Filter.Status);

        // Count on the filtered query before pagination
        var totalCount = await query.CountAsync(cancellationToken);

        // Sort, then paginate, then project
        query = ApplySort(query, request.Filter.SortBy);
        var page = Math.Max(1, request.Filter.Page);

        var items = await query
            .Skip((page - 1) * request.Filter.PageSize)
            .Take(request.Filter.PageSize)
            .Select(b => new BookingListItemResponse
            {
                BookingId   = b.Id,
                GuestName   = b.Guest != null
                    ? b.Guest.FirstName + " " + b.Guest.LastName
                    : string.Empty,
                // ... map all fields inline
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<BookingListItemResponse>
        {
            Items      = items,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = request.Filter.PageSize,
        };
    }

    // Filter helpers: receive IQueryable, return IQueryable. Early return if no filter.
    private static IQueryable<Booking> ApplySearch(IQueryable<Booking> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var term = search.Trim().ToLower();
        return query.Where(b => b.ReferenceNumber.ToLower().Contains(term));
    }

    private static IQueryable<Booking> ApplyStatusFilter(
        IQueryable<Booking> query, string? statusParam)
    {
        var statuses = ParseEnumList<BookingStatus>(statusParam);
        return statuses.Count > 0 ? query.Where(b => statuses.Contains(b.BookingStatus)) : query;
    }
}
```

### Parsing comma-separated enum filters:

```csharp
// Standard utility — copy into any query that needs it
private static List<TEnum> ParseEnumList<TEnum>(string? param) where TEnum : struct, Enum
{
    if (string.IsNullOrWhiteSpace(param)) return [];
    return param
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s =>
        {
            var normalized = s.Replace("_", ""); // "checked_in" → "checkedin"
            return Enum.TryParse<TEnum>(normalized, ignoreCase: true, out var val)
                ? (TEnum?)val : null;
        })
        .Where(v => v.HasValue)
        .Select(v => v!.Value)
        .ToList();
}
```

---

## 3. Request & Response DTOs

### Request DTOs (`Application/Services/{Domain}/Request/`)

- Suffix: `Request` (e.g., `WalkInTimeInRequest`)
- Simple data containers — no logic, no validation
- Properties use `{ get; set; }` (model binding needs setters)
- Optional fields are nullable (`string? GuestEmail`, `decimal? EstimatedHours`)

### Response DTOs (`Application/Services/{Domain}/Response/`)

- Suffix: `Response` for single records, `Response` inside `PagedResult<T>` for lists
- Prefer `{ get; set; }` (simpler) or `{ get; init; }` (immutable)
- Map inside the handler's `.Select()` or at the `return` statement — never use AutoMapper for domain → response mapping (only for Identity entity → DTO mapping)
- Include every field the frontend needs. Never return the raw entity.

---

## 4. FluentValidation

### File: `Application/Services/{Domain}/Validator/{CommandName}Validator.cs`

**Rules:**

1. Validators target the **Command or Query class**, not the Request DTO
2. Access request fields via `x.Request.FieldName`
3. One validator file per command/query
4. Validators are auto-registered via `AddValidatorsFromAssembly` — no manual registration needed
5. `ValidationBehavior` in the MediatR pipeline runs validators automatically before every handler

```csharp
public class ProcessWalkInTimeInCommandValidator
    : AbstractValidator<ProcessWalkInTimeInCommand>
{
    public ProcessWalkInTimeInCommandValidator()
    {
        // Required fields
        RuleFor(x => x.Request.ResourceId)
            .NotEmpty().WithMessage("Resource ID is required.");

        // Conditional rule — only validate when the field is present
        When(x => x.Request.EstimatedHours.HasValue, () =>
        {
            RuleFor(x => x.Request.EstimatedHours)
                .GreaterThan(0).WithMessage("Estimated hours must be greater than 0.")
                .LessThanOrEqualTo(24).WithMessage("Estimated hours must not exceed 24.");
        });

        // Optional field with format check — skip entirely when null/empty
        RuleFor(x => x.Request.GuestEmail)
            .EmailAddress().WithMessage("Must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Request.GuestEmail));

        // Enum validation
        RuleFor(x => x.Request.PaymentScheme)
            .IsInEnum().WithMessage("Invalid payment scheme.");

        // Regex
        RuleFor(x => x.Request.GuestPhone)
            .NotEmpty()
            .Matches(@"^\d{10,11}$").WithMessage("Phone must be 10 or 11 digits.");
    }
}
```

### What goes in the validator vs the handler:

| In Validator | In Handler |
|---|---|
| Format, range, required, enum check | DB-dependent checks (exists, is active, is unique) |
| Length limits | Business rules that depend on loaded data |
| Regex patterns | State machine checks (booking must be checked-in) |
| Cross-field comparisons (passwords match) | Anything that needs `await` |

**Do not** query the database from a validator. If a check requires a DB lookup, it belongs in the handler.

---

## 5. Controllers

### File: `WebApi/Controllers/{Domain}Controller.cs`

**Structure rules:**

1. Inherit `ControllerBase` — never `Controller` (no views)
2. Route goes on the class: `[Route("api/{resource}")]`
3. Authorization on the class when all endpoints share the same policy; on individual methods when they differ
4. Inject only `IMediator` — never inject services, DbContext, or UserManager
5. Extract the JWT user ID with `User.FindFirstValue(ClaimTypes.NameIdentifier)` when needed
6. Always pass `CancellationToken ct` and forward it to `_mediator.Send`
7. All endpoints return `Ok(result)` — let the `GlobalExceptionHandler` handle error HTTP codes
8. Add a short comment above each endpoint group and each endpoint showing the HTTP verb + route

```csharp
[ApiController]
[Route("api/coworking/frontdesk")]
[Authorize(Roles = "Admin,Frontdesk")]   // class-level when all endpoints share it
public class CoworkingFrontDeskController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoworkingFrontDeskController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // GET api/coworking/frontdesk/bookings
    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookings(
        [FromQuery] BookingFilterRequest filter, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetBookingsQuery(filter), ct);
        return Ok(result);
    }

    // POST api/coworking/frontdesk/time-in
    [HttpPost("time-in")]
    public async Task<IActionResult> TimeIn(
        [FromBody] WalkInTimeInRequest request, CancellationToken ct)
    {
        // Server-resolved value: read from JWT, pass into the command
        var frontDeskUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _mediator.Send(
            new ProcessWalkInTimeInCommand(request, frontDeskUserId), ct);
        return Ok(result);
    }

    // POST api/coworking/frontdesk/time-out/{bookingId}
    [HttpPost("time-out/{bookingId:guid}")]
    public async Task<IActionResult> TimeOut(
        Guid bookingId,
        [FromBody] WalkInTimeOutRequest? request,   // nullable: body is optional
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProcessWalkInTimeOutCommand(bookingId, request), ct);
        return Ok(result);
    }

    // Per-endpoint authorization when it differs from the class-level policy
    [HttpPost("payments/{paymentId:guid}/validate")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ValidatePayment(
        Guid paymentId,
        [FromBody] ValidateManualPaymentRequest request,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _mediator.Send(
            new ValidateManualPaymentCommand(paymentId, request, userId), ct);
        return Ok(result);
    }

    // Multipart form data — add [Consumes] attribute
    [HttpPost("payments/submit-proof")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitPaymentProof(
        [FromForm] SubmitManualPaymentProofRequest request,
        IFormFile file,
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var result = await _mediator.Send(
            new SubmitManualPaymentProofCommand(request, file, userId), ct);
        return Ok(result);
    }
}
```

### Controller does NOT:

- Contain any business logic
- Call the DbContext directly
- Map DTOs
- Throw exceptions (let the handler throw, `GlobalExceptionHandler` catches)
- Return `BadRequest()`, `NotFound()`, `Conflict()` manually — the `GlobalExceptionHandler` does this from the exception type

---

## 6. Checklist for Adding a New Feature

Follow this order every time:

1. **Request DTO** — `Application/Services/{Domain}/Request/{Name}Request.cs`
2. **Response DTO** — `Application/Services/{Domain}/Response/{Name}Response.cs`
3. **Command or Query** — `Application/Services/{Domain}/Command/{Name}Command.cs` (includes handler)
4. **Validator** — `Application/Services/{Domain}/Validator/{Name}CommandValidator.cs`
5. **Controller endpoint** — add method to the relevant controller in `WebApi/Controllers/`
6. `dotnet build Application/Application.csproj` — confirm 0 errors before running
