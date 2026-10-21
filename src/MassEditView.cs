using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MaturityApp
{
    public partial class MainWindow
    {
        int massCriterion;
        bool massOverwrite, massVisibleOnly;
        Expander massExpander;
        ComboBox massCriterionBox;
        TextBlock massScopeText;
        readonly List<Button> massValueButtons = new List<Button>();

        UIElement BuildMassValuePanel()
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.Text("Выберите вид требования и нажмите значение — оно будет применено сразу. Массовое действие можно отменить.", 11, Theme.Muted));
            Grid choices = new Grid { Margin = new Thickness(0, 9, 0, 9) };
            choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) }); choices.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); choices.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            massCriterionBox = new ComboBox { Height = 32, FontSize = 12, Padding = new Thickness(6), Margin = new Thickness(0, 0, 12, 0) };
            for (int j = 0; j < Data.Criteria.Length; j++) massCriterionBox.Items.Add(new ComboBoxItem { Content = Data.Criteria[j].Name, Tag = j });
            massCriterionBox.Items.Add(new ComboBoxItem { Content = "Все восемь видов требований", Tag = -1 });
            massCriterionBox.SelectedIndex = massCriterion < 0 ? 8 : massCriterion;
            massCriterionBox.SelectionChanged += (s, e) => { ComboBoxItem item = massCriterionBox.SelectedItem as ComboBoxItem; if (item == null) return; massCriterion = (int)item.Tag; RefreshMassButtons(); }; choices.Children.Add(massCriterionBox);
            ComboBox scope = new ComboBox { Height = 32, FontSize = 12, Padding = new Thickness(6), Margin = new Thickness(0, 0, 12, 0) };
            scope.Items.Add("Все включённые, включая скрытые"); scope.Items.Add("Только показанные в таблице"); scope.SelectedIndex = massVisibleOnly ? 1 : 0;
            scope.SelectionChanged += (s, e) => { massVisibleOnly = scope.SelectedIndex == 1; RefreshMassButtons(); }; Grid.SetColumn(scope, 1); choices.Children.Add(scope);
            CheckBox overwrite = new CheckBox { Content = "Заменить введённые", IsChecked = massOverwrite, Margin = new Thickness(0, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Без этой галочки заполняются только пустые ячейки. Выбранный 0 уже считается ответом." };
            overwrite.Checked += (s, e) => { massOverwrite = true; RefreshMassButtons(); }; overwrite.Unchecked += (s, e) => { massOverwrite = false; RefreshMassButtons(); }; Grid.SetColumn(overwrite, 2); choices.Children.Add(overwrite); panel.Children.Add(choices);
            StackPanel values = new StackPanel { Orientation = Orientation.Horizontal }; values.Children.Add(Theme.Text("Установить:", 12));
            massValueButtons.Clear();
            foreach (double grade in new[] { 0.0, .3, .5, 1.0 })
            {
                double value = grade;
                Button button = Theme.Button("", () => ApplyMassValue(value)); button.Tag = value; button.Padding = new Thickness(16, 8, 16, 8); button.Margin = new Thickness(9, 0, 0, 0); massValueButtons.Add(button); values.Children.Add(button);
            }
            panel.Children.Add(values);
            massScopeText = Theme.Text("", 11, Theme.Muted); massScopeText.Margin = new Thickness(0, 8, 0, 0); panel.Children.Add(massScopeText);
            Border card = Theme.Card(panel, new Thickness(13)); card.Background = Theme.B("#F0F5FE"); return card;
        }
        int[] MassDirectionIds()
        {
            if (massVisibleOnly) return quickRows.Where(r => r.Root.Visibility == Visibility.Visible && r.Model.Included).Select(r => r.Model.Id).ToArray();
            return assessment.Directions.Where(d => d.Included).Select(d => d.Id).ToArray();
        }
        void SelectMassCriterion(int criterion)
        {
            massCriterion = criterion;
            if (massCriterionBox != null) massCriterionBox.SelectedIndex = criterion < 0 ? 8 : criterion;
            RefreshMassButtons();
        }
        void RefreshMassButtons()
        {
            if (massValueButtons.Count == 0 || massScopeText == null) return;
            int[] ids = MassDirectionIds();
            foreach (Button button in massValueButtons)
            {
                double value = (double)button.Tag;
                bool allowed = massCriterion == 5 || Math.Abs(value - .3) > 1e-9;
                button.Visibility = allowed ? Visibility.Visible : Visibility.Collapsed;
                if (!allowed) continue;
                List<AnswerChange> preview = QuickInput.PreviewSetValue(assessment, massCriterion, value, massOverwrite, ids);
                button.Content = Calc.F(value) + "  ·  " + preview.Count + " ячеек"; button.IsEnabled = preview.Count > 0;
                string details = massCriterion < 0 ? "Одно значение для всех восьми видов требований. Проверьте, что оно соответствует условиям каждого вида требования." : Data.Criteria[massCriterion].Options.First(o => Math.Abs(o.Value - value) < 1e-9).Text;
                button.ToolTip = new TextBlock { Text = "Установить " + Calc.F(value) + ". Изменится ячеек: " + preview.Count + ".\n" + details, TextWrapping = TextWrapping.Wrap, MaxWidth = 450 };
            }
            massScopeText.Text = "Направлений в области действия: " + ids.Length + ". " + (massOverwrite ? "Замена заполненных включена." : "Только пустые: уже введённые оценки, включая 0, сохраняются.");
        }
        void ApplyMassValue(double value)
        {
            List<AnswerChange> changes = QuickInput.SetValue(assessment, massCriterion, value, massOverwrite, MassDirectionIds());
            if (changes.Count == 0) { status.Text = "Нет ячеек для изменения. Проверьте область действия и режим замены."; return; }
            copiedAnswers = changes; Changed(); UpdateNumbers();
            status.Text = "Установлено " + Calc.F(value) + ": " + changes.Count + " ячеек · " + (massCriterion < 0 ? "все восемь видов требований" : Data.Criteria[massCriterion].Name) + ". Действие можно отменить.";
        }
    }
}
