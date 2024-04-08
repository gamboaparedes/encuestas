using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ProyectoLogin.Filters
{
    public class AutorizacionRequeridaAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                // Almacena la URL a la que el usuario intentaba acceder antes de ser redirigido a la página de inicio de sesión
                string returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.HttpContext.Session.SetString("ReturnUrl", returnUrl);

                context.Result = new RedirectToRouteResult(new RouteValueDictionary(new
                {
                    controller = "Login",
                    action = "IniciarSesion"
                }));
            }
        }
    }
}
