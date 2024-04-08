using Firebase.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Services;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
   
    public class LoginController : Controller
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IFilesService _filesService;
        private readonly UsuarioContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;


        public LoginController(IUsuarioService usuarioService, IFilesService filesService, UsuarioContext context, IHttpContextAccessor httpContextAccessor)
        {
            _usuarioService = usuarioService;
            _filesService = filesService;
            _context = context;
            _httpContextAccessor = httpContextAccessor; // Inyecta IHttpContextAccessor

        }

        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Registro(Usuario usuario, IFormFile Imagen)
        {
            //Stream image = Imagen.OpenReadStream();
            //string urlImagen = await _filesService.SubirArchivo(image, Imagen.FileName);

            usuario.Clave = Utilidades.EncriptarClave(usuario.Clave);
            usuario.URLFotoPerfil = "NULL";
            usuario.ProveedorAutenticacion = "web";
            usuario.IdProveedor = "0";

            Usuario usuarioCreado = await _usuarioService.SaveUsuario(usuario);

            if(usuarioCreado.Id > 0)
            {
                return RedirectToAction("IniciarSesion", "Login");
            }

            ViewData["Mensaje"] = "No se pudo crear el usuario";
            return View();
        }

        //public IActionResult IniciarSesion()
        //{
        //    return View();

        //}
        public IActionResult IniciarSesion(string returnUrl = null)
        {
            // Verificar si returnUrl es nulo antes de almacenarlo en la sesión
            if (!string.IsNullOrEmpty(returnUrl))
            {
                // Almacena el returnUrl en la sesión
                _httpContextAccessor.HttpContext.Session.SetString("ReturnUrl", returnUrl);
            }

            return View();
        }



        [HttpPost]
        public async Task<IActionResult> IniciarSesion(string correo, string clave)
        {
            Usuario usuarioEncontrado = await _usuarioService.GetUsuario(correo, Utilidades.EncriptarClave(clave));

            if (usuarioEncontrado == null)
            {
                ViewData["Mensaje"] = "Usuario no encontrado";
                return View();
            }

            string idAsString = usuarioEncontrado.Id.ToString();

            var claims = new[]
            {
        new Claim(ClaimTypes.Name, usuarioEncontrado.NombreUsuario),
        new Claim("FotoPerfil", usuarioEncontrado.URLFotoPerfil),
        new Claim(ClaimTypes.NameIdentifier, idAsString)
    };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                AllowRefresh = true,
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties
            );

            // Obtener returnUrl de la sesión
            string returnUrl = _httpContextAccessor.HttpContext.Session.GetString("ReturnUrl");
            // Eliminar returnUrl de la sesión después de obtenerlo
            _httpContextAccessor.HttpContext.Session.Remove("ReturnUrl");

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl); // Redirigir al returnUrl si es válido
            }
            else
            {
                return RedirectToAction("Index", "Home"); // Si no hay returnUrl válido, redirigir a la página de inicio
            }
        }


        public async Task GoogleLogin()
        {
            await HttpContext.ChallengeAsync(GoogleDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = Url.Action("GoogleResponse")
                });
        }

        public async Task<IActionResult> GoogleResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            //var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            //{
            //    claim.Issuer,
            //    claim.OriginalIssuer,
            //    claim.Type,
            //    claim.Value
            //});

            var claims = result.Principal.Identities.FirstOrDefault().Claims;

            var userEmail = claims.FirstOrDefault(x => x.Type == ClaimTypes.Email)?.Value;
            var userId = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;

            // Guardar en la base de datos
            await GuardarUsuarioAsync(userEmail, userId, "Google");

            return RedirectToAction("Index", "Home", new { area = "" });
        }

        public async Task FacebookLogin()
        {
            await HttpContext.ChallengeAsync(FacebookDefaults.AuthenticationScheme,
                new AuthenticationProperties
                {
                    RedirectUri = Url.Action("FacebookResponse")
                });
        }

        public async Task<IActionResult> FacebookResponse()
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var claims = result.Principal.Identities.FirstOrDefault().Claims;

            //var claims = result.Principal.Identities.FirstOrDefault().Claims.Select(claim => new
            //{
            //    claim.Issuer,
            //    claim.OriginalIssuer,
            //    claim.Type,
            //    claim.Value
            //});
            var userName = claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value;
            var userId = claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;


            await GuardarUsuarioAsync(userName, userId, "Facebook");


            return RedirectToAction("Index", "Home", new { area = "" });
        }


        private async Task GuardarUsuarioAsync(string userName, string userId, string proveedor)
        {
            // Verificar si el usuario ya está registrado
            var usuarioExistente = await _context.Usuarios.FirstOrDefaultAsync(u => u.IdProveedor == userId && u.ProveedorAutenticacion == proveedor);

            if (usuarioExistente == null)
            {
                // Crear un nuevo usuario si no está registrado
                var usuario = new Usuario
                {
                    NombreUsuario = userName,
                    URLFotoPerfil = "NULL", // Asegúrate de cambiar esto si obtienes una URL de la imagen del usuario
                    Correo = "NULL",
                    Clave = "NULL",
                    ProveedorAutenticacion = proveedor,
                    IdProveedor = userId
                };

                // Guardar el usuario en la base de datos
                _context.Usuarios.Add(usuario);
                await _context.SaveChangesAsync();
            }
        }


        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            // Aquí puedes redirigir a la página de inicio de sesión de Identity
            return RedirectToAction("Login", "Identity/Account", new { returnUrl });
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return View("Index");
        }


    }
}
