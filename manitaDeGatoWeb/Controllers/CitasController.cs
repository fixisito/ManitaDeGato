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
                       cl.nombre AS ClienteNombre, cl.apellido AS ClienteApellido, cl.usuario AS ClienteUsuario,
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
                        Apellido = row["ClienteApellido"].ToString() ?? string.Empty,
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
                var esDisponible = await ValidarCitaDisponible(cita.IdEstilista, cita.IdServicio, cita.FechaCita, cita.HoraCita);
                if (!esDisponible)
                {
                    ModelState.AddModelError("", "El horario o fecha seleccionados ya no están disponibles. Por favor, selecciona otro bloque.");
                    await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
                    return View(cita);
                }

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

        // GET: Citas/ObtenerHorasDisponibles
        [HttpGet]
        public async Task<IActionResult> ObtenerHorasDisponibles(int estilistaId, int servicioId, string fecha)
        {
            if (!DateTime.TryParse(fecha, out var dateParsed))
            {
                return BadRequest("Fecha inválida.");
            }

            var bloques = await ObtenerBloquesDisponiblesInterno(estilistaId, servicioId, dateParsed);
            var result = new List<string>();
            foreach (var b in bloques)
            {
                result.Add(b.ToString(@"hh\:mm"));
            }

            return Json(result);
        }

        // GET: Citas/ObtenerDisponibilidadMes
        [HttpGet]
        public async Task<IActionResult> ObtenerDisponibilidadMes(int estilistaId, int servicioId, int anio, int mes)
        {
            if (anio < DateTime.Today.Year || mes < 1 || mes > 12)
            {
                return BadRequest("Mes o año inválidos.");
            }

            var result = new List<object>();
            int diasEnMes = DateTime.DaysInMonth(anio, mes);

            // Obtener disponibilidad semanal
            var dtDisp = await _dbHelper.ExecuteQueryAsync(
                "SELECT dia FROM disponibilidad WHERE IdEstilista = @estilistaId",
                new SqlParameter("@estilistaId", estilistaId));
            
            var diasTrabajados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DataRow row in dtDisp.Rows)
            {
                diasTrabajados.Add(row["dia"].ToString() ?? string.Empty);
            }

            for (int dia = 1; dia <= diasEnMes; dia++)
            {
                var fecha = new DateTime(anio, mes, dia);
                
                // Si la fecha es anterior a hoy, está inactiva
                if (fecha < DateTime.Today)
                {
                    result.Add(new { fecha = fecha.ToString("yyyy-MM-dd"), estado = "inactivo" });
                    continue;
                }

                // Si es fin de semana (Sábado o Domingo)
                if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
                {
                    result.Add(new { fecha = fecha.ToString("yyyy-MM-dd"), estado = "inactivo" });
                    continue;
                }

                // Si el estilista no trabaja este día de la semana
                string nombreDia = ObtenerNombreDiaEspanol(fecha.DayOfWeek);
                if (!diasTrabajados.Contains(nombreDia))
                {
                    result.Add(new { fecha = fecha.ToString("yyyy-MM-dd"), estado = "inactivo" });
                    continue;
                }

                // Calcular bloques disponibles para ese día
                var bloques = await ObtenerBloquesDisponiblesInterno(estilistaId, servicioId, fecha);

                if (bloques.Count > 0)
                {
                    result.Add(new { fecha = fecha.ToString("yyyy-MM-dd"), estado = "disponible" });
                }
                else
                {
                    result.Add(new { fecha = fecha.ToString("yyyy-MM-dd"), estado = "agotado" });
                }
            }

            return Json(result);
        }

        private string ObtenerNombreDiaEspanol(DayOfWeek day)
        {
            switch (day)
            {
                case DayOfWeek.Monday: return "Lunes";
                case DayOfWeek.Tuesday: return "Martes";
                case DayOfWeek.Wednesday: return "Miercoles";
                case DayOfWeek.Thursday: return "Jueves";
                case DayOfWeek.Friday: return "Viernes";
                default: return "";
            }
        }

        private async Task<bool> ValidarCitaDisponible(int estilistaId, int servicioId, DateTime fecha, TimeSpan hora)
        {
            if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            if (fecha.Date < DateTime.Today)
            {
                return false;
            }
            if (fecha.Date == DateTime.Today && hora <= DateTime.Now.TimeOfDay)
            {
                return false;
            }

            var dtServ = await _dbHelper.ExecuteQueryAsync(
                "SELECT duracion FROM servicios WHERE Id = @servicioId",
                new SqlParameter("@servicioId", servicioId));
            if (dtServ.Rows.Count == 0) return false;
            int duracionServicio = Convert.ToInt32(dtServ.Rows[0]["duracion"]);

            string nombreDia = ObtenerNombreDiaEspanol(fecha.DayOfWeek);
            var dtDisp = await _dbHelper.ExecuteQueryAsync(
                "SELECT hora_inicio, hora_fin FROM disponibilidad WHERE IdEstilista = @estilistaId AND dia = @dia",
                new SqlParameter("@estilistaId", estilistaId),
                new SqlParameter("@dia", nombreDia));
            if (dtDisp.Rows.Count == 0) return false;

            var rowDisp = dtDisp.Rows[0];
            var horaInicio = (TimeSpan)rowDisp["hora_inicio"];
            var horaFin = (TimeSpan)rowDisp["hora_fin"];

            if (hora < horaInicio || (hora + TimeSpan.FromMinutes(duracionServicio)) > horaFin)
            {
                return false;
            }

            var dtCitas = await _dbHelper.ExecuteQueryAsync(
                @"SELECT c.HoraCita, s.duracion 
                  FROM citas c 
                  JOIN servicios s ON c.IdServicio = s.Id 
                  WHERE c.IdEstilista = @estilistaId AND c.FechaCita = @fecha",
                new SqlParameter("@estilistaId", estilistaId),
                new SqlParameter("@fecha", fecha.Date));

            var S = hora;
            var N = duracionServicio;

            foreach (DataRow row in dtCitas.Rows)
            {
                var A = (TimeSpan)row["HoraCita"];
                var D = Convert.ToInt32(row["duracion"]);

                if (S < (A + TimeSpan.FromMinutes(D)) && (S + TimeSpan.FromMinutes(N)) > A)
                {
                    return false;
                }
            }

            return true;
        }

        private async Task<List<TimeSpan>> ObtenerBloquesDisponiblesInterno(int estilistaId, int servicioId, DateTime fecha)
        {
            var list = new List<TimeSpan>();
            
            if (fecha.DayOfWeek == DayOfWeek.Saturday || fecha.DayOfWeek == DayOfWeek.Sunday)
            {
                return list;
            }

            if (fecha.Date < DateTime.Today)
            {
                return list;
            }

            var dtServ = await _dbHelper.ExecuteQueryAsync(
                "SELECT duracion FROM servicios WHERE Id = @servicioId",
                new SqlParameter("@servicioId", servicioId));
            if (dtServ.Rows.Count == 0) return list;
            int duracionServicio = Convert.ToInt32(dtServ.Rows[0]["duracion"]);

            string nombreDia = ObtenerNombreDiaEspanol(fecha.DayOfWeek);
            var dtDisp = await _dbHelper.ExecuteQueryAsync(
                "SELECT hora_inicio, hora_fin FROM disponibilidad WHERE IdEstilista = @estilistaId AND dia = @dia",
                new SqlParameter("@estilistaId", estilistaId),
                new SqlParameter("@dia", nombreDia));
            if (dtDisp.Rows.Count == 0) return list;

            var rowDisp = dtDisp.Rows[0];
            var horaInicio = (TimeSpan)rowDisp["hora_inicio"];
            var horaFin = (TimeSpan)rowDisp["hora_fin"];

            var dtCitas = await _dbHelper.ExecuteQueryAsync(
                @"SELECT c.HoraCita, s.duracion 
                  FROM citas c 
                  JOIN servicios s ON c.IdServicio = s.Id 
                  WHERE c.IdEstilista = @estilistaId AND c.FechaCita = @fecha",
                new SqlParameter("@estilistaId", estilistaId),
                new SqlParameter("@fecha", fecha.Date));

            var citasExistentes = new List<(TimeSpan inicio, TimeSpan fin)>();
            foreach (DataRow row in dtCitas.Rows)
            {
                var A = (TimeSpan)row["HoraCita"];
                var D = Convert.ToInt32(row["duracion"]);
                citasExistentes.Add((A, A + TimeSpan.FromMinutes(D)));
            }

            var N = duracionServicio;
            var actual = horaInicio;

            while ((actual + TimeSpan.FromMinutes(N)) <= horaFin)
            {
                var S = actual;
                var endSlot = S + TimeSpan.FromMinutes(N);

                if (fecha.Date == DateTime.Today && S <= DateTime.Now.TimeOfDay)
                {
                    actual = actual.Add(TimeSpan.FromMinutes(30));
                    continue;
                }

                bool colisiona = false;
                foreach (var cita in citasExistentes)
                {
                    if (S < cita.fin && endSlot > cita.inicio)
                    {
                        colisiona = true;
                        break;
                    }
                }

                if (!colisiona)
                {
                    list.Add(S);
                }

                actual = actual.Add(TimeSpan.FromMinutes(30));
            }

            return list;
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
                
                // ValidaciÃ³n bÃ¡sica de propiedad
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
            // 1. Obtener lista de categorías
            var dtCategorias = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre FROM categoria ORDER BY nombre");
            var categoriasList = new List<object>();
            foreach (DataRow row in dtCategorias.Rows)
            {
                categoriasList.Add(new { Id = Convert.ToInt32(row["Id"]), Nombre = row["nombre"].ToString() });
            }

            // 2. Obtener lista de estilistas
            var dtEstilistas = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, apellido FROM estilistas ORDER BY nombre");
            var estilistasList = new List<object>();
            foreach (DataRow row in dtEstilistas.Rows)
            {
                estilistasList.Add(new { Id = Convert.ToInt32(row["Id"]), Nombre = $"{row["nombre"]} {row["apellido"]}" });
            }

            // 3. Obtener lista de servicios
            var dtServicios = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre, Id_categoria, IdEstilista, duracion, precio FROM servicios ORDER BY nombre");
            var serviciosList = new List<object>();
            foreach (DataRow row in dtServicios.Rows)
            {
                serviciosList.Add(new { 
                    Id = Convert.ToInt32(row["Id"]), 
                    Nombre = row["nombre"].ToString(),
                    IdCategoria = Convert.ToInt32(row["Id_categoria"]),
                    IdEstilista = row.IsNull("IdEstilista") ? (int?)null : Convert.ToInt32(row["IdEstilista"]),
                    Duracion = Convert.ToInt32(row["duracion"]),
                    Precio = Convert.ToDecimal(row["precio"])
                });
            }

            ViewBag.CategoriasJson = System.Text.Json.JsonSerializer.Serialize(categoriasList);
            ViewBag.EstilistasJson = System.Text.Json.JsonSerializer.Serialize(estilistasList);
            ViewBag.ServiciosJson = System.Text.Json.JsonSerializer.Serialize(serviciosList);

            // Inicializar listas vacías para el modelo (se llenarán mediante Javascript)
            ViewData["IdServicio"] = new SelectList(new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(), "Value", "Text");
            ViewData["IdEstilista"] = new SelectList(new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>(), "Value", "Text");
        }
    }
}
