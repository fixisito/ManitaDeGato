using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using manitaDeGatoWeb.Models;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class EstilistasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EstilistasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Estilistas
        public async Task<IActionResult> Index()
        {
            var estilistas = await _context.Estilistas
                .Include(e => e.Servicios)
                .Include(e => e.Citas)
                .ToListAsync();
            return View(estilistas);
        }

        // GET: Estilistas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Estilistas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Apellido,Usuario,Contraseña")] Estilista estilista)
        {
            if (ModelState.IsValid)
            {
                var existeUsuario = await _context.Administradores.AnyAsync(a => a.Usuario == estilista.Usuario) ||
                                    await _context.Clientes.AnyAsync(c => c.Usuario == estilista.Usuario) ||
                                    await _context.Estilistas.AnyAsync(e => e.Usuario == estilista.Usuario);

                if (existeUsuario)
                {
                    ViewBag.Error = "El nombre de usuario ya está en uso.";
                    return View(estilista);
                }

                // Guardar la contraseña original para la "simulación" de notificación
                var pwdPlana = estilista.Contraseña;
                
                // Encriptar con BCrypt
                estilista.Contraseña = BCrypt.Net.BCrypt.HashPassword(estilista.Contraseña);

                _context.Add(estilista);
                await _context.SaveChangesAsync();

                // Simulación de envío de correo
                TempData["MensajeExito"] = $"¡Estilista registrado con éxito! Se ha enviado un correo simulado a {estilista.Nombre} con su usuario ({estilista.Usuario}) y contraseña ({pwdPlana}).";

                return RedirectToAction(nameof(Index));
            }
            return View(estilista);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var estilista = await _context.Estilistas.FindAsync(id);
            if (estilista == null) return NotFound();

            // No mandamos la contraseña real a la vista por seguridad
            estilista.Contraseña = "";
            return View(estilista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Apellido,Usuario,Contraseña")] Estilista estilista)
        {
            if (id != estilista.Id) return NotFound();

            // Remueve la validación de contraseña si viene vacía (significa que no la quiso cambiar)
            if (string.IsNullOrEmpty(estilista.Contraseña))
            {
                ModelState.Remove("Contraseña");
            }

            if (ModelState.IsValid)
            {
                var original = await _context.Estilistas.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                
                if (string.IsNullOrEmpty(estilista.Contraseña))
                {
                    // Mantener la contraseña anterior
                    estilista.Contraseña = original.Contraseña;
                }
                else
                {
                    // Hashear la nueva contraseña
                    estilista.Contraseña = BCrypt.Net.BCrypt.HashPassword(estilista.Contraseña);
                }

                _context.Update(estilista);
                await _context.SaveChangesAsync();
                TempData["MensajeExito"] = $"Datos del estilista {estilista.Nombre} actualizados correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(estilista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var estilista = await _context.Estilistas.FindAsync(id);
            if (estilista != null)
            {
                try
                {
                    _context.Estilistas.Remove(estilista);
                    await _context.SaveChangesAsync();
                    TempData["MensajeExito"] = $"El estilista {estilista.Nombre} ha sido eliminado del sistema.";
                }
                catch (DbUpdateException)
                {
                    TempData["MensajeError"] = $"No se puede eliminar a {estilista.Nombre} porque tiene citas programadas o historial registrado. Por ahora la base de datos restringe esta acción para proteger tu historial.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
