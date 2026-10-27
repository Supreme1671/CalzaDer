using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var app = builder.Build();
app.UseHttpsRedirection();

var stockLock = new object();

var modelos = new Dictionary<int, Modelo>
{
    [12] = new Modelo { Id = 12, Nombre = "Runner X", FabricanteId = 3, Fabricante = "Nike", PaisOrigen = "Vietnam" },
    [13] = new Modelo { Id = 13, Nombre = "Sprint Pro", FabricanteId = 2, Fabricante = "Adidas", PaisOrigen = "China" },
    [14] = new Modelo { Id = 14, Nombre = "Trail Max", FabricanteId = 1, Fabricante = "Puma", PaisOrigen = "Brasil" }
};

var clientes = new Dictionary<string, Cliente>
{
    ["30111222"] = new Cliente { Nombre = "Ana", Apellido = "Pérez", Dni = "30111222" },
    ["30222333"] = new Cliente { Nombre = "Luis", Apellido = "García", Dni = "30222333" }
};

var zapatillas = new Dictionary<(int ModeloId, int Talle), Zapatilla>
{
    [(12, 40)] = new Zapatilla { ModeloId = 12, Talle = 40, Stock = 12 },
    [(12, 41)] = new Zapatilla { ModeloId = 12, Talle = 41, Stock = 8 },
    [(12, 42)] = new Zapatilla { ModeloId = 12, Talle = 42, Stock = 15 },
    [(12, 43)] = new Zapatilla { ModeloId = 12, Talle = 43, Stock = 6 },
    [(13, 39)] = new Zapatilla { ModeloId = 13, Talle = 39, Stock = 7 },
    [(13, 40)] = new Zapatilla { ModeloId = 13, Talle = 40, Stock = 20 },
    [(14, 42)] = new Zapatilla { ModeloId = 14, Talle = 42, Stock = 18 }
};

var compras = new Dictionary<int, Compra>
{
    [1001] = new Compra
    {
        NumeroCompra = 1001,
        ClienteDni = "30111222",
        FechaHora = new DateTime(2026, 8, 15, 18, 0, 0, DateTimeKind.Utc),
        Lineas = new List<LineaCompra>
        {
            new() { ModeloId = 12, Talle = 42, PrecioUnitario = 18500m, Cantidad = 1 },
            new() { ModeloId = 12, Talle = 43, PrecioUnitario = 19500m, Cantidad = 2 },
            new() { ModeloId = 13, Talle = 40, PrecioUnitario = 17100m, Cantidad = 1 }
        }
    },
    [1002] = new Compra
    {
        NumeroCompra = 1002,
        ClienteDni = "30111222",
        FechaHora = new DateTime(2026, 8, 28, 20, 30, 0, DateTimeKind.Utc),
        Lineas = new List<LineaCompra>
        {
            new() { ModeloId = 13, Talle = 40, PrecioUnitario = 17100m, Cantidad = 2 }
        }
    },
    [1003] = new Compra
    {
        NumeroCompra = 1003,
        ClienteDni = "30222333",
        FechaHora = new DateTime(2026, 9, 10, 9, 15, 0, DateTimeKind.Utc),
        Lineas = new List<LineaCompra>
        {
            new() { ModeloId = 14, Talle = 42, PrecioUnitario = 16000m, Cantidad = 1 }
        }
    }
};

var devoluciones = new Dictionary<int, Devolucion>
{
    [1002] = new Devolucion
    {
        NumeroCompra = 1002,
        RazonId = 1,
        FechaHora = new DateTime(2026, 8, 29, 8, 45, 0, DateTimeKind.Utc),
        Items = new List<DevolucionItem>
        {
            new() { ModeloId = 13, Talle = 40, Cantidad = 1 }
        }
    }
};
var opiniones = new List<Opinion>
{
    new() { ModeloId = 12, FechaHora = new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc), ClienteDni = "30111222", Descripcion = "Muy cómodas", Nota = 4.8m },
    new() { ModeloId = 13, FechaHora = new DateTime(2026, 8, 21, 11, 0, 0, DateTimeKind.Utc), ClienteDni = "30111222", Descripcion = "Excelente", Nota = 4.5m }
};

var razones = new Dictionary<int, string>
{
    [1] = "Producto defectuoso",
    [2] = "Cambio de talla",
    [3] = "No le gustó el color",
    [4] = "Pedido equivocado",
    [5] = "Falla de entrega",
    [6] = "Otro"
};

app.MapPost("/compras/{numero:int}/devoluciones", (int numero, DevolucionRequest request) =>
{
    lock (stockLock)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return Results.UnprocessableEntity(new { error = "La lista de items no puede estar vacía." });
        }

        if (!compras.TryGetValue(numero, out var compra))
        {
            return Results.NotFound(new { error = $"La compra {numero} no existe." });
        }

        if (devoluciones.ContainsKey(numero))
        {
            return Results.Conflict(new { error = $"La compra {numero} ya tiene una devolución asociada." });
        }

        if (!razones.ContainsKey(request.RazonId))
        {
            return Results.UnprocessableEntity(new { error = $"La razón {request.RazonId} no existe." });
        }

        var montoReintegrado = 0m;
        var itemsRegistrados = new List<DevolucionItem>();

        foreach (var item in request.Items)
        {
            if (item.Cantidad <= 0)
            {
                return Results.UnprocessableEntity(new { error = $"La cantidad del item con modelo {item.ModeloId} y talle {item.Talle} debe ser mayor a cero." });
            }

            var linea = compra.Lineas.FirstOrDefault(l => l.ModeloId == item.ModeloId && l.Talle == item.Talle);
            if (linea == null)
            {
                return Results.UnprocessableEntity(new { error = $"El ítem (modelo_id={item.ModeloId}, talle={item.Talle}) no pertenece a la compra {numero}." });
            }

            if (item.Cantidad > linea.Cantidad)
            {
                return Results.UnprocessableEntity(new { error = $"La cantidad del ítem (modelo_id={item.ModeloId}, talle={item.Talle}) excede lo comprado." });
            }

            var clave = (item.ModeloId, item.Talle);
            if (!zapatillas.ContainsKey(clave))
            {
                return Results.UnprocessableEntity(new { error = $"La zapatilla (modelo_id={item.ModeloId}, talle={item.Talle}) no está registrada." });
            }

            var stockActual = zapatillas[clave].Stock;
            var nuevoStock = Math.Min(500, stockActual + item.Cantidad);
            zapatillas[clave] = new Zapatilla { ModeloId = item.ModeloId, Talle = item.Talle, Stock = nuevoStock };

            itemsRegistrados.Add(new DevolucionItem { ModeloId = item.ModeloId, Talle = item.Talle, Cantidad = item.Cantidad });
            montoReintegrado += linea.PrecioUnitario * item.Cantidad;
        }

        var devolucion = new Devolucion
        {
            NumeroCompra = numero,
            RazonId = request.RazonId,
            FechaHora = DateTime.UtcNow,
            Items = itemsRegistrados
        };

        devoluciones[numero] = devolucion;

        return Results.Created($"/compras/{numero}/devoluciones", new
        {
            numero_compra = numero,
            razon_id = devolucion.RazonId,
            monto_reintegrado = montoReintegrado,
            items = devolucion.Items.Select(i => new { modelo_id = i.ModeloId, talle = i.Talle, cantidad = i.Cantidad })
        });
    }
})
.WithName("RegistrarDevolucion");

app.MapPost("/modelos/{id:int}/opiniones", (int id, OpinionRequest request) =>
{
    if (!modelos.ContainsKey(id))
    {
        return Results.NotFound(new { error = $"El modelo {id} no existe." });
    }

    if (!clientes.ContainsKey(request.ClienteDni))
    {
        return Results.NotFound(new { error = $"El cliente {request.ClienteDni} no existe." });
    }

    if (request.Nota < 0m || request.Nota > 5m)
    {
        return Results.UnprocessableEntity(new { error = "La nota debe estar entre 0 y 5." });
    }

    if (!string.IsNullOrEmpty(request.Descripcion) && request.Descripcion.Length > 45)
    {
        return Results.UnprocessableEntity(new { error = "La descripción no puede superar 45 caracteres." });
    }

    var compróModelo = compras.Values.Any(c => c.ClienteDni == request.ClienteDni && c.Lineas.Any(l => l.ModeloId == id));
    if (!compróModelo)
    {
        return Results.StatusCode(403);
    }

    var fechaHora = DateTime.UtcNow;
    if (opiniones.Any(o => o.ModeloId == id && o.ClienteDni == request.ClienteDni && o.FechaHora == fechaHora))
    {
        return Results.Conflict(new { error = "Ya existe una opinión duplicada para este modelo, horario y cliente." });
    }

    opiniones.Add(new Opinion
    {
        ModeloId = id,
        FechaHora = fechaHora,
        ClienteDni = request.ClienteDni,
        Descripcion = request.Descripcion,
        Nota = request.Nota
    });

    return Results.Created($"/modelos/{id}/opiniones", new
    {
        modelo_id = id,
        cliente_dni = request.ClienteDni,
        descripcion = request.Descripcion,
        nota = request.Nota,
        fecha_hora = fechaHora
    });
});

app.MapGet("/modelos/ranking", ([AsParameters] RankingQuery query) =>
{
    var orden = string.Equals(query.Orden, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
    var minOpiniones = query.MinOpiniones is null || query.MinOpiniones <= 0 ? 1 : query.MinOpiniones.Value;
    var limit = query.Limit is null || query.Limit <= 0 ? 20 : query.Limit.Value;
    var offset = query.Offset is null || query.Offset < 0 ? 0 : query.Offset.Value;

    var ranking = modelos.Values
        .Select(modelo =>
        {
            var items = opiniones.Where(o => o.ModeloId == modelo.Id).ToList();
            var count = items.Count;
            if (count < minOpiniones)
            {
                return null;
            }

            var promedio = items.Average(o => o.Nota);
            return new
            {
                modelo_id = modelo.Id,
                nombre = modelo.Nombre,
                fabricante = modelo.Fabricante,
                pais_origen = modelo.PaisOrigen,
                promedio = Math.Round(promedio, 2),
                cantidad_opiniones = count,
                stock_total = zapatillas.Where(z => z.Key.ModeloId == modelo.Id).Sum(z => z.Value.Stock)
            };
        })
        .Where(x => x != null)
        .OrderByDescending(x => x!.promedio)
        .ToList();

    if (orden == "asc")
    {
        ranking = ranking.OrderBy(x => x!.promedio).ToList();
    }

    var total = ranking.Count;
    var items = ranking.Skip(offset).Take(limit).ToList();

    return Results.Ok(new { total, items });
});

app.MapGet("/clientes/{dni}/resumen", (string dni) =>
{
    if (!clientes.ContainsKey(dni))
    {
        return Results.NotFound(new { error = $"No existe cliente con DNI {dni}." });
    }

    var comprasCliente = compras.Values.Where(c => c.ClienteDni == dni).OrderByDescending(c => c.FechaHora).ToList();
    var totalCompras = comprasCliente.Count;
    var montoTotal = comprasCliente.Sum(c => c.Lineas.Sum(l => l.PrecioUnitario * l.Cantidad));
    var promedio = totalCompras == 0 ? 0m : montoTotal / totalCompras;
    var ultimaCompra = comprasCliente.FirstOrDefault()?.FechaHora;

    var modeloMasComprado = comprasCliente
        .SelectMany(c => c.Lineas)
        .GroupBy(l => l.ModeloId)
        .Select(g => new
        {
            modelo_id = g.Key,
            nombre = modelos[g.Key].Nombre,
            unidades = g.Sum(l => l.Cantidad)
        })
        .OrderByDescending(x => x.unidades)
        .FirstOrDefault();

    var devolucionesCliente = devoluciones.Count(kvp => comprasCliente.Any(c => c.NumeroCompra == kvp.Key));
    var tasaDevoluciones = totalCompras == 0 ? 0m : (decimal)devolucionesCliente / totalCompras;

    var resumen = new
    {
        cliente = new { nombre = clientes[dni].Nombre, apellido = clientes[dni].Apellido, dni = dni },
        total_compras = totalCompras,
        monto_total_gastado = montoTotal,
        ticket_promedio = Math.Round(promedio, 2),
        ultima_compra = ultimaCompra,
        modelo_mas_comprado = modeloMasComprado,
        devoluciones = new { cantidad = devolucionesCliente, tasa = Math.Round(tasaDevoluciones, 2) },
        opiniones_realizadas = opiniones.Count(o => o.ClienteDni == dni)
    };

    return Results.Ok(resumen);
});

app.MapGet("/modelos/{id:int}/disponibilidad", (int id) =>
{
    if (!modelos.ContainsKey(id))
    {
        return Results.NotFound(new { error = $"El modelo {id} no existe." });
    }

    var talles = zapatillas
        .Where(z => z.Key.ModeloId == id && z.Value.Stock > 0)
        .OrderBy(z => z.Key.Talle)
        .Select(z => new { talle = z.Key.Talle, stock = z.Value.Stock })
        .ToList();

    return Results.Ok(new
    {
        modelo_id = id,
        nombre = modelos[id].Nombre,
        talles
    });
});

app.MapPost("/zapatillas/{modelo:int}/{talle:int}/stock", (int modelo, int talle, StockDeltaRequest request) =>
{
    lock (stockLock)
    {
        var clave = (modelo, talle);
        if (!zapatillas.ContainsKey(clave))
        {
            return Results.NotFound(new { error = $"La zapatilla {modelo}/{talle} no existe." });
        }

        var actual = zapatillas[clave].Stock;
        var nuevo = actual + request.Delta;
        if (nuevo < 0 || nuevo > 500)
        {
            return Results.Conflict(new { error = "El delta provocaría stock negativo o mayor a 500." });
        }

        zapatillas[clave] = new Zapatilla { ModeloId = modelo, Talle = talle, Stock = nuevo };
        return Results.Ok(new { modelo_id = modelo, talle, stock = nuevo });
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();

public partial class Program { }

public sealed class Modelo
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int FabricanteId { get; set; }
    public string Fabricante { get; set; } = string.Empty;
    public string PaisOrigen { get; set; } = string.Empty;
}

public sealed class Cliente
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellido { get; set; } = string.Empty;
    public string Dni { get; set; } = string.Empty;
}

public sealed class Zapatilla
{
    public int ModeloId { get; set; }
    public int Talle { get; set; }
    public int Stock { get; set; }
}

public sealed class Compra
{
    public int NumeroCompra { get; set; }
    public string ClienteDni { get; set; } = string.Empty;
    public DateTime FechaHora { get; set; }
    public List<LineaCompra> Lineas { get; set; } = new();
}

public sealed class LineaCompra
{
    public int ModeloId { get; set; }
    public int Talle { get; set; }
    public decimal PrecioUnitario { get; set; }
    public int Cantidad { get; set; }
}

public sealed class Devolucion
{
    public int NumeroCompra { get; set; }
    public int RazonId { get; set; }
    public DateTime FechaHora { get; set; }
    public List<DevolucionItem> Items { get; set; } = new();
}

public sealed class DevolucionItem
{
    [JsonPropertyName("modelo_id")]
    public int ModeloId { get; set; }

    [JsonPropertyName("talle")]
    public int Talle { get; set; }

    [JsonPropertyName("cantidad")]
    public int Cantidad { get; set; }
}

public sealed class DevolucionRequest
{
    [JsonPropertyName("razon_id")]
    public int RazonId { get; set; }

    [JsonPropertyName("items")]
    public List<DevolucionItemRequest> Items { get; set; } = new();
}

public sealed class DevolucionItemRequest
{
    [JsonPropertyName("modelo_id")]
    public int ModeloId { get; set; }

    [JsonPropertyName("talle")]
    public int Talle { get; set; }

    [JsonPropertyName("cantidad")]
    public int Cantidad { get; set; }
}

public sealed class Opinion
{
    public int ModeloId { get; set; }
    public DateTime FechaHora { get; set; }
    public string ClienteDni { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal Nota { get; set; }
}

public sealed class OpinionRequest
{
    [JsonPropertyName("cliente_dni")]
    public string ClienteDni { get; set; } = string.Empty;

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("nota")]
    public decimal Nota { get; set; }
}

public sealed class RankingQuery
{
    public int? FabricanteId { get; set; }
    public string? Pais { get; set; }
    public int? MinOpiniones { get; set; } = 1;
    public string? Orden { get; set; } = "desc";
    public int? Limit { get; set; } = 20;
    public int? Offset { get; set; } = 0;
}

public sealed class StockDeltaRequest
{
    [JsonPropertyName("delta")]
    public int Delta { get; set; }
}
