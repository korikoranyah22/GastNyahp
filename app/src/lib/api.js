// Cliente HTTP hacia el backend real (react-feature-module): BASE relativo — nginx lo proxea en Docker,
// el dev server de Vite en desarrollo. La credencial es el token de miembro (posesión = identidad, como
// antes lo era poseer el archivo JSON).

const BASE = '/api'
const TOKEN_KEY = 'gastnyahp_token'

export const getToken = () => localStorage.getItem(TOKEN_KEY)
export const setToken = (token) => localStorage.setItem(TOKEN_KEY, token)
export const clearToken = () => localStorage.removeItem(TOKEN_KEY)

export class ApiError extends Error {
  constructor(status, message, body) {
    super(message)
    this.status = status
    this.body = body   // el JSON parseado, si vino: el login lee `families` del 300 (DISENO_CUENTAS_LOGIN.md §3.2)
  }
}

async function apiFetch(path, { method = 'GET', body, auth = true } = {}) {
  const headers = { 'Content-Type': 'application/json' }
  if (auth) {
    const token = getToken()
    if (token) headers.Authorization = `Bearer ${token}`
  }

  const res = await fetch(`${BASE}${path}`, {
    method,
    headers,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  })

  if (!res.ok) {
    const text = await res.text()
    let message = text
    let parsed
    try {
      parsed = JSON.parse(text)
      message = parsed.error ?? text
    } catch { /* texto plano del 422 */ }
    throw new ApiError(res.status, message || `HTTP ${res.status}`, parsed)
  }
  if (res.status === 204) return undefined
  const text = await res.text()
  return text ? JSON.parse(text) : undefined
}

export const api = {
  // ── Familias / acceso ────────────────────────────────────────────────────────
  createFamily: (adminInviteCode, familyName, memberName) =>
    apiFetch('/families', { method: 'POST', body: { adminInviteCode, familyName, memberName }, auth: false }),
  joinFamily: (inviteCode, memberName) =>
    apiFetch('/families/join', { method: 'POST', body: { inviteCode, memberName }, auth: false }),
  familyOverview: () => apiFetch('/families/me'),

  // ── Cuentas: login y contraseñas (docs/DISENO_CUENTAS_LOGIN.md) ──────────────
  // `familyId` solo hace falta cuando el mismo email vive en varias familias y el login respondió 300.
  login: (email, password, familyId, deviceName) =>
    apiFetch('/families/login', { method: 'POST', body: { email, password, familyId, deviceName }, auth: false }),
  setCredentials: (email, password) =>
    apiFetch('/families/me/credentials', { method: 'POST', body: { email, password } }),
  changePassword: (currentPassword, newPassword) =>
    apiFetch('/families/me/password', { method: 'PUT', body: { currentPassword, newPassword } }),
  sessions: () => apiFetch('/families/me/sessions'),
  revokeSession: (sessionId) => apiFetch(`/families/me/sessions/${sessionId}/revoke`, { method: 'POST', body: {} }),
  issuePasswordReset: (memberId) => apiFetch('/families/password-resets', { method: 'POST', body: { memberId } }),
  redeemPasswordReset: (code, newPassword) =>
    apiFetch('/families/password-resets/redeem', { method: 'POST', body: { code, newPassword }, auth: false }),

  issueInvite: () => apiFetch('/families/invites', { method: 'POST', body: {} }),
  // Solo un admin de una familia del dueño: emite un código de un uso (TTL 48h) para crear una familia NUEVA.
  issueFamilyCreationInvite: () => apiFetch('/families/family-creation-invites', { method: 'POST', body: {} }),
  agentKeys: () => apiFetch('/families/agent-keys'),
  issueAgentKey: (name) => apiFetch('/families/agent-keys', { method: 'POST', body: { name } }),
  revokeAgentKey: (keyId) => apiFetch(`/families/agent-keys/${keyId}/revoke`, { method: 'POST', body: {} }),

  // ── Datos ────────────────────────────────────────────────────────────────────
  banks: () => apiFetch('/banks'),
  addBank: (body) => apiFetch('/banks', { method: 'POST', body }),
  updateBank: (id, body) => apiFetch(`/banks/${id}`, { method: 'PUT', body }),
  deleteBank: (id) => apiFetch(`/banks/${id}`, { method: 'DELETE' }),

  cards: () => apiFetch('/cards'),
  addCard: (body) => apiFetch('/cards', { method: 'POST', body }),
  updateCard: (id, body) => apiFetch(`/cards/${id}`, { method: 'PUT', body }),
  deleteCard: (id) => apiFetch(`/cards/${id}`, { method: 'DELETE' }),

  installments: () => apiFetch('/installments'),
  addInstallment: (body) => apiFetch('/installments', { method: 'POST', body }),
  updateInstallmentDetails: (id, body) => apiFetch(`/installments/${id}`, { method: 'PUT', body }),
  reviseInstallment: (id, body) => apiFetch(`/installments/${id}/revise`, { method: 'POST', body }),
  toggleInstallmentPaid: (id, month) => apiFetch(`/installments/${id}/months/${month}/toggle-paid`, { method: 'POST', body: {} }),
  overrideInstallmentAmount: (id, month, amount) => apiFetch(`/installments/${id}/months/${month}/amount`, { method: 'PUT', body: { amount } }),
  finishInstallment: (id) => apiFetch(`/installments/${id}/finish`, { method: 'POST', body: {} }),
  deleteInstallment: (id) => apiFetch(`/installments/${id}`, { method: 'DELETE' }),

  loans: () => apiFetch('/loans'),
  addLoan: (body) => apiFetch('/loans', { method: 'POST', body }),
  updateLoanDetails: (id, body) => apiFetch(`/loans/${id}`, { method: 'PUT', body }),
  reviseLoan: (id, body) => apiFetch(`/loans/${id}/revise`, { method: 'POST', body }),
  toggleLoanPaid: (id, month) => apiFetch(`/loans/${id}/months/${month}/toggle-paid`, { method: 'POST', body: {} }),
  overrideLoanAmount: (id, month, amount) => apiFetch(`/loans/${id}/months/${month}/amount`, { method: 'PUT', body: { amount } }),
  deleteLoan: (id) => apiFetch(`/loans/${id}`, { method: 'DELETE' }),

  services: () => apiFetch('/services'),
  addService: (body) => apiFetch('/services', { method: 'POST', body }),
  updateServiceDetails: (id, body) => apiFetch(`/services/${id}`, { method: 'PUT', body }),
  setServiceMonthAmount: (id, month, body) => apiFetch(`/services/${id}/months/${month}/amount`, { method: 'PUT', body }),
  extendServiceFuture: (id, body) => apiFetch(`/services/${id}/extend-future`, { method: 'POST', body }),
  toggleServicePaid: (id, month) => apiFetch(`/services/${id}/months/${month}/toggle-paid`, { method: 'POST', body: {} }),
  activateService: (id) => apiFetch(`/services/${id}/activate`, { method: 'POST', body: {} }),
  deactivateService: (id) => apiFetch(`/services/${id}/deactivate`, { method: 'POST', body: {} }),
  deleteService: (id) => apiFetch(`/services/${id}`, { method: 'DELETE' }),

  reserves: () => apiFetch('/reserves'),
  addReserve: (body) => apiFetch('/reserves', { method: 'POST', body }),
  updateReserveDetails: (id, body) => apiFetch(`/reserves/${id}`, { method: 'PUT', body }),
  setReserveMonth: (id, month, body) => apiFetch(`/reserves/${id}/months/${month}`, { method: 'PUT', body }),
  applyReserveBase: (id, amount) => apiFetch(`/reserves/${id}/apply-base`, { method: 'POST', body: { amount } }),
  deleteReserve: (id) => apiFetch(`/reserves/${id}`, { method: 'DELETE' }),

  people: () => apiFetch('/people'),
  addPerson: (body) => apiFetch('/people', { method: 'POST', body }),
  updatePerson: (id, body) => apiFetch(`/people/${id}`, { method: 'PUT', body }),
  archivePerson: (id) => apiFetch(`/people/${id}/archive`, { method: 'POST', body: {} }),

  expensesByMonth: (month) => apiFetch(`/expenses?month=${month}`),
  addExpense: (body) => apiFetch('/expenses', { method: 'POST', body }),
  updateExpense: (id, body) => apiFetch(`/expenses/${id}`, { method: 'PUT', body }),
  deleteExpense: (id) => apiFetch(`/expenses/${id}`, { method: 'DELETE' }),

  ticketsByMonth: (month) => apiFetch(`/tickets?month=${month}`),
  addTicket: (body) => apiFetch('/tickets', { method: 'POST', body }),
  updateTicket: (id, body) => apiFetch(`/tickets/${id}`, { method: 'PUT', body }),
  deleteTicket: (id) => apiFetch(`/tickets/${id}`, { method: 'DELETE' }),

  importLegacy: (legacyData, { force = false, replace = false } = {}) =>
    apiFetch(`/import?force=${force}&replace=${replace}`, { method: 'POST', body: legacyData }),
  importStatus: () => apiFetch('/import/status'),

  // Borradores conversacionales: los crea/moldea un agente MCP; la UI los lista y confirma/descarta.
  drafts: () => apiFetch('/drafts'),
  confirmDraft: (id) => apiFetch(`/drafts/${id}/confirm`, { method: 'POST', body: {} }),
  discardDraft: (id) => apiFetch(`/drafts/${id}/discard`, { method: 'POST', body: {} }),

  budgets: () => apiFetch('/planning/budgets'),
  setBudget: (month, body) => apiFetch(`/planning/budget/${month}`, { method: 'PUT', body }),
  income: () => apiFetch('/planning/income'),
  updateIncome: (body) => apiFetch('/planning/income', { method: 'PUT', body }),
  copyMonth: (fromMonth, toMonth) => apiFetch('/planning/copy-month', { method: 'POST', body: { fromMonth, toMonth } }),
}
