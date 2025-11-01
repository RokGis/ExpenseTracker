using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExpenceTracker.Models;
using ExpenceTracker.Services;
using ExpenceTracker.Views;
using Microsoft.Win32;

namespace ExpenceTracker
{
    public partial class MainWindow : Window
    {
        private static readonly Regex _amountRegex = new Regex(@"^\d{0,9}([,\.]\d{0,2})?$", RegexOptions.Compiled);

        private decimal monthlyIncome;
        private DateTime incomeMonth;

        private decimal bucketRatio50 = 0.50m;
        private decimal bucketRatio30 = 0.30m;
        private decimal bucketRatio20 = 0.20m;

        private bool overageNotifiedForMonth;

        public ObservableCollection<Expense> Expenses { get; set; }

        private DateTime selectedMonth;

        private Dictionary<string, decimal> monthlyIncomes = new();

        private record CategoryEntry(string Name, string Bucket);

        public MainWindow()
        {
            InitializeComponent();

            Expenses = new ObservableCollection<Expense>();
            ExpensesDataGrid.ItemsSource = Expenses;

            AddButton.Click += AddButton_Click;
            SetIncomeButton.Click += SetIncomeButton_Click;

            AmountTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;
            AmountTextBox.PreviewKeyDown += AmountTextBox_PreviewKeyDown;
            DataObject.AddPastingHandler(AmountTextBox, AmountTextBox_Pasting);

            IncomeTextBox.PreviewTextInput += AmountTextBox_PreviewTextInput;
            DataObject.AddPastingHandler(IncomeTextBox, AmountTextBox_Pasting);

            PopulateCategoryComboBox();
            CategoryComboBox.SelectionChanged += CategoryComboBox_SelectionChanged;

            var cm = new ContextMenu();
            var miEdit = new MenuItem { Header = "Redaguoti" };
            miEdit.Click += EditMenuItem_Click;
            var miDelete = new MenuItem { Header = "Ištrinti" };
            miDelete.Click += DeleteMenuItem_Click;
            cm.Items.Add(miEdit);
            cm.Items.Add(miDelete);
            ExpensesDataGrid.ContextMenu = cm;

            this.Loaded += async (_, __) =>
            {
                var state = await DataService.LoadStateAsync();

                foreach (var e in state.Expenses) Expenses.Add(e);

                bucketRatio50 = state.Bucket50;
                bucketRatio30 = state.Bucket30;
                bucketRatio20 = state.Bucket20;

                monthlyIncomes = state.MonthlyIncomes ?? new Dictionary<string, decimal>();

                if (state.MonthlyIncome > 0 && state.IncomeMonth != DateTime.MinValue)
                {
                    var legacyKey = KeyForMonth(state.IncomeMonth);
                    if (!monthlyIncomes.ContainsKey(legacyKey))
                        monthlyIncomes[legacyKey] = state.MonthlyIncome;
                }

                incomeMonth = state.IncomeMonth == DateTime.MinValue ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1) : state.IncomeMonth;
                selectedMonth = incomeMonth == DateTime.MinValue ? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1) : incomeMonth;

                UpdateSelectedMonthUi();
                RecalculateBudgetsAndUI();
            };

            this.Closing += async (_, __) =>
            {
                var state = new AppState
                {
                    Expenses = Expenses.ToList(),
                    MonthlyIncomes = monthlyIncomes,
                    MonthlyIncome = monthlyIncome,
                    IncomeMonth = incomeMonth,
                    Bucket50 = bucketRatio50,
                    Bucket30 = bucketRatio30,
                    Bucket20 = bucketRatio20
                };

                await DataService.SaveStateAsync(state);
            };
        }

        private void EditMenuItem_Click(object sender, RoutedEventArgs e) => EditSelectedExpense();
        private void DeleteMenuItem_Click(object sender, RoutedEventArgs e) => DeleteSelectedExpense();

        private void EditButton_Click(object sender, RoutedEventArgs e) => EditSelectedExpense();

        private void DeleteButton_Click(object sender, RoutedEventArgs e) => DeleteSelectedExpense();

        private void ExpensesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ExpensesDataGrid.SelectedItem is Expense)
                EditSelectedExpense();
        }

        private void EditSelectedExpense()
        {
            if (ExpensesDataGrid.SelectedItem is not Expense selected) return;
            int idx = Expenses.IndexOf(selected);
            if (idx < 0) return;

            var categories = GetCategoryNames();
            var editWin = new EditExpenseWindow(selected, categories) { Owner = this };

            if (editWin.ShowDialog() == true && editWin.Result is Expense edited)
            {
                edited.BudgetBucket = selected.BudgetBucket;

                Expenses[idx] = edited;

                overageNotifiedForMonth = false;
                RecalculateBudgetsAndUI();
            }
        }

        private void DeleteSelectedExpense()
        {
            if (ExpensesDataGrid.SelectedItem is not Expense selected) return;

            var res = MessageBox.Show($"Ar tikrai norite ištrinti išlaidą:\n{selected.Category} — {selected.Amount.ToString("N2", CultureInfo.GetCultureInfo("lt-LT"))} ?",
                                      "Patvirtinti ištrynimą", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                Expenses.Remove(selected);
                overageNotifiedForMonth = false;
                RecalculateBudgetsAndUI();
            }
        }

        private List<string> GetCategoryNames()
        {
            var needs = new[]
            {
                "Būstas", "Maistas", "Transportas", "Sveikata", "Studijos", "Būtini rūbai", "Mokesčiai"
            };

            var wants = new[]
            {
                "Pramogos", "Prenumeratos", "Kelionės", "Drabužiai", "Restoranas", "Grožis", "Dovanos", "Technologijos"
            };

            var savings = new[]
            {
                "Santaupos", "Investicijos", "Skolų grąžinimas", "Rezervas"
            };

            return needs.Concat(wants).Concat(savings).ToList();
        }

        private static string NormalizeNumberInput(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            var sb = new System.Text.StringBuilder();
            foreach (char c in input)
            {
                if (char.IsDigit(c) || c == ',' || c == '.') sb.Append(c);
            }
            var s = sb.ToString();
            s = s.Replace('.', ',');
            int firstComma = s.IndexOf(',');
            if (firstComma >= 0)
            {
                var integerPart = s.Substring(0, firstComma);
                var fractionalPart = s.Substring(firstComma + 1).Replace(",", "");
                if (fractionalPart.Length > 2) fractionalPart = fractionalPart.Substring(0, 2);
                s = integerPart + (fractionalPart.Length > 0 ? ("," + fractionalPart) : string.Empty);
            }
            return s;
        }

        private static bool TryParseAmount(string? input, out decimal amount)
        {
            amount = 0m;
            if (string.IsNullOrWhiteSpace(input)) return false;
            var ci = CultureInfo.GetCultureInfo("lt-LT");
            var normalized = NormalizeNumberInput(input);

            if (decimal.TryParse(normalized, NumberStyles.Number, ci, out amount)) return true;

            var alt = normalized.Replace(',', '.');
            if (decimal.TryParse(alt, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) return true;

            var cleaned = new string(normalized.Where(c => char.IsDigit(c) || c == ',' || c == '.').ToArray());
            cleaned = cleaned.Replace("\u00A0", "").Replace(" ", "");
            if (string.IsNullOrEmpty(cleaned)) return false;

            cleaned = cleaned.Replace('.', ',');
            if (decimal.TryParse(cleaned, NumberStyles.Number, ci, out amount)) return true;

            alt = cleaned.Replace(',', '.');
            if (decimal.TryParse(alt, NumberStyles.Number, CultureInfo.InvariantCulture, out amount)) return true;

            return false;
        }

        private void SetIncomeButton_Click(object sender, RoutedEventArgs e)
        {
            string raw = IncomeTextBox.Text ?? string.Empty;
            if (!TryParseAmount(raw, out decimal income) || income <= 0)
            {
                IncomeTextBox.Focus();
                return;
            }

            monthlyIncome = income;
            incomeMonth = new DateTime(selectedMonth.Year, selectedMonth.Month, 1);
            monthlyIncomes[KeyForMonth(selectedMonth)] = income;

            overageNotifiedForMonth = false;

            var ci = CultureInfo.GetCultureInfo("lt-LT");
            IncomeInfoTextBlock.Text = $"Pajamos nustatytos: {income.ToString("N2", ci)} (mėn. {incomeMonth:MMMM yyyy})";

            RecalculateBudgetsAndUI();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string raw = AmountTextBox.Text ?? string.Empty;
            if (!TryParseAmount(raw, out decimal amount))
            {
                AmountTextBox.Focus();
                return;
            }

            if (BudgetBucketComboBox.SelectedItem is not ComboBoxItem bucketItem)
            {
                BudgetBucketComboBox.Focus();
                return;
            }

            string bucketTag = bucketItem.Tag?.ToString() ?? bucketItem.Content?.ToString() ?? "50";

            DateTime date = DatePicker.SelectedDate.GetValueOrDefault(DateTime.Now);
            string description = DescriptionTextBox.Text;

            string category;
            var sel = CategoryComboBox.SelectedItem;
            if (sel is CategoryEntry ce)
                category = ce.Name ?? "Kita";
            else if (sel is ComboBoxItem selItem)
                category = selItem.Content?.ToString() ?? "Kita";
            else
                category = sel as string ?? "Kita";

            Expense newExpense = new Expense
            {
                Date = date,
                Amount = amount,
                Category = category,
                Description = description,
                BudgetBucket = bucketTag
            };

            Expenses.Add(newExpense);

            overageNotifiedForMonth = false;

            AmountTextBox.Clear();
            DescriptionTextBox.Clear();
            DatePicker.SelectedDate = null;
            CategoryComboBox.SelectedIndex = -1;
            BudgetBucketComboBox.SelectedIndex = -1;

            RecalculateBudgetsAndUI();
        }

        private void RecalculateBudgetsAndUI()
        {
            monthlyIncome = GetIncomeForMonth(selectedMonth);

            if (monthlyIncome <= 0)
            {
                Budget50TextBlock.Text = Budget30TextBlock.Text = Budget20TextBlock.Text = "-";
                Remaining50TextBlock.Text = Remaining30TextBlock.Text = Remaining20TextBlock.Text = "-";
                ProgressBar50.Value = ProgressBar30.Value = ProgressBar20.Value = 0;
                return;
            }

            decimal total50 = Math.Round(monthlyIncome * bucketRatio50, 2);
            decimal total30 = Math.Round(monthlyIncome * bucketRatio30, 2);
            decimal total20 = Math.Round(monthlyIncome * bucketRatio20, 2);

            var relevantExpenses = Expenses.Where(x => new DateTime(x.Date.Year, x.Date.Month, 1) == new DateTime(selectedMonth.Year, selectedMonth.Month, 1));

            decimal spent50 = relevantExpenses.Where(x => (x.BudgetBucket ?? "") == "50").Sum(x => x.Amount);
            decimal spent30 = relevantExpenses.Where(x => (x.BudgetBucket ?? "") == "30").Sum(x => x.Amount);
            decimal spent20 = relevantExpenses.Where(x => (x.BudgetBucket ?? "") == "20").Sum(x => x.Amount);

            decimal over50 = Math.Max(0, spent50 - total50);
            decimal over30 = Math.Max(0, spent30 - total30);
            decimal totalOver = over50 + over30;

            decimal effectiveSpent20 = spent20 + totalOver;
            decimal effectiveRemaining20 = total20 - effectiveSpent20;

            decimal remaining50 = total50 - spent50;
            decimal remaining30 = total30 - spent30;

            var ci = CultureInfo.GetCultureInfo("lt-LT");

            Budget50TextBlock.Text = total50.ToString("N2", ci);
            Budget30TextBlock.Text = total30.ToString("N2", ci);
            Budget20TextBlock.Text = total20.ToString("N2", ci);

            Remaining50TextBlock.Text = remaining50.ToString("N2", ci);
            Remaining30TextBlock.Text = remaining30.ToString("N2", ci);
            Remaining20TextBlock.Text = effectiveRemaining20.ToString("N2", ci);

            ProgressBar50.Value = total50 > 0 ? (double)Math.Min(100, Math.Max(0, (spent50 / total50) * 100m)) : 0;
            ProgressBar30.Value = total30 > 0 ? (double)Math.Min(100, Math.Max(0, (spent30 / total30) * 100m)) : 0;
            ProgressBar20.Value = total20 > 0 ? (double)Math.Min(100, Math.Max(0, (effectiveSpent20 / total20) * 100m)) : 0;

            if (totalOver > 0 && !overageNotifiedForMonth)
            {
                string overText = totalOver.ToString("N2", ci);
                string message = $"Dėmesio: „Reikmės“ arba „Norai“ viršija jų limitą. Viršijančios sumos ({overText}) bus nuskaičiuotos iš „Santaupų“ dalies.";
                if (effectiveRemaining20 < 0)
                {
                    message += Environment.NewLine + $"Santaupų likutis tapo neigiamas ({effectiveRemaining20.ToString("N2", ci)}).";
                }

                MessageBox.Show(message, "Biudžeto pranešimas", MessageBoxButton.OK, MessageBoxImage.Warning);
                overageNotifiedForMonth = true;
            }
        }

        private void AmountTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox tb)
            {
                e.Handled = true;
                return;
            }

            int selStart = tb.SelectionStart;
            int selLen = tb.SelectionLength;
            string text = tb.Text ?? string.Empty;
            string proposed = text.Remove(selStart, selLen).Insert(selStart, e.Text);

            e.Handled = !_amountRegex.IsMatch(proposed);
        }

        private void AmountTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void AmountTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text) || sender is not TextBox tb)
            {
                e.CancelCommand();
                return;
            }

            string pasteText = e.DataObject.GetData(DataFormats.Text) as string ?? string.Empty;
            string normalizedPaste = NormalizeNumberInput(pasteText);

            int selStart = tb.SelectionStart;
            int selLen = tb.SelectionLength;
            string current = tb.Text ?? string.Empty;

            string final = current.Remove(selStart, selLen).Insert(selStart, normalizedPaste);
            final = NormalizeNumberInput(final);

            tb.Text = final;
            tb.SelectionStart = Math.Min(final.Length, selStart + normalizedPaste.Length);

            e.CancelCommand();
        }

        private void PopulateCategoryComboBox()
        {
            var needs = new[]
            {
                new CategoryEntry("Būstas", "50"),
                new CategoryEntry("Maistas", "50"),
                new CategoryEntry("Transportas", "50"),
                new CategoryEntry("Sveikata", "50"),
                new CategoryEntry("Studijos", "50"),
                new CategoryEntry("Būtini rūbai", "50"),
                new CategoryEntry("Mokesčiai", "50")
            };

            var wants = new[]
            {
                new CategoryEntry("Pramogos", "30"),
                new CategoryEntry("Prenumeratos", "30"),
                new CategoryEntry("Kelionės", "30"),
                new CategoryEntry("Drabužiai", "30"),
                new CategoryEntry("Restoranas", "30"),
                new CategoryEntry("Grožis", "30"),
                new CategoryEntry("Dovanos", "30"),
                new CategoryEntry("Technologijos", "30")
            };

            var savings = new[]
            {
                new CategoryEntry("Santaupos", "20"),
                new CategoryEntry("Investicijos", "20"),
                new CategoryEntry("Skolų grąžinimas", "20"),
                new CategoryEntry("Rezervas", "20")
            };

            var categories = needs.Concat(wants).Concat(savings).ToList();

            CategoryComboBox.DisplayMemberPath = "Name";
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = -1;
        }

        private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CategoryComboBox.SelectedItem is CategoryEntry ce)
            {
                string bucket = ce.Bucket;

                for (int i = 0; i < BudgetBucketComboBox.Items.Count; i++)
                {
                    if (BudgetBucketComboBox.Items[i] is ComboBoxItem item)
                    {
                        string tagOrContent = (item.Tag?.ToString() ?? item.Content?.ToString() ?? string.Empty).Trim();
                        if (tagOrContent.Equals(bucket, StringComparison.OrdinalIgnoreCase) || tagOrContent.Contains(bucket))
                        {
                            BudgetBucketComboBox.SelectedIndex = i;
                            return;
                        }
                    }
                }

                BudgetBucketComboBox.SelectedIndex = -1;
            }
            else
            {
                BudgetBucketComboBox.SelectedIndex = -1;
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            var win = new ExpenceTracker.Views.ReportsWindow(Expenses);
            win.Owner = this;
            win.ShowDialog();
        }

        private void UpdateSelectedMonthUi()
        {
            var ci = CultureInfo.GetCultureInfo("lt-LT");
            SelectedMonthTextBlock.Text = $"{selectedMonth.Year} m. {selectedMonth.ToString("MMMM", ci).ToLower(ci)}";
        }

        private void PrevMonthButton_Click(object sender, RoutedEventArgs e)
        {
            selectedMonth = selectedMonth.AddMonths(-1);
            UpdateSelectedMonthUi();
            RecalculateBudgetsAndUI();
        }

        private void NextMonthButton_Click(object sender, RoutedEventArgs e)
        {
            selectedMonth = selectedMonth.AddMonths(1);
            UpdateSelectedMonthUi();
            RecalculateBudgetsAndUI();
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = false
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var imported = await CsvService.ImportExpensesAsync(dlg.FileName, CultureInfo.GetCultureInfo("lt-LT"));
                if (imported == null || imported.Count == 0)
                {
                    MessageBox.Show("Nerasta įrašų faile.", "Importas", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var res = MessageBox.Show($"Rasti {imported.Count} įrašai. Pridėti prie esamų? (Taip = pridėti, Ne = pakeisti)", "Importas", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (res == MessageBoxResult.Cancel) return;

                if (res == MessageBoxResult.No)
                {
                    Expenses.Clear();
                }

                foreach (var exp in imported)
                {
                    Expenses.Add(exp);
                }

                overageNotifiedForMonth = false;
                RecalculateBudgetsAndUI();

                MessageBox.Show("Importas baigtas.", "Importas", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Importuojant įvyko klaida: {ex.Message}", "Klaida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = "expenses.csv",
                DefaultExt = ".csv"
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                await CsvService.ExportExpensesAsync(dlg.FileName, Expenses, CultureInfo.GetCultureInfo("lt-LT"));
                MessageBox.Show("Eksportas baigtas.", "Eksportas", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Eksportuojant įvyko klaida: {ex.Message}", "Klaida", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string KeyForMonth(DateTime dt)
        {
            return dt.ToString("yyyy-MM");
        }

        private decimal GetIncomeForMonth(DateTime month)
        {
            var key = KeyForMonth(month);
            if (monthlyIncomes.TryGetValue(key, out var income))
                return income;
            return 0m;
        }
    }
}