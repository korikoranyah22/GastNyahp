// Traducción entre el shape histórico del store (el que consumen TODAS las páginas) y el shape de la API.
// Mantener el shape del frontend intacto es lo que permite migrar el store sin reescribir las pantallas
// (zustand-store-patterns → react-feature-module).

// ── Enums (frontend usa strings legacy, la API nombres .NET) ───────────────────
const NETWORK_TO_API = { VISA: 'Visa', MASTERCARD: 'Mastercard' }
const NETWORK_TO_FRONT = { Visa: 'VISA', Mastercard: 'MASTERCARD' }
const lc = (s) => (s ? s.toLowerCase() : s)
const cap = (s) => (s ? s.charAt(0).toUpperCase() + s.slice(1) : s)

// ── ownerId (null | 'shared' | personId) ⇄ ownerKind/ownerPersonId ────────────
export const ownerToApi = (ownerId) =>
  ownerId === 'shared'
    ? { ownerKind: 'Shared', ownerPersonId: null }
    : ownerId
      ? { ownerKind: 'Owner', ownerPersonId: ownerId }
      : { ownerKind: 'Unassigned', ownerPersonId: null }

export const ownerToFront = (kind, personId) =>
  kind === 'Shared' ? 'shared' : kind === 'Owner' ? personId : null

// ── paymentMethod (cardId | 'debit-{bankId}' | 'cash' | 'modo' | 'mercadopago') ─
export const paymentToApi = (paymentMethod) => {
  if (paymentMethod === 'cash') return { paymentMethodKind: 'Cash', paymentMethodReferenceId: null }
  if (paymentMethod === 'modo') return { paymentMethodKind: 'Modo', paymentMethodReferenceId: null }
  if (paymentMethod === 'mercadopago') return { paymentMethodKind: 'MercadoPago', paymentMethodReferenceId: null }
  if (paymentMethod?.startsWith('debit-'))
    return { paymentMethodKind: 'Debit', paymentMethodReferenceId: paymentMethod.slice('debit-'.length) }
  return { paymentMethodKind: 'Card', paymentMethodReferenceId: paymentMethod }
}

export const paymentToFront = (kind, referenceId) => {
  switch (kind) {
    case 'Cash': return 'cash'
    case 'Modo': return 'modo'
    case 'MercadoPago': return 'mercadopago'
    case 'Debit': return `debit-${referenceId}`
    default: return referenceId
  }
}

// ── Entidades ──────────────────────────────────────────────────────────────────
export const bankToFront = (b) => ({ id: b.id, name: b.name, alias: b.alias, color: b.color, icon: b.icon })

export const cardToFront = (c) => ({
  id: c.id, bankId: c.bankId, label: c.label,
  network: NETWORK_TO_FRONT[c.network] ?? c.network,
  type: lc(c.type), closingDay: c.closingDay, dueDay: c.dueDay, color: c.color, active: c.active,
})
export const cardToApi = (c) => ({
  bankId: c.bankId, label: c.label,
  network: NETWORK_TO_API[c.network] ?? c.network,
  type: cap(c.type), closingDay: Number(c.closingDay), dueDay: Number(c.dueDay), color: c.color,
})

export const installmentToFront = (i) => ({
  id: i.id, cardId: i.cardId, description: i.description, category: i.category,
  purchaseDate: i.purchaseDate, frequency: lc(i.frequency), monthlyAmount: i.monthlyAmount,
  totalInstallments: i.totalInstallments, startMonth: i.startMonth, active: i.active,
  ownerId: ownerToFront(i.ownerKind, i.ownerPersonId),
  months: (i.months ?? []).map((m) => ({ month: m.month, amount: m.amount, paid: m.paid })),
})

export const loanToFront = (l) => ({
  id: l.id, bankId: l.bankId, description: l.description, totalAmount: l.totalAmount,
  monthlyInstallment: l.monthlyInstallment, startDate: `${l.startMonth}-01`,
  totalInstallments: l.totalInstallments, paidInstallments: l.paidInstallments,
  months: (l.months ?? []).map((m) => ({ month: m.month, amount: m.amount, paid: m.paid })),
})

export const serviceToFront = (s) => ({
  id: s.id, name: s.name, category: s.category, billingType: lc(s.billingType),
  linkedCardId: s.linkedCardId, active: s.active, currency: s.currency?.toUpperCase(),
  originalBaseAmount: s.originalAmount, ownerId: ownerToFront(s.ownerKind, s.ownerPersonId),
  amounts: (s.amounts ?? []).map((a) => ({ month: a.month, amount: a.amountArs, paid: a.paid })),
})

export const reserveToFront = (r) => ({
  id: r.id, label: r.label, type: lc(r.type), icon: r.icon,
  recurring: r.recurring, baseAmount: r.baseAmount,
  months: (r.months ?? []).map((m) => ({ month: m.month, amount: m.amount, note: m.note ?? '' })),
})

export const personToFront = (p) => ({ id: p.id, name: p.name, emoji: p.emoji, color: p.color })

export const expenseToFront = (e) => ({
  id: e.id, date: e.date, description: e.description, category: e.category,
  amount: e.amountArs, originalAmount: e.originalAmount, originalCurrency: e.originalCurrency?.toUpperCase(),
  paymentMethod: paymentToFront(e.paymentMethodKind, e.paymentMethodReferenceId),
  ownerId: ownerToFront(e.ownerKind, e.ownerPersonId),
})

export const ticketToFront = (t) => ({
  id: t.id, type: 'ticket', date: t.date, description: t.description,
  paymentMethod: paymentToFront(t.paymentMethodKind, t.paymentMethodReferenceId),
  discount: t.discount,
  items: (t.items ?? []).map((i) => ({
    id: i.itemId, description: i.description, amount: i.amount, category: i.category,
    ownerId: ownerToFront(i.ownerKind, i.ownerPersonId),
  })),
})

export const incomeToFront = (i) => ({
  netMonthly: i.netMonthly, usdRateOfficial: i.usdRateOfficial,
  usdRateCCL: i.usdRateCcl, splitPercent: i.splitPercent,
})

export const budgetsToFront = (list) =>
  Object.fromEntries(list.map((b) => [b.month, {
    creditLimit: b.creditLimit, debitCashLimit: b.debitCashLimit, weeklyLimit: b.weeklyLimit,
  }]))
