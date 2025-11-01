using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using ExpenceTracker.Models;

namespace ExpenceTracker.Services
{
    public static class DataService
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExpenceTracker");
        private static readonly string DataFile = Path.Combine(AppFolder, "appstate.json");
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        public static async Task SaveStateAsync(AppState state)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                using var fs = File.Create(DataFile);
                await JsonSerializer.SerializeAsync(fs, state, _options).ConfigureAwait(false);
            }
            catch
            {
            }
        }

        public static async Task<AppState> LoadStateAsync()
        {
            try
            {
                if (!File.Exists(DataFile)) return new AppState();
                using var fs = File.OpenRead(DataFile);
                var state = await JsonSerializer.DeserializeAsync<AppState>(fs, _options).ConfigureAwait(false);
                return state ?? new AppState();
            }
            catch
            {
                return new AppState();
            }
        }

        public static List<Expense> ImportExpensesFromCsv(string path)
        {
            try
            {
                return CsvService.ImportExpensesAsync(path).GetAwaiter().GetResult();
            }
            catch
            {
                return new List<Expense>();
            }
        }

        public static void ExportExpensesToCsv(List<Expense> expenses, string path)
        {
            try
            {
                CsvService.ExportExpensesAsync(path, expenses).GetAwaiter().GetResult();
            }
            catch
            {
            }
        }
    }
}