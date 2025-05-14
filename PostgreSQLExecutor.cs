using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Antlr4.Runtime;
using Npgsql;
using NppDB.Comm;
using TimeZoneConverter;

namespace NppDB.PostgreSQL
{
    public class PostgreSqlLexerErrorListener : ConsoleErrorListener<int>
    {
        public new static readonly PostgreSqlLexerErrorListener Instance = new PostgreSqlLexerErrorListener();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer,
            int offendingSymbol, int line, int col, string msg, RecognitionException e)
        {
            Console.WriteLine($@"LEXER ERROR: {e?.GetType().ToString() ?? ""}: {msg} ({line}:{col})");
        }
    }

    public class PostgreSqlParserErrorListener : BaseErrorListener
    {
        public IList<ParserError> Errors { get; } = new List<ParserError>();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer,
            IToken offendingSymbol, int line, int col, string msg, RecognitionException e)
        {
            Console.WriteLine($@"PARSER ERROR: {e?.GetType().ToString() ?? ""}: {msg} ({line}:{col})");
            Errors.Add(new ParserError
            {
                Text = msg,
                StartLine = line,
                StartColumn = col,
                StartOffset = offendingSymbol.StartIndex,
                StopOffset = offendingSymbol.StopIndex,
            });
        }
    }

    public class PostgreSqlExecutor : ISqlExecutor
    {
        private Thread _execTh;
        private readonly NpgsqlConnection _connection;

        public PostgreSqlExecutor(Func<NpgsqlConnection> connector)
        {
            if (connector == null) 
            {
                _connection = null;
            }
            else 
            {
                _connection = connector();
                _connection.Open();
            }
        }

        private string GetMonetaryLocale()
        {
            const string query = "SHOW LC_MONETARY;";
            try
            {
                using (var command = new NpgsqlCommand(query, _connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var lcMonetary = reader["lc_monetary"].ToString();
                            {
                                var lcMonetaryTargetMatch = Regex.Match(lcMonetary, @".._..", RegexOptions.IgnoreCase);
                                if (lcMonetaryTargetMatch.Success && lcMonetaryTargetMatch.Groups.Count > 0)
                                {
                                    return lcMonetaryTargetMatch.Groups[0].ToString();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return $"{e.Message}";
            }
            return null;
        }
        private string GetTimeZone()
        {
            const string query = "show timezone;";
            try
            {
                using (var command = new NpgsqlCommand(query, _connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            return reader["timezone"].ToString();
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }

        private static bool IsAborted(string message) 
        {
            if (!string.IsNullOrEmpty(message))
            {
                return message.IndexOf("current transaction is aborted", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            return false;
        }

        public void Execute(IList<string> sqlQueries, Action<IList<CommandResult>> callback)
        {

            _execTh = new Thread(new ThreadStart(
                delegate
                {
                    if (_connection.State == ConnectionState.Closed) 
                    {
                        _connection.Open();
                    }
                    var monetaryLocaleOrAbortedMessage = GetMonetaryLocale();
                    var timezoneName = GetTimeZone();
                    var isAborted = IsAborted(monetaryLocaleOrAbortedMessage);
                    var results = new List<CommandResult>();
                    string lastSql = null;
                    try
                    {
                        foreach (var sql in sqlQueries)
                        {
                            if (string.IsNullOrWhiteSpace(sql)) continue;
                            lastSql = sql;

                            Console.WriteLine($@"SQL: <{sql}>");
                            var cmd = new NpgsqlCommand(sql, _connection);
                            using (var rd = cmd.ExecuteReader())
                            {
                                var dt = new DataTable();
                                for (var i = 0; i < rd.FieldCount; i++)
                                {
                                    var type = rd.GetFieldType(i);
                                    if (type == typeof(DBNull) ||
                                        (!string.IsNullOrEmpty(monetaryLocaleOrAbortedMessage) && !isAborted && rd.GetDataTypeName(i) == "money") 
                                        || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset)
                                       )
                                    {
                                        type = typeof(string);
                                    }
                                    var existingColumnCount = dt.Columns.Cast<DataColumn>().Count(col => col.ColumnName.Trim().StartsWith(rd.GetName(i)));
                                    if (type == null) continue;
                                    var dataColumn = new DataColumn(rd.GetName(i) + (existingColumnCount == -0 ? "" : "(" + existingColumnCount + ")"), type);
                                    dataColumn.Caption = rd.GetName(i);
                                    dt.Columns.Add(dataColumn);
                                }
                                while (rd.Read())
                                {
                                    var row = dt.NewRow();

                                    for (var j = 0; j < rd.FieldCount; j++)
                                    {
                                        if (rd.IsDBNull(j))
                                        {
                                            row[j] = DBNull.Value;
                                        }
                                        else
                                        {
                                            var rdi = rd[j];
                                            var type = rd.GetFieldType(j);
                                            if (!string.IsNullOrEmpty(monetaryLocaleOrAbortedMessage) && !isAborted && rd.GetDataTypeName(j) == "money")
                                            {

                                                rdi = string.Format(new CultureInfo(monetaryLocaleOrAbortedMessage, false), "{0:c0}", rdi);
                                            }
                                            if (type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset))
                                            {
                                                if (rd.GetDataTypeName(j) != null) 
                                                {
                                                   
                                                    if (rd.GetDataTypeName(j).IndexOf("with time zone", StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        var timeZoneFinalString = "+00";
                                                        if (!string.IsNullOrEmpty(timezoneName)) 
                                                        {
                                                            var timeZoneInfo = TZConvert.GetTimeZoneInfo(timezoneName);
                                                            var hours = timeZoneInfo.BaseUtcOffset.Hours;
                                                            var timezoneStringStart = (hours < 0 ? "-" : "+");
                                                            var timezoneStringEnd = (Math.Abs(hours) >= 10 ? $"{Math.Abs(hours)}" : $"0{Math.Abs(hours)}");
                                                            timeZoneFinalString = timezoneStringStart + timezoneStringEnd;
                                                        }
                                                        if (rd.GetDataTypeName(j).StartsWith("timestamp"))
                                                        {
                                                            rdi = rd.GetDateTime(j).ToString("yyyy-MM-dd HH:mm:ss.FFFFFF") + timeZoneFinalString;
                                                        }
                                                        else
                                                        {
                                                            rdi = rd.GetFieldValue<DateTimeOffset>(j).ToString("HH:mm:ss.FFFFFF") + timeZoneFinalString;
                                                        }
                                                    }
                                                    else if (rd.GetDataTypeName(j).IndexOf("without time zone", StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        rdi = rd.GetDateTime(j).ToString(rd.GetDataTypeName(j).StartsWith("timestamp") ? "yyyy-MM-dd HH:mm:ss.FFFFFF" : "HH:mm:ss.FFFFFF");
                                                    }
                                                    else
                                                    {
                                                        rdi = rd.GetDateTime(j).ToString("yyyy-MM-dd");
                                                    }
                                                } else 
                                                {
                                                    rdi = "";
                                                }
                                            }
                                            row[j] = rdi;
                                        }
                                    }
                                    dt.Rows.Add(row);
                                }
                                var commandMessage = (sql.Trim().ToLower() == "commit" && isAborted) ? "ROLLBACK" : null;
                                results.Add(new CommandResult { CommandText = sql, QueryResult = dt, RecordsAffected = rd.RecordsAffected, CommandMessage = commandMessage });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        results.Add(new CommandResult { CommandText = lastSql, Error = ex });
                        callback(results);
                        return;
                    }
                    callback(results);
                    _execTh = null;
                }))
            {
                IsBackground = true
            };
            _execTh.Start();
        }

        public ParserResult Parse(string sqlText, CaretPosition caretPosition)
        {
            var input = CharStreams.fromString(sqlText);

            var lexer = new PostgreSQLLexer(input);
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(PostgreSqlLexerErrorListener.Instance);

            CommonTokenStream tokens;
            try
            {
                tokens = new CommonTokenStream(lexer);
            }
            catch (Exception e)
            {
                Console.WriteLine($@"Lexer Exception: {e}");
                throw;
            }

            var parserErrorListener = new PostgreSqlParserErrorListener();
            var parser = new PostgreSQLParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErrorListener);
            try
            {
                var tree = parser.root();
                var enclosingCommandIndex = tree.CollectCommands(caretPosition, " ", PostgreSQLParser.SEMI, out var commands);
                return new ParserResult
                {
                    Errors = parserErrorListener.Errors,
                    Commands = commands.ToList<ParsedCommand>(),
                    EnclosingCommandIndex = enclosingCommandIndex
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($@"Parser Exception: {e}");
                throw;
            }
        }

        public SqlDialect Dialect => SqlDialect.POSTGRE_SQL;

        public bool CanExecute()
        {
            return !CanStop();
        }

        public bool CanStop()
        {
            // ReSharper disable once NonConstantEqualityExpressionHasConstantResult
            return _execTh != null && (_execTh.ThreadState & ThreadState.Running) != 0;
        }

        public void Stop()
        {
            if (_connection != null )
            {
                _connection.Close();
                _connection.Dispose();
            }
            _execTh?.Abort();
            _execTh = null;
        }
    }
}
