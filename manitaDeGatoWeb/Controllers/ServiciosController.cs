using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using manitaDeGatoWeb.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize]
    public class ServiciosController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServiciosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Servicios
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var servicios = await _context.Servicios.Include(s => s.Categoria).Include(s => s.Estilista).ToListAsync();
            return View(servicios);
        }

        // GET: Servicios/MisServicios
        [Authorize(Roles = "Estilista")]
        public async Task<IActionResult> MisServicios()
        {
            var estilistaId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var servicios = await _context.Servicios
                .Include(s => s.Categoria)
                .Where(s => s.IdEstilista == null || s.IdEstilista == estilistaId)
                .ToListAsync();
            return View("Index", servicios);
        }

        [Authorize(Roles = "Administrador,Estilista")]
        public IActionResult Create()
        {
            ViewData["Id_categoria"] = new SelectList(_context.Categorias, "Id", "Nombre");
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,Estilista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Precio,Duracion,Descripcion,Id_categoria")] Servicio servicio)
        {
            if (ModelState.IsValid)
            {
                if (User.IsInRole("Estilista"))
                {
                    servicio.IdEstilista = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                }
                
                _context.Add(servicio);
                await _context.SaveChangesAsync();
                return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
            }
            ViewData["Id_categoria"] = new SelectList(_context.Categorias, "Id", "Nombre", servicio.Id_categoria);
            return View(servicio);
        }

        [Authorize(Roles = "Administrador,Estilista")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio == null) return NotFound();

            // Validación: un estilista solo puede editar sus propios servicios
            if (User.IsInRole("Estilista") && servicio.IdEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
            {
                return Unauthorized();
            }

            ViewData["Id_categoria"] = new SelectList(_context.Categorias, "Id", "Nombre", servicio.Id_categoria);
            return View(servicio);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,Estilista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Precio,Duracion,Descripcion,Id_categoria,IdEstilista")] Servicio servicio)
        {
            if (id != servicio.Id) return NotFound();

            if (ModelState.IsValid)
            {
                // Validación de propiedad para estilista
                if (User.IsInRole("Estilista") && servicio.IdEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                {
                    return Unauthorized();
                }

                _context.Update(servicio);
                await _context.SaveChangesAsync();
                return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
            }
            ViewData["Id_categoria"] = new SelectList(_context.Categorias, "Id", "Nombre", servicio.Id_categoria);
            return View(servicio);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,Estilista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var servicio = await _context.Servicios.FindAsync(id);
            if (servicio != null)
            {
                if (User.IsInRole("Estilista") && servicio.IdEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                {
                    return Unauthorized();
                }
                
                _context.Servicios.Remove(servicio);
                await _context.SaveChangesAsync();
            }
            return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
        }
    }
}
