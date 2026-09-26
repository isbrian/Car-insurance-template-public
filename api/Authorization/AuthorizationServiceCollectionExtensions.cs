using Microsoft.AspNetCore.Authorization;

namespace CarInsurance.Api.Authorization;

public static class AuthorizationServiceCollectionExtensions
{
    public static IServiceCollection AddCarInsuranceAuthorization(this IServiceCollection services)
    {
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            foreach (var (policyName, permission) in AppPolicies.PermissionPolicies)
            {
                options.AddPolicy(policyName, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.AddRequirements(new PermissionRequirement(permission));
                });
            }

            options.AddPolicy(AppPolicies.SystemAdministrator, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.RequireRole(AppRoles.SystemAdministrator);
            });
        });

        return services;
    }
}
