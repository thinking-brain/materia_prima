﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MateriasPrimasApp.Data;
using MateriasPrimasApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using MateriasPrimasApp.HelperClass;
using MateriasPrimasApp.ViewModels;

namespace MateriasPrimasApp.Controllers
{
    [Authorize]
    public class ReportesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ReportesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["UebId"] = new SelectList(await _context.UEB.ToListAsync(), "Id", "Nombre");
            return View();
        }

        // GET: Procesamientos
        public async Task<IActionResult> ExistenciasPorUeb(int Id)
        {
            var helper = new ReporteExistenciaHelper(_context);
            var ueb = await _context.UEB.FindAsync(Id);
            ViewBag.UEB = ueb.Nombre;
            return View(helper.GetExistencias(Id));
        }

        public async Task<IActionResult> ConciliacionVentas()
        {
            ViewBag.Ueb = new SelectList(_context.Set<UEB>(), "Id", "Nombre");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ConciliacionVentas(ParametroVentasVM parametros)
        {
            var result = new List<ConciliacionVentasVM>();
            var ventas = _context.Set<DetalleDeVenta>()
                .Include(v => v.Producto.Unidad)
                .Include(v => v.Venta)
                .Where(v => v.Venta.Fecha.Year == parametros.Año);
            result = ventas.GroupBy(v => v.Producto).Select(v => new ConciliacionVentasVM
            {
                Producto = v.Key.Nombre,
                Um = v.Key.Unidad.Descripcion,
                VentaMn = v.Where(d => d.Venta.Fecha.Month == parametros.Mes).Sum(d => d.PrecioVentaMn * d.Cantidad),
                VentaCuc = v.Where(d => d.Venta.Fecha.Month == parametros.Mes).Sum(d => d.PrecioVentaMlc * d.Cantidad),
                AcumuladoMn = v.Sum(d => d.PrecioVentaMn * d.Cantidad),
                AcumuladoCuc = v.Sum(d => d.PrecioVentaMlc * d.Cantidad),
            }).ToList();
            ViewBag.Ueb = _context.Set<UEB>().SingleOrDefault(u => u.Id == parametros.Ueb).Nombre;
            ViewBag.Mes = $"{parametros.Mes}/{parametros.Año}";
            return View("ConciliacionVentasData", result);
        }

        public async Task<IActionResult> GraficoVentas()
        {
            ViewBag.Ueb = new SelectList(_context.Set<UEB>(), "Id", "Nombre");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GraficoVentas(ParametroVentasVM parametros)
        {
            var ventas = _context.Set<Venta>()
                .Include(v => v.DetallesDeVenta)
                .Where(v => v.Fecha.Year == parametros.Año)
                .GroupBy(v => v.Fecha.Month)
                .Select(v => new
                {
                    Mes = v.Key,
                    Ventas = v.Sum(d => d.DetallesDeVenta.Sum(e => e.Cantidad * e.PrecioVentaMlc)) + v.Sum(d => d.DetallesDeVenta.Sum(e => e.Cantidad * e.PrecioVentaMn)),
                }).ToList();
            var compras = _context.Set<Entrada>()
                .Include(c => c.DetallesDeEntrada)
                .Where(c => c.Fecha.Year == parametros.Año)
                .GroupBy(v => v.Fecha.Month)
                .Select(v => new
                {
                    Mes = v.Key,
                    Compras = v.Sum(d => d.DetallesDeEntrada.Sum(e => e.Cantidad * e.PrecioMlc)) + v.Sum(d => d.DetallesDeEntrada.Sum(e => e.Cantidad * e.PrecioMn)),
                }).ToList();

            var result = ventas.Join(compras, v => v.Mes, c => c.Mes, (v, c) => new
            {
                Mes = v.Mes,
                Ventas = v.Ventas,
                Compras = c.Compras,
            }).ToList();


            var labels = result.OrderBy(r => r.Mes).Select(r => r.Mes + "/" + parametros.Año).ToList();
            var datosCosto = new DatosGraficas()
            {
                Labels = labels,
            };
            var datosVentas = new DatosGraficas()
            {
                Labels = labels,
            };
            var index = 0;
            foreach (var item in labels)
            {
                datosVentas.Datasets.Add(new Dataset
                {
                    Label = "Ventas",
                    BackgroundColor = "#f3f3f3",
                    BorderColor = "#f3f3f3",
                    Fill = false,
                    Data = labels.Select(c => ventas.Any(d => d.Mes + "/" + parametros.Año == c) ? ventas.Where(d => d.Mes + "/" + parametros.Año == c).Sum(s => s.Ventas) : 0).ToList()
                });
                datosVentas.Datasets.Add(new Dataset
                {
                    Label = "Compras",
                    BackgroundColor = "#b4b4b4",
                    BorderColor = "#b4b4b4",
                    Fill = false,
                    Data = labels.Select(c => compras.Any(d => d.Mes + "/" + parametros.Año == c) ? compras.Where(d => d.Mes + "/" + parametros.Año == c).Sum(s => s.Compras) : 0).ToList()
                });
            }
            ViewBag.Ueb = new SelectList(_context.Set<UEB>(), "Id", "Nombre");
            ViewBag.Ventas = datosVentas;
            return View();
        }
    }
}
