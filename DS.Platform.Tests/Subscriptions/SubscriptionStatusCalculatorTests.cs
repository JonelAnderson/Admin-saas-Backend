using DS.Platform.Application.Features.Subscriptions;
using DS.Platform.Domain.Entities.Billing;
using DS.Platform.Domain.Enums;

namespace DS.Platform.Tests.Subscriptions
{
    /// <summary>Regla pura del estado: prueba, activa, en gracia, suspendida, cancelada; manual gana; fechas de Lima.</summary>
    public class SubscriptionStatusCalculatorTests
    {
        private static readonly DateOnly Start = new(2026, 11, 1);

        private static Subscription Sub(DateOnly? trial = null, DateOnly? paid = null, int grace = 5) => new()
        {
            CustomerId = 1, PlanId = 1, Product = PlanProduct.Erp, StartOn = Start, TrialEndsOn = trial, PaidUntil = paid, GraceDays = grace
        };

        private static DateOnly D(int month, int day) => new(2026, month, day);

        [Theory]
        // Prueba del 1 al 14: Prueba; del 15 al 19 en gracia (5 días); el 20 suspendida.
        [InlineData(11, 14, SubscriptionStatus.Trial)]
        [InlineData(11, 15, SubscriptionStatus.Grace)]
        [InlineData(11, 19, SubscriptionStatus.Grace)]
        [InlineData(11, 20, SubscriptionStatus.Suspended)]
        public void A_trial_ends_into_grace_and_then_suspension(int month, int day, SubscriptionStatus expected)
        {
            var s = Sub(trial: D(11, 14));
            SubscriptionStatusCalculator.Apply(s, D(month, day));
            Assert.Equal(expected, s.Status);
            Assert.Equal((D(11, 14), D(11, 20)), (s.CoveredUntil, s.SuspendOn!.Value));
        }

        [Fact]
        public void Paid_wins_over_the_trial_and_covers_until_the_latest_date()
        {
            var s = Sub(trial: D(11, 14), paid: D(12, 31));
            SubscriptionStatusCalculator.Apply(s, D(11, 5));
            Assert.Equal((SubscriptionStatus.Active, D(12, 31), new DateOnly(2027, 1, 6)), (s.Status, s.CoveredUntil, s.SuspendOn!.Value));
        }

        [Fact]
        public void Without_trial_nor_payment_it_is_in_grace_from_the_start_and_then_suspended()
        {
            var s = Sub(grace: 3);
            SubscriptionStatusCalculator.Apply(s, Start);
            Assert.Equal((SubscriptionStatus.Grace, D(10, 31), D(11, 4)), (s.Status, s.CoveredUntil, s.SuspendOn!.Value));

            SubscriptionStatusCalculator.Apply(s, D(11, 4));
            Assert.Equal(SubscriptionStatus.Suspended, s.Status);
        }

        [Fact]
        public void A_future_start_is_active_until_it_begins()
        {
            var s = Sub(paid: null);
            s.StartOn = D(12, 1);
            SubscriptionStatusCalculator.Apply(s, D(11, 20));
            Assert.Equal(SubscriptionStatus.Active, s.Status);
        }

        [Fact]
        public void Manual_suspension_wins_and_cancelled_is_final()
        {
            var s = Sub(paid: D(12, 31));
            s.SuspendedManually = true;
            SubscriptionStatusCalculator.Apply(s, D(11, 5));
            Assert.Equal(SubscriptionStatus.Suspended, s.Status);

            s.Status = SubscriptionStatus.Cancelled;
            s.CancelledOn = D(11, 6);
            SubscriptionStatusCalculator.Apply(s, D(11, 6));
            Assert.Equal(SubscriptionStatus.Cancelled, s.Status);
            Assert.Null(s.SuspendOn);
        }

        [Fact]
        public void Apply_reports_whether_something_changed()
        {
            var s = Sub(trial: D(11, 14));
            Assert.True(SubscriptionStatusCalculator.Apply(s, D(11, 1)));
            Assert.False(SubscriptionStatusCalculator.Apply(s, D(11, 2)));
            Assert.True(SubscriptionStatusCalculator.Apply(s, D(11, 15)));
        }
    }
}
