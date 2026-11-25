using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace DS.Platform.Infrastructure.Persistence.Repositories.Common
{
    /// <summary>
    /// Case-insensitive text filters for the lists. <see cref="WhereSearch{T}"/> takes a normal predicate and turns every
    /// <c>string.Contains(text)</c> in it into <c>ILIKE '%text%'</c> on PostgreSQL (with <c>%</c>, <c>_</c> and <c>\</c> escaped),
    /// or into <c>lower(column).Contains(lower(text))</c> on other providers (the in-memory provider of the tests).
    /// Other calls in the predicate (e.g. <c>ids.Contains(x.Id)</c>) are left as they are.
    /// </summary>
    public static class TextSearch
    {
        /// <summary>Set once at startup when the context is registered with Npgsql (AddDatabase).</summary>
        public static bool UsePostgresILike { get; set; }

        private static readonly MethodInfo StringContains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        private static readonly MethodInfo PatternMethod = typeof(TextSearch).GetMethod(nameof(Pattern))!;
        private static readonly MethodInfo ILike = typeof(NpgsqlDbFunctionsExtensions)
            .GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike), [typeof(DbFunctions), typeof(string), typeof(string), typeof(string)])!;
        private static readonly Expression Functions = Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

        /// <summary>ILIKE pattern "%text%" with the wildcard characters of the user's text escaped (escape character "\").</summary>
        public static string Pattern(string text)
            => "%" + text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";

        public static IQueryable<T> WhereSearch<T>(this IQueryable<T> query, Expression<Func<T, bool>> predicate)
            => query.Where(Rewrite(predicate, UsePostgresILike));

        /// <summary>The predicate with its text <c>Contains</c> made case-insensitive for the given provider.</summary>
        public static Expression<Func<T, bool>> Rewrite<T>(Expression<Func<T, bool>> predicate, bool postgres)
            => Expression.Lambda<Func<T, bool>>(new Rewriter(postgres).Visit(predicate.Body)!, predicate.Parameters);

        private sealed class Rewriter(bool postgres) : ExpressionVisitor
        {
            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method != StringContains || node.Object is null)
                    return base.VisitMethodCall(node);
                var column = Visit(node.Object)!;
                var text = node.Arguments[0];
                return postgres
                    ? Expression.Call(ILike, Functions, column, Expression.Call(PatternMethod, text), Expression.Constant("\\"))
                    : Expression.Call(Expression.Call(column, ToLower), StringContains, Expression.Call(text, ToLower));
            }
        }
    }
}
