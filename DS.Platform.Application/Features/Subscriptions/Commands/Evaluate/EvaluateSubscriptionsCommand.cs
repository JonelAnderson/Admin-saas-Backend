using DS.Platform.Application.Helpers;
using DS.Platform.Application.Features.ApiSunat.Common;
using DS.Platform.Application.Features.Notifications.Common;
using DS.Platform.Application.Interfaces.Repositories;
using DS.Platform.Application.Interfaces.Services;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Shared.Wrapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Application.Features.Subscriptions.Commands.Evaluate
{
    /// <summary>
    /// Recalcula el estado de todas las suscripciones abiertas con la fecha de hoy (Lima): pasan solas a En gracia o Suspendida
    /// al vencer. Lo envía el job diario <c>SubscriptionsDaily</c> (06:00 Lima). Devuelve cuántas cambiaron.
    /// También encola los avisos de cobro por correo (por vencer, vence hoy, gracia, suspensión) en la bandeja de avisos, una sola vez
    /// por suscripción y periodo (<see cref="NoticeRules"/>), y las acciones remotas de la API SUNAT.
    /// </summary>
    public class EvaluateSubscriptionsCommand : IRequest<Result<int>>
    {
    }

    internal class EvaluateSubscriptionsCommandHandler(
        IUnitOfWork<int> unitOfWork,
        IDateTimeService dateTime) : IRequestHandler<EvaluateSubscriptionsCommand, Result<int>>
    {
        public async Task<Result<int>> Handle(EvaluateSubscriptionsCommand request, CancellationToken cancellationToken)
        {
            var today = LimaDate.Today(dateTime.NowUtc);
            var subscriptions = await unitOfWork.Subscriptions.GetOpenForEvaluationAsync(cancellationToken);

            var changed = 0;
            var changedCustomers = new HashSet<int>();
            foreach (var subscription in subscriptions)
            {
                if (SubscriptionStatusCalculator.Apply(subscription, today))
                {
                    changed++;
                    changedCustomers.Add(subscription.CustomerId);
                }
            }

            // Avisos de cobro: uno por suscripción y periodo (la clave evita repetirlos aunque el job corra más de una vez al día).
            var specs = await BuildNoticesAsync(subscriptions, today, cancellationToken);
            var queued = await NotificationOutbox.EnqueueAsync(unitOfWork, specs, dateTime.NowUtc, cancellationToken);

            if (changed > 0 || queued > 0)
            {
                // Los clientes cuyo estado cambió pueden haber perdido (o recuperado) la emisión en la API SUNAT.
                foreach (var customerId in changedCustomers)
                {
                    await ApiAccessReconciler.ReconcileAsync(unitOfWork, customerId, dateTime.NowUtc, cancellationToken);
                }
                await unitOfWork.Commit(cancellationToken);
            }

            return await Result<int>.SuccessAsync(changed, $"{changed} suscripciones cambiaron de estado.");
        }

        private async Task<List<NoticeSpec>> BuildNoticesAsync(IReadOnlyList<Subscription> subscriptions, DateOnly today, CancellationToken cancellationToken)
        {
            var due = subscriptions.Select(s => (Subscription: s, Notice: NoticeRules.For(s, today))).Where(x => x.Notice is not null).ToList();
            if (due.Count == 0)
            {
                return [];
            }
            var customerIds = due.Select(x => x.Subscription.CustomerId).Distinct().ToList();
            var planIds = due.Select(x => x.Subscription.PlanId).Distinct().ToList();
            var customers = await unitOfWork.Customers.GetAllQueryable().Where(c => customerIds.Contains(c.Id)).ToListAsync(cancellationToken);
            var plans = await unitOfWork.Plans.GetAllQueryable().Where(p => planIds.Contains(p.Id)).ToListAsync(cancellationToken);

            var specs = new List<NoticeSpec>();
            foreach (var (subscription, notice) in due)
            {
                var customer = customers.FirstOrDefault(c => c.Id == subscription.CustomerId);
                var plan = plans.FirstOrDefault(p => p.Id == subscription.PlanId);
                if (customer is null || plan is null)
                {
                    continue;
                }
                var (subject, body) = NoticeTemplates.Build(notice!.Type, new NoticeContext(
                    customer.BusinessName, NotificationOutbox.ProductName(subscription.Product), plan.Name, subscription.CoveredUntil,
                    subscription.SuspendOn, subscription.Currency, subscription.Price));
                specs.Add(new NoticeSpec(notice.Type, customer, notice.DedupKey, subject, body, SubscriptionId: subscription.Id));
            }
            return specs;
        }
    }
}
