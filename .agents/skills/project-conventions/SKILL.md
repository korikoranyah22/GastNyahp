---
name: project-conventions
description: Convenciones generales de código, comentarios y tooling (ESLint, Vite, scripts) inspiradas en GastNyahp. Usar como referencia de estilo general al escribir o revisar código en el proyecto.
---

# Project conventions

Reglas generales de estilo y tooling, transversales a front y back.

## Comentarios

- **Identificadores siempre en inglés** (variables, funciones, archivos, componentes).
- **Comentarios en el idioma del equipo** (ej. español), y solo cuando explican el *por qué*, no el qué: una regla de negocio no obvia, una restricción oculta, un workaround puntual. Si el nombre ya lo dice, no hace falta comentario.
- **Banners de sección** para dividir archivos largos (stores, configs) en bloques navegables:
  ```js
  // ─── Nombre de la sección ───────────────────────────────────────────────────
  ```
- **JSDoc corto** (`/** ... */`) solo arriba de funciones cuya lógica no es evidente por el nombre — nunca docstrings de varios párrafos.

## Sin abstracción prematura

- Un formulario vive junto a su página hasta que el mismo patrón se repite en 3+ lugares — recién ahí se extrae a un componente compartido (así surgieron los componentes base del sistema de UI).
- Preferir 3 líneas repetidas antes que una función genérica parametrizada que solo tiene un caso de uso real.
- No agregar manejo de errores, validaciones o fallbacks para escenarios que no pueden pasar dado el resto del código.

## Tooling baseline (front-end)

- **Vite** + `@vitejs/plugin-react`.
- **ESLint flat config** (`eslint.config.js`) extendiendo `js.configs.recommended` + `eslint-plugin-react-hooks` + `eslint-plugin-react-refresh`, con `no-unused-vars` permitiendo constantes (`varsIgnorePattern: '^[A-Z_]'`).
- **TailwindCSS** vía su plugin oficial de Vite (`@tailwindcss/vite`), sin archivo de configuración de PostCSS aparte.
- `package.json` con scripts mínimos: `dev`, `build`, `lint`, `preview`. Agregar `test` solo si hay tests reales.

```js
// eslint.config.js
import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import { defineConfig, globalIgnores } from 'eslint/config'

export default defineConfig([
  globalIgnores(['dist']),
  {
    files: ['**/*.{js,jsx}'],
    extends: [js.configs.recommended, reactHooks.configs.flat.recommended, reactRefresh.configs.vite],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
      parserOptions: { ecmaVersion: 'latest', ecmaFeatures: { jsx: true }, sourceType: 'module' },
    },
    rules: { 'no-unused-vars': ['error', { varsIgnorePattern: '^[A-Z_]' }] },
  },
])
```

## Formatters y utilidades centralizadas

- Formato de moneda y fechas centralizado en `lib/formatters.js` — nunca formatear inline dentro del JSX más allá de llamar a estos helpers.
- Moneda vía `Intl.NumberFormat` con el locale/currency del proyecto; fechas vía `date-fns` + su locale correspondiente.

```js
export function formatAmount(amount) {
  if (amount === null || amount === undefined || amount === '') return '—'
  return new Intl.NumberFormat('es-AR', { style: 'currency', currency: 'ARS', maximumFractionDigits: 2 }).format(amount)
}
```

## Datos como JSON serializable

Todo el estado de la app (front) debe poder serializarse a JSON en cualquier momento: sin `Map`/`Set`/clases con métodos en el estado, solo arrays y objetos planos. Esto habilita el patrón de import/export (`zustand-store-patterns`) y que ese mismo estado viaje tal cual por HTTP hacia el backend real (`react-feature-module`).

Estas mismas reglas (identificadores en inglés, comentarios que explican el *por qué* no el *qué*, sin abstracción prematura) aplican igual del lado backend en C#/.NET — ver `csharp-conventions-and-patterns`.
