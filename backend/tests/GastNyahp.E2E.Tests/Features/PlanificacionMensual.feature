Característica: Planificación mensual
    Al empezar un mes nuevo se pueden copiar las estimaciones del mes anterior sin pisar datos existentes.
    Nunca se copian gastos ni tickets: son transacciones reales, no estimaciones.

Escenario: Copiar el mes anterior copia presupuesto y reservas puntuales
    Dado un presupuesto para "2026-02" con meta de crédito $480000
    Y una reserva no recurrente "Cami" con $30000 para "2026-02"
    Cuando copio el mes "2026-02" al mes "2026-03"
    Entonces la operación es aceptada
    Y el presupuesto de "2026-03" tiene meta de crédito $480000
    Y la reserva "Cami" tiene $30000 para "2026-03"

Escenario: Copiar el mes no pisa un presupuesto ya cargado
    Dado un presupuesto para "2026-02" con meta de crédito $480000
    Y un presupuesto para "2026-03" con meta de crédito $999999
    Cuando copio el mes "2026-02" al mes "2026-03"
    Entonces el presupuesto de "2026-03" tiene meta de crédito $999999

Escenario: Las reservas recurrentes no necesitan copia
    Dado una reserva recurrente "Efectivo" con base $100000
    Cuando copio el mes "2026-02" al mes "2026-03"
    Entonces la reserva "Efectivo" no tiene entradas puntuales
