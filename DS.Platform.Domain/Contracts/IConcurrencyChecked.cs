namespace DS.Platform.Domain.Contracts
{
    /// <summary>
    /// Entidades con control de concurrencia optimista: cada UPDATE / DELETE compara la versión de la fila (la columna de sistema
    /// <c>xmin</c> de PostgreSQL, sin columna nueva). Si otra transacción la cambió desde que se leyó, el guardado falla con
    /// <c>DbUpdateConcurrencyException</c> y la transacción se deshace: dos peticiones simultáneas sobre el mismo documento o
    /// saldo (confirmar dos veces, gastar dos veces el mismo saldo, la última unidad de stock) no aplican el efecto dos veces.
    /// La API responde 409 y el usuario vuelve a intentar con el dato actualizado.
    /// </summary>
    public interface IConcurrencyChecked
    {
    }
}
