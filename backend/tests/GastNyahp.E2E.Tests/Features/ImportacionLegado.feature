Característica: Importación del JSON de la maqueta
    El export de la versión anterior (localStorage/Drive) se importa reproduciendo la carga manual como
    comandos: los guards corren, los ids se remapean y el event store queda auditable como si se hubiera
    tipeado todo a mano hasta el punto de guardado del archivo.

Escenario: Importar el JSON reproduce toda la carga hasta su estado final
    Cuando importo el JSON de ejemplo de la maqueta
    Entonces la operación es aceptada
    Y el resumen de importación reporta 1 banco, 1 tarjeta, 1 cuota, 1 préstamo y 2 movimientos
    Y el listado de bancos contiene "BBVA"
    Y la cuota importada "Smart TV" tiene el mes "2025-10" pagado
    Y la cuota importada "Smart TV" tiene el mes "2025-11" por $90000
    Y el gasto "Coto" de "2026-02" figura por $95000
    Y el ticket importado "Super" de "2026-02" totaliza $33000
    Y el dólar CCL importado quedó en 1250

Escenario: No se importa sobre una familia con datos, salvo con force
    Dado un banco "Galicia"
    Cuando importo el JSON de ejemplo de la maqueta
    Entonces la operación es rechazada con "ya tiene datos"
    Cuando importo el JSON de ejemplo de la maqueta con force
    Entonces la operación es aceptada

Escenario: Importar reemplazando borra lo anterior y queda auditado
    Dado un banco "Galicia"
    Y un gasto "Kiosco viejo" de $500 en "Comida" pagado con efectivo
    Cuando importo el JSON de ejemplo de la maqueta reemplazando todo
    Entonces la operación es aceptada
    Y el listado de bancos contiene "BBVA"
    Y el listado de bancos no contiene "Galicia"
    Y no hay gastos este mes
    Y el resumen de importación reporta 1 banco, 1 tarjeta, 1 cuota, 1 préstamo y 2 movimientos

Escenario: Una clave de agente no puede importar
    Dado una clave de agente llamada "asistente"
    Cuando la clave de agente "asistente" intenta importar el JSON
    Entonces la operación es prohibida
