---
name: skill-author
description: Crear o editar otras skills de este proyecto en formato nativo de Codex (SKILL.md con frontmatter name+description). Usar cuando se descubre un patrón repetible propio de ESTE repo que conviene documentar como skill nueva, o cuando piden mejorar una existente.
---

# skill-author

Meta-skill para seguir construyendo esta suite. Una skill es conocimiento empaquetado para que Codex lo
cargue bajo demanda — no es código de la app, es un playbook.

## Cuándo usar / cuándo no

- **Usar**: encontraste un procedimiento específico de ESTE proyecto que vale la pena fijar por escrito (una
  convención propia, no algo que cualquier modelo ya sabe de memoria), o te piden crear/mejorar una skill.
- **No usar**: para documentar conocimiento genérico de programación (eso ya lo sabe el modelo sin skill) — una
  skill vale por lo que es ESPECÍFICO de este repo/proyecto.

## Dónde se escribe

`.Codex/skills/<nombre-kebab-case>/SKILL.md` en la raíz del proyecto. Codex descubre automáticamente
cualquier carpeta con ese archivo.

## Formato exacto

```markdown
---
name: <kebab-case, igual al nombre de la carpeta>
description: <UNA línea, en tercera persona, que dice CUÁNDO usar la skill — Codex la lee para decidir si la carga. Incluí las palabras/situaciones gatillo dentro de la misma oración, no una lista aparte de triggers.>
---

# <nombre>

## Cuándo usar / cuándo no
## <Secciones de contenido — patrón, código de ejemplo, pasos>
## Procedimiento
## Verificación
## Anti-patrones
```

Notas sobre el frontmatter:
- Solo `name` y `description` — Codex no necesita (ni usa) campos como `triggers` o `tools` separados;
  la `description` tiene que hacer ese trabajo en una sola oración bien escrita.
- La `description` describe **cuándo usarla**, no **qué es**. Mal: "Convenciones de EF Core." Bien: "Diseñar o
  modificar un DbContext de EF Core sobre PostgreSQL... Usar para cualquier cambio de schema del read-model."

## Procedimiento

1. Mirá 1-2 skills existentes de esta suite como referencia de tono y nivel de detalle (ej.
   `eventuous-event-sourced-aggregate` o `application-service-layer`).
2. Si la skill documenta un patrón del código, incluí un snippet REAL o representativo — no expliques en
   abstracto, mostrá el shape exacto (nombres de clases, firma de métodos) que el modelo debe reproducir.
3. Enlazá skills relacionadas con `[[nombre-de-la-otra-skill]]` cuando el procedimiento se apoya en otra (ej. un
   controller que llama a un service de aplicación).
4. Escribí el archivo en `.Codex/skills/<nombre>/SKILL.md`.
5. Verificá que el frontmatter parsea (bloque `---`...`---` bien formado) y que la `description` sola alcanza
   para decidir cuándo cargarla.

## Verificación

- El archivo existe en la ruta correcta con frontmatter válido.
- La `description` es específica del proyecto/situación, no genérica ("seguir buenas prácticas" no sirve).
- El cuerpo tiene código/pasos concretos, no solo teoría.

## Anti-patrones

- ❌ Skills vagas o puramente teóricas.
- ❌ `description` que explica QUÉ es la skill en vez de CUÁNDO usarla.
- ❌ Reintroducir campos de frontmatter de otros formatos (`triggers`, `tools`) que Codex no usa.
- ❌ Documentar algo que cualquier modelo ya sabe sin necesidad de que se lo repitan (ej. "cómo hacer un
  for-loop en C#").
- ❌ Copiar código de un repo/proyecto anterior sin adaptar nombres/paths al proyecto actual.
