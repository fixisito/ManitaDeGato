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
using manitaDeGatoWeb.Services;
using Transbank.Common;
using Transbank.Webpay.Common;
using Transbank.Webpay.WebpayPlus;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize]
    public class CitasController : Controller
    {
        private readonly DataBaseHelper _dbHelper;
        private readonly EmailService _emailService;

        public CitasController(DataBaseHelper dbHelper, EmailService emailService)
        {
            _dbHelper = dbHelper;
            _emailService = emailService;
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
                    ModelState.AddModelError("", "El horario o fecha seleccionados ya no estAn disponibles. Por favor, selecciona otro bloque.");
                    await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
                    return View(cita);
                }

                var clienteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // Obtener el precio neto del servicio
                var precioRaw = await _dbHelper.ExecuteScalarAsync(
                    "SELECT precio FROM servicios WHERE Id = @servicioId",
                    new SqlParameter("@servicioId", cita.IdServicio));

                if (precioRaw == null)
                {
                    ModelState.AddModelError("", "El servicio seleccionado no es válido.");
                    await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
                    return View(cita);
                }

                decimal precioNeto = Convert.ToDecimal(precioRaw);
                int precioTotalConIva = (int)Math.Round(precioNeto * 1.19m);

                // Insertar cita en estado 'Pendiente' y recuperar el ID generado
                var citaIdObj = await _dbHelper.ExecuteScalarAsync(
                    @"INSERT INTO citas (IdCliente, IdServicio, IdEstilista, FechaCita, HoraCita, estado) 
                      VALUES (@clienteId, @servicioId, @estilistaId, @fecha, @hora, 'Pendiente');
                      SELECT SCOPE_IDENTITY();",
                    new SqlParameter("@clienteId", clienteId),
                    new SqlParameter("@servicioId", cita.IdServicio),
                    new SqlParameter("@estilistaId", cita.IdEstilista),
                    new SqlParameter("@fecha", cita.FechaCita),
                    new SqlParameter("@hora", cita.HoraCita));

                if (citaIdObj == null)
                {
                    ModelState.AddModelError("", "Ocurrió un error al registrar la cita.");
                    await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
                    return View(cita);
                }

                int citaId = Convert.ToInt32(citaIdObj);

                // Crear transacción en Transbank Webpay Plus
                try
                {
                    var returnUrl = Url.Action("PagoRetorno", "Citas", null, Request.Scheme);
                    var tx = new Transaction(new Options(
                        IntegrationCommerceCodes.WEBPAY_PLUS,
                        IntegrationApiKeys.WEBPAY,
                        WebpayIntegrationType.Test
                    ));
                    var response = tx.Create(
                        buyOrder: citaId.ToString(),
                        sessionId: "session-" + citaId,
                        amount: precioTotalConIva,
                        returnUrl: returnUrl!
                    );

                    // Redirigir al portal de Webpay Plus de Transbank
                    return Redirect($"{response.Url}?token_ws={response.Token}");
                }
                catch (Exception ex)
                {
                    // Si falla la llamada a Transbank, dejamos la cita como 'Fallida'
                    await _dbHelper.ExecuteNonQueryAsync(
                        "UPDATE citas SET estado = 'Fallida' WHERE Id = @id",
                        new SqlParameter("@id", citaId));

                    ModelState.AddModelError("", "Error al conectar con el servicio de pagos: " + ex.Message);
                    await CargarServiciosYEstilistasEnViewBag(cita.IdServicio, cita.IdEstilista);
                    return View(cita);
                }
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
                  WHERE c.IdEstilista = @estilistaId AND c.FechaCita = @fecha AND c.estado <> 'Cancelada'",
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
                  WHERE c.IdEstilista = @estilistaId AND c.FechaCita = @fecha AND c.estado <> 'Cancelada'",
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

                await _dbHelper.ExecuteNonQueryAsync("UPDATE citas SET estado = 'Cancelada' WHERE Id = @id", new SqlParameter("@id", id));
            }

            if (User.IsInRole("Cliente")) return RedirectToAction(nameof(Historial));
            if (User.IsInRole("Estilista")) return RedirectToAction(nameof(MisCitas));
            return RedirectToAction(nameof(Index));
        }

        // POST: Citas/LimpiarHistorial
        [HttpPost]
        [Authorize(Roles = "Cliente,Administrador")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LimpiarHistorial()
        {
            var isCliente = User.IsInRole("Cliente");
            var isAdmin = User.IsInRole("Administrador");
            
            var today = DateTime.Today;
            var nowTime = DateTime.Now.TimeOfDay;

            string query = "";
            var parameters = new List<SqlParameter>
            {
                new SqlParameter("@today", today),
                new SqlParameter("@nowTime", nowTime)
            };

            if (isCliente)
            {
                var clienteId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                query = @"
                    DELETE FROM citas 
                    WHERE IdCliente = @clienteId 
                      AND (estado = 'Cancelada' 
                           OR FechaCita < @today 
                           OR (FechaCita = @today AND HoraCita < @nowTime))";
                parameters.Add(new SqlParameter("@clienteId", clienteId));
            }
            else if (isAdmin)
            {
                query = @"
                    DELETE FROM citas 
                    WHERE estado = 'Cancelada' 
                       OR FechaCita < @today 
                       OR (FechaCita = @today AND HoraCita < @nowTime)";
            }

            if (!string.IsNullOrEmpty(query))
            {
                int rowsDeleted = await _dbHelper.ExecuteNonQueryAsync(query, parameters.ToArray());
                TempData["MensajeExito"] = isCliente 
                    ? "Historial de citas limpiado correctamente." 
                    : $"Historial global depurado. Se eliminaron {rowsDeleted} citas pasadas o canceladas.";
            }

            if (isCliente) return RedirectToAction(nameof(Historial));
            return RedirectToAction(nameof(Index));
        }

        // GET/POST: Citas/PagoRetorno
        [AllowAnonymous]
        public async Task<IActionResult> PagoRetorno(string token_ws, string TBK_TOKEN, string TBK_ORDEN_COMPRA, string TBK_ID_SESION)
        {
            if (!string.IsNullOrEmpty(TBK_TOKEN) || string.IsNullOrEmpty(token_ws))
            {
                string orderIdStr = TBK_ORDEN_COMPRA ?? TBK_ID_SESION?.Replace("session-", "");
                if (int.TryParse(orderIdStr, out int citaIdCancelada))
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "UPDATE citas SET estado = 'Cancelada' WHERE Id = @id AND estado = 'Pendiente'",
                        new SqlParameter("@id", citaIdCancelada));
                }

                return RedirectToAction(nameof(PagoFallido), new { mensaje = "El pago fue anulado por el usuario." });
            }

            try
            {
                var tx = new Transaction(new Options(
                    IntegrationCommerceCodes.WEBPAY_PLUS,
                    IntegrationApiKeys.WEBPAY,
                    WebpayIntegrationType.Test
                ));
                var response = tx.Commit(token_ws);

                int citaId = Convert.ToInt32(response.BuyOrder);

                if (response.ResponseCode == 0 && response.Status == "AUTHORIZED")
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "UPDATE citas SET estado = 'Confirmada' WHERE Id = @id",
                        new SqlParameter("@id", citaId));

                    // Obtener datos detallados de la cita para el correo
                    try
                    {
                        var dtCorreo = await _dbHelper.ExecuteQueryAsync(
                            @"SELECT c.FechaCita, c.HoraCita, s.nombre AS ServicioNombre, s.precio AS ServicioPrecio,
                                     cl.nombre + ' ' + cl.apellido AS ClienteNombre, cl.correo AS ClienteCorreo,
                                     e.nombre + ' ' + e.apellido AS EstilistaNombre, e.correo AS EstilistaCorreo
                              FROM citas c
                              INNER JOIN servicios s ON c.IdServicio = s.Id
                              INNER JOIN clientes cl ON c.IdCliente = cl.Id
                              INNER JOIN estilistas e ON c.IdEstilista = e.Id
                              WHERE c.Id = @citaId",
                            new SqlParameter("@citaId", citaId));

                        if (dtCorreo.Rows.Count > 0)
                        {
                            var row = dtCorreo.Rows[0];
                            var fechaC = Convert.ToDateTime(row["FechaCita"]).ToString("dd/MM/yyyy");
                            var horaC = ((TimeSpan)row["HoraCita"]).ToString(@"hh\:mm");
                            var servicioN = row["ServicioNombre"].ToString();
                            var estilistaN = row["EstilistaNombre"].ToString();
                            var clienteN = row["ClienteNombre"].ToString();
                            
                            var clienteEmail = row["ClienteCorreo"].ToString();
                            var estilistaEmail = row["EstilistaCorreo"].ToString();

                            var precioN = Convert.ToDecimal(row["ServicioPrecio"]);
                            var precioIva = (int)Math.Round(precioN * 1.19m);
                            var precioFormateado = precioIva.ToString("C0", new System.Globalization.CultureInfo("es-CL"));

                            // Enviar correo al Cliente
                            string clienteSubject = "Confirmación de tu Cita - Manita de Gato";
                            string clienteBody = $@"
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;'>
                                <div style='text-align: center; border-bottom: 2px solid #db2777; padding-bottom: 15px; margin-bottom: 20px;'>
                                    <h2 style='color: #db2777; margin: 0;'>Manita de Gato</h2>
                                    <p style='color: #6b7280; font-size: 14px; margin: 5px 0 0 0;'>Centro de Estética & Belleza</p>
                                </div>
                                <h3 style='color: #111827;'>¡Hola {clienteN}!</h3>
                                <p style='color: #374151; line-height: 1.6;'>Te confirmamos que hemos recibido tu pago a través de Webpay Plus y tu cita ha sido reservada con éxito. A continuación te presentamos el detalle:</p>
                                
                                <div style='background-color: #f9fafb; padding: 15px; border-radius: 6px; border-left: 4px solid #db2777; margin: 20px 0;'>
                                    <table style='width: 100%; border-collapse: collapse;'>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Servicio:</td>
                                            <td style='padding: 6px 0; color: #111827; font-weight: bold;'>{servicioN}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Estilista:</td>
                                            <td style='padding: 6px 0; color: #111827;'>{estilistaN}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Fecha:</td>
                                            <td style='padding: 6px 0; color: #111827;'>{fechaC}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Hora:</td>
                                            <td style='padding: 6px 0; color: #111827;'>{horaC} hrs</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Monto Pagado:</td>
                                            <td style='padding: 6px 0; color: #db2777; font-weight: bold; font-size: 16px;'>{precioFormateado} (IVA Incluido)</td>
                                        </tr>
                                    </table>
                                </div>
                                
                                <p style='color: #374151; line-height: 1.6;'>Si necesitas reprogramar o cancelar, por favor ponte en contacto con nosotros al menos con 24 horas de anticipación.</p>
                                <p style='color: #374151;'>¡Te esperamos en nuestro salón!</p>
                                
                                <div style='border-top: 1px solid #e5e7eb; padding-top: 15px; margin-top: 25px; text-align: center; color: #9ca3af; font-size: 12px;'>
                                    <p style='margin: 0;'>Manita de Gato Estética</p>
                                    <p style='margin: 5px 0 0 0;'>Santiago, Chile</p>
                                </div>
                            </div>";

                            _ = Task.Run(() => _emailService.EnviarCorreoAsync(clienteEmail!, clienteN!, clienteSubject, clienteBody));

                            // Enviar correo al Estilista
                            string estilistaSubject = "Nueva Cita Agendada - Manita de Gato";
                            string estilistaBody = $@"
                            <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px; background-color: #ffffff;'>
                                <div style='text-align: center; border-bottom: 2px solid #db2777; padding-bottom: 15px; margin-bottom: 20px;'>
                                    <h2 style='color: #db2777; margin: 0;'>Manita de Gato</h2>
                                    <p style='color: #6b7280; font-size: 14px; margin: 5px 0 0 0;'>Notificación para Estilistas</p>
                                </div>
                                <h3 style='color: #111827;'>Hola {estilistaN},</h3>
                                <p style='color: #374151; line-height: 1.6;'>Se ha agendado y confirmado un nuevo servicio en tu agenda a través del portal de pagos. Aquí tienes los detalles:</p>
                                
                                <div style='background-color: #f9fafb; padding: 15px; border-radius: 6px; border-left: 4px solid #db2777; margin: 20px 0;'>
                                    <table style='width: 100%; border-collapse: collapse;'>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Cliente:</td>
                                            <td style='padding: 6px 0; color: #111827; font-weight: bold;'>{clienteN}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Servicio:</td>
                                            <td style='padding: 6px 0; color: #111827; font-weight: bold;'>{servicioN}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Fecha:</td>
                                            <td style='padding: 6px 0; color: #111827;'>{fechaC}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 6px 0; color: #6b7280; font-size: 14px;'>Hora:</td>
                                            <td style='padding: 6px 0; color: #111827;'>{horaC} hrs</td>
                                        </tr>
                                    </table>
                                </div>
                                
                                <p style='color: #374151; line-height: 1.6;'>Por favor, recuerda estar listo 10 minutos antes del bloque agendado.</p>
                                
                                <div style='border-top: 1px solid #e5e7eb; padding-top: 15px; margin-top: 25px; text-align: center; color: #9ca3af; font-size: 12px;'>
                                    <p style='margin: 0;'>Manita de Gato Estética</p>
                                </div>
                            </div>";

                            _ = Task.Run(() => _emailService.EnviarCorreoAsync(estilistaEmail!, estilistaN!, estilistaSubject, estilistaBody));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[EmailService] Error al disparar hilos de correos: " + ex.Message);
                    }

                    return RedirectToAction(nameof(PagoExitoso), new 
                    { 
                        citaId = citaId,
                        authorizationCode = response.AuthorizationCode,
                        amount = response.Amount,
                        cardNumber = response.CardDetail?.CardNumber,
                        paymentType = response.PaymentTypeCode,
                        transDate = response.TransactionDate
                    });
                }
                else
                {
                    await _dbHelper.ExecuteNonQueryAsync(
                        "UPDATE citas SET estado = 'Fallida' WHERE Id = @id",
                        new SqlParameter("@id", citaId));

                    string errorMsg = response.ResponseCode switch
                    {
                        -1 => "Rechazo de transacción.",
                        -2 => "Transacción rechazada por el emisor.",
                        -3 => "Error en transacción.",
                        -4 => "Rechazada por emisor.",
                        -5 => "Rechazada por emisor.",
                        _ => "Transacción rechazada por Transbank."
                    };

                    return RedirectToAction(nameof(PagoFallido), new { mensaje = errorMsg });
                }
            }
            catch (Exception ex)
            {
                return RedirectToAction(nameof(PagoFallido), new { mensaje = "Ocurrió un error al procesar el pago: " + ex.Message });
            }
        }

        // GET: Citas/PagoExitoso
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> PagoExitoso(int citaId, string authorizationCode, int amount, string cardNumber, string paymentType, DateTime? transDate)
        {
            var dt = await _dbHelper.ExecuteQueryAsync(
                @"SELECT c.Id, c.FechaCita, c.HoraCita, s.nombre AS ServicioNombre, s.duracion AS ServicioDuracion, 
                         e.nombre + ' ' + e.apellido AS EstilistaNombre
                  FROM citas c
                  INNER JOIN servicios s ON c.IdServicio = s.Id
                  INNER JOIN estilistas e ON c.IdEstilista = e.Id
                  WHERE c.Id = @citaId",
                new SqlParameter("@citaId", citaId));

            if (dt.Rows.Count == 0)
            {
                return NotFound();
            }

            var row = dt.Rows[0];
            ViewBag.CitaId = row["Id"];
            ViewBag.FechaCita = Convert.ToDateTime(row["FechaCita"]);
            ViewBag.HoraCita = (TimeSpan)row["HoraCita"];
            ViewBag.ServicioNombre = row["ServicioNombre"].ToString();
            ViewBag.ServicioDuracion = row["ServicioDuracion"];
            ViewBag.EstilistaNombre = row["EstilistaNombre"].ToString();

            ViewBag.AuthorizationCode = authorizationCode;
            ViewBag.Amount = amount;
            ViewBag.CardNumber = cardNumber;
            ViewBag.PaymentType = paymentType;
            ViewBag.TransDate = transDate ?? DateTime.Now;

            return View();
        }

        // GET: Citas/PagoFallido
        [Authorize(Roles = "Cliente")]
        public IActionResult PagoFallido(string mensaje)
        {
            ViewBag.ErrorMessage = mensaje;
            return View();
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
