Característica: Día hábil
    El evento diario de nuevo día hábil habilita las novedades que un agente consulta cada mañana vía MCP.

Escenario: Abrir el mismo día dos veces es rechazado
    Cuando abro el día hábil "2026-07-09"
    Entonces la operación es aceptada
    Cuando abro el día hábil "2026-07-09"
    Entonces la operación es rechazada
    Y el día hábil "2026-07-09" figura abierto

Escenario: Las novedades del día muestran los vencimientos pendientes
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA" con cierre el día 15 y vencimiento el día 9
    Y una compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2026-07"
    Cuando abro el día hábil "2026-07-09"
    Entonces las novedades del "2026-07-09" incluyen la cuota pendiente "Smart TV"
    Y las novedades del "2026-07-09" indican que "VISA BBVA" vence hoy

Escenario: Una cuota pagada deja de aparecer en las novedades
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"
    Y una compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2026-07"
    Y marco como pagada la cuota de "2026-07" de "Smart TV"
    Cuando abro el día hábil "2026-07-09"
    Entonces las novedades del "2026-07-09" no incluyen la cuota "Smart TV"
