using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace ExpenceTracker.Services
{
    public static class CsvService
    {
        public static async Task ExportExpensesAsync(string path, IEnumerable<Expense> expenses, CultureInfo? ci = null)
        {
            ci ??= CultureInfo.GetCultureInfo("lt-LT");
            const string delimiter = ";";

            using var sw = new StreamWriter(path, false, new UTF8Encoding(false));
            await sw.WriteLineAsync(string.Join(delimiter, new[] { "Date", "Amount", "Category", "Description", "BudgetBucket" })).ConfigureAwait(false);

            foreach (var e in expenses)
            {
                string date = e.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                string amount = e.Amount.ToString("N2", ci);
                string category = EscapeField(e.Category, delimiter);
                string description = EscapeField(e.Description, delimiter);
                string bucket = EscapeField(e.BudgetBucket, delimiter);

                string line = string.Join(delimiter, new[] { QuoteIfNeeded(date), QuoteIfNeeded(amount), category, description, bucket });
                await sw.WriteLineAsync(line).ConfigureAwait(false);
            }
        }

        public static async Task<List<Expense>> ImportExpensesAsync(string path, CultureInfo? ci = null)
        {
            ci ??= CultureInfo.GetCultureInfo("lt-LT");
            const char delimiter = ';';
            var result = new List<Expense>();

            using var sr = new StreamReader(path, Encoding.UTF8);
            string? headerLine = await sr.ReadLineAsync().ConfigureAwait(false);
            string[]? headers = null;
            if (headerLine is not null)
            {
                headers = ParseCsvLine(headerLine, delimiter).ToArray();
            }

            int idxDate = 0, idxAmount = 1, idxCategory = 2, idxDescription = 3, idxBucket = 4;
            if (headers != null && headers.Length >= 3)
            {
                for (int i = 0; i < headers.Length; i++)
                {
                    var h = headers[i].Trim().ToLowerInvariant();
                    if (h == "date") idxDate = i;
                    else if (h == "amount") idxAmount = i;
                    else if (h == "category") idxCategory = i;
                    else if (h == "description") idxDescription = i;
                    else if (h == "budgetbucket") idxBucket = i;
                }
            }
            else
            {
                sr.BaseStream.Seek(0, SeekOrigin.Begin);
                sr.DiscardBufferedData();
            }

            string? line;
            while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = ParseCsvLine(line, delimiter).ToArray();
                string sDate = parts.Length > idxDate ? parts[idxDate] : string.Empty;
                string sAmount = parts.Length > idxAmount ? parts[idxAmount] : string.Empty;
                string sCategory = parts.Length > idxCategory ? parts[idxCategory] : string.Empty;
                string sDescription = parts.Length > idxDescription ? parts[idxDescription] : string.Empty;
                string sBucket = parts.Length > idxBucket ? parts[idxBucket] : string.Empty;

                DateTime date = DateTime.Now;
                if (!DateTime.TryParseExact(sDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                {
                    DateTime.TryParse(sDate, ci, DateTimeStyles.None, out date);
                }

                decimal amount = 0m;
                decimal.TryParse(sAmount, NumberStyles.Number, ci, out amount);

                var expense = new Expense
                {
                    Date = date,
                    Amount = amount,
                    Category = sCategory ?? string.Empty,
                    Description = sDescription ?? string.Empty,
                    BudgetBucket = sBucket ?? string.Empty
                };

                result.Add(expense);
            }

            return result;
        }

        private static IEnumerable<string> ParseCsvLine(string line, char delimiter)
        {
            if (string.IsNullOrEmpty(line)) yield break;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == delimiter && !inQuotes)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }

            yield return sb.ToString();
        }

        private static string EscapeField(string? input, string delimiter)
        {
            input ??= string.Empty;
            bool needsQuotes = input.Contains('"') || input.Contains(delimiter) || input.Contains('\n') || input.Contains('\r');
            var escaped = input.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{escaped}\"" : escaped;
        }

        private static string QuoteIfNeeded(string s)
        {
            return s.Contains(';') || s.Contains('"') || s.Contains('\n') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
        }
    }
}