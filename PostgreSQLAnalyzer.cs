using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;
using static NppDB.PostgreSQL.PostgreSQLAnalyzerHelper;
using static PostgreSQLParser;

namespace NppDB.PostgreSQL
{
    public static class PostgreSQLAnalyzer
    {
        public static int CollectCommands(
            this RuleContext context,
            CaretPosition caretPosition,
            string tokenSeparator,
            int commandSeparatorTokenType,
            out IList<ParsedTreeCommand> commands)
        {
            commands = new List<ParsedTreeCommand>();
            commands.Add(new ParsedTreeCommand
            {
                StartOffset = -1,
                StopOffset = -1,
                StartLine = -1,
                StopLine = -1,
                StartColumn = -1,
                StopColumn = -1,
                Text = "",
                Context = null
            });
            return _CollectCommands(context, caretPosition, tokenSeparator, commandSeparatorTokenType, commands, -1, null, new List<StringBuilder>());
        }

        private static void _AnalyzeRuleContext(RuleContext context, ParsedTreeCommand command)
        {
            switch (context.RuleIndex)
            {
                case PostgreSQLParser.RULE_simple_select_pramary:
                    {
                        if (context is PostgreSQLParser.Simple_select_pramaryContext ctx)
                        {
                            int tableCount = CountTablesInFromClause(ctx);
                            Target_elContext[] columns = GetColumns(ctx);
                            int columnCount = columns.Count();
                            int groupingTermCount = CountGroupingTerms(ctx);
                            bool hasGroupByClause = HasGroupByClause(ctx);
                            int whereCount = CountWheres(ctx, 0);
                            int whereClauseCount = CountWhereClauses(ctx);
                            bool hasWhereClause = HasText(ctx.where_clause());
                            if (HasMissingColumnAlias(columns))
                            {
                                command.AddWarning(ctx, ParserMessageType.MISSING_COLUMN_ALIAS_IN_SELECT_CLAUSE);
                            }
                            if (hasWhereClause && whereCount > whereClauseCount)
                            {
                                command.AddWarning(ctx.where_clause(), ParserMessageType.MULTIPLE_WHERE_USED);
                            }
                            if (HasDuplicateColumns(columns))
                            {
                                command.AddWarning(columns[0], ParserMessageType.DUPLICATE_SELECTED_COLUMN_IN_SELECT_CLAUSE);
                            }
                            if (!hasGroupByClause)
                            {
                                if (columnCount > 1 && HasAggregateFunction(ctx))
                                {
                                    command.AddWarning(ctx, ParserMessageType.AGGREGATE_FUNCTION_WITHOUT_GROUP_BY_CLAUSE);
                                }
                            }
                            else if (hasGroupByClause)
                            {
                                if (columnCount != 0 && columnCount - 1 != groupingTermCount && columnCount != groupingTermCount)
                                {
                                    command.AddWarning(ctx.group_clause(), ParserMessageType.MISSING_COLUMN_IN_GROUP_BY_CLAUSE);
                                }
                                if (IsColumnAliasWithAggregateFunctionUsedInGroupBy(ctx.group_clause(), columns) || HasAggregateFunction(ctx.group_clause()))
                                {
                                    command.AddWarning(ctx.group_clause(), ParserMessageType.AGGREGATE_FUNCTION_IN_GROUP_BY_CLAUSE);
                                }
                            }
                            if (HasOuterJoin(ctx) && HasSpecificAggregateFunctionAndArgument(ctx, "*", "count"))
                            {
                                command.AddWarning(ctx, ParserMessageType.COUNT_FUNCTION_WITH_OUTER_JOIN);
                            }
                            if (HasDistinctClause(ctx) && hasGroupByClause)
                            {
                                command.AddWarning(ctx.distinct_clause(), ParserMessageType.DISTINCT_KEYWORD_WITH_GROUP_BY_CLAUSE);
                            }
                            if (HasSelectStar(ctx))
                            {
                                if (tableCount > 1)
                                {
                                    command.AddWarning(ctx, ParserMessageType.SELECT_ALL_WITH_MULTIPLE_JOINS);
                                }
                            }

                            List<IParseTree> subQueries = new List<IParseTree>();
                            FindAllTargetTypes(ctx, typeof(PostgreSQLParser.Simple_select_pramaryContext), subQueries);
                            A_expr_inContext a_expr_inContext = (A_expr_inContext)FindFirstTargetType(ctx, typeof(A_expr_inContext));
                            bool whereClauseHasExistsKeyword = false;
                            if (hasWhereClause)
                            {
                                C_expr_existsContext value = (C_expr_existsContext)FindFirstTargetType(ctx.where_clause(), typeof(C_expr_existsContext));
                                if (HasText(value) && value.EXISTS() != null) 
                                {
                                    whereClauseHasExistsKeyword = true;
                                }
                            }
                            if (!whereClauseHasExistsKeyword) 
                            {
                                foreach (Simple_select_pramaryContext subQuery in subQueries)
                                {
                                    if (HasSubqueryColumnMismatch(a_expr_inContext, subQuery))
                                    {
                                        command.AddWarning(subQuery, ParserMessageType.SUBQUERY_COLUMN_COUNT_MISMATCH);
                                    }
                                    if (HasSelectStar(subQuery))
                                    {
                                        command.AddWarning(ctx, ParserMessageType.SELECT_ALL_IN_SUB_QUERY);
                                    }

                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_having_clause: 
                    {
                        if (context is PostgreSQLParser.Having_clauseContext ctx && HasText(ctx))
                        {
                            if (string.IsNullOrEmpty(ctx.a_expr().GetText()))
                            {
                                command.AddWarning(ctx, ParserMessageType.MISSING_EXPRESSION_IN_HAVING_CLAUSE);
                            }
                            if (!HasAggregateFunction(ctx) && !IsHavingClauseUsingGroupByTerm(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.HAVING_CLAUSE_WITHOUT_AGGREGATE_FUNCTION);
                            }
                            int havingCountInHavingClause = CountHavings(ctx, 0);
                            if (havingCountInHavingClause > 1 || (havingCountInHavingClause > 0 && // Sometimes second having part an go into parents' window_clause
                                ctx.Parent is Simple_select_pramaryContext parent && HasText(parent)
                                && HasText(parent.window_clause()) && CountHavings(parent.window_clause(), 0) > 0))
                            {
                                command.AddWarning(ctx, ParserMessageType.MULTIPLE_HAVING_USED);
                            }
                            //A_expr_compareContext compareCtx = (A_expr_compareContext)FindFirstTargetType(ctx, typeof(A_expr_compareContext));
                            //if (HasText(compareCtx))
                            //{
                            //    if (compareCtx._operands != null && compareCtx._operands.Count > 0)
                            //    {
                            //        command.AddWarning(ctx, ParserMessageType.HAVING_CLAUSE_WITHOUT_AGGREGATE_FUNCTION);
                            //    }
                            //}
                            if (HasAndOrExprWithoutParens(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AND_OR_MISSING_PARENTHESES_IN_HAVING_CLAUSE);
                            }
                            if (!IsLogical(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.NOT_LOGICAL_OPERAND);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_from_clause:
                    {
                        if (context is PostgreSQLParser.From_clauseContext ctx)
                        {
                            //if (HasJoinButNoJoinQual(ctx))
                            //{
                            //    command.AddWarning(ctx, ParserMessageType.MISSING_EXPRESSION_IN_JOIN_CLAUSE);
                            //}
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_where_clause: 
                    {
                        if (context is PostgreSQLParser.Where_clauseContext ctx && HasText(ctx))
                        {
                            if (string.IsNullOrEmpty(ctx.a_expr().GetText()))
                            {
                                command.AddWarning(ctx, ParserMessageType.MISSING_EXPRESSION_IN_WHERE_CLAUSE);
                            }
                            if (HasAggregateFunction(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AGGREGATE_FUNCTION_IN_WHERE_CLAUSE);
                            }
                            if (HasAndOrExprWithoutParens(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AND_OR_MISSING_PARENTHESES_IN_WHERE_CLAUSE);
                            }
                            if (!IsLogical(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.NOT_LOGICAL_OPERAND);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_select_clause: 
                    {
                        if (context is PostgreSQLParser.Select_clauseContext ctx)
                        {
                            if ((ctx.union != null || ctx.except != null) && ctx.first_intersect != null && ctx.second_intersect != null) 
                            {
                                if (IsSelectPramaryContextSelectStar(ctx.first_intersect?.simple_select_pramary()) 
                                    || IsSelectPramaryContextSelectStar(ctx.second_intersect?.simple_select_pramary()))
                                {
                                    command.AddWarning(ctx, ParserMessageType.SELECT_ALL_IN_UNION_STATEMENT);
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_simple_select_intersect: 
                    {
                        if (context is PostgreSQLParser.Simple_select_intersectContext ctx)
                        {
                            if (ctx.intersect != null && ctx.first_pramary != null && ctx.second_pramary != null) 
                            {
                                if (IsSelectPramaryContextSelectStar(ctx.first_pramary) 
                                    || IsSelectPramaryContextSelectStar(ctx.second_pramary))
                                {
                                    command.AddWarning(ctx, ParserMessageType.SELECT_ALL_IN_UNION_STATEMENT);
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_select_no_parens: 
                    {
                        if (context is PostgreSQLParser.Select_no_parensContext ctx && HasText(ctx))
                        {
                            List<IParseTree> subQueries = new List<IParseTree>();
                            FindAllTargetTypes(ctx, typeof(PostgreSQLParser.Select_no_parensContext), subQueries);
                            foreach (Select_no_parensContext subQuery in subQueries)
                            {
                                if (HasText(subQuery.opt_sort_clause()) && !(HasText(subQuery.opt_select_limit()) || HasText(subQuery.select_limit())))
                                {
                                    command.AddWarning(subQuery.opt_sort_clause(), ParserMessageType.ORDER_BY_CLAUSE_IN_SUB_QUERY_WITHOUT_LIMIT);
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_sortby_list: 
                    {
                        if (context is PostgreSQLParser.Sortby_listContext ctx)
                        {
                            List<IParseTree> aexprconsts = new List<IParseTree>();
                            FindAllTargetTypes(ctx, typeof(PostgreSQLParser.AexprconstContext), aexprconsts);
                            foreach (IParseTree item in aexprconsts)
                            {
                                if (item is AexprconstContext aexprconst) 
                                {
                                    if (aexprconst != null && !string.IsNullOrEmpty(aexprconst.GetText()))
                                    {
                                        command.AddWarning(ctx, ParserMessageType.ORDERING_BY_ORDINAL);
                                        break;
                                    }
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_insertstmt: 
                    {
                        if (context is PostgreSQLParser.InsertstmtContext ctx)
                        {
                            int insertColumnCount = CountInsertColumns(ctx);
                            if (insertColumnCount == 0 && ctx?.insert_rest()?.OVERRIDING() == null && ctx?.insert_rest()?.DEFAULT() == null && ctx?.insert_rest()?.VALUES() == null)
                            {
                                command.AddWarning(ctx, ParserMessageType.INSERT_STATEMENT_WITHOUT_COLUMN_NAMES);
                            }
                            Simple_select_pramaryContext select_PramaryContext = FindSelectPramaryContext(ctx);
                            if (select_PramaryContext != null && HasSelectStar(select_PramaryContext))
                            {
                                command.AddWarning(ctx, ParserMessageType.SELECT_ALL_IN_INSERT_STATEMENT);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_func_application: 
                    {
                        if (context is PostgreSQLParser.Func_applicationContext ctx)
                        {
                            if (ctx.func_name().GetText().ToLower() == "sum" && ctx.func_arg_list().GetText() == "1")
                            {
                                command.AddWarning(ctx, ParserMessageType.USE_COUNT_FUNCTION);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_a_expr_like: 
                    {
                        if (context is PostgreSQLParser.A_expr_likeContext ctx && HasText(ctx))
                        {
                            if (HasText(ctx.rhs))
                            {
                                C_expr_exprContext value = (C_expr_exprContext)FindFirstTargetType(ctx.rhs, typeof(C_expr_exprContext));
                                if (ctx._ILIKE == null && value != null && value.ChildCount > 0 && value.GetChild(0) is AexprconstContext)
                                {
                                    if (!ctx.rhs.GetText().Contains("%") && !ctx.rhs.GetText().Contains("_")) 
                                    {
                                        if ((ctx._SIMILAR != null
                                            && !ctx.rhs.GetText().Contains("|")
                                            && !ctx.rhs.GetText().Contains("*")
                                            && !ctx.rhs.GetText().Contains("+")
                                            && !ctx.rhs.GetText().Contains("?")
                                            && !ctx.rhs.GetText().Contains("{")
                                            && !ctx.rhs.GetText().Contains("}")
                                            && !ctx.rhs.GetText().Contains("(")
                                            && !ctx.rhs.GetText().Contains(")")
                                            && !ctx.rhs.GetText().Contains("[")
                                            && !ctx.rhs.GetText().Contains("]")
                                            && !ctx.rhs.GetText().Contains("*")) || ctx._SIMILAR == null)
                                        {
                                            command.AddWarning(ctx, ParserMessageType.MISSING_WILDCARDS_IN_LIKE_EXPRESSION);
                                        }
                                    }
                                }
                                if (value.ChildCount > 0 && value.GetChild(0) is ColumnrefContext)
                                {
                                    command.AddWarning(ctx, ParserMessageType.COLUMN_LIKE_COLUMN);
                                }
                                
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_a_expr_between: 
                    {
                        if (context is PostgreSQLParser.A_expr_betweenContext ctx && HasText(ctx))
                        {
                            if ((HasText(ctx.rhs) && ctx.rhs.GetText().ToLower().Equals("null")) ||
                                    (HasText(ctx.between_r_h_s) && ctx.between_r_h_s.GetText().ToLower().Equals("null")))
                            {
                                command.AddWarning(ctx, ParserMessageType.EQUALITY_WITH_NULL);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_a_expr_compare: 
                    {
                        if (context is PostgreSQLParser.A_expr_compareContext ctx && HasText(ctx))
                        {
                            ParserRuleContext rhs = FindCompareRhs(ctx);
                            if (HasEqualityWithTextPattern(rhs) || HasEqualityWithTextPattern(ctx.lhs))
                            {
                                command.AddWarning(rhs, ParserMessageType.EQUALITY_WITH_TEXT_PATTERN);
                            }
                            if ((HasText(rhs) && rhs.GetText().ToLower().Equals("null")) ||
                                    (HasText(ctx.lhs) && ctx.lhs.GetText().ToLower().Equals("null")))
                            {
                                if (ctx.LT() != null || ctx.GT() != null || ctx.LESS_EQUALS() != null || ctx.GREATER_EQUALS() != null)
                                {
                                    command.AddWarning(ctx, ParserMessageType.COMPARING_WITH_NULL);
                                }
                                if (ctx.EQUAL() != null)
                                {
                                    command.AddWarning(ctx, ParserMessageType.EQUALITY_WITH_NULL);
                                }
                                if (ctx.NOT_EQUALS() != null)
                                {
                                    command.AddWarning(ctx, ParserMessageType.NOT_EQUALITY_WITH_NULL);
                                }
                            }

                            if (HasText(ctx.subquery_Op())) 
                            {
                                MathopContext mathOperand = (MathopContext)FindFirstTargetType(ctx.subquery_Op(), typeof(MathopContext));
                                Sub_typeContext subType = ctx.sub_type();
                                if (HasText(subType) && HasText(mathOperand))
                                {
                                    if (mathOperand.EQUAL() != null && subType.ALL() != null)
                                    {
                                        command.AddWarning(ctx, ParserMessageType.EQUALS_ALL);
                                    }
                                    if (mathOperand.NOT_EQUALS() != null && (subType.ANY() != null || subType.SOME() != null))
                                    {
                                        command.AddWarning(ctx, ParserMessageType.NOT_EQUALS_ANY);
                                    }
                                }
                            }
                            
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_a_expr_mul: 
                    {
                        if (context is PostgreSQLParser.A_expr_mulContext ctx && HasText(ctx))
                        {
                            if (HasText(ctx.lhs) && ctx._rhs != null && ctx._rhs.Count > 0 && HasText(ctx._rhs[0]) && ctx.SLASH() != null &&
                                ctx.lhs.GetText().ToLower().Contains("sum") &&
                                ctx._rhs[0].GetText().ToLower().Contains("count"))
                            {
                                command.AddWarning(ctx, ParserMessageType.USE_AVG_FUNCTION);
                            }
                            
                        }
                        break;
                    }
            }
        }

        private static ParserRuleContext FindCompareRhs(A_expr_compareContext ctx)
        {
            if (HasText(ctx.rhs1)) 
            {
                return ctx.rhs1;
            }
            if (HasText(ctx.rhs2)) 
            {
                return ctx.rhs2;
            }
            if (HasText(ctx.rhs3)) 
            {
                return ctx.rhs3;
            }
            return null;
        }

        private static int _CollectCommands(
            RuleContext context,
            CaretPosition caretPosition,
            string tokenSeparator,
            int commandSeparatorTokenType,
            in IList<ParsedTreeCommand> commands,
            int enclosingCommandIndex,
            in IList<StringBuilder> functionParams,
            in IList<StringBuilder> transactionStatementsEncountered)
        {
            if (context is PostgreSQLParser.RootContext && commands.Last().Context == null)
            {
                commands.Last().Context = context; // base starting branch
            }
            _AnalyzeRuleContext(context, commands.Last());
            for (var i = 0; i < context.ChildCount; ++i)
            {
                var child = context.GetChild(i);
                if (child is PostgreSQLParser.IdentifierContext identifier)
                {
                    _AnalyzeForDoubleQuotes(identifier, commands.Last());
                }
                if (child is ITerminalNode terminalNode)
                {
                    var token = terminalNode.Symbol;
                    var tokenLength = token.StopIndex - token.StartIndex + 1;
                    if (transactionStatementsEncountered?.Count % 2 == 0 && (token.Type == TokenConstants.EOF || token.Type == commandSeparatorTokenType))
                    {

                        if (enclosingCommandIndex == -1 && (
                            caretPosition.Line > commands.Last().StartLine && caretPosition.Line < token.Line ||
                            caretPosition.Line == commands.Last().StartLine && caretPosition.Column >= commands.Last().StartColumn && (caretPosition.Line < token.Line || caretPosition.Column <= token.Column) ||
                            caretPosition.Line == token.Line && caretPosition.Column <= token.Column))
                        {
                            enclosingCommandIndex = commands.Count - 1;
                        }

                        if (token.Type == TokenConstants.EOF)
                        {
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(commands.Last().Text))
                        {
                            commands.Last().AddWarning(token, ParserMessageType.UNNECESSARY_SEMICOLON);
                        }
                        commands.Add(new ParsedTreeCommand());
                    }
                    else
                    {
                        if (commands.Last().StartOffset == -1)
                        {
                            commands.Last().StartLine = token.Line;
                            commands.Last().StartColumn = token.Column;
                            commands.Last().StartOffset = token.StartIndex;
                        }
                        commands.Last().StopLine = token.Line;
                        commands.Last().StopColumn = token.Column + tokenLength;
                        commands.Last().StopOffset = token.StopIndex;

                        if (functionParams is null)
                        {
                            commands.Last().Text = commands.Last().Text + child.GetText() + tokenSeparator;
                        }
                        else if (context.RuleIndex != PostgreSQLParser.RULE_function_or_procedure ||
                            token.Type != PostgreSQLParser.OPEN_PAREN &&
                            child.GetText() != "(" &&
                            token.Type != PostgreSQLParser.CLOSE_PAREN &&
                            child.GetText() != ")" &&
                            token.Type != PostgreSQLParser.COMMA &&
                            child.GetText() != ",")
                        {
                            functionParams.Last().Append(child.GetText() + tokenSeparator);
                        }
                    }
                }
                else
                {
                    var ctx = child as RuleContext;


                    if (ctx?.RuleIndex == PostgreSQLParser.RULE_transactionstmt)
                    {
                        var statements = new string[] { "BEGIN", "END" };
                        if (statements.Any(s => ctx?.GetText().IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            transactionStatementsEncountered?.Add(new StringBuilder(ctx?.GetText()));
                        }
                    };


                    if (ctx?.RuleIndex == PostgreSQLParser.RULE_empty_grouping_set) continue;

                    enclosingCommandIndex = _CollectCommands(ctx, caretPosition, tokenSeparator, commandSeparatorTokenType, commands, enclosingCommandIndex, functionParams, transactionStatementsEncountered);
                }
            }
            return enclosingCommandIndex;
        }
    }
}
