---
name: reqnroll-gherkin-features
description: Escribir features Gherkin en español y sus step definitions con Reqnroll (el sucesor open-source de SpecFlow) sobre xUnit — convenciones de archivos .feature, bindings con inyección de contexto, y hooks. Usar al agregar o modificar escenarios BDD del proyecto; para el harness HTTP/base de datos de los tests end-to-end ver reqnroll-e2e-api-tests.
---

# reqnroll-gherkin-features

Convenciones para la capa BDD del proyecto: archivos `.feature` en **español** (el idioma del dominio de
GastNyahp) + step definitions en C#. El framework es **Reqnroll** — NO SpecFlow: SpecFlow fue discontinuado por
Tricentis a fines de 2024 (su última versión fue `4.0.31-beta`, nunca estable, sin soporte .NET 8+); Reqnroll
es el fork oficial de su creador original, mantiene la misma sintaxis de attributes (`[Binding]`, `[Given]`,
`[When]`, `[Then]`) pero con namespace `Reqnroll` en vez de `TechTalk.SpecFlow`. Si encontrás documentación o
snippets de SpecFlow, casi todo aplica cambiando el `using`.

## Cuándo usar / cuándo no

- **Usar**: agregar un escenario de negocio nuevo, un step definition, o un hook a la suite BDD existente.
- **No usar**: para decidir el harness técnico de los tests E2E (WebApplicationFactory, base de datos,
  event store) — eso está en [[reqnroll-e2e-api-tests]]. Tampoco para tests unitarios de dominio o de
  proyecciones — esos son xUnit directo (más rápidos de escribir y de leer para reglas técnicas).

## Setup del proyecto de tests

```xml
<PackageReference Include="Reqnroll.xUnit" Version="3.3.4" />
<!-- Reqnroll.xUnit ya incluye el generador MSBuild (los .feature se compilan a facts de xUnit al buildear)
     y el runtime. xunit y xunit.runner.visualstudio van aparte, como en cualquier proyecto de test. -->
```

`reqnroll.json` en la raíz del proyecto de tests — define el idioma de los `.feature` una sola vez:

```json
{
  "$schema": "https://schemas.reqnroll.net/reqnroll-config-latest.json",
  "language": { "feature": "es" }
}
```

Con esto los `.feature` usan las palabras clave en español: `Característica`, `Antecedentes`, `Escenario`,
`Esquema del escenario`, `Ejemplos`, `Dado`, `Cuando`, `Entonces`, `Y`, `Pero`.

## Anatomía de un .feature

```gherkin
Característica: Bancos
    Administración de las entidades bancarias que agrupan tarjetas y préstamos.

Antecedentes:
    Dado que la app está iniciada

Escenario: No se puede eliminar un banco con tarjetas asociadas
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"
    Cuando intento eliminar el banco "BBVA"
    Entonces la operación es rechazada con "tarjetas o préstamos asociados"
    Y el banco "BBVA" sigue existiendo
```

Reglas de redacción:
- **El lenguaje del escenario es el del dominio** (ver la tabla canónica en `gastnyahp-domain-model`): banco,
  tarjeta, cuota, préstamo, servicio, reserva, gasto, ticket, día hábil. Nunca vocabulario técnico (endpoint,
  status code, fila, aggregate) en el `.feature` — eso vive en los steps.
- Un escenario = una regla de negocio verificable. Si necesitás "y además..." para describirlo, son dos
  escenarios.
- Referencias por **nombre de negocio** ("el banco \"BBVA\""), nunca por id/GUID — los steps mantienen el mapa
  nombre → id del escenario en el estado compartido.
- `Esquema del escenario` + `Ejemplos` para la misma regla con datos distintos (ej. validaciones de rango).

## Step definitions — inyección de contexto, no estáticos

```csharp
using Reqnroll;

namespace GastNyahp.E2E.Tests.Steps;

[Binding]
public sealed class BancosSteps(E2EWorld world)
{
    [Given(@"un banco ""(.*)""")]
    public async Task GivenUnBanco(string nombre)
    {
        var id = await world.PostAndGetId("/api/banks", new { name = nombre, color = "#004B9B", icon = "bank" });
        world.RememberId("banco", nombre, id);
    }

    [When(@"intento eliminar el banco ""(.*)""")]
    public async Task WhenIntentoEliminarElBanco(string nombre) =>
        world.LastResponse = await world.Client.DeleteAsync($"/api/banks/{world.IdOf("banco", nombre)}");

    [Then(@"la operación es rechazada con ""(.*)""")]
    public async Task ThenLaOperacionEsRechazada(string fragmento)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, world.LastResponse!.StatusCode);
        Assert.Contains(fragmento, await world.LastResponse.Content.ReadAsStringAsync());
    }
}
```

Reglas:
1. **Una clase de steps por área de negocio** (`BancosSteps`, `TarjetasSteps`, ...), primary constructor
   recibiendo el "world" compartido — Reqnroll crea e inyecta automáticamente cualquier clase pedida por
   ctor, con lifetime de UN escenario (su contenedor BoDi). No usar campos estáticos ni `ScenarioContext`
   por string-key: el estado tipado del world es más seguro.
2. El **world** (`E2EWorld`, definido en [[reqnroll-e2e-api-tests]]) es el único dueño del `HttpClient`, la
   última respuesta, y el mapa nombre→id. Los steps no crean clientes ni guardan estado propio.
3. Steps **finos**: traducen Gherkin ↔ llamadas HTTP + asserts. Cero lógica de negocio (si un step calcula
   algo del dominio para armar el request, esa regla debería estar del lado de la API).
4. Regex de step con `@"..."` y grupos `""(.*)""` para los nombres citados. Un step reutilizable entre
   features va en una clase `Common`/`SharedSteps`.

## Hooks

```csharp
[Binding]
public sealed class Hooks
{
    [BeforeScenario]   // corre antes de cada escenario; el world ya está construido si algún step lo pide
    public void BeforeScenario() { /* seed común, reset de estado externo */ }

    [AfterScenario]
    public void AfterScenario() { /* limpieza; el world IDisposable se dispone solo */ }
}
```

El ciclo de vida normal (crear app + base limpia por escenario) NO va en hooks — va en el constructor/Dispose
del world (ver [[reqnroll-e2e-api-tests]]), así los escenarios quedan 100% aislados sin coordinación global.

## Procedimiento

1. Identificá la regla de negocio (consultá `gastnyahp-domain-model` / `backend/docs/DOMAIN_MODEL.md` para el
   vocabulario y el comportamiento ya decidido).
2. Escribí el escenario en el `.feature` del área (o creá `Features/<Area>.feature` nuevo).
3. Compilá: Reqnroll genera el test; los steps faltantes aparecen como *pending* con el snippet sugerido en el
   output del runner.
4. Implementá los steps en la clase del área, reusando los helpers del world.
5. `dotnet test` — el escenario aparece como un test xUnit con el nombre del escenario.

## Verificación

- `dotnet test` en verde y el escenario listado por su nombre Gherkin.
- El `.feature` se entiende sin leer los steps (se lo podés mostrar a alguien no-técnico).
- Ningún step quedó en estado *pending/undefined*.

## Anti-patrones

- ❌ Referenciar SpecFlow (`TechTalk.SpecFlow`, paquetes `SpecFlow.*`) — proyecto muerto; es `Reqnroll`.
- ❌ Gherkin técnico ("Cuando hago POST a /api/banks devuelve 200") — el feature describe negocio, el step
  traduce a HTTP.
- ❌ Estado compartido entre escenarios (estáticos, orden de ejecución implícito) — cada escenario arranca de
  cero con su world.
- ❌ Un escenario gigante que recorre toda la app — escenarios chicos, una regla cada uno.
- ❌ Steps con lógica de negocio duplicada de la API (cálculos de totales, fechas de cierre) — el test debe
  fallar si la API se equivoca, no re-implementarla.
