# Authentication & User Management — Complete Backend Reference

This document covers the entire auth stack chronologically: from the domain model through persistence, token generation, application commands, validation, and finally the HTTP API layer.

---

## 1. Domain Entities

### 1.1 `User` — `Domain/Entity/Identity/User.cs`

Extends ASP.NET Identity's `IdentityUser` (which already provides `Id`, `Email`, `UserName`, `PasswordHash`, `PhoneNumber`, `LockoutEnd`, etc.).

```csharp
public class User : IdentityUser
{
    public string FirstName { get; set; } = String.Empty;
    public string LastName  { get; set; } = String.Empty;
    public string MiddleName { get; set; } = String.Empty;
    public string Address   { get; set; } = String.Empty;

    public RegistrationStatus Status { get; set; } = RegistrationStatus.Pending;

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    // Set to true when an admin creates/resets the account.
    // Forces the user to change the password on their first login.
    public bool RequiresPasswordChange { get; set; } = false;

    public ICollection<UserRole> Roles { get; set; } = default!;

    // Soft delete flag — set to false instead of deleting the record.
    public bool IsActive { get; set; } = true;
}
```

`RegistrationStatus` is a domain enum: `Pending`, `Active`, `Approved`.

- Admin and FrontDesk accounts are created with `Status = Approved` and `EmailConfirmed = true` immediately.
- `RequiresPasswordChange = true` is set on creation and on admin-initiated password resets, cleared to `false` when the user changes their own password.
- `IsActive = false` combined with `LockoutEnd = DateTimeOffset.MaxValue` is used for soft deletion — the record stays in the DB but the user cannot log in.

---

### 1.2 `Role` — `Domain/Entity/Identity/Role.cs`

Extends `IdentityRole<string>`. Three roles exist in the system: **Admin**, **Frontdesk**, **Customer**.

---

### 1.3 `UserRole` — `Domain/Entity/Identity/UserRole.cs`

Extends `IdentityUserRole<string>`. Bridges the many-to-many relationship between `User` and `Role`. Has navigation properties back to both sides.

---

### 1.4 `RefreshToken` — `Domain/Entity/Identity/RefreshToken.cs`

Persisted separately in the database. Each login and each token refresh creates a new record.

```csharp
public class RefreshToken
{
    public Guid Id { get; set; }

    public string Token   { get; set; } = string.Empty; // 64-byte cryptographically random, Base64-encoded
    public string JwtId   { get; set; } = string.Empty; // The `jti` claim of the paired access token

    public DateTime CreatedAt  { get; set; }
    public DateTime UpdatedAt  { get; set; }
    public DateTime ExpiryDate { get; set; }

    public bool Used        { get; set; } // true after the token has been exchanged once (rotation)
    public bool Invalidated { get; set; } // true after manual revocation (logout) or breach detection

    public string UserId { get; set; } = string.Empty;
    public User User { get; set; } = default!;
}
```

Key design points:
- `Token` is the value stored in the client's HttpOnly cookie.
- `JwtId` links it to the access token that was issued with it. This ensures a refresh token can only be used to extend the specific access token family it was created for.
- `Used` enables single-use rotation. Once a token is used to get a new pair, it is marked `Used = true` and is never accepted again.
- `Invalidated` enables manual revocation. Logout sets this to `true`. If a `Used = true` token is presented a second time, breach detection sets `Invalidated = true` on every token belonging to the user, forcing a full re-login.

---

## 2. Persistence Layer

### 2.1 `ApplicationDbContext` — `Persistence/Data/ApplicationDbContext.cs`

Extends `IdentityDbContext<User, Role, string, IdentityUserClaim<string>, UserRole, IdentityUserLogin<string>, IdentityRoleClaim<string>, IdentityUserToken<string>>`.

All entity type configurations are applied via `ApplyConfigurationsFromAssembly`. The Identity tables are renamed from the default `AspNet*` prefix to custom names defined in the configurations.

### 2.2 EF Core Configurations

**`UserConfiguration`**
- `FirstName`, `LastName`: required, max 100 chars
- `MiddleName`: optional, max 100 chars
- `Address`: max 250 chars
- `Status`: stored as a string (snake_case enum)
- `IsActive`: defaults to `true`
- One-to-many with `RefreshTokens` (cascade delete on user deletion)

**`RoleConfiguration`**
- Primary key on `Id`
- Many-to-many with `Users` via `UserRole`

**`UserRoleConfiguration`**
- Composite primary key: `(UserId, RoleId)`
- Foreign keys to `User` and `Role`, both required

**`RefreshTokenConfiguration`**
- Primary key on `Id`
- Foreign key to `User` with cascade delete — deleting a user removes all their refresh tokens

---

## 3. Infrastructure — Token Service

### 3.1 `ITokenService` Interface

Defines three methods:
- `string GenerateAccessToken(User user, string role)`
- `ClaimsPrincipal GetPrincipalFromExpiredToken(string token)`
- `Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId)`

### 3.2 `TokenService` — `Infrastructure/Services/TokenService.cs`

#### `GenerateAccessToken`

```csharp
public string GenerateAccessToken(User user, string role)
{
    var secret = _configuration["Jwt:Secret"];
    var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var jti    = Guid.NewGuid().ToString();

    var claims = new List<Claim>
    {
        new Claim("sub",           user.Id),
        new Claim("email",         user.Email ?? string.Empty),
        new Claim("name",          $"{user.FirstName} {user.LastName}"),
        new Claim(ClaimTypes.Role, role),
        new Claim("jti",           jti)
    };

    var token = new JwtSecurityToken(
        issuer:            _configuration["Jwt:Issuer"],
        audience:          _configuration["Jwt:Audience"],
        claims:            claims,
        expires:           DateTime.UtcNow.AddMinutes(double.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "15")),
        signingCredentials: creds
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}
```

Claims included in every access token:

| Claim | Value |
|-------|-------|
| `sub` | User's identity GUID (ASP.NET Identity `Id`) |
| `email` | User's email address |
| `name` | `FirstName LastName` |
| `ClaimTypes.Role` | The user's single role (`Admin`, `Frontdesk`, `Customer`) |
| `jti` | A new `Guid` per token, used to link it to its refresh token |

Algorithm: **HMAC-SHA256**. Signing key sourced from `JWT_SECRET` environment variable.

#### `GetPrincipalFromExpiredToken`

Used to extract claims from an expired access token without rejecting it due to lifetime. `ValidateLifetime = false` is set explicitly. `MapInboundClaims = false` preserves the raw claim names (`sub`, `jti`) without WS-Federation remapping. Throws `SecurityTokenException` if the token signature is invalid.

#### `GenerateRefreshTokenAsync`

```csharp
public async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string jwtId)
{
    var randomNumber = new byte[64];
    using (var rng = RandomNumberGenerator.Create())
        rng.GetBytes(randomNumber);

    return new RefreshToken
    {
        Id         = Guid.NewGuid(),
        Token      = Convert.ToBase64String(randomNumber), // 64 cryptographically random bytes → Base64
        JwtId      = jwtId,
        UserId     = userId,
        CreatedAt  = DateTime.UtcNow,
        UpdatedAt  = DateTime.UtcNow,
        ExpiryDate = DateTime.UtcNow.AddDays(double.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7")),
        Used        = false,
        Invalidated = false
    };
}
```

The `Token` value (64 Base64 characters) is what gets stored in the database and sent to the client as an HttpOnly cookie.

---

## 4. Application Layer — Commands & Queries

### 4.1 Login — `Application/Services/User/Command/LoginCommand.cs`

**Request:** `LoginRequest { Email, Password }`
**Response:** `LoginResponse { AccessToken, RefreshToken }`

```csharp
public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
{
    // 1. Find user by email (case-insensitive via Identity)
    var user = await _userManager.FindByEmailAsync(loginRequest.Email)
        ?? throw new UnauthorizedException("Invalid email or password");

    // 2. Verify password hash
    var passwordValid = await _userManager.CheckPasswordAsync(user, loginRequest.Password);
    if (!passwordValid)
        throw new UnauthorizedException("Invalid email or password");

    // 3. Fetch the user's roles (takes first role; defaults to "Customer")
    var roles    = await _userManager.GetRolesAsync(user);
    string role  = roles.FirstOrDefault() ?? "Customer";

    // 4. Generate signed JWT access token with claims
    var accessToken = _tokenService.GenerateAccessToken(user, role);

    // 5. Extract the `jti` claim from the access token to link the refresh token
    var handler  = new JwtSecurityTokenHandler();
    var jsonToken = handler.ReadToken(accessToken) as JwtSecurityToken;
    var jwtId    = jsonToken?.Claims.FirstOrDefault(c => c.Type == "jti")?.Value
                   ?? Guid.NewGuid().ToString();

    // 6. Generate a new refresh token, save it to the DB
    var refreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, jwtId);
    _dbContext.Set<RefreshToken>().Add(refreshToken);
    await _dbContext.SaveChangesAsync(cancellationToken);

    return new LoginResponse
    {
        AccessToken  = accessToken,
        RefreshToken = refreshToken.Token
    };
}
```

> The controller then writes `result.RefreshToken` into an **HttpOnly, Secure, SameSite=None** cookie that expires in 7 days. The `AccessToken` is returned in the response body for the client to store in memory.

---

### 4.2 Token Refresh — `Application/Services/User/Command/RefreshTokenCommand.cs`

**Request:** `RefreshTokenRequest { RefreshToken }` (the raw token string from the cookie)
**Response:** `RefreshTokenResponse { AccessToken, RefreshToken }`

```csharp
public async Task<RefreshTokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
{
    // 1. Look up the token in the database
    var storedToken = await _dbContext.Set<RefreshToken>()
        .FirstOrDefaultAsync(rt => rt.Token == request.Request.RefreshToken, cancellationToken)
        ?? throw new UnauthorizedException("Invalid refresh token");

    // 2. BREACH DETECTION: if this token was already used once, someone is replaying it.
    //    Invalidate ALL tokens for this user immediately and force a re-login.
    if (storedToken.Used)
    {
        var allUserTokens = await _dbContext.Set<RefreshToken>()
            .Where(rt => rt.UserId == storedToken.UserId)
            .ToListAsync(cancellationToken);

        foreach (var token in allUserTokens)
        {
            token.Invalidated = true;
            token.UpdatedAt   = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        throw new UnauthorizedException("Potential token reuse detected. All sessions have been invalidated. Please log in again.");
    }

    // 3. Check revocation and expiry
    if (storedToken.Invalidated)
        throw new UnauthorizedException("Refresh token has been revoked");

    if (storedToken.ExpiryDate < DateTime.UtcNow)
        throw new UnauthorizedException("Refresh token has expired");

    // 4. Mark old token as used (single-use rotation)
    storedToken.Used      = true;
    storedToken.UpdatedAt = DateTime.UtcNow;

    // 5. Re-fetch user and role for the new access token
    var user  = await _userManager.FindByIdAsync(storedToken.UserId)
        ?? throw new NotFoundException("User not found");
    var roles = await _userManager.GetRolesAsync(user);
    string role = roles.FirstOrDefault() ?? "Customer";

    // 6. Generate fresh access token
    var newAccessToken = _tokenService.GenerateAccessToken(user, role);

    // 7. Extract new jti and create a new refresh token
    var handler   = new JwtSecurityTokenHandler();
    var jsonToken = handler.ReadToken(newAccessToken) as JwtSecurityToken;
    var newJti    = jsonToken?.Claims.FirstOrDefault(c => c.Type == "jti")?.Value
                    ?? Guid.NewGuid().ToString();

    var newRefreshToken = await _tokenService.GenerateRefreshTokenAsync(user.Id, newJti);
    _dbContext.Set<RefreshToken>().Add(newRefreshToken);

    await _dbContext.SaveChangesAsync(cancellationToken);

    return new RefreshTokenResponse
    {
        AccessToken  = newAccessToken,
        RefreshToken = newRefreshToken.Token
    };
}
```

Every successful refresh:
- Marks the old refresh token as `Used = true`
- Creates a brand-new refresh token with a new `jti` link
- Issues a new access token
- The controller writes the new refresh token into the cookie (overwriting the old one)

---

### 4.3 Logout — `Application/Services/User/Command/LogoutCommand.cs`

**Request:** raw refresh token string (read from cookie in controller)
**Response:** `Unit` (void)

```csharp
public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
{
    var refreshToken = await _dbContext.Set<RefreshToken>()
        .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

    if (refreshToken != null)
    {
        refreshToken.Invalidated = true;
        refreshToken.UpdatedAt   = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    return Unit.Value;
}
```

If no token is found (already expired or unknown), the logout silently succeeds — the controller still deletes the cookie regardless.

---

### 4.4 Change Password — `Application/Services/User/Command/ChangePasswordCommand.cs`

Called by a **logged-in user** to change their own password. Requires the current password.

```csharp
public async Task<Unit> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
{
    var user = await _userManager.FindByIdAsync(command.UserId)
        ?? throw new NotFoundException("User not found.");

    // Identity's ChangePasswordAsync verifies CurrentPassword before setting the new one
    var result = await _userManager.ChangePasswordAsync(user, command.Request.CurrentPassword, command.Request.NewPassword);

    if (!result.Succeeded)
        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(e.Code, e.Description)).ToList());

    // Clear the forced-change flag once the user has set their own password
    user.RequiresPasswordChange = false;
    await _userManager.UpdateAsync(user);

    return Unit.Value;
}
```

The `UserId` is extracted from the `sub` claim of the access token in the controller — the user cannot supply a different ID.

---

### 4.5 Reset Password (Admin) — `Application/Services/User/Command/ResetPasswordCommand.cs`

Called by an **admin** to reset any user's password. Does not require knowing the current password.

```csharp
public async Task<Unit> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
{
    var user = await _userManager.FindByIdAsync(command.TargetUserId)
        ?? throw new NotFoundException($"User with ID '{command.TargetUserId}' not found.");

    // Remove existing hash, then add the new one — no current password required
    await _userManager.RemovePasswordAsync(user);
    await _userManager.AddPasswordAsync(user, command.Request.NewPassword);

    // Force the user to change it on next login
    user.RequiresPasswordChange = true;
    await _userManager.UpdateAsync(user);

    return Unit.Value;
}
```

---

### 4.6 Create Admin — `Application/Services/User/Command/CreateAdminCommand.cs`

```csharp
public async Task<CreateAdminResponse> Handle(CreateAdminCommand command, CancellationToken cancellationToken)
{
    // 1. Duplicate email check
    var existing = await _userManager.FindByEmailAsync(request.Email);
    if (existing != null)
        throw new InvalidOperationException($"User with email {request.Email} already exists.");

    // 2. Build the user entity
    var user = new User
    {
        UserName               = request.Email,
        Email                  = request.Email,
        FirstName              = request.FirstName,
        LastName               = request.LastName,
        PhoneNumber            = request.PhoneNumber,
        Status                 = RegistrationStatus.Approved, // immediately active
        EmailConfirmed         = true,
        RequiresPasswordChange = true  // must change on first login
    };

    // 3. Persist with hashed password
    var result = await _userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
        throw new ValidationException(result.Errors.Select(e => new ValidationFailure(e.Code, e.Description)).ToList());

    // 4. Assign role — rollback the user if role assignment fails
    var roleResult = await _userManager.AddToRoleAsync(user, "Admin");
    if (!roleResult.Succeeded)
    {
        await _userManager.DeleteAsync(user);
        throw new InvalidOperationException("Failed to assign Admin role.");
    }

    return new CreateAdminResponse { UserId = user.Id, Email = user.Email, Message = "Admin account created successfully." };
}
```

---

### 4.7 Create FrontDesk — `Application/Services/User/Command/CreateFrontDeskCommand.cs`

Identical logic to `CreateAdminCommand`, but assigns the `"Frontdesk"` role instead of `"Admin"`.

```csharp
var roleResult = await _userManager.AddToRoleAsync(user, "Frontdesk");
```

---

### 4.8 Update User — `Application/Services/User/Command/UpdateUserCommand.cs`

Used for both Admin and FrontDesk profile updates.

```csharp
public async Task<UpdateUserResponse> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
{
    var user = await _userManager.FindByIdAsync(command.Id)
        ?? throw new KeyNotFoundException($"User with ID {command.Id} not found.");

    user.FirstName   = request.FirstName;
    user.LastName    = request.LastName;
    user.Address     = request.Address;
    user.PhoneNumber = request.PhoneNumber;

    // Email changes require updating normalized username/email fields too
    if (request.Email != user.Email)
    {
        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing != null && existing.Id != user.Id)
            throw new InvalidOperationException("Email is already in use.");

        user.Email    = request.Email;
        user.UserName = request.Email;
        await _userManager.UpdateNormalizedEmailAsync(user);
        await _userManager.UpdateNormalizedUserNameAsync(user);
    }

    await _userManager.UpdateAsync(user);
    return new UpdateUserResponse { Id = user.Id, Message = "User updated successfully." };
}
```

---

### 4.9 Delete (Deactivate) User — `Application/Services/User/Command/DeleteUserCommand.cs`

This is a **soft delete** — the record is never removed from the database.

```csharp
public async Task<DeleteUserResponse> Handle(DeleteUserCommand command, CancellationToken cancellationToken)
{
    var user = await _userManager.FindByIdAsync(command.Id)
        ?? throw new KeyNotFoundException($"User with ID {command.Id} not found.");

    user.IsActive = false;

    // LockoutEnd = MaxValue means they can never log in again, even with a valid password
    await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    await _userManager.UpdateAsync(user);

    return new DeleteUserResponse { Id = user.Id, Message = "User deactivated successfully." };
}
```

---

### 4.10 Queries

**`GetUserByIdQuery`** — fetches a user by ID and their roles via `UserManager.GetRolesAsync`.

**`GetActiveUsersByRoleQuery`** — fetches all `IsActive = true` users in the specified role.

**`GetArchivedUsersByRoleQuery`** — fetches all `IsActive = false` users in the specified role.

All three return `UserDto`:
```csharp
public class UserDto
{
    public string       Id          { get; set; }
    public string       Email       { get; set; }
    public string       FirstName   { get; set; }
    public string       LastName    { get; set; }
    public string       PhoneNumber { get; set; }
    public string       Address     { get; set; }
    public bool         IsActive    { get; set; }
    public IList<string> Roles      { get; set; }
}
```

---

### 4.11 Validators (FluentValidation via MediatR Pipeline)

All commands pass through `ValidationBehavior<TRequest, TResponse>` in the MediatR pipeline before reaching the handler.

| Command | Key Rules |
|---------|-----------|
| `LoginCommand` | Email: required + valid format. Password: required. |
| `RefreshTokenCommand` | RefreshToken: required. |
| `CreateAdminCommand` | FirstName/LastName: 2–50 chars. Email: valid. Password: min 10 chars, requires uppercase + lowercase + digit. PhoneNumber: min 10 chars. ConfirmPassword must match. |
| `CreateFrontDeskCommand` | Same as Admin. PhoneNumber: exactly 10 digits only. |
| `UpdateUserCommand` | FirstName/LastName: required, max 50 chars. Email: valid. PhoneNumber: exactly 10 digits, no leading zero. |
| `ChangePasswordCommand` | CurrentPassword: required. NewPassword: min 10 chars + uppercase + lowercase + digit + must differ from current. ConfirmNewPassword must match. |
| `ResetPasswordCommand` | TargetUserId: required. NewPassword: same rules as above. ConfirmNewPassword must match. |

---

## 5. Web API Layer

### 5.1 JWT Authentication Setup — `WebApi/DependencyInjection.cs`

```csharp
services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = configuration["Jwt:Issuer"],
        ValidAudience            = configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(
                                       Encoding.UTF8.GetBytes(configuration["Jwt:Secret"])),

        // Maps `ClaimTypes.Role` correctly to the `role` claim from the token handler
        RoleClaimType  = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        NameClaimType  = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
    };
});

services.AddAuthorization();
```

Every incoming request with a `Bearer` token in the `Authorization` header is validated against these parameters. Expired tokens, invalid signatures, or wrong issuer/audience all result in `401 Unauthorized` before reaching any controller.

**Configuration values** come from environment variables loaded in `Program.cs`:

```csharp
builder.Configuration["Jwt:Secret"]                = Environment.GetEnvironmentVariable("JWT_SECRET");
builder.Configuration["Jwt:ExpiryMinutes"]         = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES");
builder.Configuration["Jwt:RefreshTokenExpiryDays"] = Environment.GetEnvironmentVariable("JWT_REFRESH_TOKEN_EXPIRY_DAYS");
```

Defaults (if env vars are absent): `ExpiryMinutes = 15`, `RefreshTokenExpiryDays = 7`.

---

### 5.2 Auth Controller — `WebApi/Controllers/AuthController.cs`

Base route: `api/auth`

#### `POST /api/auth/register`
Creates a regular (Customer) user account. No authorization required.

#### `POST /api/auth/login`
```csharp
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] LoginRequest request)
{
    var result = await _mediator.Send(new LoginCommand(request));

    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,               // inaccessible to JavaScript — XSS protection
        Secure   = true,               // HTTPS only
        SameSite = SameSiteMode.None,  // allows cross-origin requests (frontend on different domain)
        Expires  = DateTime.UtcNow.AddDays(7)
    };

    Response.Cookies.Append("refreshToken", result.RefreshToken, cookieOptions);
    return Ok(result); // response body contains AccessToken + RefreshToken
}
```

The `RefreshToken` is written into a cookie AND returned in the body. The frontend should store `AccessToken` in memory only (not localStorage). The cookie is used automatically by the browser on subsequent requests.

#### `POST /api/auth/refresh`
```csharp
[HttpPost("refresh")]
public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
{
    var result = await _mediator.Send(new RefreshTokenCommand(request));

    // Overwrite the old refresh token cookie with the new one
    Response.Cookies.Append("refreshToken", result.RefreshToken, cookieOptions);
    return Ok(result);
}
```

The frontend sends the refresh token in the request body (read from its own cookie via the Next.js route handler). The backend issues a new access token + new refresh token, overwrites the cookie, and returns both in the body.

#### `POST /api/auth/change-password` `[Authorize]`
```csharp
[HttpPost("change-password")]
[Authorize]
public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
{
    // Extract the user's ID from the `sub` claim of the validated JWT
    var userId = User.FindFirst("sub")?.Value
        ?? throw new UnauthorizedException("Invalid token.");

    await _mediator.Send(new ChangePasswordCommand(userId, request));
    return Ok(new { message = "Password changed successfully." });
}
```

#### `POST /api/auth/logout`
```csharp
[HttpPost("logout")]
public async Task<IActionResult> Logout()
{
    // Read the refresh token from the HttpOnly cookie
    var refreshToken = Request.Cookies["refreshToken"];

    if (!string.IsNullOrEmpty(refreshToken))
        await _mediator.Send(new LogoutCommand(refreshToken));

    // Always delete the cookie regardless of whether the token was found
    Response.Cookies.Delete("refreshToken", new CookieOptions
    {
        HttpOnly = true,
        Secure   = true,
        SameSite = SameSiteMode.None
    });

    return Ok(new { message = "Logged out successfully" });
}
```

---

### 5.3 Admin User Management — `WebApi/Controllers/AdminController.cs`

Base route: `api/admin`. All endpoints require `[Authorize(Roles = "Admin")]`.

| Method | Route | Command | Description |
|--------|-------|---------|-------------|
| `POST` | `/create-admin` | `CreateAdminCommand` | Create a new Admin account |
| `PUT` | `/edit-admin/{id}` | `UpdateUserCommand` | Update an admin's profile |
| `DELETE` | `/delete-admin/{id}` | `DeleteUserCommand` | Soft-delete (deactivate) an admin |
| `GET` | `/get-admin/{id}` | `GetUserByIdQuery` | Get a single admin by ID |
| `GET` | `/active-admins` | `GetActiveUsersByRoleQuery("Admin")` | List all active admins |
| `GET` | `/archived-admins` | `GetArchivedUsersByRoleQuery("Admin")` | List all deactivated admins |
| `POST` | `/reset-password/{userId}` | `ResetPasswordCommand` | Admin resets any user's password |

---

### 5.4 FrontDesk User Management — `WebApi/Controllers/FrontDeskController.cs`

Base route: `api/frontdesk`. Authorization varies per endpoint.

```csharp
[Route("api/frontdesk")]
[ApiController]
public class FrontDeskController : ControllerBase
{
    // POST api/frontdesk/create-frontdesk
    [HttpPost("create-frontdesk")]
    [Authorize(Roles = "Admin")]              // Only admins can create FrontDesk accounts
    public async Task<IActionResult> CreateFrontDesk([FromBody] CreateFrontDeskRequest request)
        => Ok(await _mediator.Send(new CreateFrontDeskCommand(request)));

    // PUT api/frontdesk/edit-frontdesk/{id}
    [HttpPut("edit-frontdesk/{id}")]
    [Authorize(Roles = "Admin,Frontdesk")]    // Admin or the FrontDesk user themselves
    public async Task<IActionResult> EditFrontDesk(string id, [FromBody] UpdateUserRequest request)
        => Ok(await _mediator.Send(new UpdateUserCommand(id, request)));

    // DELETE api/frontdesk/delete-frontdesk/{id}
    [HttpDelete("delete-frontdesk/{id}")]
    [Authorize(Roles = "Admin")]              // Only admins can deactivate
    public async Task<IActionResult> DeleteFrontDesk(string id)
        => Ok(await _mediator.Send(new DeleteUserCommand(id)));

    // GET api/frontdesk/get-frontdesk/{id}
    [HttpGet("get-frontdesk/{id}")]
    [Authorize(Roles = "Admin,Frontdesk")]
    public async Task<IActionResult> GetFrontDeskById(string id)
        => Ok(await _mediator.Send(new GetUserByIdQuery(id)));

    // GET api/frontdesk/active-frontdesks
    [HttpGet("active-frontdesks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllActiveFrontDesks()
        => Ok(await _mediator.Send(new GetActiveUsersByRoleQuery("Frontdesk")));

    // GET api/frontdesk/archived-frontdesks
    [HttpGet("archived-frontdesks")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllArchivedFrontDesks()
        => Ok(await _mediator.Send(new GetArchivedUsersByRoleQuery("Frontdesk")));
}
```

No `reset-password` endpoint exists on `FrontDeskController` — admin password resets for FrontDesk accounts go through `POST /api/admin/reset-password/{userId}`.

---

## 6. Request / Response Contracts

### Login
```
POST /api/auth/login
Body: { "email": "...", "password": "..." }
Response: { "accessToken": "...", "refreshToken": "..." }
Cookie set: refreshToken (HttpOnly, Secure, SameSite=None, 7 days)
```

### Refresh
```
POST /api/auth/refresh
Body: { "refreshToken": "..." }
Response: { "accessToken": "...", "refreshToken": "..." }
Cookie updated: refreshToken (same options, new 7-day window)
```

### Logout
```
POST /api/auth/logout
Body: (none — token is read from cookie)
Response: { "message": "Logged out successfully" }
Cookie deleted: refreshToken
```

### Create Admin
```
POST /api/admin/create-admin
Authorization: Admin
Body: { "firstName", "lastName", "email", "password", "confirmPassword", "phoneNumber" }
Response: { "userId", "email", "message" }
```

### Create FrontDesk
```
POST /api/frontdesk/create-frontdesk
Authorization: Admin
Body: { "firstName", "lastName", "email", "password", "confirmPassword", "phoneNumber" }
Response: { "userId", "email", "message" }
```

### Update User (Admin or FrontDesk)
```
PUT /api/admin/edit-admin/{id}         — Admin only
PUT /api/frontdesk/edit-frontdesk/{id} — Admin or Frontdesk
Body: { "firstName", "lastName", "email", "phoneNumber", "address" }
Response: { "id", "message" }
```

### Deactivate User
```
DELETE /api/admin/delete-admin/{id}
DELETE /api/frontdesk/delete-frontdesk/{id}
Authorization: Admin
Response: { "id", "message" }
```

### Reset Password (Admin-initiated)
```
POST /api/admin/reset-password/{userId}
Authorization: Admin
Body: { "newPassword", "confirmNewPassword" }
Response: { "message": "Password reset successfully. The user will be required to change their password on next login." }
```

### Change Password (self)
```
POST /api/auth/change-password
Authorization: Bearer token (any role)
Body: { "currentPassword", "newPassword", "confirmNewPassword" }
Response: { "message": "Password changed successfully." }
```

---

## 7. Authentication & Authorization Flow Summary

```
1. POST /api/auth/login
   ├── Validate credentials (email + password via UserManager)
   ├── Generate AccessToken (JWT, 15 min, HMAC-SHA256)
   │     Claims: sub, email, name, role, jti
   ├── Generate RefreshToken (64-byte random, DB-persisted, 7 days)
   │     Links to AccessToken via JwtId = jti
   ├── Write RefreshToken to HttpOnly cookie
   └── Return { accessToken, refreshToken } in body

2. Client stores AccessToken in memory
   Client browser holds RefreshToken in HttpOnly cookie

3. API request with expired AccessToken
   └── Server returns 401 Unauthorized

4. POST /api/auth/refresh (triggered by client interceptor)
   ├── Client sends RefreshToken in body (from cookie, via Next.js route handler)
   ├── DB lookup: find stored RefreshToken
   ├── If Used = true → BREACH: invalidate ALL user tokens → 401
   ├── If Invalidated = true → 401 "Revoked"
   ├── If ExpiryDate < now → 401 "Expired"
   ├── Mark old token Used = true
   ├── Generate new AccessToken + new RefreshToken
   ├── Save new RefreshToken to DB
   ├── Update cookie with new RefreshToken
   └── Return new { accessToken, refreshToken }

5. POST /api/auth/logout
   ├── Read RefreshToken from cookie
   ├── Set Invalidated = true in DB
   └── Delete cookie

6. Protected endpoint request
   ├── [Authorize] → JwtBearer validates token (issuer, audience, lifetime, signature)
   ├── [Authorize(Roles = "Admin")] → checks ClaimTypes.Role claim in token
   └── Controller proceeds if all checks pass
```

---

## 8. Security Design Decisions

| Decision | Rationale |
|----------|-----------|
| RefreshToken stored in HttpOnly cookie | Cannot be read by JavaScript — prevents XSS from stealing the token |
| SameSite=None | Required for cross-origin frontend/backend setup (different ports in dev) |
| Single-use rotation | Each refresh issues a new token and invalidates the old one |
| Breach detection (Used=true reuse → invalidate all) | If an attacker steals and uses a refresh token, replaying the old one triggers full session wipe |
| Soft delete (IsActive=false + Lockout=MaxValue) | Audit trail preserved; user cannot log in again |
| RequiresPasswordChange flag | Admin-created passwords are temporary; users must set their own on first login |
| Role embedded in JWT | No DB lookup needed on every request for authorization — role is validated once at login and embedded in the signed token |
| jti linking RefreshToken to AccessToken | Ensures a refresh token is tied to a specific access token family |
