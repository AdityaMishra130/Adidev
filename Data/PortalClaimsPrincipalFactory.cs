using System.Security.Claims;
using CsharpProject.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CsharpProject.Data;

public sealed class PortalClaimsPrincipalFactory(
    UserManager<AppUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser, IdentityRole>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        var nameClaim = identity.FindFirst(ClaimTypes.Name);
        if (nameClaim is not null)
        {
            identity.RemoveClaim(nameClaim);
        }

        identity.AddClaim(new Claim(ClaimTypes.Name, user.DisplayName));
        return identity;
    }
}
