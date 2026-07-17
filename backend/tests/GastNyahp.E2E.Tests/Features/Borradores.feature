Característica: Borradores conversacionales
    Un agente (Telegram vía MCP) o la UI moldean la carga de a poco — "estoy en el super", ítems, "me
    descontaron 20%" — y recién al confirmar el borrador se dispara el comando real con todos sus guards.
    El historial del borrador queda auditable versión por versión.

Escenario: La fila del super — el borrador se moldea y al confirmar carga el ticket
    Dado un borrador de ticket "Super Coto"
    Cuando le agrego al borrador el ítem "Carne" de $30000 en "Comida"
    Y le agrego al borrador el ítem "Lavandina" de $2000 en "Limpieza"
    Y actualizo el borrador con un descuento de $6400
    Y confirmo el borrador
    Entonces la operación es aceptada
    Y el ticket "Super Coto" de este mes totaliza $25600
    Y no quedan borradores abiertos

Escenario: Un borrador incompleto no se puede confirmar y sigue abierto
    Dado un borrador de ticket "Chino de la vuelta"
    Cuando confirmo el borrador
    Entonces la operación es rechazada con "ítems"
    Y el borrador sigue abierto

Escenario: Confirmar dos veces no duplica la carga
    Dado un borrador de gasto "Nafta" por $20000
    Cuando confirmo el borrador
    Entonces la operación es aceptada
    Cuando confirmo el borrador
    Entonces la operación es rechazada con "ya fue confirmado"

Escenario: Descartar un borrador no toca la contabilidad
    Dado un borrador de gasto "Impulso de compra" por $999999
    Cuando descarto el borrador
    Entonces la operación es aceptada
    Y no quedan borradores abiertos
    Y no hay gastos este mes

Escenario: Un borrador de cuotas confirma la compra con su calendario
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA"
    Y un borrador de cuotas "Smart TV" con la tarjeta "VISA BBVA" en 12 cuotas de $85000
    Cuando confirmo el borrador
    Entonces la operación es aceptada
    Y la compra en cuotas "Smart TV" tiene 12 meses en su calendario

Escenario: Una clave de agente puede operar borradores de punta a punta
    Dado una clave de agente llamada "telegram"
    Cuando la clave "telegram" crea un borrador de gasto "Kiosco" por $1500
    Y la clave "telegram" confirma ese borrador
    Entonces el gasto "Kiosco" de este mes figura por $1500
