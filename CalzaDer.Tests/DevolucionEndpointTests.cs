using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace CalzaDer.Tests;

public class DevolucionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public DevolucionEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostDevolucion_ConDatosValidos_Retorna201Created()
    {
        var request = new
        {
            razon_id = 3,
            items = new[]
            {
                new { modelo_id = 12, talle = 42, cantidad = 1 },
                new { modelo_id = 12, talle = 43, cantidad = 2 }
            }
        };

        var response = await _client.PostAsJsonAsync("/compras/1001/devoluciones", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task PostDevolucion_ConItemsVacios_Retorna422UnprocessableEntity()
    {
        var request = new
        {
            razon_id = 2,
            items = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/compras/1001/devoluciones", request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostDevolucion_ConCompraYaDevolucionada_Retorna409Conflict()
    {
        var request = new
        {
            razon_id = 1,
            items = new[]
            {
                new { modelo_id = 13, talle = 40, cantidad = 1 }
            }
        };

        var response = await _client.PostAsJsonAsync("/compras/1002/devoluciones", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostOpinion_ConClienteQueCompróModelo_Retorna201Created()
    {
        var request = new
        {
            cliente_dni = "30111222",
            descripcion = "Muy cómodas para correr",
            nota = 4.5m
        };

        var response = await _client.PostAsJsonAsync("/modelos/12/opiniones", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetRanking_RetornaListaPaginada()
    {
        var response = await _client.GetAsync("/modelos/ranking?min_opiniones=1&orden=desc&limit=20&offset=0");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetResumenCliente_ConComprasExistentes_Retorna200OK()
    {
        var response = await _client.GetAsync("/clientes/30111222/resumen");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetDisponibilidadModelo_RetornaTallesConStock()
    {
        var response = await _client.GetAsync("/modelos/12/disponibilidad");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostStockZapatilla_ConDeltaValido_Retorna200OK()
    {
        var request = new { delta = 10 };

        var response = await _client.PostAsJsonAsync("/zapatillas/12/42/stock", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
