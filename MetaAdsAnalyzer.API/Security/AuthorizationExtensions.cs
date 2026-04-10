using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace MetaAdsAnalyzer.API.Security;

public static class AuthorizationExtensions
{
    public const string UserIdClaimType = "uid";

    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var v = user.FindFirstValue(UserIdClaimType) ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    /// <summary>Rota veya gövdedeki userId ile JWT id eşleşmezse Forbid.</summary>
    public static ActionResult? EnsureOwnUser(this ControllerBase controller, int routeUserId)
    {
        var id = controller.User.GetUserId();
        if (id is null)
        {
            return controller.Unauthorized();
        }

        if (id.Value != routeUserId)
        {
            return controller.Forbid();
        }

        return null;
    }
}
