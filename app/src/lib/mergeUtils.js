/**
 * mergeUtils.js — Utilidades para merge multi-instancia con Google Drive
 *
 * Cada "unidad" (item top-level y sub-array entry) tiene:
 *   - _updatedAt: ISO timestamp de última modificación (auto-generado en cada mutación)
 *
 * Algoritmo de merge:
 *   1. Por cada array, comparar items por campo 'id' (top-level) o 'month'/'id' (sub-items)
 *   2. Solo en local  → auto-tomar (local wins)
 *   3. Solo en remote → auto-tomar (remote aporta dato nuevo)
 *   4. En ambos:
 *        remote._updatedAt > local._updatedAt → CONFLICT (mostrar al usuario)
 *        local._updatedAt >= remote._updatedAt → local wins (auto-merge silencioso)
 *   5. Sin conflictos → auto-merge (union de ambos lados)
 *   6. Con conflictos → modal para que el usuario decida por ítem
 *
 * Sub-arrays (months, amounts, items):
 *   Siempre se mergean en modo last-write-wins sin generar conflictos propios.
 *   El conflicto se registra a nivel del ítem padre.
 */

export const ts = () => new Date().toISOString()

// ── Merge de arrays top-level ──────────────────────────────────────────────────

/**
 * Merge de un array de items top-level (clave: campo 'id').
 *
 * @param {string}   arrayName  - nombre del array (para metadata del conflicto)
 * @param {object[]} localArr   - array local
 * @param {object[]} remoteArr  - array remoto (de Drive)
 * @returns {{ merged: object[], conflicts: object[] }}
 */
export function mergeTopLevelArray(arrayName, localArr, remoteArr) {
  const merged    = []
  const conflicts = []

  const localMap  = new Map((localArr  || []).map((i) => [i.id, i]))
  const remoteMap = new Map((remoteArr || []).map((i) => [i.id, i]))
  const allIds    = new Set([...localMap.keys(), ...remoteMap.keys()])

  for (const id of allIds) {
    const local  = localMap.get(id)
    const remote = remoteMap.get(id)

    if (!remote) {
      merged.push(local)   // solo en local → keep
    } else if (!local) {
      merged.push(remote)  // solo en remote → add
    } else {
      const lt = local._updatedAt  || '1970-01-01T00:00:00.000Z'
      const rt = remote._updatedAt || '1970-01-01T00:00:00.000Z'

      if (rt > lt) {
        // Remote es más nuevo → CONFLICT
        conflicts.push({ arrayName, id, local, remote })
        merged.push(local)  // provisional; applyResolutions lo puede reemplazar
      } else {
        merged.push(local)  // local es igual o más nuevo → keep local
      }
    }
  }

  return { merged, conflicts }
}

// ── Merge de sub-arrays ────────────────────────────────────────────────────────

/**
 * Merge de un sub-array (e.g., service.amounts, loan.months, expense.items).
 * Estrategia: last-write-wins por _updatedAt, sin generar conflictos propios.
 * Items solo en un lado siempre se incluyen (union).
 *
 * @param {object[]} localArr   - sub-array local
 * @param {object[]} remoteArr  - sub-array remoto
 * @param {string}   keyField   - campo clave ('month' o 'id')
 * @returns {object[]}
 */
export function mergeSubArray(localArr, remoteArr, keyField) {
  if (!localArr?.length  && !remoteArr?.length)  return []
  if (!remoteArr?.length) return localArr  || []
  if (!localArr?.length)  return remoteArr || []

  const localMap  = new Map(localArr.map( (i) => [i[keyField], i]))
  const remoteMap = new Map(remoteArr.map((i) => [i[keyField], i]))
  const allKeys   = new Set([...localMap.keys(), ...remoteMap.keys()])
  const merged    = []

  for (const key of allKeys) {
    const local  = localMap.get(key)
    const remote = remoteMap.get(key)

    if (!remote) { merged.push(local);  continue }
    if (!local)  { merged.push(remote); continue }

    const lt = local._updatedAt  || '1970-01-01T00:00:00.000Z'
    const rt = remote._updatedAt || '1970-01-01T00:00:00.000Z'
    merged.push(rt > lt ? remote : local)
  }

  if (keyField === 'month') merged.sort((a, b) => a.month.localeCompare(b.month))
  return merged
}

// ── Sub-arrays por tipo de array ───────────────────────────────────────────────

const ITEM_SUB_ARRAYS = {
  installments:  [{ field: 'months',  keyField: 'month' }],
  loans:         [{ field: 'months',  keyField: 'month' }],
  services:      [{ field: 'amounts', keyField: 'month' }],
  fixedExpenses: [{ field: 'months',  keyField: 'month' }],
  expenses:      [{ field: 'items',   keyField: 'id'    }], // solo tickets
}

// ── Merge del estado completo ──────────────────────────────────────────────────

/**
 * Merge completo del estado local vs remoto.
 *
 * @param {object} localState  - estado actual del store
 * @param {object} remoteState - estado del JSON descargado de Drive
 * @returns {{ merged: object, conflicts: object[] }}
 */
export function mergeStates(localState, remoteState) {
  const allConflicts = []
  const merged       = {}

  // ── Arrays top-level ────────────────────────────────────────────────────────
  const allArrays = [
    'banks', 'creditCards', 'people',
    'installments', 'loans', 'services', 'fixedExpenses', 'expenses',
  ]

  for (const key of allArrays) {
    const { merged: m, conflicts: c } = mergeTopLevelArray(
      key,
      localState[key]  || [],
      remoteState[key] || [],
    )
    allConflicts.push(...c)

    // Para items con sub-arrays, mergear sub-arrays granularmente
    const subDefs = ITEM_SUB_ARRAYS[key]
    if (!subDefs) {
      merged[key] = m
      continue
    }

    merged[key] = m.map((item) => {
      const localItem  = (localState[key]  || []).find((i) => i.id === item.id)
      const remoteItem = (remoteState[key] || []).find((i) => i.id === item.id)

      // Solo en un lado → sin merge de sub-arrays (ya tiene los datos correctos)
      if (!localItem || !remoteItem) return item

      let result = { ...item }
      for (const { field, keyField } of subDefs) {
        // expense.items solo para tickets
        if (field === 'items' && item.type !== 'ticket') continue
        result[field] = mergeSubArray(
          localItem[field]  || [],
          remoteItem[field] || [],
          keyField,
        )
      }
      return result
    })
  }

  // ── Budgets (objeto plano keyed por mes) ─────────────────────────────────────
  const lb        = localState.budgets  || {}
  const rb        = remoteState.budgets || {}
  const allMonths = new Set([...Object.keys(lb), ...Object.keys(rb)])
  merged.budgets  = {}

  for (const month of allMonths) {
    const l = lb[month]
    const r = rb[month]
    if (!r) { merged.budgets[month] = l; continue }
    if (!l) { merged.budgets[month] = r; continue }
    const lt = l._updatedAt || '1970-01-01T00:00:00.000Z'
    const rt = r._updatedAt || '1970-01-01T00:00:00.000Z'
    if (rt > lt) {
      allConflicts.push({ arrayName: 'budgets', id: month, local: l, remote: r })
      merged.budgets[month] = l  // provisional
    } else {
      merged.budgets[month] = l
    }
  }

  // ── Income (objeto único) ─────────────────────────────────────────────────────
  const li   = localState.income  || {}
  const ri   = remoteState.income || {}
  const lt_i = li._updatedAt || '1970-01-01T00:00:00.000Z'
  const rt_i = ri._updatedAt || '1970-01-01T00:00:00.000Z'

  if (rt_i > lt_i && lt_i !== '1970-01-01T00:00:00.000Z') {
    // Ambos tienen _updatedAt y el remoto es más nuevo → conflict
    allConflicts.push({ arrayName: 'income', id: 'income', local: li, remote: ri })
    merged.income = li  // provisional
  } else if (rt_i > lt_i) {
    // Local nunca tuvo timestamp → tomar remoto directamente
    merged.income = ri
  } else {
    merged.income = li
  }

  return { merged, conflicts: allConflicts }
}

// ── Aplicar resoluciones del usuario ──────────────────────────────────────────

/**
 * Aplica las decisiones del usuario sobre los conflictos detectados.
 *
 * @param {object}   mergedState - estado después del merge automático
 * @param {object[]} conflicts   - lista de conflictos de mergeStates()
 * @param {Record<string, 'local' | 'remote'>} choices - decisiones por conflicto.id
 * @returns {object} estado final listo para importar
 */
export function applyResolutions(mergedState, conflicts, choices) {
  // Deep-copy shallow para no mutar el original
  const state = {
    ...mergedState,
    budgets: { ...mergedState.budgets },
  }

  for (const conflict of conflicts) {
    const choice = choices[conflict.id] ?? 'local'
    if (choice !== 'remote') continue

    if (conflict.arrayName === 'income') {
      state.income = conflict.remote
    } else if (conflict.arrayName === 'budgets') {
      state.budgets[conflict.id] = conflict.remote
    } else {
      state[conflict.arrayName] = state[conflict.arrayName].map((item) =>
        item.id === conflict.id ? conflict.remote : item
      )
    }
  }

  return state
}

// ── Metadata de conflictos para el modal ──────────────────────────────────────

const ARRAY_META = {
  banks:         { screen: 'Bancos',      icon: '🏦', labelFn: (i)    => i.name },
  creditCards:   { screen: 'Tarjetas',    icon: '💳', labelFn: (i)    => i.label || i.name },
  installments:  { screen: 'Cuotas',      icon: '📦', labelFn: (i)    => i.description || '(sin nombre)' },
  loans:         { screen: 'Préstamos',   icon: '💰', labelFn: (i)    => i.description || '(sin nombre)' },
  services:      { screen: 'Servicios',   icon: '⚡', labelFn: (i)    => i.name || '(sin nombre)' },
  expenses:      { screen: 'Gastos',      icon: '🛒', labelFn: (i)    => i.description || `Gasto del ${i.date}` },
  fixedExpenses: { screen: 'Reservas',    icon: '📌', labelFn: (i)    => i.name || '(sin nombre)' },
  people:        { screen: 'Personas',    icon: '👤', labelFn: (i)    => i.name || '(sin nombre)' },
  budgets:       { screen: 'Presupuesto', icon: '📊', labelFn: (_, k) => `Presupuesto ${k}` },
  income:        { screen: 'Ingresos',    icon: '💵', labelFn: ()     => 'Configuración de ingresos' },
}

/**
 * Devuelve metadata legible de un conflicto para mostrar en el modal.
 *
 * @param {object} conflict - conflicto de mergeStates()
 * @returns {{ screen, icon, label, localUpdatedAt, remoteUpdatedAt, arrayName, id }}
 */
export function getConflictMeta(conflict) {
  const meta = ARRAY_META[conflict.arrayName] || {
    screen: conflict.arrayName,
    icon:   '📋',
    labelFn: (i) => i.id || '(desconocido)',
  }
  return {
    screen:          meta.screen,
    icon:            meta.icon,
    label:           meta.labelFn(conflict.remote, conflict.id),
    localUpdatedAt:  conflict.local._updatedAt  || null,
    remoteUpdatedAt: conflict.remote._updatedAt || null,
    arrayName:       conflict.arrayName,
    id:              conflict.id,
  }
}
