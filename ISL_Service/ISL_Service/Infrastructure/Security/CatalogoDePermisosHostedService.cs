using ISL_Service.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ISL_Service.Infrastructure.Security;

/*
  SIEMBRA EL CATALOGO DE PERMISOS AL ARRANCAR.

  QUE PROBLEMA RESUELVE

  `PermissionCatalogSeeds` es la lista de permisos que declara el codigo. Hasta
  ahora solo llegaba a `WPermiso` si una persona entraba a la pantalla de
  permisos y pulsaba sincronizar.

  La consecuencia no se veia de frente, y por eso duro: un permiso que no esta
  en `WPermiso` no puede tener fila de concesion, asi que el endpoint que lo
  exige contesta 403 a TODO EL MUNDO menos al SuperAdmin, que pasa antes de
  mirar nada (`PermissionAuthorizationHandler`). O sea que un modulo podia estar
  escrito, probado y comprobado contra Legacy... y estar muerto para los
  usuarios de verdad.

  Medido en esta base: 0 filas para `catalogos.*`, `obra.*`, `cobranza.*` y
  `productos.*`, con 68 capacidades ya migradas colgando de ellas.

  POR QUE AL ARRANCAR Y NO AL CALCULAR PERMISOS

  Se intento primero dentro de `BuildSnapshotAsync`, junto a las demas
  sincronizaciones. No sirve: el SuperAdmin ni siquiera llega ahi, y para los
  demas la siembra ocurriria DESPUES del primer 403. El primer usuario del dia
  pagaba el error.

  Al arrancar es determinista: cuando la primera peticion llega, el catalogo ya
  esta. Es el mismo patron del calentamiento del motor de reportes.

  SEMBRAR NO ES CONCEDER

  Esto crea la clave para que se pueda repartir desde la pantalla de permisos.
  Quien no la tenga concedida sigue sin poder entrar. No abre nada.

  SI FALLA, LA APP ARRANCA IGUAL

  Es una comodidad, no un requisito para servir. Si la base no esta lista o le
  faltan las tablas de capacidades, se anota y se sigue: romper el arranque por
  esto dejaria el sistema entero caido por un permiso.
*/
public sealed class CatalogoDePermisosHostedService : IHostedService
{
    private readonly IServiceProvider _servicios;
    private readonly ILogger<CatalogoDePermisosHostedService> _log;

    public CatalogoDePermisosHostedService(
        IServiceProvider servicios,
        ILogger<CatalogoDePermisosHostedService> log)
    {
        _servicios = servicios;
        _log = log;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // Se deja respirar al arranque: la primera conexion a SQL Server
                // compite con el resto de la inicializacion y no hay prisa.
                await Task.Delay(TimeSpan.FromSeconds(3), ct);

                using var ambito = _servicios.CreateScope();
                var permisos = ambito.ServiceProvider.GetRequiredService<IPermissionService>();

                // La empresa del tenant. Este backend corre una instalacion por
                // empresa (ver el despliegue), asi que es siempre la 1.
                var resultado = await permisos.SyncPermissionCatalogAsync(1, null, ct);

                _log.LogInformation(
                    "Catalogo de permisos sembrado: {Creados} nuevos, {Existentes} ya estaban.",
                    resultado.InsertedCount,
                    resultado.SkippedPermissions.Count);
            }
            catch (OperationCanceledException)
            {
                // Se esta apagando.
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "No se pudo sembrar el catalogo de permisos.");
            }
        }, ct);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
