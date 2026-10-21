using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MaturityApp
{
    public partial class MainWindow
    {
        readonly List<QuickAnswerCell> commonCells = new List<QuickAnswerCell>();

        void BuildQuickAssessment()
        {
            Grid root = new Grid(); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); center.Children.Add(root);
            StackPanel top = new StackPanel();
            Grid info = new Grid(); info.ColumnDefinitions.Add(new ColumnDefinition()); info.ColumnDefinitions.Add(new ColumnDefinition()); info.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            AddQuickField(info, 0, "Организация", assessment.Organization, value => assessment.Organization = value);
            AddQuickField(info, 1, "Объект оценки", assessment.SystemName, value => assessment.SystemName = value);
            StackPanel categoryField = new StackPanel(); categoryField.Children.Add(Theme.Text("Минимальный целевой уровень (п. 11 Методики)", 11, Theme.Muted));
            ComboBox category = new ComboBox { Margin = new Thickness(0, 6, 0, 0), Height = 34, FontSize = 12, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Категория задаёт минимальную цель. Её смена устанавливает новую цель для всех направлений; ответы сохраняются." };
            category.Items.Add("Класс не определён — 1"); category.Items.Add("1 — ГИС К3, УЗ-3/4, ЗОКИИ 3 кат."); category.Items.Add("2 — ГИС К2, УЗ-2, ЗОКИИ 2 кат."); category.Items.Add("3 — ГИС К1, УЗ-1, ЗОКИИ 1 кат."); category.SelectedIndex = assessment.Category;
            category.SelectionChanged += (s, e) =>
            {
                if (building || category.SelectedIndex < 0) return;
                assessment.Category = category.SelectedIndex; building = true;
                foreach (DirectionAssessment d in assessment.Directions) d.Target = Math.Max(1, assessment.Category);
                foreach (QuickRowView row in quickRows)
                {
                    foreach (ComboBoxItem item in row.Target.Items) item.IsEnabled = (int)item.Tag >= Math.Max(1, assessment.Category);
                    row.Target.SelectedIndex = row.Model.Target - 1;
                }
                building = false; Changed(); UpdateNumbers();
            };
            if (assessment.Objects.Length > 0) { category.IsEnabled = false; category.ToolTip = "Определяется типом объекта защиты на вкладке «Объект оценки» (п. 11 Методики, по самому строгому)."; }
            categoryField.Children.Add(category); Grid.SetColumn(categoryField, 2); info.Children.Add(categoryField);
            top.Children.Add(Theme.Card(info, new Thickness(14, 11, 14, 12)));
            WrapPanel tools = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            Button library = Theme.Button("Шаблоны…", OpenLibrary); library.ToolTip = "Подставить шаблон в эту оценку или сохранить её как новый шаблон. Шаблонов может быть сколько угодно; папку можно сделать общей."; tools.Children.Add(library);
            int[] inapplicable = QuickInput.PassportInapplicable(assessment.Passport).Where(id => assessment.Directions[id - 1].Included).ToArray();
            if (inapplicable.Length > 0)
            {
                Button exclude = Theme.Button("Исключить неприменимые направления: " + String.Join(", ", inapplicable), () => ExcludeByPassport(inapplicable));
                exclude.ToolTip = "В описании объекта указано, что технология не применяется (п. 13 Методики):\n" + String.Join("\n", inapplicable.Select(id => id + ". " + Data.DirectionNames[id - 1])) + "\n\nОтветы сохранятся; вернуть направление можно галочкой в таблице.";
                tools.Children.Add(exclude);
            }
            foreach (Button b in tools.Children.OfType<Button>()) { b.Padding = new Thickness(12, 6, 12, 6); b.FontSize = 12; }
            top.Children.Add(tools);
            Grid summary = new Grid { Margin = new Thickness(0, 12, 0, 10) }; summary.ColumnDefinitions.Add(new ColumnDefinition()); summary.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            StackPanel score = new StackPanel { Orientation = Orientation.Horizontal };
            quickScore = Theme.Text("Узи —", 18, Theme.Blue, true); score.Children.Add(quickScore);
            quickNotice = Theme.Text("", 11, Theme.Muted); quickNotice.Margin = new Thickness(16, 0, 12, 0); score.Children.Add(quickNotice); summary.Children.Add(score);
            StackPanel filters = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(filters, 1); summary.Children.Add(filters);
            CheckBox pending = new CheckBox { Content = "Только незаполненные", IsChecked = onlyIncomplete, FontSize = 11, Margin = new Thickness(0, 0, 15, 0) };
            pending.Checked += (s, e) => { onlyIncomplete = true; FilterDirections(); }; pending.Unchecked += (s, e) => { onlyIncomplete = false; FilterDirections(); }; filters.Children.Add(pending);
            Grid searchBox = new Grid { Width = 180 };
            search = new TextBox { Text = quickSearch, Padding = new Thickness(7, 5, 7, 5), FontSize = 11, ToolTip = "Поиск направления по названию или номеру" }; searchBox.Children.Add(search);
            TextBlock placeholder = Theme.Text("Поиск направления", 11, Theme.Muted); placeholder.Margin = new Thickness(8, 0, 0, 0); placeholder.IsHitTestVisible = false; placeholder.Visibility = quickSearch.Length == 0 ? Visibility.Visible : Visibility.Collapsed; searchBox.Children.Add(placeholder);
            search.TextChanged += (s, e) => { quickSearch = search.Text; placeholder.Visibility = quickSearch.Length == 0 ? Visibility.Visible : Visibility.Collapsed; FilterDirections(); }; filters.Children.Add(searchBox); top.Children.Add(summary);
            root.Children.Add(top);
            Expander repeat = new Expander { Header = Theme.Text("Массовое заполнение · одно значение для всех", 12, Theme.Blue, true), Margin = new Thickness(0, 0, 0, 10) }; massExpander = repeat;
            StackPanel repeatBody = new StackPanel { Margin = new Thickness(0, 7, 0, 2) };
            repeatBody.Children.Add(BuildMassValuePanel());
            StackPanel copyBody = new StackPanel { Margin = new Thickness(0, 7, 0, 0) };
            copyBody.Children.Add(Theme.Text("Если условия одинаковы: повторите ответы выбранного направления только в пустых ячейках остальных включённых направлений. Уже введённые оценки сохранятся. Действие можно отменить.", 11, Theme.Muted));
            Grid repeatControls = new Grid { Margin = new Thickness(0, 8, 0, 0) }; repeatControls.ColumnDefinitions.Add(new ColumnDefinition()); repeatControls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ComboBox source = new ComboBox { FontSize = 12, Height = 33, Padding = new Thickness(6), Margin = new Thickness(0, 0, 10, 0) }; quickCopySource = source;
            foreach (DirectionAssessment d in assessment.Directions) source.Items.Add(new ComboBoxItem { Content = d.Id + ". " + Data.DirectionNames[d.Id - 1], Tag = d.Id, IsEnabled = d.Included });
            source.SelectedIndex = Array.FindIndex(assessment.Directions, d => d.Included); repeatControls.Children.Add(source);
            Button copy = Theme.Button("Повторить в пустых ячейках", () =>
            {
                ComboBoxItem item = source.SelectedItem as ComboBoxItem; if (item == null || !assessment.Directions[(int)item.Tag - 1].Included) { status.Text = "Выберите включённое направление с ответами."; return; }
                List<AnswerChange> changes = QuickInput.CopyIntoEmpty(assessment, (int)item.Tag);
                if (changes.Count == 0) { status.Text = "Нет ответов для переноса: источник пуст или подходящие ячейки уже заполнены."; return; }
                copiedAnswers = changes; Changed(); UpdateNumbers();
                status.Text = "Заполнено ответов: " + changes.Count + ". Перенесены только оценки; цели и комментарии сохранены.";
            }); copy.Padding = new Thickness(13, 7, 13, 7); Grid.SetColumn(copy, 1); repeatControls.Children.Add(copy);
            copyBody.Children.Add(repeatControls); repeatBody.Children.Add(new Expander { Header = "Или повторить восемь ответов из направления", Content = copyBody, Margin = new Thickness(0, 10, 0, 0) }); repeat.Content = repeatBody; Grid.SetRow(repeat, 1); root.Children.Add(repeat);
            Grid table = new Grid(); table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); table.RowDefinitions.Add(new RowDefinition());
            Grid headers = MatrixGrid(); headers.Margin = new Thickness(0, 0, 17, 0); headers.Background = Theme.B("#EAF0F8");
            AddMatrixHeader(headers, 0, "✓", "Учитывать направление в расчёте"); AddMatrixHeader(headers, 1, "Направление · нажмите для подробностей", "Полная анкета, пояснения и подтверждающие материалы");
            for (int j = 0; j < 8; j++)
            {
                string name = new[] { "Документи-\nрование", "Выполнение", "Инстру-\nменты", "Квалифи-\nкация", "Контроль", "Обучение", "Внешний\nаудит", "Актуали-\nзация" }[j];
                AddMatrixHeader(headers, j + 2, name, Data.Criteria[j].Name + "\n" + Data.Criteria[j].Description + "\n\n" + String.Join("\n\n", Data.Criteria[j].Options.Select(o => Calc.F(o.Value) + " — " + o.Text)));
            }
            AddMatrixHeader(headers, 10, "Уровень", "Текущий уровень и число ответов в направлении"); AddMatrixHeader(headers, 11, "Цель", "Целевой уровень направления"); table.Children.Add(headers);
            table.RowDefinitions.Insert(1, new RowDefinition { Height = GridLength.Auto });
            Grid commonRow = MatrixGrid(); commonRow.Margin = new Thickness(0, 0, 17, 0); commonRow.Background = Theme.B("#F0F5FE"); commonRow.MinHeight = 52;
            StackPanel commonTitle = new StackPanel { Margin = new Thickness(7, 6, 8, 6), VerticalAlignment = VerticalAlignment.Center };
            commonTitle.Children.Add(Theme.Text("Общий ответ для всех направлений", 12, Theme.Blue, true));
            commonTitle.Children.Add(Theme.Text("Отличия правьте в строках ниже — они сохранятся", 10.5, Theme.Muted));
            Grid.SetColumn(commonTitle, 1); commonRow.Children.Add(commonTitle);
            commonCells.Clear();
            for (int j = 0; j < 8; j++)
            {
                int criterion = j;
                QuickAnswerCell cell = QuickAnswerCell.Common(j, () => QuickInput.CommonValue(assessment, criterion), value => ApplyCommonValue(criterion, value), description => { if (quickHint != null) quickHint.Text = description; });
                commonCells.Add(cell); Grid.SetColumn(cell, j + 2); commonRow.Children.Add(cell);
            }
            Border commonLine = new Border { Height = 2, Background = Theme.B("#C9D8F2"), VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false }; Grid.SetColumnSpan(commonLine, 12); commonRow.Children.Add(commonLine);
            Grid.SetRow(commonRow, 1); table.Children.Add(commonRow);
            StackPanel rows = new StackPanel(); ScrollViewer scroll = new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; Grid.SetRow(scroll, 2); table.Children.Add(scroll);
            foreach (DirectionAssessment d in assessment.Directions)
            {
                DirectionAssessment item = d; Grid row = MatrixGrid(); row.Background = d.Id % 2 == 0 ? Theme.B("#F8FAFD") : Brushes.White; row.MinHeight = 52;
                QuickRowView view = new QuickRowView { Model = d, Root = row, Cells = new QuickAnswerCell[8] }; quickRows.Add(view);
                CheckBox include = new CheckBox { IsChecked = d.Included, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Учитывать направление в расчёте" };
                include.Checked += (s, e) => { if (building) return; item.Included = true; Changed(); UpdateNumbers(); }; include.Unchecked += (s, e) => { if (building) return; item.Included = false; Changed(); UpdateNumbers(); }; row.Children.Add(include);
                TextBlock name = Theme.Text(d.Id + ". " + Data.DirectionNames[d.Id - 1], 12); name.LineHeight = 17;
                Button details = new Button { Content = name, Background = Brushes.Transparent, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(7, 7, 8, 7), Margin = new Thickness(0), ToolTip = "Открыть подробную анкету и комментарии" };
                details.Click += (s, e) => { selected = item.Id - 1; page = "assessment"; ShowPage(); }; Grid.SetColumn(details, 1); row.Children.Add(details);
                for (int j = 0; j < 8; j++)
                {
                    int criterion = j;
                    QuickAnswerCell cell = new QuickAnswerCell(d, j, () => { ForgetCopiedAnswer(item.Id, criterion); Changed(); UpdateNumbers(); }, description => { if (quickHint != null) quickHint.Text = description; }, () => QuickInput.CommonValue(assessment, criterion));
                    view.Cells[j] = cell; Grid.SetColumn(cell, j + 2); row.Children.Add(cell);
                }
                view.Level = Theme.Text("", 12, Theme.Blue, true); view.Level.TextAlignment = TextAlignment.Center; Grid.SetColumn(view.Level, 10); row.Children.Add(view.Level);
                ComboBox target = new ComboBox { Width = 42, Height = 28, Margin = new Thickness(3, 0, 3, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
                for (int level = 1; level <= 4; level++) target.Items.Add(new ComboBoxItem { Content = level, Tag = level, IsEnabled = level >= Math.Max(1, assessment.Category) }); target.SelectedIndex = d.Target - 1;
                target.SelectionChanged += (s, e) => { if (building || target.SelectedIndex < 0) return; item.Target = target.SelectedIndex + 1; Changed(); UpdateNumbers(); }; view.Target = target; Grid.SetColumn(target, 11); row.Children.Add(target);
                Border line = new Border { Height = 1, Background = Theme.Line, VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false }; Grid.SetColumnSpan(line, 12); row.Children.Add(line); rows.Children.Add(row);
            }
            Border tableCard = Theme.Card(table, new Thickness(0)); tableCard.ClipToBounds = true; Grid.SetRow(tableCard, 2); root.Children.Add(tableCard);
            quickHint = Theme.Text("Наведите на оценку, чтобы прочитать точное условие. Повторное нажатие снимает ответ. Пусто — не оценено; выбранный 0 — явный ответ. Клавиши: 0 → 0, 5 → 0,5, 1 → 1; обучение: 3 → 0,3. Delete — снять ответ.", 12, Theme.Muted);
            Border hint = Theme.Card(quickHint, new Thickness(12, 9, 12, 9)); hint.Height = 70; hint.Margin = new Thickness(0, 9, 0, 0); Grid.SetRow(hint, 3); root.Children.Add(hint);
        }
        void AddQuickField(Grid grid, int col, string label, string text, Action<string> setter)
        {
            StackPanel field = new StackPanel { Margin = new Thickness(0, 0, 14, 0) }; field.Children.Add(Theme.Text(label, 11, Theme.Muted)); TextBox input = new TextBox { Text = text, Margin = new Thickness(0, 6, 0, 0), Height = 34, Padding = new Thickness(8, 5, 8, 5) };
            input.TextChanged += (s, e) => { setter(input.Text); Changed(); UpdateHeader(); }; field.Children.Add(input); Grid.SetColumn(field, col); grid.Children.Add(field);
        }
        static Grid MatrixGrid()
        {
            Grid grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3.4, GridUnitType.Star), MinWidth = 220 });
            for (int j = 0; j < 8; j++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(j == 5 ? 1.25 : 1, GridUnitType.Star), MinWidth = j == 5 ? 94 : 73 });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(59) }); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(51) }); return grid;
        }
        void AddMatrixHeader(Grid grid, int col, string text, string tooltip)
        {
            TextBlock title = Theme.Text(text, 11, Theme.Muted, true); title.TextAlignment = col == 1 ? TextAlignment.Left : TextAlignment.Center;
            Border cell = new Border { Child = title, Padding = new Thickness(4, 11, 4, 11), ToolTip = new TextBlock { Text = tooltip + (col >= 2 && col <= 9 ? "\n\nНажмите заголовок для массового заполнения этого вида требования." : ""), MaxWidth = 480, TextWrapping = TextWrapping.Wrap } };
            if (col >= 2 && col <= 9) { int criterion = col - 2; cell.Cursor = Cursors.Hand; cell.MouseLeftButtonUp += (s, e) => { SelectMassCriterion(criterion); if (massExpander != null) massExpander.IsExpanded = true; }; }
            Grid.SetColumn(cell, col); grid.Children.Add(cell);
        }
        void ApplyCommonValue(int criterion, double value)
        {
            List<AnswerChange> changes = QuickInput.SetCommonValue(assessment, criterion, value);
            if (changes.Count == 0) { status.Text = "Общий ответ уже стоит во всех подходящих ячейках. Отличающиеся направления не изменены."; UpdateNumbers(); return; }
            copiedAnswers = changes; Changed(); UpdateNumbers();
            int kept = assessment.Directions.Count(d => d.Included && d.Answers[criterion].HasValue && Math.Abs(d.Answers[criterion].Value - value) > 1e-10);
            status.Text = Data.Criteria[criterion].Name + " = " + Calc.F(value) + ": заполнено " + changes.Count + " ячеек" + (kept > 0 ? " Отличия сохранены: " + kept + "." : "") + " Действие можно отменить.";
        }
        void ExcludeByPassport(int[] ids)
        {
            foreach (int id in ids)
            {
                DirectionAssessment d = assessment.Directions[id - 1]; d.Included = false;
                if (String.IsNullOrWhiteSpace(d.Evidence)) d.Evidence = PassportExclusionReason(id);
            }
            Changed(); ShowPage();
            status.Text = "Исключены неприменимые направления: " + String.Join(", ", ids) + ". Основание по п. 13 Методики записано в материалы направления; ответы сохранены.";
        }
        void OpenLibrary() { OpenLibrary(false); }
        void OpenLibrary(bool organizations)
        {
            LibraryWindow window = new LibraryWindow(assessment, () => { copiedAnswers = null; Changed(); ShowPage(); }, organizations) { Owner = this };
            window.ShowDialog();
        }
        static string PassportExclusionReason(int id)
        {
            string technology = id == 8 ? "мобильные устройства" : id == 9 ? "удалённый доступ (удалённое администрирование)" : id == 10 ? "беспроводной доступ (Wi-Fi)" : id == 13 ? "собственная разработка программного обеспечения" : "технологии искусственного интеллекта";
            return "Исключено из области оценки по п. 13 Методики: в объекте не применяются " + technology + " (описание объекта).";
        }
        void ForgetCopiedAnswer(int directionId, int criterion)
        {
            if (copiedAnswers == null) return; copiedAnswers.RemoveAll(x => x.DirectionId == directionId && x.Criterion == criterion);
            if (undoButton != null) undoButton.Visibility = copiedAnswers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        void UndoCopiedAnswers()
        {
            if (copiedAnswers == null) return; int restored = QuickInput.Revert(assessment, copiedAnswers); copiedAnswers = null; Changed(); UpdateNumbers(); status.Text = "Отменено перенесённых ответов: " + restored + ". Ручные изменения сохранены.";
        }
    }
    public class QuickRowView
    {
        public DirectionAssessment Model; public Grid Root; public QuickAnswerCell[] Cells; public TextBlock Level; public ComboBox Target;
    }
    public class QuickAnswerCell : Border
    {
        static readonly Brush DifferentBackground = Theme.B("#FFF1D9");
        readonly int criterion; readonly Func<double?> current, reference; readonly Func<bool> enabled; readonly Action<double?> pick; readonly Action<string> hint; readonly bool common;
        readonly List<Button> buttons = new List<Button>();
        // reference: the common answer; a differing grade is highlighted so exceptions stay visible.
        public QuickAnswerCell(DirectionAssessment model, int criterion, Action changed, Action<string> hint, Func<double?> reference = null)
            : this(criterion, () => model.Answers[criterion], () => model.Included, value => { model.Answers[criterion] = value; changed(); }, hint, reference, false) { }
        // The common row sets a value for all following directions; it never clears answers.
        public static QuickAnswerCell Common(int criterion, Func<double?> current, Action<double> pick, Action<string> hint)
        {
            return new QuickAnswerCell(criterion, current, () => true, value => { if (value.HasValue) pick(value.Value); }, hint, null, true);
        }
        QuickAnswerCell(int criterion, Func<double?> current, Func<bool> enabled, Action<double?> pick, Action<string> hint, Func<double?> reference, bool common)
        {
            this.criterion = criterion; this.current = current; this.enabled = enabled; this.pick = pick; this.hint = hint; this.reference = reference; this.common = common;
            Focusable = true; Margin = new Thickness(2, 3, 2, 3); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(5);
            UniformGrid values = new UniformGrid { Columns = Data.Criteria[criterion].Options.Length, VerticalAlignment = VerticalAlignment.Center }; Child = values;
            foreach (AnswerOption option in Data.Criteria[criterion].Options)
            {
                AnswerOption selectedOption = option;
                Button b = new Button { Content = Calc.F(option.Value), Padding = new Thickness(0, 8, 0, 8), Margin = new Thickness(1, 0, 1, 0), FontSize = 12, Focusable = false, BorderThickness = new Thickness(1), BorderBrush = Theme.Line };
                string description = (common ? "Общий ответ · " : "") + Data.Criteria[criterion].Name + " · " + Calc.F(option.Value) + " — " + QuickInput.ShortLabel(criterion, option.Value) + "\n" + option.Text;
                string action = common ? "Значение получат все включённые направления, кроме тех, где ответ отличается от общего." : "Повторное нажатие снимает ответ.";
                b.ToolTip = new TextBlock { Text = description + "\n\n" + action, MaxWidth = 450, TextWrapping = TextWrapping.Wrap }; ToolTipService.SetInitialShowDelay(b, 180);
                b.MouseEnter += (s, e) => hint(description);
                b.Click += (s, e) => { if (!enabled()) return; double? now = current(); pick(!common && now.HasValue && now.Value == selectedOption.Value ? (double?)null : selectedOption.Value); Focus(); }; buttons.Add(b); values.Children.Add(b);
            }
            GotKeyboardFocus += (s, e) => { BorderBrush = Theme.B("#7AA7EE"); ShowCurrentHint(); };
            LostKeyboardFocus += (s, e) => BorderBrush = Brushes.Transparent;
            KeyDown += OnAnswerKey; Refresh();
        }
        void ShowCurrentHint()
        {
            double? now = current();
            AnswerOption option = Data.Criteria[criterion].Options.FirstOrDefault(x => now.HasValue && x.Value == now.Value);
            hint((common ? "Общий ответ · " : "") + Data.Criteria[criterion].Name + (option == null ? " — не оценено. Выберите 0, 5 или 1" + (criterion == 5 ? "; 3 — эпизодически" : "") + "." : " · " + Calc.F(option.Value) + " — " + option.Text));
        }
        void OnAnswerKey(object sender, KeyEventArgs e)
        {
            if (!enabled() || (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) != 0) return; double? value = null; bool recognized = true;
            if (e.Key == Key.D0 || e.Key == Key.NumPad0) value = 0;
            else if (e.Key == Key.D1 || e.Key == Key.NumPad1) value = 1;
            else if (e.Key == Key.D5 || e.Key == Key.NumPad5) value = .5;
            else if ((e.Key == Key.D3 || e.Key == Key.NumPad3) && criterion == 5) value = .3;
            else if (common || e.Key != Key.Delete && e.Key != Key.Back) recognized = false;
            if (!recognized) return; pick(value); e.Handled = true; ShowCurrentHint();
        }
        public void Refresh()
        {
            bool on = enabled(); double? now = current(), shared = reference == null ? null : reference();
            IsEnabled = on; Opacity = on ? 1 : .3;
            Background = on && now.HasValue && shared.HasValue && Math.Abs(now.Value - shared.Value) > 1e-10 ? DifferentBackground : Brushes.Transparent;
            ToolTip = Background == DifferentBackground ? "Отличается от общего ответа (" + Calc.F(shared.Value) + ")" : null;
            for (int i = 0; i < buttons.Count; i++)
            {
                bool selected = now.HasValue && now.Value == Data.Criteria[criterion].Options[i].Value;
                buttons[i].Background = selected ? Theme.Blue : Brushes.White; buttons[i].Foreground = selected ? Brushes.White : Theme.Muted;
                buttons[i].BorderBrush = selected ? Theme.Blue : Theme.Line;
            }
        }
    }
}
