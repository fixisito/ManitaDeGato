using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Data;
using System.Data.SqlClient;
using manitaDeGatoWeb.Models;
using manitaDeGatoWeb.Data;

namespace manitaDeGatoWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly DataBaseHelper _dbHelper;

        public AuthController(DataBaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // Si ya está autenticado, redirigir
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string usuario, string contrasena)
        {
            if (string.IsNullOrEmpty(usuario) || string.IsNullOrEmpty(contrasena))
            {
                ViewBag.Error = "Por favor, ingrese usuario y contraseña.";
                return View();
            }

            // 1. Check Administrador
            var adminTable = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, usuario FROM administradores WHERE usuario = @usuario AND contraseña = @contrasena",
                new SqlParameter("@usuario", usuario),
                new SqlParameter("@contrasena", contrasena));

            if (adminTable.Rows.Count > 0)
            {
                var row = adminTable.Rows[0];
                await SignInUser(row["Id"].ToString()!, row["usuario"].ToString()!, "Administrador", row["usuario"].ToString()!);
                return RedirectToAction("Index", "Home");
            }

            // 2. Check Cliente
            var clienteTable = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, usuario, nombre FROM clientes WHERE usuario = @usuario AND contraseña = @contrasena",
                new SqlParameter("@usuario", usuario),
                new SqlParameter("@contrasena", contrasena));

            if (clienteTable.Rows.Count > 0)
            {
                var row = clienteTable.Rows[0];
                await SignInUser(row["Id"].ToString()!, row["usuario"].ToString()!, "Cliente", row["nombre"].ToString()!);
                return RedirectToAction("Index", "Home");
            }

            // 3. Check Estilista
            var estilistaTable = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, usuario, nombre, apellido FROM estilistas WHERE usuario = @usuario AND contraseña = @contrasena",
                new SqlParameter("@usuario", usuario),
                new SqlParameter("@contrasena", contrasena));

            if (estilistaTable.Rows.Count > 0)
            {
                var row = estilistaTable.Rows[0];
                await SignInUser(
                    row["Id"].ToString()!, 
                    row["usuario"].ToString()!, 
                    "Estilista", 
                    $"{row["nombre"]} {row["apellido"]}");
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Usuario o contraseña incorrectos.";
            return View();
        }

        [HttpGet]
        public IActionResult Registro()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registro([Bind("Nombre,Usuario,Contraseña")] Cliente cliente)
        {
            if (ModelState.IsValid)
            {
                // Verificar si el usuario ya existe en alguna de las 3 tablas
                var countAdmin = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM administradores WHERE usuario = @usuario",
                    new SqlParameter("@usuario", cliente.Usuario)));

                var countCliente = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM clientes WHERE usuario = @usuario",
                    new SqlParameter("@usuario", cliente.Usuario)));

                var countEstilista = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM estilistas WHERE usuario = @usuario",
                    new SqlParameter("@usuario", cliente.Usuario)));

                if (countAdmin > 0 || countCliente > 0 || countEstilista > 0)
                {
                    ViewBag.Error = "El nombre de usuario ya está en uso.";
                    return View(cliente);
                }

                // Insertar el nuevo cliente en texto plano y obtener su ID asignado (telefono es obligatorio en el MDF, asignamos cadena vacia por defecto)
                var insertQuery = "INSERT INTO clientes (nombre, telefono, usuario, contraseña) VALUES (@nombre, '', @usuario, @contraseña); SELECT SCOPE_IDENTITY();";
                var parameters = new[]
                {
                    new SqlParameter("@nombre", cliente.Nombre),
                    new SqlParameter("@usuario", cliente.Usuario),
                    new SqlParameter("@contraseña", cliente.Contraseña)
                };

                object result = await _dbHelper.ExecuteScalarAsync(insertQuery, parameters);
                int newId = Convert.ToInt32(result);

                // Iniciar sesión automáticamente tras el registro exitoso
                await SignInUser(newId.ToString(), cliente.Usuario, "Cliente", cliente.Nombre);
                
                return RedirectToAction("Index", "Home");
            }
            return View(cliente);
        }

        private async Task SignInUser(string id, string username, string role, string displayName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("DisplayName", displayName)
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
