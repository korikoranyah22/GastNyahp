---
name: tailwind-ui-system
description: Sistema de diseño con TailwindCSS (paleta oscura, componentes base, layout responsive) inspirado en GastNyahp. Usar al maquetar pantallas o componentes UI nuevos.
---

# Tailwind UI system

Sistema de diseño minimalista: dark-mode por defecto, utilidades de Tailwind directo en el JSX (sin CSS modules ni styled-components), un puñado de componentes base reutilizables.

## Paleta (dark, valores arbitrarios de Tailwind)

| Uso                     | Valor      |
|-------------------------|------------|
| Fondo de página         | `#0d0f14`  |
| Superficie / card       | `#151820`  |
| Superficie elevada / input | `#1c2030` |
| Borde                   | `#2e3350`  |
| Borde hover              | `#3d4466`  |
| Texto principal          | `#e2e8f0` / `white` |
| Texto secundario         | `#94a3b8`  |
| Texto muted              | `#64748b`  |
| Texto muy tenue / label  | `#3d4466`  |

Usar estos como clases arbitrarias (`bg-[#151820]`, `border-[#2e3350]`, `text-[#64748b]`) de forma consistente en todos los componentes — es lo que da la sensación de "un solo sistema" sin necesitar un archivo de tema separado.

## Recetas de componentes

**Botón primario**
```
bg-blue-600 hover:bg-blue-500 text-white text-sm font-medium rounded-lg px-4 py-2 transition-colors
```

**Botón secundario / cancelar**
```
border border-[#2e3350] text-[#94a3b8] hover:text-white hover:bg-[#1c2030] rounded-lg px-4 py-2 text-sm transition-colors
```

**Input**
```
w-full px-3 py-2 bg-[#1c2030] border border-[#2e3350] rounded-lg text-white text-sm
focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500/30
placeholder:text-[#3d4466]
```

**Badge / chip semántico** — tint del color + texto + borde, todos con la misma fórmula:
```
bg-{color}-500/15 text-{color}-400 border border-{color}-500/20   // blue, green, red, orange, purple, yellow
bg-[#2e3350] text-[#94a3b8] border border-[#3d4466]                 // gray/neutral
```

**Card**
```
bg-[#151820] border border-[#2e3350] rounded-xl p-5 hover:border-[#3d4466] transition-colors
```

Escala de radios: `rounded-md` (badges) < `rounded-lg` (botones/inputs) < `rounded-xl`/`rounded-2xl` (cards, contenedores grandes). Siempre `transition-colors` en elementos interactivos.

## Componentes base a construir primero

1. **`Badge`** — variantes por color, una sola fórmula de clases (arriba).
2. **`EmptyState`** — ícono + título + descripción + acción opcional, para listados vacíos.
3. **`SlideOver`** — panel lateral deslizante (no modal centrado) para crear/editar entidades:
   ```jsx
   <div className="fixed inset-0 z-50 flex">
     <div className="fixed inset-0 bg-black/60 fade-in" onClick={onClose} />
     <div className="relative ml-auto h-full max-w-md w-full bg-[#151820] border-l border-[#2e3350] shadow-2xl flex flex-col slide-in">
       {/* header con título + botón cerrar, luego children con scroll */}
     </div>
   </div>
   ```
   Se prefiere sobre un modal centrado porque deja más espacio para formularios largos y se siente menos intrusivo en mobile.

## Animaciones globales

Definir una sola vez en el CSS global, aplicar por clase utilitaria (no animar cada componente por separado):

```css
@keyframes slideInRight { from { transform: translateX(100%); opacity: 0; } to { transform: translateX(0); opacity: 1; } }
@keyframes fadeIn       { from { opacity: 0; } to { opacity: 1; } }
.slide-in { animation: slideInRight 0.25s ease-out; }
.fade-in  { animation: fadeIn 0.15s ease-out; }
```

## Layout responsive (shell de la app)

- **Desktop/tablet:** `Sidebar` fijo a la izquierda, oculto en mobile (`hidden md:flex`), colapsable con `useState(collapsed)` local.
- **Mobile:** `BottomNav` fijo abajo, con `md:hidden` interno (así el mismo componente decide su propia visibilidad, no el padre).
- El `<main>` de contenido lleva `pb-14 md:pb-0` para no quedar tapado por el bottom nav en mobile.

```jsx
<div className="flex h-screen bg-[#0d0f14] text-[#e2e8f0] overflow-hidden">
  <div className="hidden md:flex shrink-0"><Sidebar /></div>
  <div className="flex flex-col flex-1 min-w-0">
    <Header />
    <main className="flex-1 overflow-auto pb-14 md:pb-0"><Outlet /></main>
  </div>
  <BottomNav />
</div>
```

## Theming opcional (avanzado — omitir en un MVP)

Si más adelante se necesita un segundo tema, no reescribir componentes: agregar overrides de CSS scopeados a `[data-theme="nombre"]` sobre las mismas clases arbitrarias ya usadas, más un singleton `themeManager.js` (`getTheme/subscribeTheme/toggleTheme`) consumido vía hook (ver `react-component-patterns`). Esto evita tocar el JSX de cada pantalla.
