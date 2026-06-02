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
    public class ServiciosController : Controller
    {
        private readonly DataBaseHelper _dbHelper;

        public ServiciosController(DataBaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // GET: Servicios
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Index()
        {
            var servicios = await ObtenerServiciosInterno(null);
            return View(servicios);
        }

        // GET: Servicios/MisServicios
        [Authorize(Roles = "Estilista")]
        public async Task<IActionResult> MisServicios()
        {
            var estilistaId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var servicios = await ObtenerServiciosInterno(estilistaId);
            return View("Index", servicios);
        }

        private async Task<List<Servicio>> ObtenerServiciosInterno(int? estilistaIdFiltro)
        {
            var list = new List<Servicio>();
            string query = @"
                SELECT s.Id, s.nombre, s.precio, s.duracion, s.descripcion, s.Id_categoria, s.IdEstilista,
                       c.nombre AS CategoriaNombre,
                       e.nombre AS EstilistaNombre, e.apellido AS EstilistaApellido
                FROM servicios s
                LEFT JOIN categoria c ON s.Id_categoria = c.Id
                LEFT JOIN estilistas e ON s.IdEstilista = e.Id";

            DataTable dt;
            if (estilistaIdFiltro.HasValue)
            {
                query += " WHERE s.IdEstilista IS NULL OR s.IdEstilista = @estId";
                dt = await _dbHelper.ExecuteQueryAsync(query, new SqlParameter("@estId", estilistaIdFiltro.Value));
            }
            else
            {
                dt = await _dbHelper.ExecuteQueryAsync(query);
            }

            foreach (DataRow row in dt.Rows)
            {
                var servicio = new Servicio
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Nombre = row["nombre"].ToString() ?? string.Empty,
                    Precio = Convert.ToDecimal(row["precio"]),
                    Duracion = Convert.ToInt32(row["duracion"]),
                    Descripcion = row["descripcion"].ToString() ?? string.Empty,
                    Id_categoria = Convert.ToInt32(row["Id_categoria"]),
                    IdEstilista = row["IdEstilista"] == DBNull.Value ? null : Convert.ToInt32(row["IdEstilista"]),
                    Categoria = new Categoria
                    {
                        Id = Convert.ToInt32(row["Id_categoria"]),
                        Nombre = row["CategoriaNombre"].ToString() ?? string.Empty
                    }
                };

                if (row["IdEstilista"] != DBNull.Value)
                {
                    servicio.Estilista = new Estilista
                    {
                        Id = Convert.ToInt32(row["IdEstilista"]),
                        Nombre = row["EstilistaNombre"].ToString() ?? string.Empty,
                        Apellido = row["EstilistaApellido"].ToString() ?? string.Empty
                    };
                }

                list.Add(servicio);
            }

            return list;
        }

        [Authorize(Roles = "Administrador,Estilista")]
        public async Task<IActionResult> Create()
        {
            await CargarCategoriasEnViewBag(null);
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,Estilista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Precio,Duracion,Descripcion,Id_categoria")] Servicio servicio)
        {
            if (ModelState.IsValid)
            {
                int? estId = null;
                if (User.IsInRole("Estilista"))
                {
                    estId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                }

                await _dbHelper.ExecuteNonQueryAsync(
                    @"INSERT INTO servicios (nombre, precio, duracion, descripcion, Id_categoria, IdEstilista) 
                      VALUES (@nombre, @precio, @duracion, @descripcion, @id_categoria, @idEstilista)",
                    new SqlParameter("@nombre", servicio.Nombre),
                    new SqlParameter("@precio", servicio.Precio),
                    new SqlParameter("@duracion", servicio.Duracion),
                    new SqlParameter("@descripcion", servicio.Descripcion ?? string.Empty),
                    new SqlParameter("@id_categoria", servicio.Id_categoria),
                    new SqlParameter("@idEstilista", (object)estId ?? DBNull.Value));

                return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
            }

            await CargarCategoriasEnViewBag(servicio.Id_categoria);
            return View(servicio);
        }

        [Authorize(Roles = "Administrador,Estilista")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, nombre, precio, duracion, descripcion, Id_categoria, IdEstilista FROM servicios WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count == 0) return NotFound();

            var row = dt.Rows[0];
            var servicio = new Servicio
            {
                Id = Convert.ToInt32(row["Id"]),
                Nombre = row["nombre"].ToString() ?? string.Empty,
                Precio = Convert.ToDecimal(row["precio"]),
                Duracion = Convert.ToInt32(row["duracion"]),
                Descripcion = row["descripcion"].ToString() ?? string.Empty,
                Id_categoria = Convert.ToInt32(row["Id_categoria"]),
                IdEstilista = row["IdEstilista"] == DBNull.Value ? null : Convert.ToInt32(row["IdEstilista"])
            };

            // Validacion: un estilista solo puede editar sus propios servicios
            if (User.IsInRole("Estilista") && servicio.IdEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
            {
                return Unauthorized();
            }

            await CargarCategoriasEnViewBag(servicio.Id_categoria);
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
                // Validacion de propiedad para estilista
                if (User.IsInRole("Estilista") && servicio.IdEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                {
                    return Unauthorized();
                }

                await _dbHelper.ExecuteNonQueryAsync(
                    @"UPDATE servicios 
                      SET nombre = @nombre, precio = @precio, duracion = @duracion, descripcion = @descripcion, 
                          Id_categoria = @id_categoria, IdEstilista = @idEstilista 
                      WHERE Id = @id",
                    new SqlParameter("@nombre", servicio.Nombre),
                    new SqlParameter("@precio", servicio.Precio),
                    new SqlParameter("@duracion", servicio.Duracion),
                    new SqlParameter("@descripcion", servicio.Descripcion ?? string.Empty),
                    new SqlParameter("@id_categoria", servicio.Id_categoria),
                    new SqlParameter("@idEstilista", (object)servicio.IdEstilista ?? DBNull.Value),
                    new SqlParameter("@id", id));

                return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
            }

            await CargarCategoriasEnViewBag(servicio.Id_categoria);
            return View(servicio);
        }

        [HttpPost]
        [Authorize(Roles = "Administrador,Estilista")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT IdEstilista FROM servicios WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count > 0)
            {
                var idEstilista = dt.Rows[0]["IdEstilista"] == DBNull.Value ? null : (int?)Convert.ToInt32(dt.Rows[0]["IdEstilista"]);

                if (User.IsInRole("Estilista") && idEstilista != int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!))
                {
                    return Unauthorized();
                }

                await _dbHelper.ExecuteNonQueryAsync(
                    "DELETE FROM servicios WHERE Id = @id",
                    new SqlParameter("@id", id));
            }

            return User.IsInRole("Estilista") ? RedirectToAction(nameof(MisServicios)) : RedirectToAction(nameof(Index));
        }

        private async Task CargarCategoriasEnViewBag(int? selectedId)
        {
            var dt = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre FROM categoria ORDER BY nombre");
            var list = new List<Categoria>();
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Categoria
                {
                    Id = Convert.ToInt32(row["Id"]),
                    Nombre = row["nombre"].ToString() ?? string.Empty
                });
            }
            ViewData["Id_categoria"] = new SelectList(list, "Id", "Nombre", selectedId);
        }
    }
}
