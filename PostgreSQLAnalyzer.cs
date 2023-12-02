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
                            if (HasMissingColumnAlias(columns))
                            {
                                command.AddWarning(ctx, ParserMessageType.MISSING_COLUMN_ALIAS_IN_SELECT_CLAUSE);
                            }
                            if (HasWhereClause(ctx.where_clause()) && whereCount > whereClauseCount)
                            {
                                command.AddWarning(ctx.where_clause(), ParserMessageType.MULTIPLE_WHERE_USED);
                            }
                            if (HasDuplicateColumns(columns))
                            {
                                command.AddWarning(columns[0], ParserMessageType.DUPLICATE_SELECTED_COLUMN_IN_SELECT_CLAUSE);
                            }
                            if (!hasGroupByClause)
                            {
                                if (tableCount > 1 && HasAggregateFunction(ctx))
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
                                if (HasAggregateFunction(ctx.group_clause()))
                                {
                                    command.AddWarning(ctx.group_clause(), ParserMessageType.AGGREGATE_FUNCTION_IN_GROUP_BY_CLAUSE);
                                }
                            }
                            if (HasOuterJoin(ctx) && HasSpecificAggregateFunction(ctx, "count"))
                            {
                                command.AddWarning(ctx, ParserMessageType.COUNT_FUNCTION_WITH_OUTER_JOIN);
                            }
                            if (HasDistinctClause(ctx) && hasGroupByClause)
                            {
                                command.AddWarning(ctx, ParserMessageType.DISTINCT_KEYWORD_WITH_GROUP_BY_CLAUSE);
                            }
                            if (HasSelectStar(ctx))
                            {
                                if (tableCount > 1)
                                {
                                    command.AddWarning(ctx, ParserMessageType.SELECT_ALL_WITH_MULTIPLE_JOINS);
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_having_clause: 
                    {
                        if (context is PostgreSQLParser.Having_clauseContext ctx && HasHavingClause(ctx))
                        {
                            if (!HasAggregateFunction(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.HAVING_CLAUSE_WITHOUT_AGGREGATE_FUNCTION);
                            }
                            if (HasAndOrExprWithoutParens(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AND_OR_MISSING_PARENTHESES_IN_WHERE_CLAUSE);
                            }
                            if (!IsLogicalExpression(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.NOT_LOGICAL_OPERAND);
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_where_clause: 
                    {
                        if (context is PostgreSQLParser.Where_clauseContext ctx && HasWhereClause(ctx))
                        {
                            if (HasAggregateFunction(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AGGREGATE_FUNCTION_IN_WHERE_CLAUSE);
                            }
                            if (HasAndOrExprWithoutParens(ctx))
                            {
                                command.AddWarning(ctx, ParserMessageType.AND_OR_MISSING_PARENTHESES_IN_WHERE_CLAUSE);
                            }
                            if (!IsLogicalExpression(ctx))
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
                            if (ctx.union != null && ctx.first_intersect != null && ctx.second_intersect != null) 
                            {
                                if (IsSelectPramaryContextSelectStar(ctx.first_intersect?.simple_select_pramary()) || IsSelectPramaryContextSelectStar(ctx.second_intersect?.simple_select_pramary()))
                                {
                                    command.AddWarning(ctx, ParserMessageType.SELECT_ALL_IN_UNION_STATEMENT);
                                }
                            }
                        }
                        break;
                    }
                case PostgreSQLParser.RULE_sortby: 
                    {
                        if (context is PostgreSQLParser.SortbyContext ctx)
                        {
                            AexprconstContext aexprconst = (AexprconstContext) FindFirstTargetType(ctx, typeof(AexprconstContext));
                            if (aexprconst != null && !string.IsNullOrEmpty(aexprconst.GetText()))
                            {
                                command.AddWarning(ctx, ParserMessageType.ORDERING_BY_ORDINAL);
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
            }
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
                    _AnalyzeIdentifier(identifier, commands.Last());
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
                        else if (context.RuleIndex != PostgreSQLParser.RULE_function_or_procedure || token.Type != PostgreSQLParser.OPEN_PAREN && token.Type != PostgreSQLParser.CLOSE_PAREN && token.Type != PostgreSQLParser.COMMA)
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

                    if (ctx?.RuleIndex == PostgreSQLParser.RULE_indirection_el)
                    {
                        enclosingCommandIndex = _CollectCommands(ctx, caretPosition, "", commandSeparatorTokenType, commands, enclosingCommandIndex, functionParams, transactionStatementsEncountered);
                        if (functionParams is null)
                            commands.Last().Text += tokenSeparator;
                        else
                            functionParams.Last().Append(tokenSeparator);
                    }
                    else if (ctx?.RuleIndex == PostgreSQLParser.RULE_ruleactionlist)
                    {
                        var p = new List<StringBuilder> { new StringBuilder() };
                        enclosingCommandIndex = _CollectCommands(ctx, caretPosition, tokenSeparator, commandSeparatorTokenType, commands, enclosingCommandIndex, p, transactionStatementsEncountered);
                        var functionName = p[0].ToString().ToLower();
                        p.RemoveAt(0);
                        var functionCallString = "";
                        if (functionName == "nz" && (p.Count == 2 || p.Count == 3))
                        {
                            if (p[1].Length == 0) p[1].Append("''");
                            functionCallString = $"IIf(IsNull({p[0]}), {p[1]}, {p[0]})";
                        }
                        else
                        {
                            functionCallString = $"{functionName}({string.Join(", ", p).TrimEnd(',', ' ')})";
                        }

                        if (functionParams is null)
                        {
                            commands.Last().Text = commands.Last().Text + functionCallString + tokenSeparator;
                        }
                        else
                        {
                            functionParams.Last().Append(functionCallString + tokenSeparator);
                        }
                    }
                    else
                    {
                        enclosingCommandIndex = _CollectCommands(ctx, caretPosition, tokenSeparator, commandSeparatorTokenType, commands, enclosingCommandIndex, functionParams, transactionStatementsEncountered);
                        if (!(functionParams is null) && (ctx?.RuleIndex == PostgreSQLParser.RULE_colid || ctx?.RuleIndex == PostgreSQLParser.RULE_stmtblock))
                        {
                            functionParams.Last().Length -= tokenSeparator.Length;
                            functionParams.Add(new StringBuilder());
                        }
                    }
                }
            }
            return enclosingCommandIndex;
        }
    }
}
