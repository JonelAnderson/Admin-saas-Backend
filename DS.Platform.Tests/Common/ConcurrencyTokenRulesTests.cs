using DS.Platform.Domain.Contracts;
using DS.Platform.Domain.Entities.ApiSunat;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Entities.Ops;
using DS.Platform.Infrastructure.Persistence.Contexts;
using DS.Platform.Tests.Support;

namespace DS.Platform.Tests.Common
{
    /// <summary>
    /// Lo que dos administradores pueden tocar a la vez lleva la versión de fila xmin como token de concurrencia
    /// (IConcurrencyChecked): el segundo guardado falla con 409 en lugar de pisar al primero. Cada entidad nueva con estado o
    /// saldo (suscripción, pago, licencia, acción remota) se agrega aquí.
    /// </summary>
    public class ConcurrencyTokenRulesTests
    {
        [Theory]
        [InlineData(typeof(Plan))]
        [InlineData(typeof(Customer))]
        [InlineData(typeof(Subscription))]
        [InlineData(typeof(Payment))]
        [InlineData(typeof(ApiSunatIssuer))]
        [InlineData(typeof(RemoteAction))]
        [InlineData(typeof(Notification))]
        [InlineData(typeof(InstanceDeployment))]
        [InlineData(typeof(PortalAccess))]
        [InlineData(typeof(ApiCredentialDelivery))]
        public void Race_prone_entities_carry_the_xmin_concurrency_token(Type entity)
        {
            Assert.True(typeof(IConcurrencyChecked).IsAssignableFrom(entity), $"{entity.Name} debe implementar IConcurrencyChecked.");
            using var db = new TestDatabase().CreateContext(TestCurrentUserService.User());
            var property = db.Model.FindEntityType(entity)!.FindProperty(ApplicationDbContext.RowVersionProperty);
            Assert.NotNull(property);
            Assert.True(property!.IsConcurrencyToken);
        }
    }
}
