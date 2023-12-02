using Antlr4.Runtime.Tree;
using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Threading.Tasks;
using static PostgreSQLParser;

namespace NppDB.PostgreSQL
{
    internal static class PostgreSQLAnalyzerHelper
    {

        public static bool In<T>(this T x, params T[] set)
        {
            return set.Contains(x);
        }

        public static void _AnalyzeIdentifier(PostgreSQLParser.IdentifierContext context, ParsedTreeCommand command)
        {
            if (context.GetText()[0] == '"' && context.GetText()[context.GetText().Length - 1] == '"')
            {
                command.AddWarning(context, ParserMessageType.DOUBLE_QUOTES);
            }
        }

        private static int CountTables(IList<PostgreSQLParser.Table_refContext> ctxs, int count)
        {
            if (ctxs != null && ctxs.Count > 0)
            {
                foreach (var ctx in ctxs)
                {
                    count++;
                    if (ctx._tables != null && ctx._tables.Count > 0)
                    {
                        count = CountTables(ctx._tables, count);
                    }
                }
            }
            return count;
        }

        public static bool HasAggregateFunction(IParseTree context)
        {
            return HasSpecificAggregateFunction(context, "sum", "avg", "min", "max", "count");
        }

        public static bool HasSpecificAggregateFunction(IParseTree context, params string[] functionNames)
        {
            if (context is PostgreSQLParser.Func_applicationContext ctx &&
                ctx.func_name().GetText().ToLower().In(functionNames))
            {
                return true;
            }

            for (var n = 0; n < context.ChildCount; ++n)
            {
                var child = context.GetChild(n);
                var result = HasSpecificAggregateFunction(child, functionNames);
                if (result) return true;
            }
            return false;
        }

        public static bool HasAndOrExprWithoutParens(IParseTree context)
        {
            A_expr_orContext a_ExprOR = (A_expr_orContext)FindFirstTargetType(context, typeof(PostgreSQLParser.A_expr_orContext));

            while (a_ExprOR != null)
            {
                int ORCount = 0;
                int ANDCount = 0;
                ORCount += a_ExprOR.OR().Length;
                foreach (A_expr_andContext aExprAnd in a_ExprOR.a_expr_and())
                {
                    ANDCount += aExprAnd.AND().Length;
                }
                if (ORCount > 0 && ANDCount > 0) 
                {
                    return true;
                }
                a_ExprOR = (A_expr_orContext)FindFirstTargetType(a_ExprOR, typeof(PostgreSQLParser.A_expr_orContext));
            }
            return false;
        }

        public static bool IsSelectPramaryContextSelectStar(PostgreSQLParser.Simple_select_pramaryContext[] contexts)
        {
            if (contexts != null && contexts.Length > 0)
            {
                PostgreSQLParser.Simple_select_pramaryContext ctx = contexts[0];
                if (HasSelectStar(ctx))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasOuterJoin(PostgreSQLParser.Simple_select_pramaryContext context)
        {
            if (context.from_clause()?.from_list()?._tables != null && context.from_clause()?.from_list()?._tables.Count > 0)
            {
                foreach (Table_refContext table in context.from_clause()?.from_list()?._tables)
                {
                    if (context.from_clause()?.from_list()?._tables[0].CROSS().Length > 0)
                    {
                        return true;
                    }
                    foreach (Join_typeContext joinType in table.join_type())
                    {
                        if (joinType.GetText().ToLower().In("full", "left", "right", "outer"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public static bool HasSelectStar(Simple_select_pramaryContext ctx)
        {
            return ctx.opt_target_list()?.target_list()?.target_el() != null
                && ctx?.opt_target_list()?.target_list()?.target_el()?.Length > 0
                && ctx?.opt_target_list()?.target_list()?.target_el()[0] is PostgreSQLParser.Target_starContext;
        }

        public static IParseTree FindFirstTargetType(IParseTree context, Type target)
        {
            for (var n = 0; n < context.ChildCount; ++n)
            {
                var child = context.GetChild(n);
                if (target.IsAssignableFrom(child.GetType()))
                {
                    return child;
                }
                var result = FindFirstTargetType(child, target);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }

        public static void FindAllTargetTypes(IParseTree context, Type target, IList<IParseTree> results)
        {
            for (var n = 0; n < context.ChildCount; ++n)
            {
                var child = context.GetChild(n);
                if (target.IsAssignableFrom(child.GetType()))
                {
                    results.Add(child);
                }
                FindAllTargetTypes(child, target, results);
            }
        }

        public static bool HasAExprConst(Sortby_listContext ctx)
        {
            foreach (SortbyContext sortBy in ctx.sortby())
            {
                var result = FindFirstTargetType(sortBy, typeof(AexprconstContext));
                if (result != null)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool HasGroupByClause(Simple_select_pramaryContext ctx)
        {
            return ctx.group_clause() != null && !string.IsNullOrEmpty(ctx.group_clause().GetText());
        }

        public static bool HasDistinctClause(Simple_select_pramaryContext ctx)
        {
            return ctx.distinct_clause() != null && !string.IsNullOrEmpty(ctx.distinct_clause().GetText());
        }

        public static bool HasHavingClause(Simple_select_pramaryContext ctx)
        {
            return ctx.having_clause() != null && !string.IsNullOrEmpty(ctx.having_clause().GetText());
        }

        public static int CountTablesInFromClause(Simple_select_pramaryContext ctx)
        {
            if (ctx.from_clause()?.from_list()?._tables != null) 
            {
                return CountTables(ctx.from_clause().from_list()._tables, 0);
            }
            return 0;
        }

        public static int CountColumns(Simple_select_pramaryContext ctx)
        {
            if (ctx?.opt_target_list()?.target_list()?.target_el() != null) 
            {
                return ctx.opt_target_list().target_list().target_el().Length;
            }
            return 0;
        }

        public static int CountGroupingTerms(Simple_select_pramaryContext ctx)
        {
            if (ctx.group_clause()?.group_by_list()?._grouping_term != null)
            {
                return ctx.group_clause().group_by_list()._grouping_term.Count;
            }
            return 0;
        }

        public static int CountInsertColumns(InsertstmtContext ctx)
        {
            if (ctx?.insert_rest()?.insert_column_list()?._insert_columns != null)
            {
                return ctx.insert_rest().insert_column_list()._insert_columns.Count;
            }
            return 0;
        }

        public static Simple_select_pramaryContext FindSelectPramaryContext(IParseTree context) 
        {
            if (context != null)
            {
                if (context is Simple_select_pramaryContext ctx)
                {
                    return ctx;
                }
                for (var n = 0; n < context.ChildCount; ++n)
                {
                    var child = context.GetChild(n);
                    var result = FindSelectPramaryContext(child);
                    if (result is Simple_select_pramaryContext) 
                    {
                        return result;
                    }
                }
            }
            return null;
        }
    }
}
