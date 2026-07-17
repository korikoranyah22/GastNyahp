Característica: Familias
    El acceso funciona por posesión, como el JSON original: sin emails ni SMS. Crear una familia requiere un
    código del administrador de la app; unirse requiere una invitación QR de un administrador de la familia.
    Cada código e invitación es de un solo uso, y los datos de cada familia están aislados de las demás.

Escenario: No se puede crear una familia con un código inválido
    Cuando intento crear la familia "Los Intrusos" con el código "codigo-falso"
    Entonces la operación es rechazada con "no es válido"

Escenario: Un código de administrador es de un solo uso
    Dado un código de administrador emitido
    Cuando creo la familia "Los Pérez" con ese código
    Entonces la operación es aceptada
    Cuando creo la familia "Los Clonadores" con ese código
    Entonces la operación es rechazada con "ya fue utilizado"

Escenario: Unirse a la familia con una invitación QR
    Cuando genero una invitación para mi familia
    Y alguien se une con esa invitación como "Cami"
    Entonces la operación es aceptada
    Y mi familia tiene 2 miembros

Escenario: Una invitación es de un solo uso
    Dado una invitación generada para mi familia
    Y alguien se une con esa invitación como "Cami"
    Cuando otra persona intenta unirse con la misma invitación como "Intruso"
    Entonces la operación es rechazada con "ya fue utilizada"

Escenario: Sin credencial no se puede acceder a los datos
    Cuando consulto los bancos sin credencial
    Entonces recibo un error de credencial requerida

Escenario: Los datos de una familia no se ven desde otra familia
    Dado un banco "BBVA"
    Y una segunda familia "Los Vecinos"
    Entonces la segunda familia no ve ningún banco
    Y el listado de bancos contiene "BBVA"

Escenario: Una clave de agente sirve como credencial de solo datos
    Dado un banco "BBVA"
    Cuando genero una clave de agente llamada "cron matutino"
    Entonces la operación es aceptada
    Y con la clave de agente "cron matutino" se ve el banco "BBVA"

Escenario: Una clave de agente revocada deja de funcionar
    Dado una clave de agente llamada "Claude Desktop"
    Cuando revoco la clave de agente "Claude Desktop"
    Entonces la operación es aceptada
    Y la clave de agente "Claude Desktop" ya no puede acceder a los datos

Escenario: Una clave de agente no puede generar invitaciones QR
    Dado una clave de agente llamada "cron matutino"
    Cuando la clave de agente "cron matutino" intenta generar una invitación
    Entonces la operación es rechazada con "no es miembro de la familia"
