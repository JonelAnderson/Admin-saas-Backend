using DS.Platform.Application.DTOs.Customers.Request;
using DS.Platform.Application.Features.Customers;
using DS.Platform.Application.Features.Customers.Commands.ChangeState;
using DS.Platform.Application.Features.Customers.Commands.Create;
using DS.Platform.Application.Features.Customers.Commands.Delete;
using DS.Platform.Application.Features.Customers.Queries.GetAllPaged;
using DS.Platform.Application.Features.Customers.Queries.Lookup;
using DS.Platform.Application.Features.Customers.Validators;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Entities.Customers;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Customers
{
    /// <summary>Clientes de la plataforma: RUC válido y único, buscador, suscripciones abiertas, borrado solo sin suscripciones.</summary>
    public class CustomerHandlersTests
    {
        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("cobranzas");

        public static readonly string RucAlfa = "20131312955";
        public static readonly string RucBeta = "20100070970";

        private static CustomerRequestDto Form(string ruc, string name = "ALFA SAC") => new()
        {
            Ruc = ruc, BusinessName = name, TradeName = "Alfa", ContactName = "Ana Ruiz", ContactEmail = "Ana.Ruiz@Alfa.PE",
            ContactPhone = "+51 966464645"
        };

        private async Task<int> CreateAsync(CustomerRequestDto dto)
        {
            await using var db = _database.CreateContext(_user);
            var result = await new CreateCustomerCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                .Handle(new CreateCustomerCommand { CustomerRequestDto = dto }, CancellationToken.None);
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        [Theory]
        [InlineData("20131312955", true)]
        [InlineData("20100070970", true)]
        [InlineData("20131312954", false)] // dígito verificador
        [InlineData("30131312955", false)] // prefijo
        [InlineData("2013131295", false)]  // 10 dígitos
        [InlineData("2013131295A", false)]
        [InlineData(null, false)]
        public void Ruc_rules_check_length_prefix_and_check_digit(string? ruc, bool valid)
            => Assert.Equal(valid, RucRules.IsValid(ruc));

        [Fact]
        public void The_validator_rejects_an_invalid_ruc_email_and_phone()
        {
            var dto = Form("20131312954");
            dto.ContactEmail = "no-es-correo";
            dto.ContactPhone = "abc";
            var fields = new CustomerRequestValidator().Validate(dto).Errors.Select(e => e.PropertyName).ToHashSet();
            Assert.Superset(new HashSet<string> { "Ruc", "ContactEmail", "ContactPhone" }, fields);
            Assert.True(new CustomerRequestValidator().Validate(Form(RucAlfa)).IsValid);
        }

        [Fact]
        public async Task Create_saves_the_customer_and_rejects_a_repeated_ruc()
        {
            var id = await CreateAsync(Form(" " + RucAlfa + " "));

            await using (var db = _database.CreateContext(_user))
            {
                var saved = await db.Set<Customer>().SingleAsync(c => c.Id == id);
                Assert.Equal((RucAlfa, "ana.ruiz@alfa.pe", true, "cobranzas"), (saved.Ruc, saved.ContactEmail, saved.IsActive, saved.CreatedBy));

                var repeated = await new CreateCustomerCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                    .Handle(new CreateCustomerCommand { CustomerRequestDto = Form(RucAlfa, "OTRO") }, CancellationToken.None);
                Assert.False(repeated.Succeeded);
                Assert.Contains(RucAlfa, string.Join(" ", repeated.Messages));
            }
        }

        [Fact]
        public async Task List_counts_open_subscriptions_and_lookup_skips_inactive_customers()
        {
            var alfa = await CreateAsync(Form(RucAlfa, "ALFA SAC"));
            var beta = await CreateAsync(Form(RucBeta, "BETA SAC"));
            await _database.SeedAsync(db =>
            {
                db.Set<Plan>().Add(new Plan { Id = 1, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP" });
                db.Set<Subscription>().AddRange(
                    new Subscription { CustomerId = alfa, PlanId = 1, Product = PlanProduct.Erp, Status = SubscriptionStatus.Active },
                    new Subscription { CustomerId = alfa, PlanId = 1, Product = PlanProduct.ApiSunat, Status = SubscriptionStatus.Cancelled });
            });

            await using var db = _database.CreateContext(_user);
            var uow = HandlerKit.UnitOfWork(db, _user);
            Assert.True((await new ChangeStateCustomerCommandHandler(uow, new TestLocalizer())
                .Handle(new ChangeStateCustomerCommand { Id = beta }, CancellationToken.None)).Succeeded);

            var list = await new GetAllCustomersPagedQueryHandler(uow, new TestLocalizer())
                .Handle(new GetAllCustomersPagedQuery { BaseFiltersRequest = new BaseFiltersRequest() }, CancellationToken.None);
            Assert.Equal(["ALFA SAC", "BETA SAC"], list.Data.Select(c => c.BusinessName));
            Assert.Equal(1, list.Data.First().OpenSubscriptions); // la cancelada no cuenta

            var lookup = await new LookupCustomersQueryHandler(uow).Handle(new LookupCustomersQuery { Text = "sac" }, CancellationToken.None);
            Assert.Equal([alfa], lookup.Data.Select(c => c.Id));
        }

        [Fact]
        public async Task A_customer_with_open_subscriptions_cannot_be_deleted()
        {
            var alfa = await CreateAsync(Form(RucAlfa));
            await _database.SeedAsync(db =>
            {
                db.Set<Plan>().Add(new Plan { Id = 1, Product = PlanProduct.Erp, Code = "ERP", Name = "ERP" });
                db.Set<Subscription>().Add(new Subscription { Id = 7, CustomerId = alfa, PlanId = 1, Product = PlanProduct.Erp, Status = SubscriptionStatus.Grace });
            });

            await using (var db = _database.CreateContext(_user))
            {
                var result = await new DeleteCustomerCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                    .Handle(new DeleteCustomerCommand { Id = alfa }, CancellationToken.None);
                Assert.False(result.Succeeded);
                Assert.Contains(DeleteCustomerCommandHandler.HasSubscriptionsMessage, result.Messages);
            }

            await _database.SeedAsync(db => db.Set<Subscription>().Single(s => s.Id == 7).Status = SubscriptionStatus.Cancelled);

            await using (var db = _database.CreateContext(_user))
            {
                var result = await new DeleteCustomerCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                    .Handle(new DeleteCustomerCommand { Id = alfa }, CancellationToken.None);
                Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            }

            await CreateAsync(Form(RucAlfa)); // el RUC del cliente borrado queda libre
        }
    }
}
