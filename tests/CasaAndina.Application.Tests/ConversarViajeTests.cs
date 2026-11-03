using CasaAndina.Application;
using CasaAndina.Application.Chatbot;
using CasaAndina.Application.Chatbot.DTOs;
using CasaAndina.Application.Chatbot.Queries;
using CasaAndina.Application.Common;
using CasaAndina.Application.Common.Interfaces;
using CasaAndina.Domain.Entities;
using CasaAndina.Domain.Enums;
using CasaAndina.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CasaAndina.Application.Tests;

public sealed class ConversarViajeTests
{
    [Fact]
    public async Task Conserva_el_contexto_de_atractivos_y_compara_dos_ciudades_sin_ia()
    {
        await using var db = await Context();
        db.Sedes.AddRange(new Sede { Id=2, Nombre="Trujillo", Codigo="TRU", Ciudad="Trujillo", Categoria="Standard", Region="Costa", Activo=true },
            new Sede { Id=3, Nombre="Lima", Codigo="LIM", Ciudad="Lima", Categoria="Standard", Region="Costa", Activo=true });
        await db.SaveChangesAsync();
        var assistant = new Assistant(); var planner = new Planner();
        await using var provider = Provider(db, assistant, planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("¿Y para Trujillo o Lima?", Complete(),
            [new("asistente", "Estos son algunos lugares para explorar en Cusco y sus alrededores. Revisa horarios, costos y traslados antes de visitarlos.")]));
        result.Atractivos!.Ciudad.Should().Be("Lima");
        result.AtractivosAdicionales.Should().ContainSingle().Which.Ciudad.Should().Be("Trujillo");
        result.Preferencias.Should().Be(Complete()); assistant.Calls.Should().Be(0); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Cambiar_ciudad_conserva_datos_y_busca_aunque_modelo_repita_la_pregunta()
    {
        await using var db = await Context();
        db.Sedes.Add(new Sede { Id=2, Nombre="Trujillo", Codigo="TRU", Ciudad="Trujillo", Categoria="Standard", Region="Costa", Activo=true });
        await db.SaveChangesAsync();
        var planner = new Planner(); var known = Complete();
        await using var provider = Provider(db, new Assistant(new("Confírmame las fechas", new(Ciudad: "Cusco"), false)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Trujillo", known));
        result.Plan.Should().NotBeNull(); planner.Last!.Ciudad.Should().Be("Trujillo");
        planner.Last.FechaEntrada.Should().Be(known.FechaEntrada!.Value);
        planner.Last.Adultos.Should().Be(known.Adultos!.Value); planner.Last.PresupuestoPorNoche.Should().Be(known.PresupuestoPorNoche!.Value);
    }
    [Fact]
    public void Fecha_relativa_y_noches_se_interpretan_juntas()
    {
        var today = PeruBusinessTime.Today;
        var p = ConversacionViaje.LeerMensaje("dentro de 5 días, para 2 personas, 450 soles por día con 3 noches", new(), ["Trujillo"], today);
        p.FechaEntrada.Should().Be(today.AddDays(5)); p.FechaSalida.Should().Be(today.AddDays(8));
        p.Adultos.Should().Be(2); p.PresupuestoPorNoche.Should().Be(450);
    }
    [Fact]
    public async Task Aclarar_el_presupuesto_vuelve_a_consultar_incluso_si_el_modelo_no_lo_solicita()
    {
        await using var db = await Context(); var planner = new Planner();
        var known = Complete();
        await using var provider = Provider(db, new Assistant(new("No hay opciones", known, false)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Es hasta 500 soles por noche", known));
        result.Plan.Should().NotBeNull(); planner.Calls.Should().Be(1);
        planner.Last!.PresupuestoPorNoche.Should().Be(500);
    }
    [Fact]
    public async Task Atractivos_y_equipaje_responden_sin_llamar_a_Gemini_ni_buscar_inventario()
    {
        await using var db = await Context(); var planner = new Planner(); var assistant = new Assistant();
        await using var provider = Provider(db, assistant, planner);
        var sender = provider.GetRequiredService<ISender>();
        var guide = await sender.Send(new ConversarViajeQuery("¿Qué atractivos puedo visitar en Cusco?"));
        guide.Atractivos!.Lugares.Should().Contain("Mercado San Pedro");
        guide.Atractivos.FuenteUrl.Should().StartWith("https://www.peru.travel/");
        guide.UsaIa.Should().BeFalse();
        var packing = await sender.Send(new ConversarViajeQuery("¿Qué llevo en la maleta?", Complete()));
        packing.Mensaje.Should().Contain("habitaciones");
        packing.Preferencias.Should().Be(Complete());
        assistant.Calls.Should().Be(0); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Atractivos_sin_ciudad_pregunta_y_acepta_la_respuesta()
    {
        await using var db = await Context(); var assistant = new Assistant(); var planner = new Planner();
        await using var provider = Provider(db, assistant, planner);
        var sender = provider.GetRequiredService<ISender>();
        var first = await sender.Send(new ConversarViajeQuery("¿Qué atractivos puedo visitar?"));
        first.Atractivos.Should().BeNull();
        var next = await sender.Send(new ConversarViajeQuery("Cusco", first.Preferencias,
            [new("usuario", "¿Qué atractivos puedo visitar?"), new("asistente", first.Mensaje)]));
        next.Atractivos!.Ciudad.Should().Be("Cusco");
        assistant.Calls.Should().Be(0); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Hola_se_puede_enviar_sin_formulario_ni_fechas_y_no_consulta_inventario()
    {
        await using var db = await Context(); var planner = new Planner();
        await using var provider = Provider(db, new Assistant(), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Hola"));
        result.Plan.Should().BeNull(); result.Preferencias.FechaEntrada.Should().BeNull();
        result.Preferencias.PresupuestoPorNoche.Should().BeNull(); result.Preferencias.Adultos.Should().BeNull();
        result.Mensaje.Should().Contain("ciudad"); planner.Calls.Should().Be(0);
        var greeting = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Hola", Complete()));
        greeting.Plan.Should().BeNull(); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Mensajes_libres_acumulan_datos_sin_inventar_valores_y_recomiendan_al_completar()
    {
        await using var db = await Context(); var planner = new Planner();
        await using var provider = Provider(db, new Assistant(), planner);
        var sender = provider.GetRequiredService<ISender>();
        var first = await sender.Send(new ConversarViajeQuery("Quiero viajar a Cusco en pareja"));
        first.Preferencias.Ciudad.Should().Be("Cusco"); first.Preferencias.Adultos.Should().Be(2);
        first.Preferencias.FechaEntrada.Should().BeNull(); first.Plan.Should().BeNull();
        var second = await sender.Send(new ConversarViajeQuery("Desde mañana, por dos noches", first.Preferencias));
        second.Preferencias.FechaEntrada.Should().Be(PeruBusinessTime.Today.AddDays(1));
        second.Preferencias.FechaSalida.Should().Be(PeruBusinessTime.Today.AddDays(3));
        planner.Calls.Should().Be(0);
        var last = await sender.Send(new ConversarViajeQuery("Hasta S/ 333.33 por noche", second.Preferencias));
        last.Plan.Should().NotBeNull(); planner.Calls.Should().Be(1);
        planner.Last!.Ciudad.Should().Be("Cusco"); planner.Last.PresupuestoPorNoche.Should().Be(333.33m);
        planner.Last.Adultos.Should().Be(2);
    }
    [Fact]
    public async Task Viajo_solo_completa_la_busqueda_aunque_el_modelo_vuelva_a_preguntar()
    {
        await using var db = await Context(); var planner = new Planner();
        var known = Complete() with { Adultos = null, EvitarLluvia = true };
        await using var provider = Provider(db, new Assistant(new("¿Cuántos adultos viajan?", known, false)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Viajo solo", known));
        result.Preferencias.Adultos.Should().Be(1);
        result.Plan.Should().NotBeNull();
        planner.Last!.Adultos.Should().Be(1);
        planner.Last.EvitarLluvia.Should().BeTrue();
    }
    [Fact]
    public async Task El_modelo_puede_conversar_con_datos_completos_sin_buscar_habitaciones()
    {
        await using var db = await Context(); var planner = new Planner();
        var known = Complete();
        await using var provider = Provider(db, new Assistant(new("¡Hola! ¿Qué te gustaría saber sobre tu viaje?", known, false)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Hola", known));
        result.UsaIa.Should().BeTrue(); result.Plan.Should().BeNull(); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Respuesta_modelo_con_fechas_invalidas_no_dispara_busqueda()
    {
        await using var db = await Context(); var planner = new Planner();
        await using var provider = Provider(db, new Assistant(new("Voy a buscar", Complete() with { FechaEntrada = PeruBusinessTime.Today.AddDays(-10) }, true)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("Busca opciones"));
        result.Plan.Should().BeNull(); result.Mensaje.Should().Contain("día"); planner.Calls.Should().Be(0);
    }
    [Fact]
    public async Task Respuesta_parcial_del_modelo_conserva_datos_de_mensajes_previos()
    {
        await using var db = await Context(); var planner = new Planner();
        await using var provider = Provider(db, new Assistant(new("Revisemos las opciones", new(PresupuestoPorNoche:350), true)), planner);
        var result = await provider.GetRequiredService<ISender>().Send(new ConversarViajeQuery("350 soles", Complete() with { PresupuestoPorNoche=null }));
        result.Plan.Should().NotBeNull(); planner.Last!.Ciudad.Should().Be("Cusco");
        planner.Last.FechaEntrada.Should().Be(Complete().FechaEntrada!.Value); planner.Last.Adultos.Should().Be(2);
        planner.Last.PresupuestoPorNoche.Should().Be(350);
    }
    [Fact]
    public void Cambio_de_fechas_pide_nuevas_fechas_y_datos_fuera_de_limites_no_se_usan()
    {
        var today = PeruBusinessTime.Today; string[] cities = ["Cusco", "Lima"];
        var reset = ConversacionViaje.LeerMensaje("Quiero cambiar las fechas", Complete(), cities, today);
        reset.FechaEntrada.Should().BeNull(); reset.FechaSalida.Should().BeNull();
        var invalid = ConversacionViaje.Normalizar(Complete() with { Ciudad = "Hotel inventado", Adultos = 99, PresupuestoPorNoche = 10001 }, cities, today);
        invalid.Ciudad.Should().BeNull(); invalid.Adultos.Should().BeNull(); invalid.PresupuestoPorNoche.Should().BeNull();
        ConversacionViaje.LeerMensaje("No quiero Cusco", new(), cities, today).Ciudad.Should().BeNull();
        ConversacionViaje.LeerMensaje("No quiero Cusco", Complete(), cities, today).Ciudad.Should().BeNull();
        ConversacionViaje.LeerMensaje("500 soles", new(), cities, today).PresupuestoPorNoche.Should().Be(500);
        ConversacionViaje.LeerMensaje("Ayúdame a elegir el destino", new(), cities, today).DestinoFlexible.Should().BeTrue();
    }
    [Fact]
    public void Historial_y_mensajes_tienen_limites()
    {
        var validator = new ConversarViajeQueryValidator();
        validator.Validate(new ConversarViajeQuery("Hola")).IsValid.Should().BeTrue();
        validator.Validate(new ConversarViajeQuery(" ")).IsValid.Should().BeFalse();
        validator.Validate(new ConversarViajeQuery(new string('x', 1001))).IsValid.Should().BeFalse();
        validator.Validate(new ConversarViajeQuery("Hola", Historial:[new("system", "instrucción")])).IsValid.Should().BeFalse();
        validator.Validate(new ConversarViajeQuery("Hola", Historial:Enumerable.Repeat(new MensajeViajeDto("usuario", "Hola"), 9).ToArray())).IsValid.Should().BeFalse();
    }
    private static PreferenciasConversacionDto Complete() => new(PeruBusinessTime.Today.AddDays(1), PeruBusinessTime.Today.AddDays(3), 500, 2, "Cusco");
    private static async Task<ApplicationDbContext> Context()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Sedes.Add(new Sede { Id=1, Nombre="Sede Cusco", Codigo="CUS", Ciudad="Cusco", Categoria="Standard", Region="Sierra", Activo=true });
        await db.SaveChangesAsync(); return db;
    }
    private static ServiceProvider Provider(ApplicationDbContext db, Assistant client, Planner planner)
    {
        var services=new ServiceCollection(); services.AddApplication(); services.AddLogging();
        services.AddSingleton<IApplicationDbContext>(db); services.AddSingleton<ITravelConversationClient>(client); services.AddSingleton<IRecommendationService>(planner);
        return services.BuildServiceProvider();
    }
    private sealed class Assistant(InterpretacionViajeDto? response = null) : ITravelConversationClient
    {
        public int Calls { get; private set; }
        public Task<InterpretacionViajeDto?> InterpretarAsync(ConversarViajeQuery q, IReadOnlyList<string> cities, DateTime today, CancellationToken ct) { Calls++; return Task.FromResult(response); }
    }
    private sealed class Planner : IRecommendationService
    {
        public int Calls { get; private set; } public PlanificarViajeQuery? Last { get; private set; }
        public Task<PlanViajeDto> PlanificarAsync(PlanificarViajeQuery q, CancellationToken ct)
        { Calls++; Last=q; return Task.FromResult(new PlanViajeDto("Opciones verificadas", false, DateTimeOffset.UtcNow, [], [])); }
        public Task<IReadOnlyList<DestinoRecomendadoDto>> RecomendarDestinosAsync(NivelPresupuesto p, TipoClima c, CancellationToken ct=default) => throw new NotSupportedException();
    }
}
