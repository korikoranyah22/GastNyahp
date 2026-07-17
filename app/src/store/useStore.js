import { create } from 'zustand'
import { currentYM, addMonthsToYM } from '../lib/dateUtils'
import { getPaymentMonth, getExpenseAmount } from '../pages/expenses/expensesConfig'
import { api, getToken, setToken, clearToken, ApiError } from '../lib/api'
import {
  bankToFront, cardToFront, cardToApi, installmentToFront, loanToFront, serviceToFront,
  reserveToFront, personToFront, expenseToFront, ticketToFront, incomeToFront, budgetsToFront,
  ownerToApi, paymentToApi,
} from '../lib/apiMappers'

// El store conserva EXACTAMENTE el shape que consumen las páginas (bancos, tarjetas, cuotas, etc.), pero
// las mutaciones ahora van a la API y recargan el slice (fetch-then-hydrate, react-feature-module). El
// backend es la fuente de verdad: multi-dispositivo real, sin merges P2P.

/** Mes efectivo de un gasto: para tarjetas de crédito usa el mes de PAGO del resumen;
 *  para débito/efectivo usa el mes calendario. */
function effectiveMonth(expense, creditCards) {
  const card = creditCards.find((c) => c.id === expense.paymentMethod)
  if (card?.closingDay) return getPaymentMonth(expense.date, card.closingDay, card.dueDay)
  return expense.date.slice(0, 7)
}

/** Etiqueta con qué mostrar esta sesión en la lista de dispositivos ("Chrome en Windows"). Es solo cosmética:
 *  sirve para reconocer cuál cerrar, no para identificar nada. */
function deviceName() {
  const ua = navigator.userAgent
  const browser = /Edg\//.test(ua) ? 'Edge'
    : /OPR\//.test(ua) ? 'Opera'
    : /Chrome\//.test(ua) ? 'Chrome'
    : /Firefox\//.test(ua) ? 'Firefox'
    : /Safari\//.test(ua) ? 'Safari'
    : 'Navegador'
  const os = /Android/.test(ua) ? 'Android'
    : /iPhone|iPad/.test(ua) ? 'iOS'
    : /Windows/.test(ua) ? 'Windows'
    : /Mac OS/.test(ua) ? 'Mac'
    : /Linux/.test(ua) ? 'Linux'
    : null
  return os ? `${browser} en ${os}` : browser
}

/** Convierte un throw de la API al shape `{ error }` que ya esperan las páginas. */
const asResult = async (promise) => {
  try {
    await promise
    return { error: null }
  } catch (e) {
    return { error: e instanceof ApiError ? e.message : String(e.message ?? e) }
  }
}

const EMPTY_DATA = {
  banks: [], creditCards: [], installments: [], loans: [], services: [],
  expenses: [], budgets: {}, fixedExpenses: [], people: [],
  income: { netMonthly: 0, usdRateOfficial: 0, usdRateCCL: 0, splitPercent: 70 },
  loadedExpenseMonths: {},
  drafts: [], // borradores abiertos (los moldea un agente MCP; acá se confirman/descartan)
}

const useStore = create((set, get) => ({
  // ─── Auth / familia ────────────────────────────────────────────────────────
  authStatus: 'init',          // 'init' | 'anon' | 'loading' | 'ready'
  family: null,                // { familyId, name, members: [{memberId, name, role}] }
  ...EMPTY_DATA,
  currentMonth: currentYM(),

  /** Al abrir la app: si hay token guardado, validarlo y cargar todo. */
  initAuth: async () => {
    if (!getToken()) { set({ authStatus: 'anon' }); return }
    set({ authStatus: 'loading' })
    try {
      const family = await api.familyOverview()
      set({ family })
      await get().loadAll()
      set({ authStatus: 'ready' })
    } catch {
      clearToken()
      set({ authStatus: 'anon', family: null, ...EMPTY_DATA })
    }
  },

  createFamily: async (adminInviteCode, familyName, memberName) => {
    try {
      const credential = await api.createFamily(adminInviteCode, familyName, memberName)
      setToken(credential.memberToken)
      await get().initAuth()
      return { error: null }
    } catch (e) {
      return { error: e.message }
    }
  },

  joinFamily: async (inviteCode, memberName) => {
    try {
      const credential = await api.joinFamily(inviteCode, memberName)
      setToken(credential.memberToken)
      await get().initAuth()
      return { error: null }
    } catch (e) {
      return { error: e.message }
    }
  },

  /**
   * Entrar con email+contraseña. Devuelve `{ choices }` en vez de entrar cuando el mismo email vive en varias
   * familias: la pantalla pregunta a cuál y reintenta con `familyId` (DISENO_CUENTAS_LOGIN.md §3.2).
   */
  login: async (email, password, familyId) => {
    try {
      const credential = await api.login(email, password, familyId, deviceName())
      setToken(credential.memberToken)
      await get().initAuth()
      return { error: null }
    } catch (e) {
      if (e.status === 300) return { error: null, choices: e.body?.families ?? [] }
      return { error: e.message }
    }
  },

  /** Etapa 1: el miembro que entró con su token de posesión se crea la cuenta para no quedar afuera (§3.3). */
  setCredentials: async (email, password) => {
    const result = await asResult(api.setCredentials(email, password))
    if (!result.error) await get().refreshFamily()
    return result
  },

  /** Cambiar la contraseña cierra TODAS las sesiones, incluida esta: hay que volver a entrar (amenaza #6). */
  changePassword: async (currentPassword, newPassword) => {
    const result = await asResult(api.changePassword(currentPassword, newPassword))
    if (!result.error) get().logout()
    return result
  },

  logout: () => {
    clearToken()
    set({ authStatus: 'anon', family: null, ...EMPTY_DATA, currentMonth: currentYM() })
  },

  refreshFamily: async () => set({ family: await api.familyOverview() }),

  // ─── Carga desde el backend ────────────────────────────────────────────────
  loadAll: async () => {
    const month = get().currentMonth
    const [banks, cards, installments, loans, services, reserves, people, budgets, income, drafts] = await Promise.all([
      api.banks(), api.cards(), api.installments(), api.loans(), api.services(),
      api.reserves(), api.people(), api.budgets(), api.income(), api.drafts(),
    ])
    set({
      banks: banks.map(bankToFront),
      creditCards: cards.map(cardToFront),
      installments: installments.map(installmentToFront),
      loans: loans.map(loanToFront),
      services: services.map(serviceToFront),
      fixedExpenses: reserves.map(reserveToFront),
      people: people.map(personToFront),
      budgets: budgetsToFront(budgets),
      income: incomeToFront(income),
      expenses: [],
      loadedExpenseMonths: {},
      drafts,
    })
    // Ventana de gastos: los gastos con tarjeta de meses anteriores impactan el resumen del mes actual.
    const months = [-3, -2, -1, 0, 1].map((n) => addMonthsToYM(month, n))
    await Promise.all(months.map((m) => get().loadExpensesMonth(m)))
  },

  loadExpensesMonth: async (month) => {
    if (get().loadedExpenseMonths[month]) return
    // Marcar ANTES del fetch: evita cargas duplicadas en paralelo y tormentas de reintento ante un error.
    set((s) => ({ loadedExpenseMonths: { ...s.loadedExpenseMonths, [month]: true } }))
    try {
      const [expenses, tickets] = await Promise.all([api.expensesByMonth(month), api.ticketsByMonth(month)])
      set((s) => ({
        expenses: [
          ...s.expenses.filter((e) => !e.date.startsWith(month)),
          ...expenses.map(expenseToFront),
          ...tickets.map(ticketToFront),
        ],
      }))
    } catch {
      // El mes queda marcado (vacío); navegar de nuevo al mes con reloadExpensesMonth lo reintenta.
    }
  },

  reloadDrafts: async () => set({ drafts: await api.drafts() }),

  /** Confirmar dispara la carga REAL en el backend — refrescamos borradores y los datos afectados. */
  confirmDraft: async (draft) => {
    try {
      await api.confirmDraft(draft.id)
      await get().reloadDrafts()
      if (draft.kind === 'Installment') await get().reloadInstallments()
      else await get().reloadExpensesMonth(draft.payload?.date?.slice(0, 7) ?? currentYM())
      return { error: null }
    } catch (e) {
      return { error: e.message }
    }
  },

  discardDraft: async (draftId) => {
    try {
      await api.discardDraft(draftId)
      await get().reloadDrafts()
      return { error: null }
    } catch (e) {
      return { error: e.message }
    }
  },

  reloadBanks: async () => set({ banks: (await api.banks()).map(bankToFront) }),
  reloadCards: async () => set({ creditCards: (await api.cards()).map(cardToFront) }),
  reloadInstallments: async () => set({ installments: (await api.installments()).map(installmentToFront) }),
  reloadLoans: async () => set({ loans: (await api.loans()).map(loanToFront) }),
  reloadServices: async () => set({ services: (await api.services()).map(serviceToFront) }),
  reloadReserves: async () => set({ fixedExpenses: (await api.reserves()).map(reserveToFront) }),
  reloadPeople: async () => set({ people: (await api.people()).map(personToFront) }),
  reloadBudgets: async () => set({ budgets: budgetsToFront(await api.budgets()) }),
  reloadIncome: async () => set({ income: incomeToFront(await api.income()) }),
  reloadExpensesMonth: async (month) => {
    set((s) => {
      const loaded = { ...s.loadedExpenseMonths }
      delete loaded[month]
      return { loadedExpenseMonths: loaded }
    })
    await get().loadExpensesMonth(month)
  },

  // ─── Month navigation ──────────────────────────────────────────────────────
  setCurrentMonth: (ym) => {
    set({ currentMonth: ym })
    // fire-and-forget: garantiza que el mes visible (y el anterior) estén cargados
    get().loadExpensesMonth(ym)
    get().loadExpensesMonth(addMonthsToYM(ym, -1))
  },

  // ─── Banks ─────────────────────────────────────────────────────────────────
  addBank: async (bank) => asResult(api.addBank(bank).then(() => get().reloadBanks())),
  updateBank: async (id, data) => asResult(api.updateBank(id, data).then(() => get().reloadBanks())),
  deleteBank: async (id) => asResult(api.deleteBank(id).then(() => get().reloadBanks())),

  // ─── Credit Cards ──────────────────────────────────────────────────────────
  addCard: async (card) => asResult(api.addCard(cardToApi(card)).then(() => get().reloadCards())),
  updateCard: async (id, data) => asResult(api.updateCard(id, cardToApi(data)).then(() => get().reloadCards())),
  deleteCard: async (id) => asResult(api.deleteCard(id).then(() => get().reloadCards())),

  // ─── Installments ──────────────────────────────────────────────────────────
  addInstallment: async (inst) => asResult(api.addInstallment({
    cardId: inst.cardId,
    description: inst.description,
    category: inst.category,
    purchaseDate: inst.purchaseDate,
    frequency: inst.frequency === 'monthly' ? 'Monthly' : 'Fixed',
    monthlyAmount: Number(inst.monthlyAmount),
    totalInstallments: inst.frequency === 'monthly' ? null : Number(inst.totalInstallments),
    startMonth: inst.startMonth,
    ...ownerToApi(inst.ownerId),
  }).then(() => get().reloadInstallments())),

  updateInstallment: async (id, data) => {
    const existing = get().installments.find((i) => i.id === id)
    if (!existing) return { error: 'La compra en cuotas no existe.' }

    // Cambios de calendario → revise (regenera preservando pagadas); cosméticos → update details.
    const needsRegen =
      data.startMonth !== existing.startMonth ||
      Number(data.totalInstallments ?? 0) !== Number(existing.totalInstallments ?? 0) ||
      data.frequency !== existing.frequency ||
      Number(data.monthlyAmount) !== Number(existing.monthlyAmount)

    return asResult((async () => {
      if (needsRegen) {
        await api.reviseInstallment(id, {
          startMonth: data.startMonth ?? existing.startMonth,
          totalInstallments: (data.frequency ?? existing.frequency) === 'monthly'
            ? null
            : Number(data.totalInstallments ?? existing.totalInstallments),
          frequency: (data.frequency ?? existing.frequency) === 'monthly' ? 'Monthly' : 'Fixed',
          monthlyAmount: Number(data.monthlyAmount ?? existing.monthlyAmount),
        })
      }
      await api.updateInstallmentDetails(id, {
        description: data.description ?? existing.description,
        category: data.category ?? existing.category,
        purchaseDate: data.purchaseDate ?? existing.purchaseDate,
        ...ownerToApi(data.ownerId !== undefined ? data.ownerId : existing.ownerId),
      })
      await get().reloadInstallments()
    })())
  },

  toggleInstallmentPaid: async (instId, month) =>
    asResult(api.toggleInstallmentPaid(instId, month).then(() => get().reloadInstallments())),
  updateInstallmentMonthAmount: async (instId, month, amount) =>
    asResult(api.overrideInstallmentAmount(instId, month, Number(amount)).then(() => get().reloadInstallments())),
  deleteInstallment: async (id) => asResult(api.deleteInstallment(id).then(() => get().reloadInstallments())),
  finishInstallment: async (id) => asResult(api.finishInstallment(id).then(() => get().reloadInstallments())),

  // ─── Loans ─────────────────────────────────────────────────────────────────
  addLoan: async (loan) => asResult(api.addLoan({
    bankId: loan.bankId,
    description: loan.description,
    totalAmount: loan.totalAmount ? Number(loan.totalAmount) : null,
    monthlyInstallment: Number(loan.monthlyInstallment),
    startMonth: loan.startDate.slice(0, 7),
    totalInstallments: Number(loan.totalInstallments),
  }).then(() => get().reloadLoans())),

  updateLoan: async (id, data) => {
    const existing = get().loans.find((l) => l.id === id)
    if (!existing) return { error: 'El préstamo no existe.' }

    const needsRegen =
      (data.startDate && data.startDate.slice(0, 7) !== existing.startDate.slice(0, 7)) ||
      (data.totalInstallments !== undefined && Number(data.totalInstallments) !== existing.totalInstallments) ||
      (data.monthlyInstallment !== undefined && Number(data.monthlyInstallment) !== Number(existing.monthlyInstallment))

    return asResult((async () => {
      if (needsRegen) {
        await api.reviseLoan(id, {
          startMonth: (data.startDate ?? existing.startDate).slice(0, 7),
          totalInstallments: Number(data.totalInstallments ?? existing.totalInstallments),
          monthlyInstallment: Number(data.monthlyInstallment ?? existing.monthlyInstallment),
        })
      }
      await api.updateLoanDetails(id, {
        description: data.description ?? existing.description,
        totalAmount: data.totalAmount !== undefined ? Number(data.totalAmount) || null : existing.totalAmount,
      })
      await get().reloadLoans()
    })())
  },

  toggleLoanPaid: async (loanId, month) =>
    asResult(api.toggleLoanPaid(loanId, month).then(() => get().reloadLoans())),
  updateLoanMonthAmount: async (loanId, month, amount) =>
    asResult(api.overrideLoanAmount(loanId, month, Number(amount)).then(() => get().reloadLoans())),
  deleteLoan: async (id) => asResult(api.deleteLoan(id).then(() => get().reloadLoans())),

  // ─── Services ──────────────────────────────────────────────────────────────
  addService: async (svc) => asResult(api.addService({
    name: svc.name,
    category: svc.category,
    billingType: svc.billingType === 'bimonthly' ? 'Bimonthly' : svc.billingType === 'quarterly' ? 'Quarterly' : 'Monthly',
    linkedCardId: svc.linkedCardId || null,
    currency: svc.originalCurrency === 'USD' || svc.currency === 'USD' ? 'Usd' : 'Ars',
    baseAmount: Number(svc.originalCurrency === 'USD' ? svc.originalBaseAmount : svc.baseAmount) || 0,
    registeredFromMonth: get().currentMonth,
    ...ownerToApi(svc.ownerId),
  }).then(() => get().reloadServices())),

  updateService: async (id, data) => {
    const existing = get().services.find((s) => s.id === id)
    if (!existing) return { error: 'El servicio no existe.' }

    return asResult((async () => {
      await api.updateServiceDetails(id, {
        name: data.name ?? existing.name,
        category: data.category ?? existing.category,
        billingType: (data.billingType ?? existing.billingType) === 'bimonthly' ? 'Bimonthly'
          : (data.billingType ?? existing.billingType) === 'quarterly' ? 'Quarterly' : 'Monthly',
        linkedCardId: (data.linkedCardId !== undefined ? data.linkedCardId : existing.linkedCardId) || null,
        currency: (data.currency ?? existing.currency) === 'USD' ? 'Usd' : 'Ars',
        ...ownerToApi(data.ownerId !== undefined ? data.ownerId : existing.ownerId),
      })
      // El toggle de activo es un comando propio en el backend.
      if (data.active !== undefined && data.active !== existing.active) {
        if (data.active) await api.activateService(id)
        else await api.deactivateService(id)
      }
      await get().reloadServices()
    })())
  },

  deleteService: async (id) => asResult(api.deleteService(id).then(() => get().reloadServices())),
  updateServiceMonthAmount: async (svcId, month, amount) =>
    asResult(api.setServiceMonthAmount(svcId, month, { amount: Number(amount), currency: 'Ars' }).then(() => get().reloadServices())),
  updateServiceFutureAmounts: async (svcId, fromMonth, amount) =>
    asResult(api.extendServiceFuture(svcId, { fromMonth, amountArs: Number(amount), monthsAhead: 12 }).then(() => get().reloadServices())),
  toggleServiceMonthPaid: async (svcId, month) =>
    asResult(api.toggleServicePaid(svcId, month).then(() => get().reloadServices())),

  // ─── Computed helpers (idénticos a la maqueta — operan sobre el estado local) ─
  getCardInstallmentsTotal: (cardId, month) => {
    const { installments } = get()
    return installments
      .filter((i) => i.cardId === cardId && i.active !== false)
      .reduce((sum, inst) => {
        const m = inst.months.find((m) => m.month === month)
        return sum + (m ? m.amount : 0)
      }, 0)
  },

  getCardServicesTotal: (cardId, month) => {
    const { services } = get()
    return services
      .filter((s) => s.linkedCardId === cardId && s.active !== false)
      .reduce((sum, svc) => {
        const a = svc.amounts.find((a) => a.month === month)
        return sum + (a ? a.amount : 0)
      }, 0)
  },

  getCardTotal: (cardId, month) => {
    const inst = get().getCardInstallmentsTotal(cardId, month)
    const svc = get().getCardServicesTotal(cardId, month)
    return inst + svc
  },

  getIndependentServicesTotal: (month) => {
    const { services } = get()
    return services
      .filter((s) => !s.linkedCardId && s.active !== false)
      .reduce((sum, svc) => {
        const a = svc.amounts.find((a) => a.month === month)
        return sum + (a ? a.amount : 0)
      }, 0)
  },

  getLoanAmountForMonth: (loanId, month) => {
    const loan = get().loans.find((l) => l.id === loanId)
    if (!loan) return 0
    const m = loan.months.find((m) => m.month === month)
    return m ? m.amount : 0
  },

  // ─── Expenses ──────────────────────────────────────────────────────────────
  addExpense: async (exp) => {
    if (exp.type === 'ticket') {
      return asResult(api.addTicket({
        date: exp.date,
        description: exp.description,
        ...paymentToApi(exp.paymentMethod),
        discount: Number(exp.discount) || 0,
        items: exp.items.map((i) => ({
          description: i.description, amount: Number(i.amount), category: i.category, ...ownerToApi(i.ownerId),
        })),
      }).then(() => get().reloadExpensesMonth(exp.date.slice(0, 7))))
    }
    // Si el form convirtió desde USD, el backend hace la conversión canónica con el CCL configurado.
    const isUsd = exp.originalCurrency === 'USD'
    return asResult(api.addExpense({
      date: exp.date,
      description: exp.description,
      category: exp.category,
      amount: Number(isUsd ? exp.originalAmount : exp.amount),
      currency: isUsd ? 'Usd' : 'Ars',
      ...paymentToApi(exp.paymentMethod),
      ...ownerToApi(exp.ownerId),
    }).then(() => get().reloadExpensesMonth(exp.date.slice(0, 7))))
  },

  updateExpense: async (id, data) => {
    const existing = get().expenses.find((e) => e.id === id)
    if (!existing) return { error: 'El gasto no existe.' }
    const merged = { ...existing, ...data }
    const reload = () => Promise.all([
      get().reloadExpensesMonth(existing.date.slice(0, 7)),
      get().reloadExpensesMonth(merged.date.slice(0, 7)),
    ])

    if (merged.type === 'ticket') {
      return asResult(api.updateTicket(id, {
        date: merged.date,
        description: merged.description,
        ...paymentToApi(merged.paymentMethod),
        discount: Number(merged.discount) || 0,
        items: merged.items.map((i) => ({
          description: i.description, amount: Number(i.amount), category: i.category, ...ownerToApi(i.ownerId),
        })),
      }).then(reload))
    }
    const isUsd = merged.originalCurrency === 'USD'
    return asResult(api.updateExpense(id, {
      date: merged.date,
      description: merged.description,
      category: merged.category,
      amount: Number(isUsd ? merged.originalAmount : merged.amount),
      currency: isUsd ? 'Usd' : 'Ars',
      ...paymentToApi(merged.paymentMethod),
      ...ownerToApi(merged.ownerId),
    }).then(reload))
  },

  deleteExpense: async (id) => {
    const existing = get().expenses.find((e) => e.id === id)
    if (!existing) return { error: null }
    const call = existing.type === 'ticket' ? api.deleteTicket(id) : api.deleteExpense(id)
    return asResult(call.then(() => get().reloadExpensesMonth(existing.date.slice(0, 7))))
  },

  // La carga del mes la garantiza setCurrentMonth/loadAll — un getter nunca dispara fetches (evita set()
  // durante render).
  getMonthExpenses: (month) => get().expenses.filter((e) => e.date.startsWith(month)),
  getMonthExpenseTotal: (month) => {
    const { expenses, creditCards } = get()
    return expenses
      .filter((e) => effectiveMonth(e, creditCards) === month)
      .reduce((sum, e) => sum + getExpenseAmount(e), 0)
  },
  getMonthCreditTotal: (month) => {
    const { expenses, creditCards } = get()
    const cardIds = new Set(creditCards.map((c) => c.id))
    return expenses
      .filter((e) => cardIds.has(e.paymentMethod) && e.date.startsWith(month))
      .reduce((sum, e) => sum + getExpenseAmount(e), 0)
  },
  getMonthDebitCashTotal: (month) => {
    return get().expenses
      .filter((e) => e.date.startsWith(month) &&
        (e.paymentMethod === 'cash' || e.paymentMethod?.startsWith('debit-')))
      .reduce((sum, e) => sum + getExpenseAmount(e), 0)
  },
  getWeekExpenseTotal: (month, week) => {
    const ranges = { 1: [1, 7], 2: [8, 14], 3: [15, 22], 4: [23, 31] }
    const [from, to] = ranges[week] || [1, 31]
    return get().expenses
      .filter((e) => {
        if (!e.date.startsWith(month)) return false
        const day = parseInt(e.date.slice(8, 10), 10)
        return day >= from && day <= to
      })
      .reduce((sum, e) => sum + getExpenseAmount(e), 0)
  },

  // ─── Budgets ───────────────────────────────────────────────────────────────
  setBudget: async (month, data) =>
    asResult(api.setBudget(month, data).then(() => get().reloadBudgets())),
  getBudget: (month) => get().budgets[month] || { creditLimit: 0, debitCashLimit: 0, weeklyLimit: 0 },

  // ─── Fixed Expenses (Reservas) ─────────────────────────────────────────────
  addFixedExpense: async (item) => asResult(api.addReserve({
    label: item.label,
    type: item.type === 'cash' ? 'Cash' : item.type === 'debt' ? 'Debt' : item.type === 'other' ? 'Other' : 'Reserve',
    icon: item.icon,
    recurring: !!item.recurring,
    baseAmount: Number(item.baseAmount) || 0,
  }).then(() => get().reloadReserves())),

  updateFixedExpense: async (id, data) => {
    const existing = get().fixedExpenses.find((f) => f.id === id)
    if (!existing) return { error: 'La reserva no existe.' }
    return asResult((async () => {
      await api.updateReserveDetails(id, {
        label: data.label ?? existing.label,
        type: ((data.type ?? existing.type) === 'cash') ? 'Cash'
          : ((data.type ?? existing.type) === 'debt') ? 'Debt'
          : ((data.type ?? existing.type) === 'other') ? 'Other' : 'Reserve',
        icon: data.icon ?? existing.icon,
      })
      // Cambio de base/recurrencia = "aplicar a todos los meses" (borra overrides, como en la maqueta).
      const baseChanged = data.baseAmount !== undefined && Number(data.baseAmount) !== Number(existing.baseAmount)
      const recurringChanged = data.recurring !== undefined && data.recurring !== existing.recurring
      if (baseChanged || recurringChanged) {
        await api.applyReserveBase(id, data.recurring === false ? 0 : Number(data.baseAmount ?? existing.baseAmount) || 0)
      }
      await get().reloadReserves()
    })())
  },

  deleteFixedExpense: async (id) => asResult(api.deleteReserve(id).then(() => get().reloadReserves())),
  setFixedExpenseMonth: async (id, month, amount, note) =>
    asResult(api.setReserveMonth(id, month, { amount: Number(amount), note: note || null }).then(() => get().reloadReserves())),
  setFixedExpenseBase: async (id, baseAmount) =>
    asResult(api.applyReserveBase(id, Number(baseAmount)).then(() => get().reloadReserves())),

  getFixedExpenseTotal: (month) => {
    return get().fixedExpenses.reduce((sum, f) => {
      const m = f.months.find((m) => m.month === month)
      const amount = m ? m.amount : (f.recurring ? (f.baseAmount || 0) : 0)
      return sum + amount
    }, 0)
  },

  // ─── People ────────────────────────────────────────────────────────────────
  addPerson: async (person) => asResult(api.addPerson(person).then(() => get().reloadPeople())),
  updatePerson: async (id, data) => asResult(api.updatePerson(id, data).then(() => get().reloadPeople())),
  // La API archiva (nunca borra) para que la atribución histórica siga resolviendo.
  deletePerson: async (id) => asResult(api.archivePerson(id).then(() => get().reloadPeople())),

  // ─── Income ────────────────────────────────────────────────────────────────
  setIncome: async (data) => asResult(api.updateIncome({
    netMonthly: data.netMonthly !== undefined ? Number(data.netMonthly) : null,
    usdRateOfficial: data.usdRateOfficial !== undefined ? Number(data.usdRateOfficial) : null,
    usdRateCcl: data.usdRateCCL !== undefined ? Number(data.usdRateCCL) : null,
    splitPercent: data.splitPercent !== undefined ? Number(data.splitPercent) : null,
  }).then(() => get().reloadIncome())),

  // ─── Copy month data ───────────────────────────────────────────────────────
  copyMonthData: async (fromMonth, toMonth) =>
    asResult(api.copyMonth(fromMonth, toMonth).then(() => Promise.all([get().reloadReserves(), get().reloadBudgets()]))),

  // ─── Export (backup local de lo cargado) / Import (ya no aplica) ───────────
  exportData: () => {
    const { banks, creditCards, installments, loans, services, expenses, budgets, fixedExpenses, income, people } = get()
    return JSON.stringify(
      { meta: { version: '2.0', exported: new Date().toISOString(), source: 'api' }, banks, creditCards, installments, loans, services, expenses, budgets, fixedExpenses, income, people },
      null, 2
    )
  },
  importData: () => ({ error: 'La importación directa ya no aplica: los datos viven en el servidor de la familia.' }),
}))

export default useStore
