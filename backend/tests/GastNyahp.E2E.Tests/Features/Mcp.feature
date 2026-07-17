Característica: Servidor MCP
    Un agente de IA se conecta al endpoint MCP con una clave de agente de la familia (Authorization: Bearer,
    el estándar para servidores MCP self-hosted) y usa las tools para consultar novedades y cargar gastos.

Escenario: El MCP requiere credencial
    Cuando un agente intenta inicializar la sesión MCP sin credencial
    Entonces recibo un error de credencial requerida

Escenario: Un agente consulta las novedades del día por MCP
    Dado un banco "BBVA"
    Y una tarjeta "VISA BBVA" del banco "BBVA" con cierre el día 15 y vencimiento el día 9
    Y una compra "Smart TV" en 12 cuotas de $85000 con la tarjeta "VISA BBVA" desde "2026-07"
    Y una clave de agente llamada "cron matutino"
    Cuando el agente "cron matutino" llama a la tool "novedades_del_dia" con fecha "2026-07-09"
    Entonces la respuesta de la tool menciona "Smart TV"
    Y la respuesta de la tool menciona "VISA BBVA"

Escenario: Un agente registra un gasto por MCP
    Dado una clave de agente llamada "asistente"
    Cuando el agente "asistente" registra por MCP un gasto "Verdulería" de $8500 en efectivo el "2026-02-03"
    Entonces la respuesta de la tool menciona "Gasto registrado"
    Y el gasto "Verdulería" de "2026-02" figura por $8500
