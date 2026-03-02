using System.Security.Claims;
using BlazorShared.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.Infrastructure.Identity;
using Microsoft.eShopWeb.Web.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace Microsoft.eShopWeb.Web.Controllers;

[Route("[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly ITokenClaimsService _tokenClaimsService;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<UserController> _logger;
    private readonly IMemoryCache _cache;

    public UserController(ITokenClaimsService tokenClaimsService,
                          SignInManager<ApplicationUser> signInManager,
                          ILogger<UserController> logger,
                          IMemoryCache cache)
    {
        _tokenClaimsService = tokenClaimsService;
        _signInManager = signInManager;
        _logger = logger;
        _cache = cache;
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userInfo = await CreateUserInfo(User);
        return Ok(userInfo);
    }

    [Route("Logout")]
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var userName = User?.FindFirstValue(ClaimTypes.Name) ?? User?.Identity?.Name;
        if (string.IsNullOrEmpty(userName))
        {
            _logger.LogWarning("Logout invoked but user name claim is missing.");
            return Ok();
        }

        var identityKey = Request.Cookies[ConfigureCookieSettings.IdentifierCookieName];
        if (string.IsNullOrEmpty(identityKey))
        {
            _logger.LogWarning("No identity cookie present on logout for user {UserName}", userName);
            return Ok();
        }

        var cacheKey = $"{userName}:{identityKey}";
        _cache.Set(cacheKey, identityKey, new MemoryCacheEntryOptions
        {
            AbsoluteExpiration = DateTime.UtcNow.AddMinutes(ConfigureCookieSettings.ValidityMinutesPeriod)
        });

        _logger.LogInformation("User {UserName} logged out and identity key cached", userName);
        return Ok();
    }

    private async Task<UserInfo> CreateUserInfo(ClaimsPrincipal claimsPrincipal)
    {
        if (claimsPrincipal?.Identity == null || string.IsNullOrEmpty(claimsPrincipal.Identity.Name) || !claimsPrincipal.Identity.IsAuthenticated)
        {
            return UserInfo.Anonymous;
        }

        var userInfo = new UserInfo
        {
            IsAuthenticated = true
        };

        if (claimsPrincipal.Identity is ClaimsIdentity claimsIdentity)
        {
            userInfo.NameClaimType = claimsIdentity.NameClaimType;
            userInfo.RoleClaimType = claimsIdentity.RoleClaimType;
        }
        else
        {
            userInfo.NameClaimType = "name";
            userInfo.RoleClaimType = "role";
        }

        if (claimsPrincipal.Claims.Any())
        {
            var claims = new List<ClaimValue>();
            var nameClaims = claimsPrincipal.FindAll(userInfo.NameClaimType);
            foreach (var claim in nameClaims)
            {
                claims.Add(new ClaimValue(userInfo.NameClaimType, claim.Value));
            }

            foreach (var claim in claimsPrincipal.Claims.Except(nameClaims))
            {
                claims.Add(new ClaimValue(claim.Type, claim.Value));
            }

            userInfo.Claims = claims;
        }

        var token = await _tokenClaimsService.GetTokenAsync(claimsPrincipal.Identity.Name);
        userInfo.Token = token;

        return userInfo;
    }
}
