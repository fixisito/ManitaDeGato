using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using manitaDeGatoWeb.Models;

namespace manitaDeGatoWeb.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
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
            var admin = await _context.Administradores.FirstOrDefaultAsync(a => a.Usuario == usuario);
            // Comparamos el hash usando BCrypt
            if (admin != null && BCrypt.Net.BCrypt.Verify(contrasena, admin.Contraseña))
            {
                await SignInUser(admin.Id.ToString(), admin.Usuario, "Administrador", admin.Usuario);
                return RedirectToAction("Index", "Home");
            }

            // 2. Check Cliente
            var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Usuario == usuario);
            if (cliente != null && BCrypt.Net.BCrypt.Verify(contrasena, cliente.Contraseña))
            {
                await SignInUser(cliente.Id.ToString(), cliente.Usuario, "Cliente", cliente.Nombre);
                return RedirectToAction("Index", "Home");
            }

            // 3. Check Estilista
            var estilista = await _context.Estilistas.FirstOrDefaultAsync(e => e.Usuario == usuario);
            if (estilista != null && BCrypt.Net.BCrypt.Verify(contrasena, estilista.Contraseña))
            {
                await SignInUser(estilista.Id.ToString(), estilista.Usuario, "Estilista", $"{estilista.Nombre} {estilista.Apellido}");
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
                // Verificar si el usuario ya existe
                var existeAdmin = await _context.Administradores.AnyAsync(a => a.Usuario == cliente.Usuario);
                var existeCliente = await _context.Clientes.AnyAsync(c => c.Usuario == cliente.Usuario);
                var existeEstilista = await _context.Estilistas.AnyAsync(e => e.Usuario == cliente.Usuario);

                if (existeAdmin || existeCliente || existeEstilista)
                {
                    ViewBag.Error = "El nombre de usuario ya está en uso.";
                    return View(cliente);
                }

                // Encriptar la contraseña usando BCrypt (Estándar de la industria)
                // BCrypt genera un texto ilegible (Hash) matemáticamente irreversible.
                cliente.Contraseña = BCrypt.Net.BCrypt.HashPassword(cliente.Contraseña);

                _context.Add(cliente);
                await _context.SaveChangesAsync();

                // Iniciar sesión automáticamente tras el registro exitoso
                await SignInUser(cliente.Id.ToString(), cliente.Usuario, "Cliente", cliente.Nombre);
                
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
