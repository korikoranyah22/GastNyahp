Característica: Bancos
    Los bancos agrupan tarjetas y préstamos; no pueden eliminarse mientras tengan dependencias.

Escenario: Registrar un banco y verlo en el listado
    Dado un banco "BBVA"
    Entonces el listado de bancos contiene "BBVA"

Escenario: No se puede eliminar un banco con tarjetas asociadas
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"
    Cuando intento eliminar el banco "BBVA"
    Entonces la operación es rechazada con "tarjetas o préstamos asociados"
    Y el listado de bancos contiene "BBVA"

Escenario: No se puede eliminar un banco con préstamos asociados
    Dado un banco "Galicia"
    Y un préstamo "Préstamo personal" del banco "Galicia" de 12 cuotas de $180000 desde "2025-11"
    Cuando intento eliminar el banco "Galicia"
    Entonces la operación es rechazada con "tarjetas o préstamos asociados"

Escenario: Eliminar un banco sin dependencias
    Dado un banco "Santander"
    Cuando intento eliminar el banco "Santander"
    Entonces la operación es aceptada
    Y el listado de bancos no contiene "Santander"
