using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using ExpenceTracker;

namespace ExpenceTracker.Views
{
    public partial class EditExpenseWindow : Window
    {
        public Expense Result { get; private set; }

        private readonly CultureInfo _ci = CultureInfo.GetCultureInfo("lt-LT");

        public EditExpenseWindow()
        {
            InitializeComponent();
        }

        public EditExpenseWindow(Expense initial, IEnumerable<string> categories) : this()
        {
            if (initial is not null)
            {
                DatePicker.SelectedDate = initial.Date;
                AmountTextBox.Text = initial.Amount.ToString("N2", _ci);
                DescriptionTextBox.Text = initial.Description ?? string.Empty;
            }

            CategoryComboBox.ItemsSource = categories;
            if (initial is not null && !string.IsNullOrEmpty(initial.Category))
                CategoryComboBox.SelectedItem = initial.Category;
            else
                CategoryComboBox.SelectedIndex = -1;

            OkButton.Click += OkButton_Click;
            CancelButton.Click += (_, __) => { DialogResult = false; };
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (!DatePicker.SelectedDate.HasValue)
            {
                MessageBox.Show("Pasirinkite datą.", "Klaida", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(AmountTextBox.Text, System.Globalization.NumberStyles.Number, _ci, out decimal amount))
            {
                MessageBox.Show("Klaida: Įveskite teisingą sumą LT formatu (pvz., 12,50).", "Klaida", MessageBoxButton.OK, MessageBoxImage.Warning);
                AmountTextBox.Focus();
                return;
            }

            var date = DatePicker.SelectedDate.Value;
            var desc = DescriptionTextBox.Text ?? string.Empty;
            var cat = CategoryComboBox.SelectedItem as string ?? CategoryComboBox.Text ?? "Kita";

            Result = new Expense
            {
                Date = date,
                Amount = amount,
                Category = cat,
                Description = desc,
                BudgetBucket = string.Empty
            };

            DialogResult = true;
        }

        private void CategoryComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {

        }
    }
}