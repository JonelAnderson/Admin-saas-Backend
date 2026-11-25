namespace DS.Platform.Domain.Enums
{
    /// <summary>Qué se le pide a Coolify sobre el stack de una instancia del ERP (bandeja <c>ops.InstanceDeployments</c>, fase C5).</summary>
    public enum DeploymentType
    {
        /// <summary>Crear el stack del cliente (base, migraciones, API y web), cargar sus variables y arrancarlo.</summary>
        Create = 1,

        /// <summary>Actualizar el stack a otra versión de las imágenes.</summary>
        Update = 2
    }

    /// <summary>
    /// Hasta dónde llegó un <see cref="DeploymentType.Create"/> en Coolify. Cada paso se guarda al terminar, así un reintento
    /// continúa donde quedó y nunca crea dos stacks ni cambia las variables de uno que ya arrancó.
    /// </summary>
    public enum DeploymentStep
    {
        None = 0,
        /// <summary>El servicio existe en Coolify (su uuid ya está en la instancia).</summary>
        ServiceCreated = 1,
        /// <summary>Todas las variables del stack están cargadas.</summary>
        Configured = 2,
        /// <summary>Se pidió el arranque.</summary>
        Started = 3
    }
}
