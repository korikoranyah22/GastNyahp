Característica: Gastos en dólares
    Un gasto en USD se convierte a pesos con el dólar CCL configurado en Ingresos; sin CCL no se puede cargar.

Escenario: No se puede cargar un gasto en dólares sin CCL configurado
    Cuando registro un gasto "Compra online" de USD 50 en efectivo el "2026-02-03"
    Entonces la operación es rechazada con "UsdRateCcl"

Escenario: Un gasto en dólares se convierte con el CCL configurado
    Dado que el dólar CCL configurado es 1250
    Cuando registro un gasto "Compra online" de USD 50 en efectivo el "2026-02-03"
    Entonces la operación es aceptada
    Y el gasto "Compra online" de "2026-02" figura por $62500
    Y el gasto "Compra online" de "2026-02" conserva el monto original de USD 50
