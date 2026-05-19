using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using manitaDeGatoWeb.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CitasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Citas (Admin)
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var citas = await _context.Citas
                .Include(c => c.Cliente)
                .Include(c => c.Estilista)
                .Include(c => c.Servicio)
                .OrderByDescending(c => c.FechaCita)
                .ThenBy(c => c.HoraCita)
                .ToListAsync();
            return View(citas);
        }

        // GET: Citas/MisCitas (Estilista)
        [Authorize(Roles = "Estilista")]
        public async Task<IActionResult> MisCitas()
        {
            var estilistaId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var citas = await _context.Citas
                .Include(c => c.Cliente)
                .Include(c => c.Servicio)
                .Where(c => c.IdEstilista == estilistaId)
                .OrderBy(c => c.FechaCita)
                .ThenBy(c => c.HoraCita)
                .ToListAsync();
            return View("Index", citas);
        }

        // GET: Citas/Historial (Cliente)
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> Historial()
        {
            var clienteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var citas = await _context.Citas
                .Include(c => c.Estilista)
                .Include(c => c.Servicio)
                .Where(c => c.IdCliente == clienteId)
                .OrderByDescending(c => c.FechaCita)
                .ThenBy(c => c.HoraCita)
                .ToListAsync();
            return View("Index", citas);
        }

        // GET: Citas/Agendar
        [Authorize(Roles = "Cliente")]
        public IActionResult Agendar()
        {
            ViewData["IdServicio"] = new SelectList(_context.Servicios, "Id", "Nombre");
            ViewData["IdEstilista"] = new SelectList(_context.Estilistas, "Id", "Nombre");
            return View();
        }

        // POST: Citas/Agendar
        [HttpPost]
        [Authorize(Roles = "Cliente")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Agendar([Bind("FechaCita,HoraCita,IdServicio,IdEstilista")] Cita cita)
        {
            if (ModelState.IsValid)
            {
                cita.IdCliente = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                cita.Estado = "Confirmada";
                _context.Add(cita);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Historial));
            }
            ViewData["IdServicio"] = new SelectList(_context.Servicios, "Id", "Nombre", cita.IdServicio);
            ViewData["IdEstilista"] = new SelectList(_context.Estilistas, "Id", "Nombre", cita.IdEstilista);
            return View(cita);
        }

        // POST: Citas/Cancelar/5
        [HttpPost, ActionName("Cancelar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarConfirmed(int id)
        {
            var cita = await _context.Citas.FindAsync(id);
            if (cita != null)
            {
                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var isCliente = User.IsInRole("Cliente");
                var isEstilista = User.IsInRole("Estilista");
                
                // Validación básica de propiedad
                if ((isCliente && cita.IdCliente != currentUserId) || 
                    (isEstilista && cita.IdEstilista != currentUserId) && 
                    !User.IsInRole("Administrador"))
                {
                    return Unauthorized();
                }

                _context.Citas.Remove(cita);
                await _context.SaveChangesAsync();
            }

            if (User.IsInRole("Cliente")) return RedirectToAction(nameof(Historial));
            if (User.IsInRole("Estilista")) return RedirectToAction(nameof(MisCitas));
            return RedirectToAction(nameof(Index));
        }
    }
}
