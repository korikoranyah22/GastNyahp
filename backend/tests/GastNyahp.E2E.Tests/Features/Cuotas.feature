Característica: Cuotas
    Una compra en cuotas genera su calendario de pagos y las cuotas pagadas nunca se pierden.

Antecedentes:
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"

Escenario: Registrar una compra en cuotas genera el calendario completo
    Cuando registro la compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2025-10"
    Entonces la compra "Smart TV" tiene 12 cuotas
    Y la cuota de "2025-10" de "Smart TV" está pendiente
    Y la cuota de "2026-09" de "Smart TV" está pendiente

Escenario: Marcar una cuota como pagada
    Dado una compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2025-10"
    Cuando marco como pagada la cuota de "2025-10" de "Smart TV"
    Entonces la cuota de "2025-10" de "Smart TV" está pagada
    Y la cuota de "2025-11" de "Smart TV" está pendiente

Escenario: Revisar el plan preserva las cuotas ya pagadas y sus montos
    Dado una compra "Notebook" en 4 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2025-10"
    Y marco como pagada la cuota de "2025-10" de "Notebook"
    Cuando reviso el plan de "Notebook" a 6 cuotas de $100000 desde "2025-10"
    Entonces la compra "Notebook" tiene 6 cuotas
    Y la cuota de "2025-10" de "Notebook" está pagada
    Y la cuota de "2025-10" de "Notebook" vale $85000
    Y la cuota de "2025-12" de "Notebook" vale $100000
