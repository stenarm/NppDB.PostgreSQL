using NppDB.Comm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Antlr4.Runtime;
using System.IO;
using System.Data;
using Npgsql;
using System.Windows.Forms;
using NpgsqlTypes;
using Npgsql.PostgresTypes;
using static Npgsql.Replication.PgOutput.Messages.RelationMessage;
using static System.Net.Mime.MediaTypeNames;
using System.Text.RegularExpressions;
using System.Globalization;
using TimeZoneConverter;

namespace NppDB.PostgreSQL
{
    public class PostgreSQLLexerErrorListener : ConsoleErrorListener<int>
    {
        public new static readonly PostgreSQLLexerErrorListener Instance = new PostgreSQLLexerErrorListener();

        public override void SyntaxError(TextWriter output, IRecognizer recognizer,
            int offendingSymbol, int line, int col, string msg, RecognitionException e)
        {
            Console.WriteLine($"LEXER ERROR: {e?.GetType().ToString() ?? ""}: {msg} ({line}:{col})");
        }
    }

    public class PostgreSQLParserErrorListener : BaseErrorListener
    {
        private readonly IList<ParserError> _errors = new List<ParserError>();
        public IList<ParserError> Errors => _errors;

        public override void SyntaxError(TextWriter output, IRecognizer recognizer,
            IToken offendingSymbol, int line, int col, string msg, RecognitionException e)
        {
            Console.WriteLine($"PARSER ERROR: {e?.GetType().ToString() ?? ""}: {msg} ({line}:{col})");
            _errors.Add(new ParserError
            {
                Text = msg,
                StartLine = line,
                StartColumn = col,
                StartOffset = offendingSymbol.StartIndex,
                // StopLine = ,
                // StopColumn = ,
                StopOffset = offendingSymbol.StopIndex,
            });
        }
    }

    public class PostgreSQLExecutor : ISQLExecutor
    {
        private Thread _execTh;
        private readonly Func<NpgsqlConnection> _connector;
        private NpgsqlConnection _connection;

        public PostgreSQLExecutor(Func<NpgsqlConnection> connector)
        {
            _connector = connector;
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
            String query = "SHOW LC_MONETARY;";
            try
            {
                using (NpgsqlCommand command = new NpgsqlCommand(query, _connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string lc_monetary = reader["lc_monetary"].ToString();
                            if (lc_monetary != null) 
                            {
                                Match lc_monetaryTargetMatch = Regex.Match(lc_monetary, @".._..", RegexOptions.IgnoreCase);
                                if (lc_monetaryTargetMatch.Success && lc_monetaryTargetMatch.Groups.Count > 0)
                                {
                                    return lc_monetaryTargetMatch.Groups[0].ToString();
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
            String query = "show timezone;";
            try
            {
                using (NpgsqlCommand command = new NpgsqlCommand(query, _connection))
                {
                    using (NpgsqlDataReader reader = command.ExecuteReader())
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

        public bool IsAborted(string message) 
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
                    string monetaryLocaleOrAbortedMessage = GetMonetaryLocale();
                    string timezoneName = GetTimeZone();
                    bool isAborted = IsAborted(monetaryLocaleOrAbortedMessage);
                    var results = new List<CommandResult>();
                    string lastSql = null;
                    try
                    {
                        foreach (var sql in sqlQueries)
                        {
                            if (string.IsNullOrWhiteSpace(sql)) continue;
                            lastSql = sql;

                            Console.WriteLine($"SQL: <{sql}>");
                            NpgsqlCommand cmd = new NpgsqlCommand(sql, _connection);
                            using (NpgsqlDataReader rd = cmd.ExecuteReader())
                            {
                                DataTable dt = new DataTable();
                                for (int i = 0; i < rd.FieldCount; i++)
                                {
                                    Type type = rd.GetFieldType(i);
                                    // Handle DBNull type
                                    if (type == typeof(System.DBNull) ||
                                        (!string.IsNullOrEmpty(monetaryLocaleOrAbortedMessage) && !isAborted && rd.GetDataTypeName(i) == "money") 
                                        || type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset)
                                        )
                                    {
                                        type = typeof(string);
                                    }
                                    int existingColumnCount = 0;
                                    foreach (DataColumn col in dt.Columns)
                                    {
                                        if (col.ColumnName.Trim().StartsWith(rd.GetName(i)))
                                        {
                                            existingColumnCount++;
                                        }
                                    }
                                    DataColumn dataColumn = new DataColumn(rd.GetName(i) + (existingColumnCount == -0 ? "" : "(" + existingColumnCount + ")"), type);
                                    dataColumn.Caption = rd.GetName(i);
                                    dt.Columns.Add(dataColumn);
                                }
                                while (rd.Read())
                                {
                                    DataRow row = dt.NewRow();

                                    for (int j = 0; j < rd.FieldCount; j++)
                                    {
                                        // Handle DBNull value
                                        if (rd.IsDBNull(j))
                                        {
                                            row[j] = DBNull.Value;
                                        }
                                        else
                                        {
                                            object rdi = rd[j];
                                            Type type = rd.GetFieldType(j);
                                            if (!string.IsNullOrEmpty(monetaryLocaleOrAbortedMessage) && !isAborted && rd.GetDataTypeName(j) == "money")
                                            {

                                                rdi = string.Format(new System.Globalization.CultureInfo(monetaryLocaleOrAbortedMessage, false), "{0:c0}", rdi);
                                            }
                                            if (type == typeof(DateTime) || type == typeof(TimeSpan) || type == typeof(DateTimeOffset))
                                            {
                                                if (rd.GetDataTypeName(j) != null) 
                                                {
                                                   
                                                    if (rd.GetDataTypeName(j).IndexOf("with time zone", StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        string timeZoneFinalString = "+00";
                                                        if (!string.IsNullOrEmpty(timezoneName)) 
                                                        {
                                                            TimeZoneInfo timeZoneInfo = TimeZoneConverter.TZConvert.GetTimeZoneInfo(timezoneName);
                                                            int hours = timeZoneInfo.BaseUtcOffset.Hours;
                                                            string timezoneStringStart = (hours < 0 ? "-" : "+");
                                                            string timezoneStringEnd = (Math.Abs(hours) >= 10 ? $"{Math.Abs(hours)}" : $"0{Math.Abs(hours)}");
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
                                                        if (rd.GetDataTypeName(j).StartsWith("timestamp"))
                                                        {
                                                            rdi = rd.GetDateTime(j).ToString("yyyy-MM-dd HH:mm:ss.FFFFFF");
                                                        }
                                                        else
                                                        {
                                                            rdi = rd.GetDateTime(j).ToString("HH:mm:ss.FFFFFF");
                                                        }
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
                                string commandMessage = (sql.Trim().ToLower() == "commit" && isAborted) ? "ROLLBACK" : null;
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
            lexer.AddErrorListener(PostgreSQLLexerErrorListener.Instance);

            CommonTokenStream tokens;
            try
            {
                tokens = new CommonTokenStream(lexer);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Lexer Exception: {e}");
                throw e;
            }

            var parserErrorListener = new PostgreSQLParserErrorListener();
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
                Console.WriteLine($"Parser Exception: {e}");
                throw e;
            }
        }

        public bool CanExecute()
        {
            return !CanStop();
        }

        public bool CanStop()
        {
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
