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
            var dtEstilistas = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, apellido, rut, usuario, contraseña, correo, telefono FROM estilistas ORDER BY nombre");

            foreach (DataRow rowEst in dtEstilistas.Rows)
            {
                var estId = Convert.ToInt32(rowEst["Id"]);
                var estilista = new Estilista
                {
                    Id = estId,
                    Nombre = rowEst["nombre"].ToString() ?? string.Empty,
                    Apellido = rowEst["apellido"].ToString() ?? string.Empty,
                    Rut = rowEst["rut"].ToString() ?? string.Empty,
                    Correo = rowEst["correo"].ToString() ?? string.Empty,
                    Telefono = rowEst["telefono"].ToString() ?? string.Empty,
                    Usuario = rowEst["usuario"].ToString() ?? string.Empty,
                    Contraseña = rowEst["contraseña"].ToString() ?? string.Empty,
                    Servicios = new List<Servicio>(),
                    Citas = new List<Cita>()
                };

                // Cargar servicios del estilista
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

                // Cargar citas asignadas al estilista
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
        public async Task<IActionResult> Create([Bind("Nombre,Apellido,Rut,Correo,Telefono,Usuario,Contraseña")] Estilista estilista)
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

                try
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "INSERT INTO estilistas (nombre, apellido, rut, telefono, usuario, contraseña, correo) VALUES (@nombre, @apellido, @rut, @telefono, @usuario, @contraseña, @correo)",
                        new SqlParameter("@nombre", estilista.Nombre),
                        new SqlParameter("@apellido", estilista.Apellido),
                        new SqlParameter("@rut", estilista.Rut),
                        new SqlParameter("@telefono", estilista.Telefono),
                        new SqlParameter("@usuario", estilista.Usuario),
                        new SqlParameter("@contraseña", estilista.Contraseña),
                        new SqlParameter("@correo", estilista.Correo));

                    TempData["MensajeExito"] = $"¡Estilista {estilista.Nombre} registrado con éxito! Su contraseña inicial es: {pwdPlana}";
                    return RedirectToAction(nameof(Index));
                }
                catch (SqlException ex) when (ex.Number == 2627)
                {
                    ViewBag.Error = "El RUT ingresado ya se encuentra registrado en el sistema.";
                    return View(estilista);
                }
            }
            return View(estilista);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var dt = await _dbHelper.ExecuteQueryAsync(
                "SELECT Id, nombre, apellido, rut, usuario, contraseña, correo, telefono FROM estilistas WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dt.Rows.Count == 0) return NotFound();

            var row = dt.Rows[0];
            var estilista = new Estilista
            {
                Id = Convert.ToInt32(row["Id"]),
                Nombre = row["nombre"].ToString() ?? string.Empty,
                Apellido = row["apellido"].ToString() ?? string.Empty,
                Rut = row["rut"].ToString() ?? string.Empty,
                Correo = row["correo"].ToString() ?? string.Empty,
                Telefono = row["telefono"].ToString() ?? string.Empty,
                Usuario = row["usuario"].ToString() ?? string.Empty,
                Contraseña = "" // No mandamos la contraseña real a la vista por seguridad
            };

            return View(estilista);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre,Apellido,Rut,Correo,Telefono,Usuario,Contraseña")] Estilista estilista)
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

                try
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "UPDATE estilistas SET nombre = @nombre, apellido = @apellido, rut = @rut, correo = @correo, telefono = @telefono, usuario = @usuario, contraseña = @contraseña WHERE Id = @id",
                        new SqlParameter("@nombre", estilista.Nombre),
                        new SqlParameter("@apellido", estilista.Apellido),
                        new SqlParameter("@rut", estilista.Rut),
                        new SqlParameter("@correo", estilista.Correo),
                        new SqlParameter("@telefono", estilista.Telefono),
                        new SqlParameter("@usuario", estilista.Usuario),
                        new SqlParameter("@contraseña", passwordToSave),
                        new SqlParameter("@id", id));

                    TempData["MensajeExito"] = $"Datos del estilista {estilista.Nombre} actualizados correctamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (SqlException ex) when (ex.Number == 2627)
                {
                    ViewBag.Error = "El RUT ingresado ya pertenece a otro estilista en el sistema.";
                    return View(estilista);
                }
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

        // GET: Estilistas/Horario/5
        public async Task<IActionResult> Horario(int? id)
        {
            if (id == null) return NotFound();

            var dtEst = await _dbHelper.ExecuteQueryAsync(
                "SELECT nombre, apellido FROM estilistas WHERE Id = @id",
                new SqlParameter("@id", id));

            if (dtEst.Rows.Count == 0) return NotFound();

            var rowEst = dtEst.Rows[0];
            var viewModel = new EstilistaHorarioViewModel
            {
                IdEstilista = id.Value,
                NombreEstilista = $"{rowEst["nombre"]} {rowEst["apellido"]}"
            };

            var dtDisp = await _dbHelper.ExecuteQueryAsync(
                "SELECT dia, hora_inicio, hora_fin FROM disponibilidad WHERE IdEstilista = @idEstilista",
                new SqlParameter("@idEstilista", id));

            var dispDict = new Dictionary<string, (TimeSpan inicio, TimeSpan fin)>();
            foreach (DataRow row in dtDisp.Rows)
            {
                var dia = row["dia"].ToString() ?? string.Empty;
                var inicio = (TimeSpan)row["hora_inicio"];
                var fin = (TimeSpan)row["hora_fin"];
                dispDict[dia] = (inicio, fin);
            }

            var diasSemana = new[] { "Lunes", "Martes", "Miercoles", "Jueves", "Viernes" };
            foreach (var dia in diasSemana)
            {
                if (dispDict.ContainsKey(dia))
                {
                    viewModel.Dias.Add(new DiaDisponibilidadItem
                    {
                        Dia = dia,
                        Activo = true,
                        HoraInicio = dispDict[dia].inicio.ToString(@"hh\:mm"),
                        HoraFin = dispDict[dia].fin.ToString(@"hh\:mm")
                    });
                }
                else
                {
                    viewModel.Dias.Add(new DiaDisponibilidadItem
                    {
                        Dia = dia,
                        Activo = false,
                        HoraInicio = "09:00",
                        HoraFin = "18:00"
                    });
                }
            }

            return View(viewModel);
        }

        // POST: Estilistas/Horario/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Horario(int id, EstilistaHorarioViewModel model)
        {
            if (id != model.IdEstilista) return NotFound();

            if (ModelState.IsValid)
            {
                // Limpiar disponibilidad anterior
                await _dbHelper.ExecuteNonQueryAsync(
                    "DELETE FROM disponibilidad WHERE IdEstilista = @idEstilista",
                    new SqlParameter("@idEstilista", id));

                foreach (var diaItem in model.Dias)
                {
                    if (diaItem.Activo)
                    {
                        if (TimeSpan.TryParse(diaItem.HoraInicio, out var tInicio) && TimeSpan.TryParse(diaItem.HoraFin, out var tFin))
                        {
                            if (tInicio >= tFin)
                            {
                                ModelState.AddModelError("", $"Para el día {diaItem.Dia}, la hora de inicio debe ser menor que la hora de término.");
                                // Recargar nombre del estilista
                                var dtEst = await _dbHelper.ExecuteQueryAsync(
                                    "SELECT nombre, apellido FROM estilistas WHERE Id = @id",
                                    new SqlParameter("@id", id));
                                model.NombreEstilista = dtEst.Rows.Count > 0 ? $"{dtEst.Rows[0]["nombre"]} {dtEst.Rows[0]["apellido"]}" : "";
                                return View(model);
                            }

                            if (tInicio.Minutes % 30 != 0 || tFin.Minutes % 30 != 0)
                            {
                                ModelState.AddModelError("", $"Para el día {diaItem.Dia}, las horas deben ser en intervalos de 30 minutos.");
                                var dtEst = await _dbHelper.ExecuteQueryAsync(
                                    "SELECT nombre, apellido FROM estilistas WHERE Id = @id",
                                    new SqlParameter("@id", id));
                                model.NombreEstilista = dtEst.Rows.Count > 0 ? $"{dtEst.Rows[0]["nombre"]} {dtEst.Rows[0]["apellido"]}" : "";
                                return View(model);
                            }

                            await _dbHelper.ExecuteNonQueryAsync(
                                "INSERT INTO disponibilidad (IdEstilista, dia, hora_inicio, hora_fin) VALUES (@idEstilista, @dia, @horaInicio, @horaFin)",
                                new SqlParameter("@idEstilista", id),
                                new SqlParameter("@dia", diaItem.Dia),
                                new SqlParameter("@horaInicio", tInicio),
                                new SqlParameter("@horaFin", tFin));
                        }
                        else
                        {
                            ModelState.AddModelError("", $"Las horas ingresadas para el día {diaItem.Dia} no son válidas.");
                            var dtEst = await _dbHelper.ExecuteQueryAsync(
                                "SELECT nombre, apellido FROM estilistas WHERE Id = @id",
                                new SqlParameter("@id", id));
                            model.NombreEstilista = dtEst.Rows.Count > 0 ? $"{dtEst.Rows[0]["nombre"]} {dtEst.Rows[0]["apellido"]}" : "";
                            return View(model);
                        }
                    }
                }

                TempData["MensajeExito"] = "Horario de disponibilidad actualizado con éxito.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }
    }

    public class EstilistaHorarioViewModel
    {
        public int IdEstilista { get; set; }
        public string NombreEstilista { get; set; } = string.Empty;
        public List<DiaDisponibilidadItem> Dias { get; set; } = new List<DiaDisponibilidadItem>();
    }

    public class DiaDisponibilidadItem
    {
        public string Dia { get; set; } = string.Empty; // Lunes, Martes, Miercoles, Jueves, Viernes
        public bool Activo { get; set; }
        public string HoraInicio { get; set; } = "09:00";
        public string HoraFin { get; set; } = "18:00";
    }
}
