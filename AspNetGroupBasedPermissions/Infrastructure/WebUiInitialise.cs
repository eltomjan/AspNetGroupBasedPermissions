#region licence
// The MIT License (MIT)
//
// Filename: WebUiInitialise.cs
// Date Created: 2014/05/20
//
// Copyright (c) 2014 Jon Smith (www.selectiveanalytics.com & www.thereformedprogrammer.net)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
#endregion
using System;
using System.Threading.Tasks;
using System.Web.Mvc;
using Autofac;
using Autofac.Integration.Mvc;
using AspNetGroupBasedPermissions.Controllers;
using GenericLibsBase;
using GenericServices;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Notifications;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using AspNetGroupBasedPermissions.Properties;
using ServiceLayer.Startup;
using System.Security.Claims;
using System.Linq;

[assembly: OwinStartup(typeof(AspNetGroupBasedPermissions.Infrastructure.WebUiInitialise))]
namespace AspNetGroupBasedPermissions.Infrastructure
{
    public enum HostTypes { NotSet, LocalHost, WebWiz, Azure };

    public static class WebUiInitialise
    {
        private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

        public const string DatabaseConnectionStringName = "DefaultConnection";

        /// <summary>
        /// This provides the host we
        /// </summary>
        public static HostTypes HostType { get; private set; }

        // The Client ID is used by the application to uniquely identify itself to Microsoft identity platform.
        readonly static string clientId = System.Configuration.ConfigurationManager.AppSettings["ClientId"];

        // RedirectUri is the URL where the user will be redirected to after they sign in.
        readonly static string redirectUri = System.Configuration.ConfigurationManager.AppSettings["RedirectUri"];

        // Tenant is the tenant ID (e.g. contoso.onmicrosoft.com, or 'common' for multi-tenant)
        static readonly string tenant = System.Configuration.ConfigurationManager.AppSettings["Tenant"];

        // Authority is the URL for authority, composed by Microsoft identity platform endpoint and the tenant name (e.g. https://login.microsoftonline.com/contoso.onmicrosoft.com/v2.0)
        readonly static string authority = String.Format(System.Globalization.CultureInfo.InvariantCulture, System.Configuration.ConfigurationManager.AppSettings["Authority"], tenant);

        private static HostTypes DecodeHostType(string hostTypeString)
        {
            Enum.TryParse(hostTypeString, true, out HostTypes hostType);
            return hostType;
        }

        private static void SetupLogging(HostTypes hostType)
        {

            switch (hostType)
            {
                case HostTypes.NotSet:
                    //we do not set up the logging
                    break;
                case HostTypes.LocalHost:
                case HostTypes.WebWiz:
                case HostTypes.Azure:
                    //we use the TraceGenericLogger when in Azure
                    GenericLibsBaseConfig.SetLoggerMethod = name => new TraceGenericLogger(name);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("hostType");
            }
        }


        /// <summary>
        /// Configure OWIN to use OpenIdConnect
        /// </summary>
        /// <param name="app"></param>
        public static void Configuration(IAppBuilder app)
        {
            HostType = DecodeHostType(Settings.Default.HostTypeString);
            //WebWiz does not allow drop/create database
            var canDropCreateDatabase = HostType != HostTypes.WebWiz;

            SetupLogging(HostType);

            //This runs the ServiceLayer initialise, whoes job it is to initialise any of the lower layers
            //NOTE: This MUST to come before the setup of the DI because it relies on the configInfo being set up
            ServiceLayerInitialise.InitialiseThis(HostType == HostTypes.Azure, canDropCreateDatabase);

            //This sets up the Autofac container for all levels in the program
            var container = AutofacDi.SetupDependency();

            //// Set the dependency resolver for MVC.
            var mvcResolver = new AutofacDependencyResolver(container);
            DependencyResolver.SetResolver(mvcResolver);

            var service = container.Resolve<IListService>();
            MailAuthorize.SetService(service);

            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());
            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    // Sets the ClientId, authority, RedirectUri as obtained from web.config
                    ClientId = clientId,
                    Authority = authority,
                    RedirectUri = redirectUri,
                    // PostLogoutRedirectUri is the page that users will be redirected to after sign-out. In this case, it is using the home page
                    PostLogoutRedirectUri = redirectUri,
                    Scope = OpenIdConnectScope.OpenIdProfile,
                    // ResponseType is set to request the id_token - which contains basic information about the signed-in user
                    ResponseType = OpenIdConnectResponseType.IdToken,
                    // ValidateIssuer set to false to allow personal and work accounts from any organization to sign in to your application
                    // To only allow users from a single organizations, set ValidateIssuer to true and 'tenant' setting in web.config to the tenant name
                    // To allow users from only a list of specific organizations, set ValidateIssuer to true and use ValidIssuers parameter
                    TokenValidationParameters = new TokenValidationParameters()
                    {
                        ValidateIssuer = false // This is a simplification
                        },
                        // OpenIdConnectAuthenticationNotifications configures OWIN to send notification of failed authentications to OnAuthenticationFailed method
                        Notifications = new OpenIdConnectAuthenticationNotifications
                    {
                        AuthenticationFailed = OnAuthenticationFailed,
                        //RedirectToIdentityProvider = OnRedirectToIdentityProvider,
                        SecurityTokenValidated = OnSecurityTokenValidated,
                    }
                }
            );
        }

        private static Task OnSecurityTokenValidated(SecurityTokenValidatedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> arg)
        {
            Claim c = arg.AuthenticationTicket.Identity.Claims.FirstOrDefault(d => d.Type == "preferred_username");
            Log.Info($"{c?.Value ?? "N/A"} logged in.");
            return Task.FromResult(0);
        }

        /// <summary>
        /// Handle failed authentication requests by redirecting the user to the home page with an error in the query string
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static Task OnAuthenticationFailed(AuthenticationFailedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            context.HandleResponse();
            context.Response.Redirect("/?errormessage=" + context.Exception.Message);
            return Task.FromResult(0);
        }
    }
}