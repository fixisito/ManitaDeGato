using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Security.Claims;
using System.Threading.Tasks;
using manitaDeGatoWeb.Models;
using manitaDeGatoWeb.Data;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly DataBaseHelper _dbHelper;

        public CitasController(DataBaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // GET: Citas (Admin)
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var citas = await ObtenerCitasInterno(null, null, false);
            return View(citas);
        }

        // GET: Citas/MisCitas (Estilista)
        [Authorize(Roles = "Estilista")]
        public async Task<IActionResult> MisCitas()
        {
            var estilistaId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var citas = await ObtenerCitasInterno(estilistaId, null, true);
            return View("Index", citas);
        }

        // GET: Citas/Historial (Cliente)
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> Historial()
        {
            var clienteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var citas = await ObtenerCitasInterno(null, clienteId, false);
            return View("Index", citas);
        }

        private async Task<List<Cita>> ObtenerCitasInterno(int? filterEstilistaId, int? filterClienteId, bool orderByAsc = false)
        {
            var list = new List<Cita>();
            string query = @"
                SELECT c.Id, c.FechaCita, c.HoraCita, c.estado, c.IdCliente, c.IdEstilista, c.IdServicio,
                       cl.nombre AS ClienteNombre, cl.usuario AS ClienteUsuario,
                       e.nombre AS EstilistaNombre, e.apellido AS EstilistaApellido,
                       s.nombre AS ServicioNombre, s.precio AS ServicioPrecio, s.duracion AS ServicioDuracion
                FROM citas c
                LEFT JOIN clientes cl ON c.IdCliente = cl.Id
                LEFT JOIN estilistas e ON c.IdEstilista = e.Id
                LEFT JOIN servicios s ON c.IdServicio = s.Id";

            var parameters = new List<SqlParameter>();
            if (filterEstilistaId.HasValue)
            {
                query += " WHERE c.IdEstilista = @estId";
                parameters.Add(new SqlParameter("@estId", filterEstilistaId.Value));
            }
            else if (filterClienteId.HasValue)
            {
                query += " WHERE c.IdCliente = @cliId";
                parameters.Add(new SqlParameter("@cliId", filterClienteId.Value));
            }

            if (orderByAsc)
            {
                query += " ORDER BY c.FechaCita ASC, c.HoraCita ASC";
            }
            else
            {
                query += " ORDER BY c.FechaCita DESC, c.HoraCita DESC";
            }

            var dt = await _dbHelper.ExecuteQueryAsync(query, parameters.ToArray());
            foreach (DataRow row in dt.Rows)
            {
                var cita = new Cita
                {
                    Id = Convert.ToInt32(row["Id"]),
                    FechaCita = Convert.ToDateTime(row["FechaCita"]),
                    HoraCita = (TimeSpan)row["HoraCita"],
                    Estado = row["estado"].ToString() ?? "Pendiente",
                    IdCliente = Convert.ToInt32(row["IdCliente"]),
                    IdEstilista = Convert.ToInt32(row["IdEstilista"]),
                    IdServicio = Convert.ToInt32(row["IdServicio"]),
                    Cliente = new Cliente
                    {
                        Id = Convert.ToInt32(row["IdCliente"]),
                        Nombre = row["ClienteNombre"].ToString() ?? string.Empty,
                        Usuario = row["ClienteUsuario"].ToString() ?? string.Empty
                    },
                    Estilista = new Estilista
                    {
                        Id = Convert.ToInt32(row["IdEstilista"]),
                        Nombre = row["EstilistaNombre"].ToString() ?? string.Empty,
                        Apellido = row["EstilistaApellido"].ToString() ?? string.Empty
                    },
                    Servicio = new Servicio
                    {
                        Id = Convert.ToInt32(row["IdServicio"]),
                        Nombre = row["ServicioNombre"].ToString() ?? string.Empty,
                        Precio = Convert.ToDecimal(row["ServicioPrecio"]),
                        Duracion = Convert.ToInt32(row["ServicioDuracion"])
                    }
                };
                list.Add(cita);
            }
            return list;
        }

        // GET: Citas/Agendar
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> Agendar()
        {
            await CargarServiciosYEstilistasEnViewBag(null, null);
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
                var clienteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await _dbHelper.ExecuteNonQueryAsync(
                    @"INSERT INTO citas (IdCliente, IdServicio, IdEstilista, FechaCita, HoraCita, estado) 
                      VALUES (@clienteId, @servicioId, @estilistaId, @fecha, @hora, 'Confirmada')",
                    new SqlParameter("@clienteId", clienteId),
                    new SqlParameter("@servicioId", cita.IdServicio),
                    new SqlParameter("@estilistaId", cita.IdEstilista),
                    new SqlParameter("@fecha", cita.FechaCita),
                    new SqlParameter("@hora", cita.HoraCita));

                return RedirectToAction(nameof(Historial));
            }
            await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
            return View(cita);
        }

        // POST: Citas/Cancelar/5
        [HttpPost, ActionName("Cancelar")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelarConfirmed(int id)
        {
            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT IdCliente, IdEstilista FROM citas WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                var idCliente = Convert.ToInt32(row["IdCliente"]);
                var idEstilista = Convert.ToInt32(row["IdEstilista"]);

                var currentUserId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var isCliente = User.IsInRole("Cliente");
                var isEstilista = User.IsInRole("Estilista");
                
                // Validación básica de propiedad
                if ((isCliente && idCliente != currentUserId) || 
                    (isEstilista && idEstilista != currentUserId) && 
                    !User.IsInRole("Administrador"))
                {
                    return Unauthorized();
                }

                await _dbHelper.ExecuteNonQueryAsync("DELETE FROM citas WHERE Id = @id", new SqlParameter("@id", id));
            }

            if (User.IsInRole("Cliente")) return RedirectToAction(nameof(Historial));
            if (User.IsInRole("Estilista")) return RedirectToAction(nameof(MisCitas));
            return RedirectToAction(nameof(Index));
        }

        private async Task CargarServiciosYEstilistasEnViewBag(int? selectedServicioId, int? selectedEstilistaId)
        {
            // 1. Fetch Categorias
            var dtCategorias = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre FROM categoria ORDER BY nombre");
            var categoriasList = new List<object>();
            foreach (DataRow row in dtCategorias.Rows)
            {
                categoriasList.Add(new { Id = Convert.ToInt32(row["Id"]), Nombre = row["nombre"].ToString() });
            }

            // 2. Fetch Estilistas
            var dtEstilistas = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, apellido FROM estilistas ORDER BY nombre");
            var estilistasList = new List<object>();
            foreach (DataRow row in dtEstilistas.Rows)
            {
                estilistasList.Add(new { Id = Convert.ToInt32(row["Id"]), Nombre = $"{row["nombre"]} {row["apellido"]}" });
            }

            // 3. Fetch Servicios
            var dtServicios = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, Id_categoria, IdEstilista FROM servicios ORDER BY nombre");
            var serviciosList = new List<object>();
            foreach (DataRow row in dtServicios.Rows)
            {
                serviciosList.Add(new { 
                    Id = Convert.ToInt32(row["Id"]), 
                    Nombre = row["nombre"].ToString(),
                    IdCategoria = Convert.ToInt32(row["Id_categoria"]),
                    IdEstilista = row.IsNull("IdEstilista") ? (int?)null : Convert.ToInt32(row["IdEstilista"])
                });
            }

            ViewBag.CategoriasJson = System.Text.Json.JsonSerializer.Serialize(categoriasList);
            ViewBag.EstilistasJson = System.Text.Json.JsonSerializer.Serialize(estilistasList);
            ViewBag.ServiciosJson = System.Text.Json.JsonSerializer.Serialize(serviciosList);

            // Empty SelectLists to satisfy standard View rendering (JS will populate them)
            ViewData["IdServicio"] = new SelectList(new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(), "Value", "Text");
            ViewData["IdEstilista"] = new SelectList(new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(), "Value", "Text");
        }
    }
}
