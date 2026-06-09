using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using manitaDeGatoWeb.Models;
using manitaDeGatoWeb.Data;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class EstilistasController : Controller
    {
        private readonly DataBaseHelper _dbHelper;

        public EstilistasController(DataBaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        // GET: Estilistas
        public async Task<IActionResult> Index()
        {
            var list = new List<Estilista>();
            var dtEstilistas = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, apellido, rut, usuario, contraseña FROM estilistas ORDER BY nombre");

            foreach (DataRow rowEst in dtEstilistas.Rows)
            {
                var estId = Convert.ToInt32(rowEst["Id"]);
                var estilista = new Estilista
                {
                    Id = estId,
                    Nombre = rowEst["nombre"].ToString() ?? string.Empty,
                    Apellido = rowEst["apellido"].ToString() ?? string.Empty,
                    Rut = rowEst["rut"].ToString() ?? string.Empty,
                    Usuario = rowEst["usuario"].ToString() ?? string.Empty,
                    Contraseña = rowEst["contraseña"].ToString() ?? string.Empty,
                    Servicios = new List<Servicio>(),
                    Citas = new List<Cita>()
                };

                // Load Services
                var dtServicios = await _dbHelper.ExecuteQueryAsync(
                    "SELECT Id, nombre, precio, duracion, Id_categoria, descripcion, IdEstilista FROM servicios WHERE IdEstilista = @estId",
                    new SqlParameter("@estId", estId));

                foreach (DataRow rowSer in dtServicios.Rows)
                {
                    estilista.Servicios.Add(new Servicio
                    {
                        Id = Convert.ToInt32(rowSer["Id"]),
                        Nombre = rowSer["nombre"].ToString() ?? string.Empty,
                        Precio = Convert.ToDecimal(rowSer["precio"]),
                        Duracion = Convert.ToInt32(rowSer["duracion"]),
                        Id_categoria = Convert.ToInt32(rowSer["Id_categoria"]),
                        Descripcion = rowSer["descripcion"].ToString() ?? string.Empty,
                        IdEstilista = estId
                    });
                }

                // Load Citas
                var dtCitas = await _dbHelper.ExecuteQueryAsync(
                    "SELECT Id, FechaCita, HoraCita, estado, IdCliente, IdServicio, IdEstilista FROM citas WHERE IdEstilista = @estId",
                    new SqlParameter("@estId", estId));

                foreach (DataRow rowCita in dtCitas.Rows)
                {
                    estilista.Citas.Add(new Cita
                    {
                        Id = Convert.ToInt32(rowCita["Id"]),
                        FechaCita = Convert.ToDateTime(rowCita["FechaCita"]),
                        HoraCita = (TimeSpan)rowCita["HoraCita"],
                        Estado = rowCita["estado"].ToString() ?? "Pendiente",
                        IdCliente = Convert.ToInt32(rowCita["IdCliente"]),
                        IdServicio = Convert.ToInt32(rowCita["IdServicio"]),
                        IdEstilista = estId
                    });
                }

                list.Add(estilista);
            }

            return View(list);
        }

        // GET: Estilistas/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Estilistas/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre,Apellido,Rut,Usuario,Contraseña")] Estilista estilista)
        {
            if (ModelState.IsValid)
            {
                var countAdmin = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM administradores WHERE usuario = @usuario",
                    new SqlParameter("@usuario", estilista.Usuario)));

                var countCliente = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM clientes WHERE usuario = @usuario",
                    new SqlParameter("@usuario", estilista.Usuario)));

                var countEstilista = Convert.ToInt32(await _dbHelper.ExecuteScalarAsync(
                    "SELECT COUNT(*) FROM estilistas WHERE usuario = @usuario",
                    new SqlParameter("@usuario", estilista.Usuario)));

                if (countAdmin > 0 || countCliente > 0 || countEstilista > 0)
                {
                    ViewBag.Error = "El nombre de usuario ya está en uso.";
                    return View(estilista);
                }

                var pwdPlana = estilista.Contraseña;

                await _dbHelper.ExecuteNonQueryAsync(
                    "INSERT INTO estilistas (nombre, apellido, rut, telefono, usuario, contraseña) VALUES (@nombre, @apellido, @rut, '', @usuario, @contraseña)",
                    new SqlParameter("@nombre", estilista.Nombre),
                    new SqlParameter("@apellido", estilista.Apellido),
                    new SqlParameter("@rut", estilista.Rut),
                    new SqlParameter("@usuario", estilista.Usuario),
                    new SqlParameter("@contraseña", estilista.Contraseña));

                TempData["MensajeExito"] = $"¡Estilista registrado con éxito! Se ha enviado un correo simulado a {estilista.Nombre} con su usuario ({estilista.Usuario}) y contraseña ({pwdPlana}).";

                return RedirectToAction(nameof(Index));
            }
            return View(estilista);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, nombre, apellido, rut, usuario, contraseña FROM estilistas WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count == 0) return NotFound();

            var row = dt.Rows[0];
            var estilista = new Estilista
            {
                Id = Convert.ToInt32(row["Id"]),
                Nombre = row["nombre"].ToString() ?? string.Empty,
                Apellido = row["apellido"].ToString() ?? string.Empty,
                Rut = row["rut"].ToString() ?? string.Empty,
                Usuario = row["usuario"].ToString() ?? string.Empty,
                Contraseña = "" // No mandamos la contraseña real a la vista por seguridad
            };

            return View(estilista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Apellido,Rut,Usuario,Contraseña")] Estilista estilista)
        {
            if (id != estilista.Id) return NotFound();

            if (string.IsNullOrEmpty(estilista.Contraseña))
            {
                ModelState.Remove("Contraseña");
            }

            if (ModelState.IsValid)
            {
                var dt = await _dbHelper.ExecuteQueryAsync(
                    "SELECT contraseña FROM estilistas WHERE Id = @id",
                    new SqlParameter("@id", id));

                if (dt.Rows.Count == 0) return NotFound();
                string currentPassword = dt.Rows[0]["contraseña"].ToString() ?? string.Empty;

                string passwordToSave;
                if (string.IsNullOrEmpty(estilista.Contraseña))
                {
                    passwordToSave = currentPassword;
                }
                else
                {
                    passwordToSave = estilista.Contraseña;
                }

                await _dbHelper.ExecuteNonQueryAsync(
                    "UPDATE estilistas SET nombre = @nombre, apellido = @apellido, rut = @rut, usuario = @usuario, contraseña = @contraseña WHERE Id = @id",
                    new SqlParameter("@nombre", estilista.Nombre),
                    new SqlParameter("@apellido", estilista.Apellido),
                    new SqlParameter("@rut", estilista.Rut),
                    new SqlParameter("@usuario", estilista.Usuario),
                    new SqlParameter("@contraseña", passwordToSave),
                    new SqlParameter("@id", id));

                TempData["MensajeExito"] = $"Datos del estilista {estilista.Nombre} actualizados correctamente.";
                return RedirectToAction(nameof(Index));
            }
            return View(estilista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT nombre FROM estilistas WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count > 0)
            {
                string nombre = dt.Rows[0]["nombre"].ToString() ?? string.Empty;
                try
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "DELETE FROM estilistas WHERE Id = @id",
                        new SqlParameter("@id", id));

                    TempData["MensajeExito"] = $"El estilista {nombre} ha sido eliminado del sistema.";
                }
                catch (SqlException ex) when (ex.Number == 547)
                {
                    TempData["MensajeError"] = $"No se puede eliminar a {nombre} porque tiene citas programadas o historial registrado. Por ahora la base de datos restringe esta acción para proteger tu historial.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
