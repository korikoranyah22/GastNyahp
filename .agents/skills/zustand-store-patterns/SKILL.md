---
name: zustand-store-patterns
description: Convenciones para el store global con Zustand (acciones, ids, timestamps, validación por retorno, import/export). Usar al agregar estado global o acciones nuevas al store.
---

# Zustand store patterns

Un único store global (`store/useStore.js`) con `persist`, en vez de múltiples stores pequeños o Context. Todo el estado de la app cabe en un objeto serializable a JSON.

**Relación con el backend real:** esto documenta el store *actual* de GastNyahp (mutación 100% local en
`localStorage`, sin red). A medida que cada slice se conecta a un backend real, sus acciones migran al patrón de
`react-feature-module` (cliente API → store → hook), que también resume las dos estrategias de integración
(optimista vs. fetch-then-hydrate). El naming de acciones (`addX`/`updateX`/`deleteX`/`toggleXField`/`getX...`)
y el shape de error `{ error }` se mantienen iguales en ambos mundos — lo que cambia es si la acción muta el
array local directo o llama a `api/<recurso>.ts` primero. Los aggregates/eventos del lado backend que reemplazan
cada slice están mapeados en `gastnyahp-domain-model`.

## Esqueleto

```js
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

const ts = () => new Date().toISOString()
const generateId = (prefix) => `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`

const useStore = create(
  persist(
    (set, get) => ({
      // ─── Items ───────────────────────────────────────────────────────────────
      items: [],

      addItem: (item) => set((s) => ({
        items: [...s.items, { ...item, id: generateId('item'), _updatedAt: ts() }],
      })),

      updateItem: (id, data) => set((s) => ({
        items: s.items.map((i) => i.id === id ? { ...i, ...data, _updatedAt: ts() } : i),
      })),

      deleteItem: (id) => {
        const { children } = get()
        const hasChildren = children.some((c) => c.itemId === id)
        if (hasChildren) return { error: 'Este item tiene datos asociados.' }
        set((s) => ({ items: s.items.filter((i) => i.id !== id) }))
        return { error: null }
      },

      // ─── Computed ────────────────────────────────────────────────────────────
      getItemTotal: (itemId) => {
        const { children } = get()
        return children.filter((c) => c.itemId === itemId).reduce((sum, c) => sum + c.amount, 0)
      },
    }),
    { name: 'app-v1', version: 1 }
  )
)

export default useStore
```

## Reglas

**Secciones con banners.** Cada dominio de datos (`items`, `children`, `settings`...) se separa con un comentario banner `// ─── Nombre ───`, y el estado crudo va seguido inmediatamente de sus acciones CRUD. Un store de 400 líneas sigue siendo navegable si está seccionado así.

**Naming de acciones.**
- `addX(data)` — crea, genera id + timestamp.
- `updateX(id, data)` — merge parcial inmutable.
- `deleteX(id)` — valida integridad referencial antes de borrar.
- `toggleXField(id, ...)` — flips de booleanos puntuales (ej. `paid`).
- `getX...(...)` — lecturas derivadas/computadas, viven en el store como métodos que llaman a `get()` internamente. No usar una librería de selectors aparte para esto a esta escala.

**Timestamps en cada mutación.** Todo create/update estampa `_updatedAt: ts()`. No es cosmético: es lo que permite más adelante hacer merge "last-write-wins" entre pestañas/dispositivos, o llevarlo a un backend con la misma columna `updated_at` — en el backend real este rol lo cumple el timestamp del evento en el event store (ver `gastnyahp-domain-model`), no una columna mutable.

**IDs sin dependencia externa.** `generateId(prefix)` combina prefijo + timestamp + sufijo random. Alcanza para una app simple; no hace falta `uuid` como dependencia hasta que haya una razón real (colisiones, requisitos de formato).

**Validación por valor de retorno, no excepciones.** Las acciones que pueden fallar (por integridad referencial, por ejemplo) devuelven `{ error: string | null }` en vez de lanzar. El componente que llama decide cómo mostrar el error (banner inline). Esto mantiene el store desacoplado de cómo se presenta el error, y es trivial de testear sin `try/catch`.

**Integridad referencial manual.** Sin ORM ni cascadas automáticas: el propio `deleteX` chequea si hay registros dependientes en otro slice del store (`get()`) y aborta con error si los hay. A esta escala es más simple y más explícito que declarar constraints.

**Composición de getters.** Un getter derivado puede llamar a otro getter vía `get()` en vez de duplicar la lógica de filtrado (ej. un total "general" que suma el resultado de dos totales parciales).

**Import/export como JSON versionado.** Todo el estado se puede serializar/deserializar como un blob con metadata:

```js
exportData: () => JSON.stringify({ meta: { version: '1.0', exported: new Date().toISOString() }, items: get().items }, null, 2),
importData: (jsonStr) => {
  try {
    const data = JSON.parse(jsonStr)
    set({ items: data.items || [] })  // defaults defensivos por campo
    return { error: null }
  } catch (e) {
    return { error: 'JSON inválido: ' + e.message }
  }
},
```

**Persistencia intercambiable.** `persist` a `localStorage` es el default para una app 100% cliente. El mismo patrón de acciones (mismo naming, mismos timestamps, mismo shape de error) es lo que permite después reemplazar la mutación local por una llamada a una API real sin rediseñar los componentes que consumen el store — ver `react-feature-module`.
