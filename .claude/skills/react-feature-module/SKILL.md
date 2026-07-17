---
name: react-feature-module
description: Agregar un feature de frontend en React + TypeScript con el patrón cliente API tipado, store global (Zustand) y hook de carga — consumiendo un endpoint del backend. Usar al conectar una pantalla/componente nuevo a datos que vienen del backend.
---

# react-feature-module

Patrón de tres capas para conectar el frontend a un endpoint del backend (ver [[aspnet-rest-endpoint]]):
**cliente API tipado** → **store global (Zustand)** → **hook de carga** que un componente consume. El
componente nunca hace `fetch` directo.

En GastNyahp esto es la migración de cada slice del store local (ver `zustand-store-patterns`, que documenta el
store actual 100% en `localStorage`) hacia datos que vienen del backend real — mismo naming de acciones y mismo
shape de error `{ error }`, pero la acción ahora llama a `api/<recurso>.ts` en vez de mutar el array local
directo. Qué aggregate/evento de Eventuous corresponde a cada slice está mapeado en `gastnyahp-domain-model`.

## Cuándo usar / cuándo no

- **Usar**: cualquier pantalla que necesita leer y/o escribir datos que vienen del backend.
- **No usar**: para estado puramente local de UI (un modal abierto/cerrado, un input controlado) — eso es
  `useState` normal dentro del componente, sin pasar por el store global.

## 1. Cliente API — un archivo por recurso, wrapper `apiFetch` compartido

```ts
// src/api/client.ts
const BASE = '/api'

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })
  if (!res.ok) throw new Error(`API ${res.status}: ${await res.text()}`)
  if (res.status === 204) return undefined as T
  return res.json()
}
```

```ts
// src/api/tasks.ts
import { apiFetch } from './client'
import type { Task } from '../types'

export interface CreateTaskRequest {
  id: string
  title: string
  createdBy: string
}

export async function listTasks(): Promise<Task[]> {
  return apiFetch<Task[]>('/tasks')
}

export async function createTask(req: CreateTaskRequest): Promise<void> {
  await apiFetch<void>('/tasks', { method: 'POST', body: JSON.stringify(req) })
}

export async function completeTask(id: string, note?: string): Promise<void> {
  await apiFetch<void>(`/tasks/${id}/complete`, {
    method: 'POST',
    body: JSON.stringify({ note: note ?? null }),
  })
}
```

Reglas:
- `BASE = '/api'` relativo — en Docker, nginx lo proxea al backend (ver
  [[docker-compose-service-network]]); en dev standalone, el dev server de Vite lo proxea igual.
- Un archivo por recurso (`api/tasks.ts`, `api/characters.ts`), nunca un cliente API monolítico.
- Los tipos de request van como `interface` exportada junto a la función que los usa; los tipos de dominio
  compartidos (`Task`) van en `src/types/index.ts`.
- Manejo de 404 como "no existe" en vez de error, cuando aplica: `catch` puntual chequeando el mensaje/status,
  no un catch-all silencioso.

## 2. Store global (Zustand) — un slice de estado + acciones que llaman al cliente API

```ts
// src/stores/useTaskStore.ts
import { create } from 'zustand'
import { listTasks, createTask as apiCreateTask, completeTask as apiCompleteTask } from '../api/tasks'
import type { Task } from '../types'

interface TaskState {
  tasks: Task[]
  loadTasks: () => Promise<void>
  createTask: (id: string, title: string, createdBy: string) => Promise<void>
  completeTask: (id: string, note?: string) => Promise<void>
}

export const useTaskStore = create<TaskState>((set, get) => ({
  tasks: [],

  loadTasks: async () => {
    const tasks = await listTasks()
    set({ tasks })
  },

  createTask: async (id, title, createdBy) => {
    await apiCreateTask({ id, title, createdBy })
    await get().loadTasks() // re-sincroniza desde el backend en vez de construir el objeto a mano en el front
  },

  completeTask: async (id, note) => {
    await apiCompleteTask(id, note)
    set({ tasks: get().tasks.map(t => t.id === id ? { ...t, status: 'Done' } : t) })
  },
}))
```

- El store llama SIEMPRE al cliente API (`api/tasks.ts`), nunca a `fetch` directo.
- Para escrituras: o bien re-fetch (`loadTasks()` de nuevo — más simple, más tráfico) o actualización optimista
  local (más rápido, hay que mantenerlo en sync manualmente) — elegí re-fetch por default salvo que la
  latencia importe.
- `persist` middleware de Zustand solo para estado que debe sobrevivir un refresh de página (selección activa,
  preferencias de UI) — no para datos que vienen del backend y pueden quedar desactualizados.

## 3. Hook de carga — conecta el store a un componente con cancelación

```ts
// src/hooks/useTasks.ts
import { useEffect, useRef } from 'react'
import { useTaskStore } from '../stores/useTaskStore'

export function useTasks() {
  const { tasks, loadTasks } = useTaskStore()
  const loadedRef = useRef(false)

  useEffect(() => {
    if (loadedRef.current) return
    loadedRef.current = true
    loadTasks()
  }, [loadTasks])

  return tasks
}
```

Para cargas que dependen de un id que puede cambiar (ej. `useConversation(conversationId)`), agregá un `let
cancelled = false` dentro del efecto y un `return () => { cancelled = true }`, chequeando `if (cancelled)
return` antes de aplicar el resultado — evita pisar el estado con la respuesta de un fetch viejo cuando el id
cambia rápido (navegación).

## 4. Componente — solo consume el hook, cero fetch/estado global directo

```tsx
export function TaskList() {
  const tasks = useTasks()
  const completeTask = useTaskStore(s => s.completeTask)

  return (
    <ul>
      {tasks.map(t => (
        <li key={t.id}>
          {t.title} — {t.status}
          {t.status !== 'Done' && <button onClick={() => completeTask(t.id)}>Completar</button>}
        </li>
      ))}
    </ul>
  )
}
```

## Realtime (opcional) — push desde el backend vía WebSocket/SignalR

Si el backend expone un hub de tiempo real (ver nota de operaciones largas en [[aspnet-rest-endpoint]]), el
patrón es un hook dedicado que arma la conexión, escucha eventos, y actualiza el store directamente (no vía
polling):

```ts
import { HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr'

export function useRealtimeTasks() {
  const connRef = useRef<HubConnection | null>(null)

  const connect = useCallback(async () => {
    const conn = new HubConnectionBuilder().withUrl('/tasksHub').withAutomaticReconnect().build()
    conn.on('TaskUpdated', (task: Task) => {
      useTaskStore.setState(s => ({ tasks: s.tasks.map(t => t.id === task.id ? task : t) }))
    })
    await conn.start()
    connRef.current = conn
  }, [])

  return { connect }
}
```

El path del hub (`/tasksHub`) tiene que estar proxeado en el nginx del frontend con `Upgrade`/`Connection`
headers (ver [[docker-compose-service-network]]) — sin eso, el WebSocket falla en producción aunque funcione
en dev.

## Procedimiento

1. Definí el tipo de dominio en `src/types/index.ts` (matcheando el shape JSON del backend, camelCase).
2. Escribí `src/api/<recurso>.ts` con una función por operación HTTP.
3. Escribí (o extendé) el store con el slice de estado + acciones.
4. Escribí el hook de carga si el componente necesita disparar la carga inicial.
5. El componente consume el hook + las acciones del store — nunca `fetch` ni `useEffect` con lógica de red
   directa dentro del componente de UI.

## Verificación

- `npm run build` (o el typecheck del proyecto) limpio.
- Probar en el browser: la lista carga, crear/completar actualiza la UI sin refresh manual.
- Si hay realtime: forzar un cambio desde otro cliente/pestaña y confirmar que se propaga sin refrescar.

## Anti-patrones

- ❌ `fetch` directo dentro de un componente — siempre pasa por `api/<recurso>.ts`.
- ❌ Duplicar la URL base (`/api`) en cada archivo de api en vez de reusar `apiFetch`.
- ❌ Estado de servidor guardado con `persist` de Zustand (queda stale al reabrir la app).
- ❌ Un hook de carga sin guard de "ya cargado"/cancelación — dispara fetches duplicados en cada re-render o
  pisa el estado con una respuesta añeja al cambiar de id rápido.
- ❌ Mezclar estado de UI puramente local (modal abierto) en el store global — infla el store y complica el
  testing sin necesidad.
