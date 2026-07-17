// Demo data — sin información personal
// IDs: bank-demo-1 y bank-demo-2 se usan para detectar "primera vez" en App.jsx
export const seedData = {
  banks: [
    { id: 'bank-demo-1', name: 'Banco Azul',   color: '#1d4ed8', icon: 'building-2', alias: 'Azul'  },
    { id: 'bank-demo-2', name: 'Banco Verde',  color: '#059669', icon: 'building-2', alias: 'Verde' },
  ],

  creditCards: [
    { id: 'card-visa-d1',   bankId: 'bank-demo-1', label: 'VISA Azul',    network: 'VISA',       type: 'credit', closingDay: 15, dueDay: 5,  color: '#1e40af', active: true },
    { id: 'card-master-d1', bankId: 'bank-demo-1', label: 'MASTER Azul',  network: 'MASTERCARD', type: 'credit', closingDay: 18, dueDay: 8,  color: '#1d4ed8', active: true },
    { id: 'card-visa-d2',   bankId: 'bank-demo-2', label: 'VISA Verde',   network: 'VISA',       type: 'credit', closingDay: 10, dueDay: 1,  color: '#047857', active: true },
    { id: 'card-master-d2', bankId: 'bank-demo-2', label: 'MASTER Verde', network: 'MASTERCARD', type: 'credit', closingDay: 10, dueDay: 1,  color: '#065f46', active: true },
  ],

  installments: [
    // ── Smart TV 55" — 12 cuotas desde Oct 2025 ──────────────────────────────
    {
      id:                 'inst-demo-smarttv',
      cardId:             'card-visa-d1',
      description:        'Smart TV 55"',
      category:           'Hogar',
      purchaseDate:       '2025-10-05',
      frequency:          'fixed',
      monthlyAmount:      85000,
      totalInstallments:  12,
      startMonth:         '2025-10',
      months: [
        { month: '2025-10', amount: 85000, paid: true  },
        { month: '2025-11', amount: 85000, paid: true  },
        { month: '2025-12', amount: 85000, paid: true  },
        { month: '2026-01', amount: 85000, paid: true  },
        { month: '2026-02', amount: 85000, paid: false },
        { month: '2026-03', amount: 85000, paid: false },
        { month: '2026-04', amount: 85000, paid: false },
        { month: '2026-05', amount: 85000, paid: false },
        { month: '2026-06', amount: 85000, paid: false },
        { month: '2026-07', amount: 85000, paid: false },
        { month: '2026-08', amount: 85000, paid: false },
        { month: '2026-09', amount: 85000, paid: false },
      ],
    },

    // ── Notebook — 18 cuotas desde Jun 2025 ──────────────────────────────────
    {
      id:                 'inst-demo-notebook',
      cardId:             'card-visa-d2',
      description:        'Notebook',
      category:           'Electrónica',
      purchaseDate:       '2025-06-01',
      frequency:          'fixed',
      monthlyAmount:      120000,
      totalInstallments:  18,
      startMonth:         '2025-06',
      months: Array.from({ length: 18 }, (_, i) => {
        const totalMonths = 6 + i       // starts at month 6 (Jun=6)
        const year  = totalMonths <= 12 ? 2025 : 2026
        const month = totalMonths <= 12 ? totalMonths : totalMonths - 12
        return {
          month: `${year}-${String(month).padStart(2, '0')}`,
          amount: 120000,
          paid: i < 8,   // Jun–Jan pagadas (8 meses), Feb en adelante sin pagar
        }
      }),
    },

    // ── Plan celular — mensual recurrente desde Ene 2026 ─────────────────────
    {
      id:                 'inst-demo-celular',
      cardId:             'card-master-d1',
      description:        'Plan celular',
      category:           'Servicios',
      purchaseDate:       '2026-01-01',
      frequency:          'monthly',
      monthlyAmount:      45000,
      totalInstallments:  null,
      startMonth:         '2026-01',
      months: [
        { month: '2026-01', amount: 45000, paid: true  },
        { month: '2026-02', amount: 45000, paid: false },
        { month: '2026-03', amount: 45000, paid: false },
        { month: '2026-04', amount: 45000, paid: false },
        { month: '2026-05', amount: 45000, paid: false },
        { month: '2026-06', amount: 45000, paid: false },
        { month: '2026-07', amount: 45000, paid: false },
        { month: '2026-08', amount: 45000, paid: false },
        { month: '2026-09', amount: 45000, paid: false },
        { month: '2026-10', amount: 45000, paid: false },
        { month: '2026-11', amount: 45000, paid: false },
        { month: '2026-12', amount: 45000, paid: false },
      ],
    },

    // ── Electrodoméstico — 6 cuotas desde Nov 2025 ───────────────────────────
    {
      id:                 'inst-demo-electro',
      cardId:             'card-master-d2',
      description:        'Electrodoméstico',
      category:           'Hogar',
      purchaseDate:       '2025-11-10',
      frequency:          'fixed',
      monthlyAmount:      35000,
      totalInstallments:  6,
      startMonth:         '2025-11',
      months: [
        { month: '2025-11', amount: 35000, paid: true  },
        { month: '2025-12', amount: 35000, paid: true  },
        { month: '2026-01', amount: 35000, paid: true  },
        { month: '2026-02', amount: 35000, paid: false },
        { month: '2026-03', amount: 35000, paid: false },
        { month: '2026-04', amount: 35000, paid: false },
      ],
    },
  ],

  loans: [
    {
      id:                 'loan-demo-1',
      bankId:             'bank-demo-1',
      description:        'Préstamo personal',
      totalAmount:        2160000,
      monthlyInstallment: 180000,
      startDate:          '2025-11-01',
      totalInstallments:  12,
      paidInstallments:   3,
      months: [
        { month: '2025-11', amount: 180000, paid: true  },
        { month: '2025-12', amount: 180000, paid: true  },
        { month: '2026-01', amount: 180000, paid: true  },
        { month: '2026-02', amount: 180000, paid: false },
        { month: '2026-03', amount: 180000, paid: false },
        { month: '2026-04', amount: 180000, paid: false },
        { month: '2026-05', amount: 180000, paid: false },
        { month: '2026-06', amount: 180000, paid: false },
        { month: '2026-07', amount: 180000, paid: false },
        { month: '2026-08', amount: 180000, paid: false },
        { month: '2026-09', amount: 180000, paid: false },
        { month: '2026-10', amount: 180000, paid: false },
      ],
    },
  ],

  services: [
    // ── Independientes (débito / transferencia) ───────────────────────────────
    {
      id: 'svc-demo-luz',
      name: 'Electricidad',
      category: 'Electricidad',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 32000 },
        { month: '2025-12', amount: 35000 },
        { month: '2026-01', amount: 35000 },
        { month: '2026-02', amount: 35000 },
        { month: '2026-03', amount: 35000 },
      ],
    },
    {
      id: 'svc-demo-gas',
      name: 'Gas',
      category: 'Gas',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 22000 },
        { month: '2025-12', amount: 25000 },
        { month: '2026-01', amount: 25000 },
        { month: '2026-02', amount: 25000 },
        { month: '2026-03', amount: 25000 },
      ],
    },
    {
      id: 'svc-demo-agua',
      name: 'Agua',
      category: 'Agua',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 16000 },
        { month: '2025-12', amount: 18000 },
        { month: '2026-01', amount: 18000 },
        { month: '2026-02', amount: 18000 },
        { month: '2026-03', amount: 18000 },
      ],
    },
    {
      id: 'svc-demo-expensas',
      name: 'Expensas',
      category: 'Expensas',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 160000 },
        { month: '2025-12', amount: 160000 },
        { month: '2026-01', amount: 180000 },
        { month: '2026-02', amount: 180000 },
        { month: '2026-03', amount: 180000 },
      ],
    },
    {
      id: 'svc-demo-internet',
      name: 'Internet',
      category: 'Conectividad',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 60000 },
        { month: '2025-12', amount: 65000 },
        { month: '2026-01', amount: 65000 },
        { month: '2026-02', amount: 65000 },
        { month: '2026-03', amount: 65000 },
      ],
    },
    {
      id: 'svc-demo-spotify',
      name: 'Spotify',
      category: 'Streaming',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 5000 },
        { month: '2025-12', amount: 5000 },
        { month: '2026-01', amount: 5000 },
        { month: '2026-02', amount: 5000 },
        { month: '2026-03', amount: 5000 },
      ],
    },
    {
      id: 'svc-demo-netflix',
      name: 'Netflix',
      category: 'Streaming',
      billingType: 'monthly',
      linkedCardId: null,
      active: true,
      amounts: [
        { month: '2025-11', amount: 7500 },
        { month: '2025-12', amount: 7500 },
        { month: '2026-01', amount: 7500 },
        { month: '2026-02', amount: 7500 },
        { month: '2026-03', amount: 7500 },
      ],
    },

    // ── En tarjeta (VISA Verde) ───────────────────────────────────────────────
    {
      id: 'svc-demo-seguro',
      name: 'Seguro auto',
      category: 'Seguro',
      billingType: 'monthly',
      linkedCardId: 'card-visa-d2',
      active: true,
      amounts: [
        { month: '2025-11', amount: 75000 },
        { month: '2025-12', amount: 75000 },
        { month: '2026-01', amount: 80000 },
        { month: '2026-02', amount: 80000 },
        { month: '2026-03', amount: 80000 },
      ],
    },
  ],

  // ─── Gastos diarios (Feb 2026) ────────────────────────────────────────────────
  expenses: [
    { id: 'exp-d01', date: '2026-02-03', description: 'Supermercado',      category: 'Comida',     amount:  95000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d02', date: '2026-02-03', description: 'Verdulería',        category: 'Comida',     amount:   8500, paymentMethod: 'debit-bank-demo-1' },
    { id: 'exp-d03', date: '2026-02-04', description: 'Nafta',             category: 'Transporte', amount:  80000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d04', date: '2026-02-05', description: 'Farmacia',          category: 'Salud',      amount:  14000, paymentMethod: 'debit-bank-demo-1' },
    { id: 'exp-d05', date: '2026-02-07', description: 'Artículos limpieza',category: 'Limpieza',   amount:  36000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d06', date: '2026-02-10', description: 'Supermercado',      category: 'Comida',     amount: 110000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d07', date: '2026-02-11', description: 'Carnicería',        category: 'Comida',     amount:  38000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d08', date: '2026-02-12', description: 'Indumentaria',      category: 'Ropa',       amount:  45000, paymentMethod: 'card-visa-d1'   },
    { id: 'exp-d09', date: '2026-02-13', description: 'Transporte',        category: 'Transporte', amount:  10000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d10', date: '2026-02-14', description: 'Salida (cena)',      category: 'Salidas',    amount:  35000, paymentMethod: 'card-visa-d1'   },
    { id: 'exp-d11', date: '2026-02-17', description: 'Panadería',         category: 'Comida',     amount:  12000, paymentMethod: 'debit-bank-demo-1' },
    { id: 'exp-d12', date: '2026-02-18', description: 'Medicamentos',      category: 'Salud',      amount:  22000, paymentMethod: 'debit-bank-demo-1' },
    { id: 'exp-d13', date: '2026-02-20', description: 'Ferretería',        category: 'Hogar',      amount:  18000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d14', date: '2026-02-22', description: 'Supermercado',      category: 'Comida',     amount:  92000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d15', date: '2026-02-24', description: 'Nafta',             category: 'Transporte', amount:  80000, paymentMethod: 'card-visa-d2'   },
    { id: 'exp-d16', date: '2026-02-26', description: 'Dietética',         category: 'Comida',     amount:  15000, paymentMethod: 'debit-bank-demo-1' },
  ],

  // ─── Presupuestos ──────────────────────────────────────────────────────────────
  budgets: {
    '2026-02': {
      creditLimit:    400000,
      debitCashLimit: 200000,
      weeklyLimit:    150000,
    },
  },

  // ─── Reservas / Gastos Fijos ───────────────────────────────────────────────────
  fixedExpenses: [
    {
      id:         'fx-demo-reserva-a',
      label:      'Reserva A',
      type:       'reserve',
      icon:       '👤',
      recurring:  false,
      baseAmount: 0,
      months:     [],
    },
    {
      id:         'fx-demo-reserva-b',
      label:      'Reserva B',
      type:       'reserve',
      icon:       '👤',
      recurring:  false,
      baseAmount: 0,
      months:     [],
    },
    {
      id:         'fx-demo-efectivo',
      label:      'Efectivo',
      type:       'cash',
      icon:       '💵',
      recurring:  true,
      baseAmount: 100000,
      months:     [],
    },
    {
      id:         'fx-demo-tarjetas',
      label:      'Estimado tarjetas',
      type:       'other',
      icon:       '📊',
      recurring:  true,
      baseAmount: 300000,
      months:     [],
    },
  ],

  // ─── Ingresos ──────────────────────────────────────────────────────────────────
  income: {
    netMonthly:      500000,   // sueldo neto mensual (editable)
    usdRateOfficial: 1050,     // tipo de cambio oficial
    usdRateCCL:      1250,     // tipo de cambio CCL
    splitPercent:    70,       // DualPay: % del ingreso que va a gastos
  },
}
