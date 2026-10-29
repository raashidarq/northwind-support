using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Teaches SQLite SQL Server's <c>DATEDIFF(second, start, end)</c>, as
/// <c>round((julianday(end) - julianday(start)) * 86400)</c>, so that queries using
/// <see cref="SqlServerDbFunctionsExtensions.DateDiffSecond(DbFunctions, DateTime?, DateTime?)"/>
/// can run against the in-memory test database.
/// </summary>
#pragma warning disable EF1001 // The SQLite translator provider is internal API; fine for a test double.
public sealed class SqliteDateDiffTranslatorProvider : SqliteMethodCallTranslatorProvider
{
    public SqliteDateDiffTranslatorProvider(RelationalMethodCallTranslatorProviderDependencies dependencies)
        : base(dependencies) =>
        AddTranslators([new DateDiffSecondTranslator(dependencies.SqlExpressionFactory)]);

    private sealed class DateDiffSecondTranslator(ISqlExpressionFactory sql) : IMethodCallTranslator
    {
        public SqlExpression? Translate(
            SqlExpression? instance,
            MethodInfo method,
            IReadOnlyList<SqlExpression> arguments,
            IDiagnosticsLogger<DbLoggerCategory.Query> logger)
        {
            if (method.DeclaringType != typeof(SqlServerDbFunctionsExtensions) ||
                method.Name != nameof(SqlServerDbFunctionsExtensions.DateDiffSecond))
            {
                return null;
            }

            // arguments[0] is EF.Functions itself.
            // Rounded, because julianday is a floating-point number of days.
            var seconds = sql.Multiply(
                sql.Subtract(JulianDay(arguments[2]), JulianDay(arguments[1])),
                sql.Constant(86400.0));

            return sql.Convert(
                sql.Function("round", [seconds], nullable: true, argumentsPropagateNullability: [true], typeof(double)),
                typeof(int?));
        }

        private SqlExpression JulianDay(SqlExpression date) =>
            sql.Function("julianday", [date], nullable: true, argumentsPropagateNullability: [true], typeof(double));
    }
}
#pragma warning restore EF1001
