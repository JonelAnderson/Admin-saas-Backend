using DS.Platform.Application.DTOs.Plans.Request;
using DS.Platform.Application.Features.Plans.Commands.ChangeState;
using DS.Platform.Application.Features.Plans.Commands.Create;
using DS.Platform.Application.Features.Plans.Commands.Delete;
using DS.Platform.Application.Features.Plans.Commands.Update;
using DS.Platform.Application.Features.Plans.Queries.GetAllPaged;
using DS.Platform.Application.Features.Plans.Queries.GetById;
using DS.Platform.Application.Features.Plans.Queries.GetSelect;
using DS.Platform.Application.Features.Plans.Validators;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;
using DS.Platform.Shared.Commons.Bases.Request;
using DS.Platform.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Tests.Plans
{
    /// <summary>
    /// Módulo de referencia de la consola (Planes): handlers con TestDatabase + UnitOfWork, mapeo explícito, código único por
    /// producto, límites del otro producto vacíos, listado ordenado y paginado, borrado lógico.
    /// </summary>
    public class PlanHandlersTests
    {
        private readonly TestDatabase _database = new();
        private readonly TestCurrentUserService _user = TestCurrentUserService.User("admin");

        private static PlanRequestDto Erp(string code = "erp-basico", string name = "ERP Básico") => new()
        {
            Product = PlanProduct.Erp, Code = code, Name = name, MonthlyPrice = 350m, YearlyPrice = 3500m, Currency = "pen",
            TrialDays = 14, MaxCompanies = 1, MaxUsers = 10, MaxBranches = 2, IncludesAi = true
        };

        private static PlanRequestDto Api(string code = "API-1000") => new()
        {
            Product = PlanProduct.ApiSunat, Code = code, Name = "API 1000", MonthlyPrice = 60m, ApiPlanSlug = "pro", MaxIssuers = 1, MaxDocumentsPerMonth = 1000
        };

        private async Task<int> CreateAsync(PlanRequestDto dto)
        {
            await using var db = _database.CreateContext(_user);
            var result = await new CreatePlanCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                .Handle(new CreatePlanCommand { PlanRequestDto = dto }, CancellationToken.None);
            Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            return result.Data;
        }

        [Fact]
        public async Task Create_normalizes_the_code_and_currency_and_saves_the_limits()
        {
            var id = await CreateAsync(Erp());

            await using var db = _database.CreateContext(_user);
            var plan = await db.Set<Plan>().SingleAsync(p => p.Id == id);
            Assert.Equal(("ERP-BASICO", "PEN", true, "admin"), (plan.Code, plan.Currency, plan.IsActive, plan.CreatedBy));
            Assert.Equal((1, 10, 2, true), (plan.MaxCompanies, plan.MaxUsers, plan.MaxBranches, plan.IncludesAi));
            Assert.Null(plan.MaxDocumentsPerMonth);
        }

        [Fact]
        public async Task The_code_is_unique_per_product_but_can_repeat_in_the_other_product()
        {
            await CreateAsync(Erp("BASICO"));
            await CreateAsync(Api("BASICO")); // otro producto: permitido

            await using var db = _database.CreateContext(_user);
            var result = await new CreatePlanCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer())
                .Handle(new CreatePlanCommand { PlanRequestDto = Erp(" basico ") }, CancellationToken.None);

            Assert.False(result.Succeeded);
            Assert.Contains("BASICO", string.Join(" ", result.Messages));
        }

        [Fact]
        public async Task Update_applies_the_form_and_clears_the_limits_of_the_other_product()
        {
            var id = await CreateAsync(Erp());
            var form = new UpdatePlanRequestDto
            {
                Id = id, Product = PlanProduct.ApiSunat, Code = "API-5000", Name = "API 5000", MonthlyPrice = 150m,
                MaxIssuers = 3, MaxDocumentsPerMonth = 5000
            };

            await using (var db = _database.CreateContext(_user))
            {
                var result = await new UpdatePlanCommandHandler(HandlerKit.UnitOfWork(db, _user), new TestClock(new DateOnly(2026, 11, 1)), new TestLocalizer())
                    .Handle(new UpdatePlanCommand { UpdatePlanRequestDto = form }, CancellationToken.None);
                Assert.True(result.Succeeded, string.Join(" ", result.Messages));
            }

            await using var check = _database.CreateContext(_user);
            var plan = await check.Set<Plan>().SingleAsync(p => p.Id == id);
            Assert.Equal((PlanProduct.ApiSunat, "API-5000", 3, 5000), (plan.Product, plan.Code, plan.MaxIssuers, plan.MaxDocumentsPerMonth));
            Assert.Equal((null, null, null, false), (plan.MaxCompanies, plan.MaxUsers, plan.MaxBranches, plan.IncludesAi));
            Assert.Equal("admin", plan.LastModifiedBy);
        }

        [Fact]
        public async Task List_is_ordered_by_product_order_and_name_filtered_and_paged()
        {
            await CreateAsync(Api("API-2"));
            await CreateAsync(Erp("ERP-B", "B"));
            await CreateAsync(Erp("ERP-A", "A"));

            await using var db = _database.CreateContext(_user);
            var handler = new GetAllPlansPagedQueryHandler(HandlerKit.UnitOfWork(db, _user), new TestLocalizer());

            var all = await handler.Handle(new GetAllPlansPagedQuery { BaseFiltersRequest = new BaseFiltersRequest { NumRecordsPage = 50 } }, CancellationToken.None);
            Assert.Equal(["ERP-A", "ERP-B", "API-2"], all.Data.Select(p => p.Code));
            Assert.Equal(3, all.TotalRecords);
            Assert.Equal(["ERP", "ERP", "API SUNAT"], all.Data.Select(p => p.ProductName));

            var onlyApi = await handler.Handle(new GetAllPlansPagedQuery { Product = PlanProduct.ApiSunat }, CancellationToken.None);
            Assert.Equal(["API-2"], onlyApi.Data.Select(p => p.Code));

            var page2 = await handler.Handle(new GetAllPlansPagedQuery
            {
                BaseFiltersRequest = new BaseFiltersRequest { NumPage = 2, NumRecordsPage = 2 }
            }, CancellationToken.None);
            Assert.Equal(["API-2"], page2.Data.Select(p => p.Code));
            Assert.Equal(3, page2.TotalRecords);
        }

        [Fact]
        public async Task Inactive_and_deleted_plans_leave_the_select_and_a_deleted_plan_frees_its_code()
        {
            var a = await CreateAsync(Erp("ERP-A"));
            var b = await CreateAsync(Erp("ERP-B"));

            await using (var db = _database.CreateContext(_user))
            {
                var uow = HandlerKit.UnitOfWork(db, _user);
                Assert.True((await new ChangeStatePlanCommandHandler(uow, new TestLocalizer()).Handle(new ChangeStatePlanCommand { Id = a }, CancellationToken.None)).Succeeded);
                Assert.True((await new DeletePlanCommandHandler(uow, new TestLocalizer()).Handle(new DeletePlanCommand { Id = b }, CancellationToken.None)).Succeeded);
            }

            await using (var db = _database.CreateContext(_user))
            {
                var uow = HandlerKit.UnitOfWork(db, _user);
                var select = await new GetPlanSelectQueryHandler(uow).Handle(new GetPlanSelectQuery { Product = PlanProduct.Erp }, CancellationToken.None);
                Assert.Empty(select.Data);
                Assert.False((await new GetPlanByIdQueryHandler(uow, new TestLocalizer()).Handle(new GetPlanByIdQuery { Id = b }, CancellationToken.None)).Succeeded);
                Assert.False((await new DeletePlanCommandHandler(uow, new TestLocalizer()).Handle(new DeletePlanCommand { Id = b }, CancellationToken.None)).Succeeded);
            }

            await CreateAsync(Erp("ERP-B")); // el código del plan borrado queda libre
        }

        [Theory]
        [InlineData(PlanProduct.Erp, nameof(PlanRequestDto.MaxDocumentsPerMonth))]
        [InlineData(PlanProduct.ApiSunat, nameof(PlanRequestDto.MaxUsers))]
        [InlineData(PlanProduct.ApiSunat, nameof(PlanRequestDto.IncludesAi))]
        public void The_validator_rejects_limits_of_the_other_product(PlanProduct product, string field)
        {
            var dto = product == PlanProduct.Erp ? Erp() : Api();
            switch (field)
            {
                case nameof(PlanRequestDto.MaxDocumentsPerMonth): dto.MaxDocumentsPerMonth = 100; break;
                case nameof(PlanRequestDto.MaxUsers): dto.MaxUsers = 5; break;
                case nameof(PlanRequestDto.IncludesAi): dto.IncludesAi = true; break;
            }

            var result = new PlanRequestValidator().Validate(dto);
            Assert.Contains(result.Errors, e => e.PropertyName == field);
        }

        [Fact]
        public void The_validator_rejects_bad_codes_currencies_prices_and_zero_limits()
        {
            var dto = Erp();
            dto.Code = "ERP BASICO";
            dto.Currency = "EUR";
            dto.MonthlyPrice = -1;
            dto.MaxUsers = 0;
            dto.TrialDays = 120;

            var fields = new PlanRequestValidator().Validate(dto).Errors.Select(e => e.PropertyName).ToHashSet();
            Assert.Superset(new HashSet<string> { "Code", "Currency", "MonthlyPrice", "MaxUsers", "TrialDays" }, fields);
            Assert.True(new PlanRequestValidator().Validate(Erp()).IsValid);
            Assert.True(new PlanRequestValidator().Validate(Api()).IsValid);
        }
    }
}
