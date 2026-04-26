# Design: SSO Challenge URL for `GetUserAuthentication` endpoint

**Bug**: [36054](https://dev.azure.com/woffu/b2d803b8-b452-441a-81a4-97d00bf662b2/_workitems/edit/36054) — Serhs | Autenticación - Redirección SSO. Endpoint no redirige al login de la company  
**Date**: 2026-04-15  
**Services affected**: `Woffu.Services.Legacy` (Woffu3), `Woffu.Services.Accounts`

---

## Problem

`GET /api/v1/users/key/{userKey}/authentication?companyId={id}` in Woffu3 always returns:

```
https://app.woffu.com/#/go/{encrypted-token}
```

This is a direct magic link that logs in the user bypassing their company's SSO. For companies with SSO configured (both old login via Woffu3 and new login via Accounts), the returned URL must initiate the SSO challenge instead.

---

## Solution Overview

The fix lives in two services:

1. **Accounts** — new endpoint `GET /authorization/external-logins-by-domain?domain={domain}` that returns the full SSO challenge URL for a company (identified by domain). Only covers companies with the `AccountsLogin` FF (config in Accounts DB). Protected endpoint.

2. **Woffu3** — `GetUserAuthentication` checks the `AccountsLogin` feature flag to decide which path to take, with three outcomes:
   - **New login (FF = true)** → call new Accounts endpoint → return SSO challenge URL
   - **Old login (FF = false, SSO in Woffu3 DB)** → build Woffu3 ExternalLogin URL locally
   - **No SSO** → return existing magic link (current behavior, unchanged)

---

## Post-SSO redirect flow (unchanged, already works)

After the SSO challenge completes, the user ends up authenticated at the company's Woffu URL:

| Login type | After IdP auth | User lands at |
|---|---|---|
| New login (Accounts) | Accounts callback sets JWT cookie → redirect to `returnUrl` | `https://{domain}/` |
| Old login (Woffu3) | OWIN implicit grant → redirect to `redirect_uri#access_token={token}` | `https://{domain}/` |

In both cases the `returnUrl`/`redirect_uri` is `https://{domain}/`.

---

## Accounts changes

### New case use: `GetExternalLoginsByDomain`

**File locations** (following existing CQRS pattern):
```
src/Woffu.Services.Accounts.Application/UseCases/Accounts/Queries/GetExternalLoginsByDomain/
  GetExternalLoginsByDomainQuery.cs
  GetExternalLoginsByDomainQueryHandler.cs
  GetExternalLoginsByDomainResponse.cs
```

**Query**:
```csharp
public record GetExternalLoginsByDomainQuery(string Domain)
    : IQuery<Result<GetExternalLoginsByDomainResponse>>;
```

**Response**:
```csharp
public record GetExternalLoginsByDomainResponse(string? ChallengeUrl);
// ChallengeUrl = null means no SSO configured for this company in Accounts
```

**Handler logic**:
```csharp
// 1. Get all registered external schemes
var allSchemes = await _authService.SignInManager.GetExternalAuthenticationSchemesAsync();

// 2. Filter by domain (reuses existing FilterAuthenticationSchemesAsync logic)
//    → looks up CompanyLoginConfiguration in Accounts DB by domain
//    → returns company's specific IdentityProvider scheme if found
var filteredSchemes = await FilterByDomainAsync(request.Domain, allSchemes);

// 3. Find the company-specific provider (not OpenIdConnect/Google generic ones)
var companyScheme = filteredSchemes
    .FirstOrDefault(s => s.Name is not "OpenIdConnect" and not "Google");

if (companyScheme is null)
{
    // Company not in Accounts DB → no new-login SSO → return null
    return Result.Success(new GetExternalLoginsByDomainResponse(null));
}

// 4. Build full challenge URL using company domain as returnUrl
var returnUrl = $"https://{request.Domain}/";
var challengeUrl = _endpointsService.GetUri("externalLogin", new
{
    provider = companyScheme.Name,
    redirect_uri = returnUrl,
    response_type = "token",
    trace_id = Guid.NewGuid().ToString()
});

// 5. Return full URL (host + path+query), not path-only like the existing endpoint
var fullUrl = _endpointsService.GetHostUri().ToString().TrimEnd('/') 
              + challengeUrl.PathAndQuery;

return Result.Success(new GetExternalLoginsByDomainResponse(fullUrl));
```

**Controller** (`AuthorizationController.cs`) — new action:
```csharp
[AllowAnonymous]
[HttpGet("external-logins-by-domain")]
public async Task<IResult> GetExternalLoginsByDomainAsync(
    [FromQuery] string domain,
    CancellationToken cancellationToken)
{
    var response = await Mediator.Send(new GetExternalLoginsByDomainQuery(domain), cancellationToken);
    return response.ToResults();
}
```

> **Auth**: `[AllowAnonymous]` — same as the existing `externallogins` endpoint. The data returned (a challenge URL) is not sensitive. Security is enforced at the Woffu3 layer (`CheckCompanyAuthorization` + `CheckCurrentUserAuthorization`).

---

## Woffu3 changes

### New typed HTTP client: `AccountsHttpClient`

**File**: `Woffu3.AppService/TypedHttpClients/AccountsHttpClient.cs`  
(Follows existing `SignsV2HttpClient` pattern)

```csharp
public interface IAccountsHttpClient
{
    Task<string?> GetSsoChallengeUrlByDomainAsync(string domain);
}

public class AccountsHttpClient(HttpClient httpClient) : IAccountsHttpClient
{
    public async Task<string?> GetSsoChallengeUrlByDomainAsync(string domain)
    {
        var response = await httpClient.GetAsync(
            $"/authorization/external-logins-by-domain?domain={Uri.EscapeDataString(domain)}");

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var result = await response.Content.ReadFromJsonAsync<GetExternalLoginsByDomainResult>();
        return result?.ChallengeUrl;
    }
}
```

**DI Registration** in `AutofacConfig.cs`:
```csharp
services.AddHttpClient<IAccountsHttpClient, AccountsHttpClient>(client =>
{
    var baseUrl = WebConfigurationManager.AppSettings["AccountsBaseUrl"];
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

New config key in `web.config` / app settings: `AccountsBaseUrl`.

### Modified: `ICompanyQueryAppService` + `CompanyQueryAppService`

Add new method to expose the existing repository call:

```csharp
// ICompanyQueryAppService.cs
OpenIdCompanyLoginConfiguration? GetOpenIdCompanyLoginConfigurationByCompanyId(int companyId);

// CompanyQueryAppService.cs
public OpenIdCompanyLoginConfiguration? GetOpenIdCompanyLoginConfigurationByCompanyId(int companyId)
    => _companyRepository.GetOpenIdCompanyLoginConfigurationByCompanyId(companyId);
```

### Modified: `GetUserAuthentication` in `UsersV1Controller`

`ICompanyFeatureFlagService` and `IAccountsHttpClient` are injected as new constructor parameters.

**Current code** (lines 232-249):
```csharp
public IHttpActionResult GetUserAuthentication(string userKey, int? companyId = null)
{
    CheckCompanyAuthorization(FeatureKeys.USE_API);
    CheckCurrentUserAuthorization(PermissionKeys.UserAdmin);
    int locationId = companyId ?? _context.CurrentUser.CompanyId;

    UserView user = _userService.GetUserView(userKey, locationId, true);
    if (user == null)
        throw new NotFoundException(userKey, locationId, ExceptionHelper.Resources.User);

    CheckCompanyAuthorization(user.CompanyId);

    UserLoginDTO ul = _userService.GetUserLoginToken(user.UserId, "/#/dashboard", 800, "");
    return Ok(ul.AbsoluteLoginUrl);
}
```

**New code**:
```csharp
public async Task<IHttpActionResult> GetUserAuthentication(string userKey, int? companyId = null)
{
    CheckCompanyAuthorization(FeatureKeys.USE_API);
    CheckCurrentUserAuthorization(PermissionKeys.UserAdmin);
    int locationId = companyId ?? _context.CurrentUser.CompanyId;

    UserView user = _userService.GetUserView(userKey, locationId, true);
    if (user == null)
        throw new NotFoundException(userKey, locationId, ExceptionHelper.Resources.User);

    CheckCompanyAuthorization(user.CompanyId);

    // 1. New login (AccountsLogin FF) → get SSO URL from Accounts
    if (await _companyFeatureFlagService.HasFeatureFlagAsync(user.CompanyId, FeatureFlagType.AccountsLogin))
    {
        var company = _companyService.GetCompany(user.CompanyId);
        var challengeUrl = await _accountsHttpClient.GetSsoChallengeUrlByDomainAsync(company.Domain);

        if (!string.IsNullOrEmpty(challengeUrl))
        {
            return Ok(challengeUrl);
        }
    }
    else
    {
        // 2. Old login → check SSO config in Woffu3 DB
        var ssoConfig = _companyRepository.GetOpenIdCompanyLoginConfigurationByCompanyId(user.CompanyId);

        if (ssoConfig is not null && !string.IsNullOrEmpty(ssoConfig.OpenIdAuthenticationType))
        {
            var challengeUrl = BuildWoffu3ChallengeUrl(ssoConfig, company.Domain);
            return Ok(challengeUrl);
        }
    }

    // 3. No SSO → return existing magic link
    UserLoginDTO ul = _userService.GetUserLoginToken(user.UserId, "/#/dashboard", 800, "");
    return Ok(ul.AbsoluteLoginUrl);
}

private string BuildWoffu3ChallengeUrl(OpenIdCompanyLoginConfiguration config, string domain)
{
    const string woffu3BaseUrl = "https://app.woffu.com";  // or from config
    var returnUrl = Uri.EscapeDataString($"https://{domain}/");
    return $"{woffu3BaseUrl}/api/Account/ExternalLogin" +
           $"?provider={config.OpenIdAuthenticationType}" +
           $"&response_type=token" +
           $"&client_id={WebConfigurationManager.AppSettings["Woffu3PublicClientId"]}" +
           $"&redirect_uri={returnUrl}" +
           $"&state={Guid.NewGuid():N}";
}
```

---

## Error handling & resilience

| Scenario | Behavior |
|---|---|
| Accounts down / timeout | `GetSsoChallengeUrlByDomainAsync` returns `null` → falls through to magic link |
| Company has FF but no IdentityProvider configured | Accounts returns `ChallengeUrl = null` → falls through to magic link |
| Company has no SSO anywhere | Returns existing magic link (current behavior) |

---

## New config keys

| Service | Key | Example value |
|---|---|---|
| Woffu3 | `AccountsBaseUrl` | `https://accounts.internal.woffu.com` |
| Woffu3 | `Woffu3PublicClientId` | `woffu` |

---

## Files changed

### Woffu.Services.Accounts
| File | Change |
|---|---|
| `UseCases/Accounts/Queries/GetExternalLoginsByDomain/GetExternalLoginsByDomainQuery.cs` | NEW |
| `UseCases/Accounts/Queries/GetExternalLoginsByDomain/GetExternalLoginsByDomainQueryHandler.cs` | NEW |
| `UseCases/Accounts/Queries/GetExternalLoginsByDomain/GetExternalLoginsByDomainResponse.cs` | NEW |
| `API/Controllers/Public/AuthorizationController.cs` | ADD endpoint |

### Woffu.Services.Legacy (Woffu3)
| File | Change |
|---|---|
| `Woffu3.AppService/TypedHttpClients/AccountsHttpClient.cs` | NEW |
| `Woffu3.AppService/Queries/ICompanyQueryAppService.cs` | ADD `GetOpenIdCompanyLoginConfigurationByCompanyId` |
| `Woffu3.AppService/Queries/CompanyQueryAppService.cs` | ADD `GetOpenIdCompanyLoginConfigurationByCompanyId` |
| `Woffu3.API/App_Start/AutofacConfig.cs` | ADD HTTP client + `ICompanyFeatureFlagService` injection |
| `Woffu3.API/Controllers/V1/UsersV1Controller.cs` | MODIFY constructor + `GetUserAuthentication` |
| `Woffu3.API/Web.config` (or app settings) | ADD `AccountsBaseUrl`, `Woffu3PublicClientId` |

---

## Out of scope

- Companies using ADFS (`WfaAuthenticationType`) — only OpenID SSO is addressed. ADFS is marked `[Obsolete]` in the external entity. Can be added later if needed.
- Frontend changes — the frontend already handles the returned URL as-is.
- The `GetLocalAccessToken2` magic link path — still works unchanged for non-SSO companies.
