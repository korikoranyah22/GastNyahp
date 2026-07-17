---
name: react-component-patterns
description: Convenciones de componentes, páginas, formularios y hooks para apps React simples tipo CRUD (inspirado en GastNyahp). Usar al crear páginas, formularios o hooks nuevos en el front-end.
---

# React component patterns

Convenciones para estructurar una SPA React (Vite + React Router) orientada a pantallas CRUD simples. No es un framework — son reglas de estilo consistentes para que cualquier página nueva se sienta igual a las demás.

## Estructura de carpetas

```
src/
  pages/
    <feature>/
      <Feature>Page.jsx   # listado + acciones de la entidad
      <Feature>Form.jsx    # crear/editar, colocado junto a la página
  components/
    ui/                    # componentes "tontos" reutilizables (Badge, EmptyState, Modal/SlideOver...)
    layout/                # shell de la app (Shell, Header, Sidebar, BottomNav)
  hooks/                   # hooks finos que envuelven módulos de lib/
  lib/                     # lógica sin React: formatters, utils de fecha, singletons de sync/tema
  store/                   # estado global (ver skill zustand-store-patterns)
```

Reglas:
- Un form vive **junto a** su página (`pages/<feature>/`), no en `components/`. Solo se promueve a `components/ui/` un patrón que se repite en 3+ lugares (así surgieron `Badge`, `EmptyState`, `SlideOver`).
- No crear un `<Entity>Form` genérico parametrizado por schema. Cada feature tiene su propio form explícito, aunque haya repetición — la repetición de 3 líneas es más barata que la abstracción prematura.

## Patrón de página (`<Feature>Page.jsx`)

```jsx
import { useState } from 'react'
import { Plus, Pencil, Trash2 } from 'lucide-react'
import useStore from '../../store/useStore'
import ItemForm from './ItemForm'
import EmptyState from '../../components/ui/EmptyState'

export default function ItemsPage() {
  // Seleccionar solo los slices necesarios, uno por línea — no desestructurar el store entero
  const items = useStore((s) => s.items)
  const deleteItem = useStore((s) => s.deleteItem)

  const [formOpen, setFormOpen] = useState(false)
  const [editItem, setEditItem] = useState(null)
  const [deleteError, setDeleteError] = useState('')

  const handleEdit = (item) => { setEditItem(item); setFormOpen(true) }
  const handleClose = () => { setFormOpen(false); setEditItem(null) }
  const handleDelete = (item) => {
    if (!window.confirm(`¿Eliminar "${item.name}"?`)) return
    const result = deleteItem(item.id)
    setDeleteError(result?.error || '')
  }

  return (
    <div className="p-6">
      {/* header con título + botón "Nuevo" */}
      {deleteError && <div className="mb-4 ...error banner...">{deleteError}</div>}
      {items.length === 0
        ? <EmptyState title="Sin datos" action={<button onClick={() => setFormOpen(true)}>Agregar</button>} />
        : items.map((item) => /* card/row con acciones edit/delete en hover */ null)}
      <ItemForm open={formOpen} onClose={handleClose} item={editItem} />
    </div>
  )
}
```

Puntos clave:
- Handlers nombrados `handleX`, no inline salvo triviales (`onClick={() => setFormOpen(true)}`).
- Confirmación de borrado con `window.confirm` (no modal custom) para acciones destructivas simples.
- El resultado de una mutación que puede fallar se lee del valor de retorno (`result?.error`), no de un `try/catch` — ver `zustand-store-patterns`.
- Estado de "form abierto / entidad en edición" siempre local (`useState`), nunca en el store global.

## Patrón de formulario (`<Feature>Form.jsx`)

```jsx
import { useState, useEffect } from 'react'
import SlideOver from '../../components/ui/SlideOver'
import useStore from '../../store/useStore'

const BLANK = { name: '', color: '#3b82f6' }

export default function ItemForm({ open, onClose, item = null }) {
  const addItem = useStore((s) => s.addItem)
  const updateItem = useStore((s) => s.updateItem)

  const [form, setForm] = useState(item || BLANK)
  const [error, setError] = useState('')

  // Resetear cada vez que se abre, sea para crear o editar
  useEffect(() => {
    if (open) { setForm(item || BLANK); setError('') }
  }, [open, item])

  const isEdit = !!item
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }))

  const handleSubmit = (e) => {
    e.preventDefault()
    if (!form.name.trim()) { setError('El nombre es requerido'); return }
    isEdit ? updateItem(item.id, form) : addItem(form)
    onClose()
  }

  return (
    <SlideOver open={open} onClose={onClose} title={isEdit ? 'Editar' : 'Nuevo'}>
      <form onSubmit={handleSubmit} className="space-y-5">
        {/* inputs controlados con value={form.x} onChange={(e) => set('x', e.target.value)} */}
        {error && <p className="text-xs text-red-400">{error}</p>}
        {/* botones cancelar / submit */}
      </form>
    </SlideOver>
  )
}
```

Puntos clave:
- El form recibe `{ open, onClose, item = null }` como props; `item` presente = modo edición.
- `useEffect` sobre `[open, item]` resetea el form — así el mismo componente sirve para crear y editar sin desmontar/remontar.
- Un solo helper `set(key, value)` para todos los campos, en vez de un `onChange` por campo.
- Validación mínima e inline (mensaje de texto), no una librería de schemas para casos simples.
- El submit llama a la acción del store y luego cierra — el form no gestiona su propio "loading state" salvo que la mutación sea async (ver `react-feature-module`, que cubre el caso donde la acción del store ya no muta local sino que llama al backend real).

## Hooks que envuelven módulos singleton

Para lógica transversal que no pertenece a un componente (tema, autosave, sync, "hay cambios sin guardar"): un módulo plano en `lib/` con `getX()` / `subscribeX(fn)` / acciones, y un hook fino que lo conecta a React:

```js
// lib/themeManager.js — sin React
let theme = 'dark'
const listeners = new Set()
export const getTheme = () => theme
export const subscribeTheme = (fn) => { listeners.add(fn); return () => listeners.delete(fn) }
export function toggleTheme() { theme = theme === 'dark' ? 'light' : 'dark'; listeners.forEach(fn => fn(theme)) }
```

```js
// hooks/useTheme.js
import { useState, useEffect } from 'react'
import { getTheme, subscribeTheme, toggleTheme } from '../lib/themeManager'

export function useTheme() {
  const [theme, setTheme] = useState(getTheme)
  useEffect(() => subscribeTheme(setTheme), [])
  return { theme, toggleTheme }
}
```

Ventaja: la lógica es testeable y portable sin React, y varios componentes pueden suscribirse sin pasar por Context.

## Routing

Todas las rutas se declaran centralizadas en `App.jsx`, envueltas en un único layout (`Shell`) vía `<Outlet/>`, con redirect de catch-all:

```jsx
<Routes>
  <Route element={<Shell />}>
    <Route path="/" element={<Home />} />
    <Route path="/items" element={<ItemsPage />} />
    <Route path="*" element={<Navigate to="/" replace />} />
  </Route>
</Routes>
```

## Otros

- Iconos vía `lucide-react`, pasados como referencia de componente en arrays de config (ej. items de navegación), no repetidos por cada item.
- Clases condicionales con `clsx`, no template strings manuales con ternarios anidados.
- Siempre `export default function Nombre()` — sin componentes de clase, sin `React.FC`.
