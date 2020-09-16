using GenericServices;
using System;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;

namespace AspNetGroupBasedPermissions.Controllers
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public class MailAuthorize : AuthorizeAttribute
    {
        /// <summary>
        /// The name of the master page or view to use when rendering the view on authorization failure.  Default
        /// is null, indicating to use the master page of the specified view.
        /// </summary>
        public virtual string MasterName { get; set; }

        /// <summary>
        /// The name of the view to render on authorization failure.  Default is "Error".
        /// </summary>
        public virtual string ViewName { get; set; }

        public MailAuthorize()
            : base()
        {
            this.ViewName = "Privileges";
        }

        protected void CacheValidateHandler(HttpContext context, object data, ref HttpValidationStatus validationStatus)
        {
            validationStatus = OnCacheAuthorization(new HttpContextWrapper(context));
        }

        public override void OnAuthorization(System.Web.Mvc.AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                throw new ArgumentNullException("filterContext");
            }

            if (filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                ClaimsPrincipal user = (ClaimsPrincipal)filterContext.HttpContext.User;
                var email = user.FindFirst("preferred_username")?.Value;
                if (email == null) throw new ArgumentNullException("Missing e-mail !");
            } else {
                // auth failed, redirect to login page
                filterContext.Result = new HttpUnauthorizedResult();
                return;
            }

            ViewDataDictionary viewData = new ViewDataDictionary
            {
                { "Message", "You do not have sufficient privileges." }
            };
            filterContext.Result = new ViewResult { MasterName = this.MasterName, ViewName = this.ViewName, ViewData = viewData };
        }

        protected override void HandleUnauthorizedRequest(System.Web.Mvc.AuthorizationContext filterContext)
        {
            // Note: To reach here, a Web.config path-specific rule 'allow users="?"' is needed (otherwise it redirects to login)
            var httpContext = filterContext.HttpContext;
            var request = httpContext.Request;
            var response = httpContext.Response;

            if (request.IsAjaxRequest())
            {
                response.SuppressFormsAuthenticationRedirect = true;
                response.TrySkipIisCustomErrors = true;
            }
            filterContext.Result = new HttpUnauthorizedResult();
        }
        protected void SetCachePolicy(System.Web.Mvc.AuthorizationContext filterContext)
        {
            // ** IMPORTANT **
            // Since we're performing authorization at the action level, the authorization code runs
            // after the output caching module. In the worst case this could allow an authorized user
            // to cause the page to be cached, then an unauthorized user would later be served the
            // cached page. We work around this by telling proxies not to cache the sensitive page,
            // then we hook our custom authorization code into the caching mechanism so that we have
            // the final say on whether a page should be served from the cache.
            HttpCachePolicyBase cachePolicy = filterContext.HttpContext.Response.Cache;
            cachePolicy.SetProxyMaxAge(new TimeSpan(0));
            cachePolicy.AddValidationCallback(CacheValidateHandler, null /* data */);
        }
    }
}