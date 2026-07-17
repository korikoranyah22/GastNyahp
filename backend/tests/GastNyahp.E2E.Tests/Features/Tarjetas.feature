Característica: Tarjetas
    Las tarjetas pertenecen a un banco y no pueden eliminarse con cuotas o servicios vinculados.

Escenario: Registrar una tarjeta de crédito
    Dado un banco "BBVA"
    Cuando registro la tarjeta "VISA BBVA" del banco "BBVA" con cierre el día 15 y vencimiento el día 5
    Entonces la operación es aceptada
    Y el listado de tarjetas contiene "VISA BBVA"

Escenario: No se puede registrar una tarjeta de un banco inexistente
    Cuando registro la tarjeta "VISA Fantasma" de un banco inexistente
    Entonces la operación es rechazada con "El banco no existe"

Escenario: No se puede eliminar una tarjeta con cuotas asociadas
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"
    Y una compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2025-10"
    Cuando intento eliminar la tarjeta "VISA BBVA"
    Entonces la operación es rechazada con "cuotas o servicios asociados"

Escenario: No se puede eliminar una tarjeta con un servicio vinculado
    Dado un banco "Galicia"
    Y una tarjeta "VISA Galicia" del banco "Galicia"
    Y un servicio "Seguro auto" de $75000 mensuales vinculado a la tarjeta "VISA Galicia"
    Cuando intento eliminar la tarjeta "VISA Galicia"
    Entonces la operación es rechazada con "cuotas o servicios asociados"
