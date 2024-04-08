using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Filters;
using ProyectoLogin.Models;
using Microsoft.Extensions.Configuration;
using System.Configuration;
using System.Diagnostics;
using Microsoft.Data.SqlClient; // Asegúrate de importar el espacio de nombres donde resides AutorizacionRequeridaAttribute
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Linq;
using Humanizer.Configuration;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [AutorizacionRequerida] // Aplica el filtro a toda la clase
    public class RosaritoController : Controller
    {
        private const int EncuestaPresidencialId = 4;
        private readonly IConfiguration _configuration;
        private readonly UsuarioContext _context;

        public RosaritoController(UsuarioContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public IActionResult Index(string zona)
        {
            if (string.IsNullOrEmpty(zona))
            {
                TempData["ErrorMessage"] = "El código no existe.";
                return RedirectToAction("Error", "Rosarito");
            }

            ViewBag.parametro = zona;
            string connectionString = _configuration.GetConnectionString("AfiliacionesDBContext");

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                string query = "SELECT * FROM encuestas WHERE activo = 1 AND palabraClave = @zona";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@zona", zona);
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            string sessionId = Guid.NewGuid().ToString();
                            HttpContext.Session.SetString("sessionId", sessionId);
                            ViewBag.SessionId = HttpContext.Session.GetString("sessionId");
                            return View();
                        }
                        else
                        {
                            return RedirectToAction("Error", "Rosarito");
                        }
                    }
                }
            }
        }
        [HttpPost]
        public IActionResult Vota([FromBody] JsonElement datos)
        {
            // Obtener los datos del voto desde la solicitud JSON
            int idOpcion = datos.GetProperty("idOpcion").GetInt32();
            string parametro = datos.GetProperty("parametro").GetString();
            string navegador = datos.GetProperty("navegador").GetString();
            string a15aq25 = datos.GetProperty("a15aq25").GetString();

            // Verificar si el sessionId de la sesión actual coincide con el proporcionado en la solicitud
            if (HttpContext.Session.GetString("sessionId") != null && HttpContext.Session.GetString("sessionId") == a15aq25)
            {
                // Generar un identificador único para el dispositivo del votante
                string dispositivo = GenerarIdentificadorDispositivo(parametro);

                // Verificar si el usuario ya ha votado antes
                if (!HaVotado(dispositivo, parametro))
                {
                    // Crear el nombre de la cookie de seguimiento de voto para este dispositivo y parámetro de encuesta
                    //string cookieName = $"VotoRealizado_{dispositivo}_{parametro}";

                    //// Crear una cookie de seguimiento de voto para evitar votos repetidos
                    //HttpContext.Response.Cookies.Append(cookieName, "true", new Microsoft.AspNetCore.Http.CookieOptions
                    //{
                    //    Expires = DateTimeOffset.Now.AddYears(100) // La cookie expirará en 100 años
                    //});

                    // Almacenar el dispositivo en la sesión para evitar votos repetidos desde el mismo dispositivo
                    HttpContext.Session.SetString(dispositivo + "_" + parametro, "true");

                    // Obtener el ID del usuario autenticado
                    string idUsuario = "";
                    ClaimsPrincipal claimsUser = HttpContext.User;
                    if (claimsUser.Identity.IsAuthenticated)
                    {
                        idUsuario = claimsUser.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    }

                    // Verificar si el usuario ya ha votado
                    bool usuarioYaVoto = _context.votacion.Any(v => v.usuario == idUsuario && v.encuesta == EncuestaPresidencialId && v.palabraClave == parametro);

                    if (usuarioYaVoto)
                    {
                        // El usuario ya ha votado, retornar un error
                        return Json(new { exito = false, mensaje = "Ya has votado anteriormente." });
                    }

                    // Crear un nuevo objeto de votacion con los datos del voto
                    var nuevoVoto = new votacion
                    {
                        IdVoto = idOpcion,
                        informacion = dispositivo,
                        navegacion = navegador,
                        encuesta = EncuestaPresidencialId,
                        palabraClave = parametro,
                        usuario = idUsuario
                    };

                    // Agregar el nuevo voto al contexto y guardar los cambios en la base de datos
                    _context.votacion.Add(nuevoVoto);
                    _context.SaveChanges();

                    // Retornar un mensaje de éxito
                    return Json(new { exito = true });
                }
                else
                {
                    // El usuario ya ha votado, retornar un error
                    return Json(new { exito = false, mensaje = "Ya has votado anteriormente." });
                }
            }
            else
            {
                // El sessionId no coincide o no está presente, retornar un error
                return Json(new { exito = false, mensaje = "Error de sesión." });
            }
        }


        private bool HaVotado(string dispositivo, string parametro)
        {
            // Crear el nombre del cookie con el parámetro de la encuesta
            //string cookieName = $"VotoRealizado_{dispositivo}_{parametro}";

            //// Verificar si existe una cookie de seguimiento de voto específica para este parámetro de encuesta
            //if (HttpContext.Request.Cookies[cookieName] != null)
            //{
            //    return true;
            //}

            if (HttpContext.Session.GetString(dispositivo + "_" + parametro) != null)
            {
                return true;
            }

            return false;
        }

        private string GenerarIdentificadorDispositivo(string parametro)
        {
            string userAgent = HttpContext.Request.Headers["User-Agent"];
            return ComputeHash(userAgent + parametro);
        }

        private string ComputeHash(string input)
        {
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hashBytes = algorithm.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        public async Task<IActionResult> Estadisticas(string zona)
        {
            // Obtener los votos por opción
            var votosPorOpcion = await _context.votacion
                .Where(v => v.encuesta == EncuestaPresidencialId && v.palabraClave == zona)
                .GroupBy(v => v.IdVoto)
                .Select(g => new votacion { IdVoto = g.Key, Count = g.Count() })
                .ToListAsync();

            // Obtener los candidatos
            var candidatos = new List<votacion>
            {
                new votacion { IdVoto = 1, CandidatoNombre = "Rocio Adame Muñoz", CandidatoImagen = "../img/testimonial/member-01.jpg", PartidoImagen= "../img/testimonial/member-partido-12.jpg"},
                new votacion { IdVoto = 2, CandidatoNombre = "Karely Leal Ramos", CandidatoImagen = "../img/testimonial/member-02.jpg", PartidoImagen= "../img/testimonial/member-partido-01.jpg"},
                new votacion { IdVoto = 3, CandidatoNombre = "Fernando Serrano Garcia", CandidatoImagen = "../img/testimonial/member-03.jpg" , PartidoImagen= "../img/testimonial/member-partido-pri.jpg"},
                new votacion { IdVoto = 4, CandidatoNombre = "Francisco Guzman Lopez", CandidatoImagen = "../img/testimonial/member-04.jpg" , PartidoImagen = "../img/testimonial/member-partido-prd.jpg"},
                new votacion { IdVoto = 5, CandidatoNombre = "Mirna Rincon Vargas", CandidatoImagen = "../img/testimonial/member-06.jpg", PartidoImagen = "../img/testimonial/member-partido-pes.jpg" }
            };

            // Combinar los votos por opción con los candidatos
            var votosCompletos = candidatos
        .GroupJoin(votosPorOpcion,
            candidato => candidato.IdVoto,
            voto => voto.IdVoto,
            (candidato, votos) => new votacion
            {
                IdVoto = candidato.IdVoto,
                Count = votos.Any() ? votos.First().Count : 0,
                CandidatoNombre = candidato.CandidatoNombre,
                CandidatoImagen = candidato.CandidatoImagen,
                PartidoImagen = candidato.PartidoImagen
            })
        .OrderByDescending(v => v.Count)
        .ToList();

            int totalVotos = (int)votosCompletos.Sum(v => v.Count);

            ViewBag.TotalVotos = totalVotos;
            return View(votosCompletos);
        }


        [HttpGet]
        public IActionResult VerificarDispositivo()
        {
            var userAgent = Request.Headers["User-Agent"].ToString();
            bool esTelefono = userAgent.Contains("Mobile");
            bool bloquearAgente = userAgent.Contains("Terrible Morales/5.0");

            if (bloquearAgente)
            {
                // Devolver un mensaje de error indicando que el agente está bloqueado
                return Json(new { mensaje = "El agente está bloqueado" });
            }
            else
            {
                // Devolver un objeto JSON con la información sobre si es un teléfono o no
                return Json(new { esTelefono = esTelefono });
            }
        }
    public IActionResult Error()
        {
            // Crear el modelo de vista de error
            var errorViewModel = new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                ErrorMessage = TempData["ErrorMessage"] as string // Obtener el mensaje de error de TempData
            };

            // Pasar el objeto ErrorViewModel a la vista de error
            return View(errorViewModel);
        }

    }
}
