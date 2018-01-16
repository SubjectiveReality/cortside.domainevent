using Microsoft.AspNetCore.Builder;

namespace Cortside.Common.Web.Security {

    public static class ApplicationBuilderExtensions {

        public static IApplicationBuilder UseMainhostAuthentication(this IApplicationBuilder application) {
            return application.UseMiddleware<MainhostAuthenticationMiddleware>();
        }
    }
}
