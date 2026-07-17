/**
 * driveSync.js — Sincronización con Google Drive
 *
 * Flujo:
 *  1. initDriveSync()     → al arrancar la app; suscribe al store para tracking
 *                           de timestamps y auto-guardado. Si hay carpeta guardada,
 *                           intenta token silencioso y carga el último archivo.
 *  2. connectFolder()     → solicita token explícito → abre Google Picker → guarda
 *                           folderId/name en localStorage → carga/crea archivo.
 *  3. saveToDrive()       → verifica si Drive fue modificado por otra instancia;
 *                           si hay cambios, hace merge antes de guardar.
 *  4. resolveMerge()      → el usuario resolvió los conflictos del modal; aplica
 *                           resoluciones y guarda.
 *  5. toggleAutoSave()    → activa/desactiva guardado automático ante cada cambio.
 *  6. disconnect()        → limpia localStorage y estado.
 *
 * Merge multi-instancia:
 *   Cada ítem del store tiene _updatedAt (ISO timestamp de última modificación).
 *   lastSyncedAt (LS_LAST_SYNCED_AT) = modifiedTime de Drive en el último sync.
 *   Antes de cada save, se compara el modifiedTime actual de Drive vs lastSyncedAt.
 *   Si Drive es más nuevo → otra instancia guardó → merge de los dos estados.
 *   Si no hay conflictos → auto-merge silencioso.
 *   Si hay conflictos → status.pendingMerge y el usuario decide en el modal.
 *
 * Requiere en index.html:
 *   <script src="https://accounts.google.com/gsi/client" async defer></script>
 *   <script src="https://apis.google.com/js/api.js" async defer></script>
 */

import useStore                                   from '../store/useStore'
import { markClean }                              from './dirtyTracker'
import { mergeStates, applyResolutions, ts }      from './mergeUtils'

// ── localStorage keys ──────────────────────────────────────────────────────────
const LS_CLIENT_ID      = 'gastnyahp_drive_client_id'
const LS_FOLDER_ID      = 'gastnyahp_drive_folder_id'
const LS_FOLDER_NAME    = 'gastnyahp_drive_folder_name'
const LS_FILE_ID        = 'gastnyahp_drive_last_file_id'
const LS_LAST_MODIFIED  = 'gastnyahp_last_modified'    // timestamp de última modificación local
const LS_LAST_SYNCED_AT = 'gastnyahp_last_synced_at'   // modifiedTime de Drive en último sync exitoso
const LS_AUTOSAVE       = 'gastnyahp_drive_autosave'   // preferencia de auto-guardado

// ── Estado interno ─────────────────────────────────────────────────────────────
let accessToken = null
let folderId    = null
let fileId      = null

let status = {
  connected:    false,
  folderName:   null,
  lastSaved:    null,
  isSaving:     false,
  isLoading:    false,
  error:        null,
  /**
   * { conflicts: Conflict[], merged: object, driveModifiedTime: string } | null
   * Se llena cuando hay conflictos de merge que requieren resolución del usuario.
   */
  pendingMerge: null,
  /** auto-guardado en Drive habilitado */
  autoSave:     false,
}

const listeners = new Set()

function notify() {
  listeners.forEach((fn) => fn({ ...status }))
}

// ── Suscripción al store ───────────────────────────────────────────────────────
// Rastrea modificaciones locales (timestamp) y dispara auto-guardado.

let _prevSnapshot  = null
let _isImporting   = false   // evita que imports de Drive actualicen lastModified
let _autoSaveTimer = null
let _storeUnsub    = null

function _getDataSnapshot(state) {
  const {
    banks, creditCards, installments, loans, services,
    expenses, budgets, fixedExpenses, income, people,
  } = state
  return JSON.stringify({
    banks, creditCards, installments, loans, services,
    expenses, budgets, fixedExpenses, income, people,
  })
}

function _setupDataSubscription() {
  if (_storeUnsub) return  // ya activa
  _prevSnapshot = _getDataSnapshot(useStore.getState())
  _storeUnsub = useStore.subscribe((newState) => {
    if (_isImporting) return
    const snap = _getDataSnapshot(newState)
    if (snap === _prevSnapshot) return
    _prevSnapshot = snap

    // Actualizar timestamp de última modificación local
    localStorage.setItem(LS_LAST_MODIFIED, ts())

    // Auto-guardado si está habilitado y hay conexión activa
    if (status.autoSave && status.connected && !status.pendingMerge && accessToken && folderId) {
      clearTimeout(_autoSaveTimer)
      _autoSaveTimer = setTimeout(() => {
        saveToDrive().catch(console.error)
      }, 2000)
    }
  })
}

// ── API pública ────────────────────────────────────────────────────────────────

/** Estado actual (snapshot inmutable). */
export const getDriveSyncStatus = () => ({ ...status })

/** Suscribirse a cambios de estado. Devuelve función de cleanup. */
export function subscribeDriveSync(fn) {
  listeners.add(fn)
  return () => listeners.delete(fn)
}

/** Leer Client ID desde localStorage. */
export const getClientId = () => localStorage.getItem(LS_CLIENT_ID) || ''

/** Guardar Client ID en localStorage. */
export const setClientId = (id) => localStorage.setItem(LS_CLIENT_ID, id.trim())

/** Preferencia de auto-guardado. */
export const getAutoSave = () => localStorage.getItem(LS_AUTOSAVE) === 'true'

/**
 * Alternar auto-guardado. Si se activa y hay conexión, guarda de inmediato.
 */
export function toggleAutoSave() {
  const next = !status.autoSave
  localStorage.setItem(LS_AUTOSAVE, String(next))
  status = { ...status, autoSave: next }
  notify()
  if (next && status.connected && !status.pendingMerge && accessToken && folderId) {
    saveToDrive().catch(console.error)
  }
}

/**
 * Inicializar al arrancar la app.
 * Suscribe al store para timestamp y auto-save.
 * Si hay folderId guardado, intenta autenticación silenciosa y carga el archivo.
 */
export async function initDriveSync() {
  // Siempre suscribir al store (tracking de timestamp + auto-guardado)
  _setupDataSubscription()

  // Leer preferencia de auto-guardado guardada
  status = { ...status, autoSave: getAutoSave() }

  folderId          = localStorage.getItem(LS_FOLDER_ID)
  fileId            = localStorage.getItem(LS_FILE_ID)
  const savedName   = localStorage.getItem(LS_FOLDER_NAME)

  if (!folderId) return

  status = { ...status, folderName: savedName, isLoading: true }
  notify()

  try {
    await _getToken(/* silent */ true)
    await _loadLatestFile()
  } catch (e) {
    // No hay Client ID → el usuario nunca llegó a configurar bien la app
    const noClientId = !getClientId()
    status = {
      ...status,
      connected: false,
      isLoading: false,
      error: noClientId
        ? 'Client ID no configurado. Configurá Drive en Ajustes.'
        : 'Sesión expirada. Reconectá para continuar con el auto-guardado.',
    }
    notify()
  }
}

/**
 * Reconectar con la carpeta ya guardada, sin abrir el Picker.
 * Intenta token silencioso primero; si falla, pide uno explícito
 * (popup de cuenta Google, sin selector de carpeta).
 * Usar desde el modal de sesión expirada.
 */
export async function reconnect() {
  const savedFolder = localStorage.getItem(LS_FOLDER_ID)
  if (!savedFolder) throw new Error('No hay carpeta guardada. Usá "Conectar con Drive".')

  const clientId = getClientId()
  if (!clientId) throw new Error('Configurá el Client ID en Ajustes antes de conectar.')

  folderId = savedFolder
  fileId   = localStorage.getItem(LS_FILE_ID)

  status = { ...status, isLoading: true, error: null }
  notify()

  try {
    // 1. Intento silencioso (sin UI si la sesión Google sigue activa)
    try {
      await _getToken(/* silent */ true)
    } catch {
      // 2. Si falla, popup de cuenta Google (sin Drive Picker)
      await _getToken(/* silent */ false)
    }
    await _loadLatestFile()
  } catch (e) {
    status = { ...status, isLoading: false, error: e.message }
    notify()
    throw e
  }
}

/**
 * Abrir Google Picker para elegir una carpeta.
 * Luego carga/crea el archivo JSON en ella.
 */
export async function connectFolder() {
  const clientId = getClientId()
  if (!clientId) throw new Error('Configurá el Client ID en Ajustes antes de conectar.')

  status = { ...status, isLoading: true, error: null }
  notify()

  try {
    await _getToken(/* silent */ false)
    await _ensurePickerLoaded()

    const folder     = await _openFolderPicker()
    folderId         = folder.id
    const folderName = folder.name
    localStorage.setItem(LS_FOLDER_ID,   folderId)
    localStorage.setItem(LS_FOLDER_NAME, folderName)

    status = { ...status, connected: true, folderName, error: null }
    notify()

    await _loadLatestFile()
  } catch (e) {
    status = { ...status, isLoading: false, error: e.message }
    notify()
    throw e
  }
}

/**
 * Guardar el estado actual del store en el archivo Drive.
 *
 * Antes de guardar, verifica si Drive fue modificado por otra instancia
 * (comparando modifiedTime vs lastSyncedAt). Si es así, hace merge primero:
 *   - Sin conflictos → auto-merge silencioso, luego guarda.
 *   - Con conflictos → sets status.pendingMerge y retorna (el modal decide).
 */
export async function saveToDrive() {
  if (!folderId || !accessToken) {
    throw new Error('No hay conexión con Google Drive.')
  }

  status = { ...status, isSaving: true, error: null }
  notify()

  try {
    // ── Verificar si Drive fue modificado por otra instancia ──────────────────
    if (fileId) {
      const needsMerge = await _checkIfDriveIsNewer()
      if (needsMerge) {
        // Descargar JSON de Drive y ejecutar merge
        const driveJson         = await _downloadFile()
        const remoteState       = JSON.parse(driveJson)
        const localState        = _extractStoreState()
        const { merged, conflicts } = mergeStates(localState, remoteState)

        if (conflicts.length > 0) {
          // Hay conflictos → mostrar modal, no guardar todavía
          status = {
            ...status,
            isSaving:    false,
            pendingMerge: {
              conflicts,
              merged,
              driveModifiedTime: await _getFileModifiedTime(),
            },
          }
          notify()
          return
        }

        // Sin conflictos → aplicar auto-merge silencioso
        _isImporting = true
        useStore.getState().importData(JSON.stringify(_buildExportData(merged)))
        _isImporting = false
        _prevSnapshot = _getDataSnapshot(useStore.getState())
      }
    }

    // ── Subir el JSON actual ─────────────────────────────────────────────────
    const json          = useStore.getState().exportData()
    const driveModTime  = await _uploadFile(json)

    markClean()
    localStorage.removeItem(LS_LAST_MODIFIED)
    if (driveModTime) localStorage.setItem(LS_LAST_SYNCED_AT, driveModTime)
    _prevSnapshot = _getDataSnapshot(useStore.getState())

    status = {
      ...status,
      isSaving:    false,
      lastSaved:   new Date(),
      connected:   true,
      pendingMerge: null,
    }
    notify()
  } catch (e) {
    console.error('[driveSync] saveToDrive:', e)
    status = { ...status, isSaving: false, error: e.message }
    notify()
    throw e
  }
}

/**
 * Aplicar las resoluciones del usuario sobre los conflictos pendientes y guardar.
 *
 * @param {Record<string, 'local' | 'remote'>} choices - keyed by conflict.id
 */
export async function resolveMerge(choices) {
  if (!status.pendingMerge) return

  const { conflicts, merged } = status.pendingMerge

  // Aplicar decisiones del usuario
  const finalState = applyResolutions(merged, conflicts, choices)

  // Importar el estado final al store
  _isImporting = true
  useStore.getState().importData(JSON.stringify(_buildExportData(finalState)))
  _isImporting = false
  _prevSnapshot = _getDataSnapshot(useStore.getState())
  localStorage.setItem(LS_LAST_MODIFIED, ts())

  status = { ...status, pendingMerge: null }
  notify()

  // Guardar el estado merged en Drive
  await saveToDrive()
}

/** Desconectar Drive: limpia localStorage y estado. */
export function disconnect() {
  clearTimeout(_autoSaveTimer)
  accessToken = null
  folderId    = null
  fileId      = null
  localStorage.removeItem(LS_FOLDER_ID)
  localStorage.removeItem(LS_FOLDER_NAME)
  localStorage.removeItem(LS_FILE_ID)
  localStorage.removeItem(LS_LAST_SYNCED_AT)
  status = {
    connected:    false,
    folderName:   null,
    lastSaved:    null,
    isSaving:     false,
    isLoading:    false,
    error:        null,
    pendingMerge: null,
    autoSave:     status.autoSave, // mantener preferencia
  }
  notify()
}

// ── Internals ──────────────────────────────────────────────────────────────────

/** Solicitar token OAuth con GIS. silent=true usa prompt:'none'. */
function _getToken(silent) {
  const clientId = getClientId()
  if (!clientId) return Promise.reject(new Error('Client ID no configurado.'))

  return new Promise((resolve, reject) => {
    const tokenClient = window.google.accounts.oauth2.initTokenClient({
      client_id: clientId,
      scope:     'https://www.googleapis.com/auth/drive.file',
      prompt:    silent ? 'none' : '',
      callback:  (resp) => {
        if (resp.error) {
          reject(new Error(resp.error_description || resp.error))
        } else {
          accessToken = resp.access_token
          resolve(resp.access_token)
        }
      },
      error_callback: (err) => {
        reject(new Error(err.message || 'Error de autenticación'))
      },
    })
    tokenClient.requestAccessToken()
  })
}

/** Asegurarse de que gapi.picker esté cargado. */
function _ensurePickerLoaded() {
  return new Promise((resolve) => window.gapi.load('picker', resolve))
}

/** Abrir el Picker de Google para seleccionar una carpeta. */
function _openFolderPicker() {
  return new Promise((resolve, reject) => {
    const view = new window.google.picker.DocsView(window.google.picker.ViewId.FOLDERS)
      .setSelectFolderEnabled(true)
      .setIncludeFolders(true)

    const picker = new window.google.picker.PickerBuilder()
      .addView(view)
      .setOAuthToken(accessToken)
      .setTitle('Elegir carpeta de GastNyahp')
      .setCallback((data) => {
        if (data.action === window.google.picker.Action.PICKED) {
          resolve(data.docs[0])
        } else if (data.action === window.google.picker.Action.CANCEL) {
          reject(new Error('Operación cancelada'))
        }
      })
      .build()

    picker.setVisible(true)
  })
}

/**
 * Obtener el modifiedTime actual del archivo en Drive.
 * @returns {Promise<string>} ISO string del modifiedTime
 */
async function _getFileModifiedTime() {
  if (!fileId) return null
  const res = await fetch(
    `https://www.googleapis.com/drive/v3/files/${fileId}?fields=modifiedTime`,
    { headers: { Authorization: `Bearer ${accessToken}` } }
  )
  if (!res.ok) throw new Error(`Drive error ${res.status}`)
  const data = await res.json()
  return data.modifiedTime
}

/**
 * Verificar si Drive fue modificado por otra instancia desde el último sync.
 * @returns {Promise<boolean>} true si Drive es más nuevo que lastSyncedAt
 */
async function _checkIfDriveIsNewer() {
  const lastSyncedAt = localStorage.getItem(LS_LAST_SYNCED_AT)
  if (!lastSyncedAt) return false  // primera vez → no hay base de comparación

  try {
    const driveModTime = await _getFileModifiedTime()
    if (!driveModTime) return false
    return new Date(driveModTime) > new Date(lastSyncedAt)
  } catch {
    return false  // si falla la consulta, proceder sin merge
  }
}

/**
 * Descargar el contenido del archivo de Drive.
 * @returns {Promise<string>} JSON string del archivo
 */
async function _downloadFile() {
  const res = await fetch(
    `https://www.googleapis.com/drive/v3/files/${fileId}?alt=media`,
    { headers: { Authorization: `Bearer ${accessToken}` } }
  )
  if (!res.ok) throw new Error(`Drive error ${res.status}`)
  return res.text()
}

/**
 * Subir el JSON al archivo de Drive (PATCH si existe, POST si es nuevo).
 * @returns {Promise<string|null>} modifiedTime del archivo después de subir
 */
async function _uploadFile(json) {
  if (fileId) {
    const res = await fetch(
      `https://www.googleapis.com/upload/drive/v3/files/${fileId}?uploadType=media&fields=modifiedTime`,
      {
        method:  'PATCH',
        headers: {
          Authorization:  `Bearer ${accessToken}`,
          'Content-Type': 'application/json',
        },
        body: json,
      }
    )
    if (!res.ok) throw new Error(`Drive error ${res.status}: ${await res.text()}`)
    const updated = await res.json()
    return updated.modifiedTime || null
  } else {
    const meta = {
      name:     'gastnyahp-data.json',
      mimeType: 'application/json',
      parents:  [folderId],
    }
    const form = new FormData()
    form.append('metadata', new Blob([JSON.stringify(meta)], { type: 'application/json' }))
    form.append('file',     new Blob([json],                  { type: 'application/json' }))

    const res = await fetch(
      'https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,modifiedTime',
      {
        method:  'POST',
        headers: { Authorization: `Bearer ${accessToken}` },
        body:    form,
      }
    )
    if (!res.ok) throw new Error(`Drive error ${res.status}: ${await res.text()}`)
    const created = await res.json()
    fileId = created.id
    localStorage.setItem(LS_FILE_ID, fileId)
    return created.modifiedTime || null
  }
}

/**
 * Extraer el estado relevante del store (sin funciones ni currentMonth).
 */
function _extractStoreState() {
  const { banks, creditCards, installments, loans, services, expenses, budgets, fixedExpenses, income, people } = useStore.getState()
  return { banks, creditCards, installments, loans, services, expenses, budgets, fixedExpenses, income, people }
}

/**
 * Construir un objeto de datos para exportar/importar desde un estado plain.
 */
function _buildExportData(state) {
  return {
    meta: { version: '1.4', exported: ts(), lastModified: ts() },
    ...state,
  }
}

/**
 * Buscar gastnyahp-data.json en la carpeta conectada.
 * Si Drive es más nuevo que lastSyncedAt, hace merge antes de importar.
 * Si no hay lastSyncedAt (primer acceso), simplemente importa el archivo de Drive.
 */
async function _loadLatestFile() {
  status = { ...status, isLoading: true }
  notify()

  try {
    const q   = encodeURIComponent(
      `'${folderId}' in parents and name = 'gastnyahp-data.json' and trashed = false`
    )
    const res = await fetch(
      `https://www.googleapis.com/drive/v3/files?q=${q}&orderBy=modifiedTime+desc&pageSize=1&fields=files(id,name,modifiedTime)`,
      { headers: { Authorization: `Bearer ${accessToken}` } }
    )
    if (!res.ok) throw new Error(`Drive error ${res.status}`)

    const list = await res.json()

    if (!list.files?.length) {
      // No hay archivo aún — se creará en el primer save
      status = { ...status, connected: true, isLoading: false }
      notify()
      return
    }

    const file      = list.files[0]
    fileId          = file.id
    const driveTime = new Date(file.modifiedTime)
    localStorage.setItem(LS_FILE_ID, fileId)

    // ── ¿Hay cambios locales no sincronizados? ─────────────────────────────
    const lastSyncedAt     = localStorage.getItem(LS_LAST_SYNCED_AT)
    const localLastModified = localStorage.getItem(LS_LAST_MODIFIED)
    const driveIsNewer     = lastSyncedAt && new Date(file.modifiedTime) > new Date(lastSyncedAt)
    const localHasChanges  = localLastModified && lastSyncedAt && new Date(localLastModified) > new Date(lastSyncedAt)

    // Descargar el archivo de Drive
    const driveJson = await _downloadFile()

    if (driveIsNewer && localHasChanges) {
      // Ambos lados tienen cambios → merge
      const remoteState = JSON.parse(driveJson)
      const localState  = _extractStoreState()
      const { merged, conflicts } = mergeStates(localState, remoteState)

      if (conflicts.length > 0) {
        // Conflictos → el usuario decide en el modal
        status = {
          ...status,
          connected:    true,
          isLoading:    false,
          lastSaved:    driveTime,
          pendingMerge: {
            conflicts,
            merged,
            driveModifiedTime: file.modifiedTime,
          },
          error: null,
        }
        notify()
        return
      }

      // Sin conflictos → auto-merge silencioso
      _isImporting = true
      useStore.getState().importData(JSON.stringify(_buildExportData(merged)))
      _isImporting = false
    } else if (driveIsNewer || !lastSyncedAt) {
      // Drive es más nuevo y no hay cambios locales (o es primer acceso) → importar directo
      _isImporting = true
      const result = useStore.getState().importData(driveJson)
      _isImporting = false
      if (result.error) throw new Error('Error al leer el archivo: ' + result.error)
    }
    // else: local tiene cambios más nuevos que Drive → no importar, dejar que el próximo save sincronice

    // Sincronizar snapshot y registrar timestamp de Drive como último sync
    _prevSnapshot = _getDataSnapshot(useStore.getState())
    localStorage.setItem(LS_LAST_SYNCED_AT, file.modifiedTime)
    markClean()

    status = {
      ...status,
      connected:    true,
      isLoading:    false,
      lastSaved:    driveTime,
      pendingMerge: null,
      error:        null,
    }
    notify()
  } catch (e) {
    _isImporting = false
    console.error('[driveSync] _loadLatestFile:', e)
    status = { ...status, connected: false, isLoading: false, error: e.message }
    notify()
  }
}
