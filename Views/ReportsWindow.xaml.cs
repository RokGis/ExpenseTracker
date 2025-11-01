using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace ExpenceTracker.Views
{
    public partial class ReportsWindow : Window
    {
        public ReportsWindow(IEnumerable<Expense> expenses)
        {
            InitializeComponent();

            var list = (expenses ?? Enumerable.Empty<Expense>()).ToList();

            if (!list.Any())
            {
                SummaryTextBlock.Text = "Nėra duomenų ataskaitoms.";
                return;
            }

            SummaryTextBlock.Text = $"Iš viso įrašų: {list.Count} — Bendra suma: {list.Sum(x => x.Amount).ToString("N2", CultureInfo.GetCultureInfo("lt-LT"))}";

            BuildCategoryPie(list);
            BuildMonthlyColumn(list);
        }

        private void BuildCategoryPie(List<Expense> list)
        {
            var model = new PlotModel { Title = "Išlaidos pagal kategoriją" };
            var totals = list.GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Kita" : x.Category)
                             .Select(g => new { Category = g.Key, Sum = g.Sum(x => x.Amount) })
                             .OrderByDescending(x => x.Sum)
                             .ToList();

            var ps = new PieSeries
            {
                StrokeThickness = 0.5,
                InsideLabelPosition = 0.7,
                AngleSpan = 360,
                StartAngle = 0,
                InsideLabelFormat = "{1}: {0:N2}"
            };

            foreach (var t in totals)
            {
                ps.Slices.Add(new PieSlice(t.Category, (double)t.Sum) { IsExploded = false });
            }

            model.Series.Add(ps);
            CategoryPlot.Model = model;
        }

        private void BuildMonthlyColumn(List<Expense> list)
        {
            var model = new PlotModel { Title = "Išlaidos pagal mėnesį" };

            var monthly = list.GroupBy(x => new DateTime(x.Date.Year, x.Date.Month, 1))
                              .Select(g => new { Month = g.Key, Sum = g.Sum(x => x.Amount) })
                              .OrderBy(x => x.Month)
                              .ToList();

            var catAxis = new CategoryAxis { Position = AxisPosition.Left, GapWidth = 0.3 };
            var labels = monthly.Select(m => m.Month.ToString("MMM yyyy", CultureInfo.GetCultureInfo("lt-LT"))).ToList();
            foreach (var l in labels) catAxis.Labels.Add(l);

            var valueAxis = new LinearAxis { Position = AxisPosition.Bottom, MinimumPadding = 0, AbsoluteMinimum = 0 };

            var series = new BarSeries { StrokeColor = OxyColors.Black, StrokeThickness = 1, FillColor = OxyColors.SkyBlue };
            foreach (var m in monthly)
            {
                series.Items.Add(new BarItem((double)m.Sum));
            }

            model.Axes.Add(catAxis);
            model.Axes.Add(valueAxis);
            model.Series.Add(series);

            MonthlyPlot.Model = model;
        }
    }
}