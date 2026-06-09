using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Data;
using System.Data.SqlClient;
using manitaDeGatoWeb.Models;
using manitaDeGatoWeb.Data;

namespace manitaDeGatoWeb.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class CategoriasController : Controller
    {
        private readonly DataBaseHelper _dbHelper;

        public CategoriasController(DataBaseHelper dbHelper)
        {
            _dbHelper = dbHelper;
        }

        public async Task<IActionResult> Index()
        {
            var categoriesList = new List<Categoria>();
            
            var dtCategories = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre FROM categoria ORDER BY nombre");
            foreach (DataRow rowCat in dtCategories.Rows)
            {
                var catId = Convert.ToInt32(rowCat["Id"]);
                var catName = rowCat["nombre"].ToString() ?? string.Empty;
                
                var categoria = new Categoria
                {
                    Id = catId,
                    Nombre = catName,
                    Servicios = new List<Servicio>()
                };

                var dtServices = await _dbHelper.ExecuteQueryAsync(
                    "SELECT Id, nombre, precio, duracion, Id_categoria, descripcion, IdEstilista FROM servicios WHERE Id_categoria = @catId",
                    new SqlParameter("@catId", catId));

                foreach (DataRow rowSer in dtServices.Rows)
                {
                    categoria.Servicios.Add(new Servicio
                    {
                        Id = Convert.ToInt32(rowSer["Id"]),
                        Nombre = rowSer["nombre"].ToString() ?? string.Empty,
                        Precio = Convert.ToDecimal(rowSer["precio"]),
                        Duracion = Convert.ToInt32(rowSer["duracion"]),
                        Id_categoria = catId,
                        Descripcion = rowSer["descripcion"].ToString() ?? string.Empty,
                        IdEstilista = rowSer["IdEstilista"] == DBNull.Value ? null : Convert.ToInt32(rowSer["IdEstilista"])
                    });
                }

                categoriesList.Add(categoria);
            }

            return View(categoriesList);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nombre")] Categoria categoria)
        {
            if (ModelState.IsValid)
            {
                await _dbHelper.ExecuteNonQueryAsync(
                    "INSERT INTO categoria (nombre) VALUES (@nombre)",
                    new SqlParameter("@nombre", categoria.Nombre));
                return RedirectToAction(nameof(Index));
            }
            return View(categoria);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var dt = await _dbHelper.ExecuteQueryAsync("SELECT Id, nombre FROM categoria WHERE Id = @id", new SqlParameter("@id", id));
            if (dt.Rows.Count == 0) return NotFound();

            var row = dt.Rows[0];
            var categoria = new Categoria
            {
                Id = Convert.ToInt32(row["Id"]),
                Nombre = row["nombre"].ToString() ?? string.Empty
            };

            return View(categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Nombre")] Categoria categoria)
        {
            if (id != categoria.Id) return NotFound();

            if (ModelState.IsValid)
            {
                await _dbHelper.ExecuteNonQueryAsync(
                    "UPDATE categoria SET nombre = @nombre WHERE Id = @id",
                    new SqlParameter("@nombre", categoria.Nombre),
                    new SqlParameter("@id", id));
                return RedirectToAction(nameof(Index));
            }
            return View(categoria);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                await _dbHelper.ExecuteNonQueryAsync("DELETE FROM categoria WHERE Id = @id", new SqlParameter("@id", id));
            }
            catch (SqlException ex) when (ex.Number == 547)
            {
                TempData["MensajeError"] = "No se puede eliminar esta categoría porque tiene servicios asociados.";
                return RedirectToAction(nameof(Index));
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
