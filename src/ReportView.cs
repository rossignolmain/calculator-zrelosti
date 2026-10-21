using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Microsoft.Win32;

namespace MaturityApp
{
    // Inputs for section VI of the methodology (item 35 а-н). Lists are ticked,
    // not typed; people default to the operator's organization.
    public partial class MainWindow
    {
        TextBlock reportMissing, previousText;
        StackPanel participantRows;

        void BuildReportData(StackPanel body)
        {
            StackPanel panel = new StackPanel();
            panel.Children.Add(Theme.Text("Сведения для отчёта по методике (раздел VI, п. 35)", 18, null, true));
            reportMissing = Theme.Text("", 12, Theme.B("#926415")); reportMissing.Margin = new Thickness(0, 8, 0, 4); panel.Children.Add(reportMissing);

            panel.Children.Add(Section("Вид оценки и лицо, проводившее оценку (п. 6, п. 35 в)"));
            Grid who = new Grid(); who.ColumnDefinitions.Add(new ColumnDefinition()); who.ColumnDefinitions.Add(new ColumnDefinition());
            StackPanel kindField = new StackPanel { Margin = new Thickness(0, 0, 18, 14) }; kindField.Children.Add(Theme.Text("Вид оценки", 12, Theme.Muted));
            ComboBox kind = new ComboBox { Margin = new Thickness(0, 7, 0, 0), Height = 32, Tag = "Вид оценки" }; kind.Items.Add("Внутренняя (самостоятельная)"); kind.Items.Add("Внешняя (лицензиат ФСТЭК России)"); kind.SelectedIndex = assessment.AssessmentKind == "external" ? 1 : 0;
            kindField.Children.Add(kind); who.Children.Add(kindField);
            StackPanel licenseField = new StackPanel { Margin = new Thickness(0, 0, 0, 14), Visibility = assessment.AssessmentKind == "external" ? Visibility.Visible : Visibility.Collapsed }; licenseField.Children.Add(Theme.Text("Лицензия оценивающей организации (номер, дата)", 12, Theme.Muted));
            TextBox license = new TextBox { Text = assessment.ExternalLicense, Margin = new Thickness(0, 7, 0, 0) }; license.TextChanged += (s, e) => { assessment.ExternalLicense = license.Text; ReportChanged(); }; licenseField.Children.Add(license); Grid.SetColumn(licenseField, 1); who.Children.Add(licenseField);
            kind.SelectionChanged += (s, e) => { assessment.AssessmentKind = kind.SelectedIndex == 1 ? "external" : "internal"; licenseField.Visibility = kind.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed; ReportChanged(); };
            panel.Children.Add(who);
            Grid assessor = new Grid(); assessor.ColumnDefinitions.Add(new ColumnDefinition()); assessor.ColumnDefinitions.Add(new ColumnDefinition());
            AddField(assessor, 0, 0, "Организация лица, проводившего оценку", assessment.AssessorOrganization, value => { assessment.AssessorOrganization = value; ReportChanged(); });
            AddField(assessor, 0, 1, "Контакты (телефон, e-mail)", assessment.AssessorContact, value => { assessment.AssessorContact = value; ReportChanged(); });
            panel.Children.Add(assessor);
            Button sameOrg = Theme.Button("Организация = оператор", () => { assessment.AssessorOrganization = assessment.Organization; Changed(); ShowPage(); }); sameOrg.HorizontalAlignment = HorizontalAlignment.Left; sameOrg.Padding = new Thickness(10, 5, 10, 5); sameOrg.FontSize = 11; sameOrg.ToolTip = "Для внутренней оценки: подставить наименование оператора."; panel.Children.Add(sameOrg);

            panel.Children.Add(Section("Другие лица: проводившие оценку, участвовавшие, предоставлявшие исходные данные (п. 35 в, г)"));
            participantRows = new StackPanel(); panel.Children.Add(participantRows); RebuildParticipants();
            WrapPanel addPeople = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            addPeople.Children.Add(Small(Theme.Button("+ Предоставлял исходные данные", () => AddParticipant("source"))));
            addPeople.Children.Add(Small(Theme.Button("+ Участвовал в оценке", () => AddParticipant("participant"))));
            addPeople.Children.Add(Small(Theme.Button("+ Ещё один оценщик", () => AddParticipant("assessor"))));
            panel.Children.Add(addPeople);

            panel.Children.Add(Section("Действующие требования по защите информации (п. 35 а)"));
            panel.Children.Add(KeyChecks(ReportCatalog.Requirements, () => assessment.Requirements, v => assessment.Requirements = v, 1));

            panel.Children.Add(Section("Исходные данные (п. 19, п. 35 е)"));
            panel.Children.Add(KeyChecks(ReportCatalog.SourceData, () => assessment.SourceData, v => assessment.SourceData = v, 2));
            panel.Children.Add(Theme.Text("Уточнение (реквизиты ключевых документов) — необязательно", 12, Theme.Muted));
            TextBox sourceNote = new TextBox { Text = assessment.SourceDataNote, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 44, Margin = new Thickness(0, 6, 0, 0) }; sourceNote.TextChanged += (s, e) => { assessment.SourceDataNote = sourceNote.Text; ReportChanged(); }; panel.Children.Add(sourceNote);

            panel.Children.Add(Section("Методы и инструменты оценки (п. 20, 23, п. 35 ж)"));
            panel.Children.Add(KeyChecks(ReportCatalog.Methods, () => assessment.Methods, v => assessment.Methods = v, 2));
            panel.Children.Add(KeyChecks(ReportCatalog.Instruments, () => assessment.Instruments, v => assessment.Instruments = v, 2));
            panel.Children.Add(Theme.Text("Названия применённых средств (сканеры, SIEM и т. п.) — необязательно", 12, Theme.Muted));
            TextBox tools = new TextBox { Text = assessment.ToolsNote, Margin = new Thickness(0, 6, 0, 0) }; tools.TextChanged += (s, e) => { assessment.ToolsNote = tools.Text; ReportChanged(); }; panel.Children.Add(tools);

            panel.Children.Add(Section("Результаты предыдущей оценки (п. 33, п. 35 д)"));
            previousText = Theme.Text("", 12); panel.Children.Add(previousText); RefreshPrevious();
            WrapPanel previousButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            previousButtons.Children.Add(Small(Theme.Button("Загрузить из прежнего проекта или комплекта…", LoadPrevious, true)));
            previousButtons.Children.Add(Small(Theme.Button("Ввести вручную…", EnterPreviousManually)));
            previousButtons.Children.Add(Small(Theme.Button("Предыдущей оценки не было", () => { assessment.Previous = null; Changed(); RefreshPrevious(); RefreshReportMissing(); })));
            panel.Children.Add(previousButtons);

            panel.Children.Add(Section("Мероприятия плана: ответственный и срок (п. 35 н)"));
            Grid plan = new Grid(); plan.ColumnDefinitions.Add(new ColumnDefinition()); plan.ColumnDefinitions.Add(new ColumnDefinition());
            AddField(plan, 0, 0, "Ответственный (подразделение или работник)", assessment.PlanResponsible, value => { assessment.PlanResponsible = value; ReportChanged(); });
            AddField(plan, 0, 1, "Срок выполнения (например, «до 31.12.2027»)", assessment.PlanDeadline, value => { assessment.PlanDeadline = value; ReportChanged(); });
            panel.Children.Add(plan);
            panel.Children.Add(Theme.Text("Значения подставляются во все строки плана в Word и Excel; отдельные строки можно уточнить в Excel.", 11, Theme.Muted));

            Border card = Theme.Card(panel); card.Margin = new Thickness(0, 18, 0, 0); body.Children.Add(card);
            RefreshReportMissing();
        }

        static TextBlock Section(string title) { TextBlock t = Theme.Text(title, 14, null, true); t.Margin = new Thickness(0, 18, 0, 8); return t; }
        static Button Small(Button b) { b.Padding = new Thickness(10, 5, 10, 5); b.FontSize = 11; b.Margin = new Thickness(0, 0, 8, 6); return b; }
        void ReportChanged() { Changed(); RefreshReportMissing(); }
        void RefreshReportMissing()
        {
            if (reportMissing == null) return;
            string[] missing = ReportFacts.Missing(assessment);
            reportMissing.Text = missing.Length == 0 ? "Все сведения раздела VI заполнены." : "Для полного отчёта осталось:\n• " + String.Join("\n• ", missing);
            reportMissing.Foreground = missing.Length == 0 ? Theme.B("#278458") : Theme.B("#926415");
        }

        UIElement KeyChecks(CatalogItem[] catalog, Func<string[]> get, Action<string[]> set, int columns)
        {
            UniformGrid grid = new UniformGrid { Columns = columns, Margin = new Thickness(0, 0, 0, 8) };
            foreach (CatalogItem item in catalog)
            {
                CatalogItem current = item;
                CheckBox box = new CheckBox { Content = new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap }, IsChecked = get().Contains(item.Key), Margin = new Thickness(0, 3, 14, 3), Tag = "report:" + item.Key };
                box.Checked += (s, e) => { if (building) return; set(catalog.Where(x => x.Key == current.Key || get().Contains(x.Key)).Select(x => x.Key).ToArray()); ReportChanged(); };
                box.Unchecked += (s, e) => { if (building) return; set(get().Where(x => x != current.Key).ToArray()); ReportChanged(); };
                grid.Children.Add(box);
            }
            return grid;
        }

        void AddParticipant(string role)
        {
            List<ReportPerson> people = assessment.Participants.ToList();
            people.Add(new ReportPerson { Role = role, Organization = role == "assessor" ? assessment.AssessorOrganization : assessment.Organization });
            assessment.Participants = people.ToArray(); Changed(); RebuildParticipants(); RefreshReportMissing();
        }
        void RebuildParticipants()
        {
            if (participantRows == null) return;
            participantRows.Children.Clear();
            if (assessment.Participants.Length == 0) { participantRows.Children.Add(Theme.Text("Пока никого. Добавьте работников, которые предоставляли документы и участвовали в опросе.", 11, Theme.Muted)); return; }
            Grid head = PeopleGrid();
            string[] titles = { "Роль", "ФИО", "Должность", "Организация", "Контакты", "" };
            for (int i = 0; i < titles.Length; i++) { TextBlock t = Theme.Text(titles[i], 11, Theme.Muted); Grid.SetColumn(t, i); head.Children.Add(t); }
            participantRows.Children.Add(head);
            foreach (ReportPerson person in assessment.Participants)
            {
                ReportPerson p = person; Grid row = PeopleGrid(); row.Margin = new Thickness(0, 4, 0, 0);
                ComboBox role = new ComboBox { Height = 30, FontSize = 11 }; role.Items.Add("Предоставлял данные"); role.Items.Add("Участвовал"); role.Items.Add("Оценщик");
                role.SelectedIndex = p.Role == "source" ? 0 : p.Role == "participant" ? 1 : 2;
                role.SelectionChanged += (s, e) => { p.Role = role.SelectedIndex == 0 ? "source" : role.SelectedIndex == 1 ? "participant" : "assessor"; ReportChanged(); }; row.Children.Add(role);
                AddPersonBox(row, 1, p.Name, v => p.Name = v); AddPersonBox(row, 2, p.Position, v => p.Position = v);
                AddPersonBox(row, 3, p.Organization, v => p.Organization = v); AddPersonBox(row, 4, p.Contact, v => p.Contact = v);
                Button remove = new Button { Content = "✕", Width = 28, Height = 28, Background = System.Windows.Media.Brushes.Transparent, Foreground = Theme.Muted, ToolTip = "Убрать" };
                remove.Click += (s, e) => { assessment.Participants = assessment.Participants.Where(x => !Object.ReferenceEquals(x, p)).ToArray(); Changed(); RebuildParticipants(); RefreshReportMissing(); };
                Grid.SetColumn(remove, 5); row.Children.Add(remove);
                participantRows.Children.Add(row);
            }
        }
        static Grid PeopleGrid()
        {
            Grid g = new Grid();
            foreach (double w in new[] { 1.1, 1.4, 1.3, 1.3, 1.2 }) g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) }); return g;
        }
        void AddPersonBox(Grid row, int column, string value, Action<string> set)
        {
            TextBox box = new TextBox { Text = value, Height = 30, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(5, 4, 5, 4) };
            box.TextChanged += (s, e) => { set(box.Text); ReportChanged(); }; Grid.SetColumn(box, column); row.Children.Add(box);
        }

        void RefreshPrevious()
        {
            if (previousText == null) return;
            PreviousResult p = assessment.Previous;
            if (p == null) { previousText.Text = "Не указана: в отчёте будет «предыдущая оценка не проводилась»."; return; }
            DynamicsResult d = ReportFacts.Dynamics(assessment);
            previousText.Text = "Предыдущая оценка от " + DateTime.ParseExact(p.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToString("dd.MM.yyyy") + ": Узи " + Calc.F(p.Uzi, "0.00") + (p.Directions == null ? " (только итог)" : " (по 21 направлению)") + ".\nДинамика (п. 33): " + d.Title.ToLower() + ". " + d.Explanation;
        }
        void LoadPrevious()
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Предыдущая оценка этого объекта", Filter = "Проект или комплект (*.uzi.json;*.zip)|*.uzi.json;*.zip|Все файлы (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            try { ApplyPrevious(ProjectTools.LoadAssessmentFile(dialog.FileName), Path.GetFileName(dialog.FileName)); }
            catch (Exception ex) { ShowError("Не удалось загрузить предыдущую оценку", ex); }
        }
        // Loading a previous assessment also records that it was analysed (п. 19 ж, п. 23 в).
        void ApplyPrevious(Assessment old, string source)
        {
            assessment.Previous = ReportFacts.FromAssessment(old, source);
            if (!assessment.SourceData.Contains("zh")) assessment.SourceData = ReportCatalog.SourceData.Where(x => x.Key == "zh" || assessment.SourceData.Contains(x.Key)).Select(x => x.Key).ToArray();
            if (!assessment.Methods.Contains("previous")) assessment.Methods = ReportCatalog.Methods.Where(x => x.Key == "previous" || assessment.Methods.Contains(x.Key)).Select(x => x.Key).ToArray();
            Changed(); ShowPage();
            status.Text = "Загружена предыдущая оценка от " + assessment.Previous.Date + ". Отмечены исходные данные «результаты последней оценки» и метод «анализ предыдущей оценки».";
        }
        void EnterPreviousManually()
        {
            Window w = new Window { Title = "Предыдущая оценка", Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = Theme.Paper };
            StackPanel p = new StackPanel { Margin = new Thickness(16) };
            p.Children.Add(Theme.Text("Дата предыдущей оценки", 12, Theme.Muted));
            DatePicker date = new DatePicker { SelectedDate = DateTime.Today.AddYears(-1), Margin = new Thickness(0, 6, 0, 12) }; p.Children.Add(date);
            p.Children.Add(Theme.Text("Достигнутое значение Узи (0–4)", 12, Theme.Muted));
            TextBox uzi = new TextBox { Margin = new Thickness(0, 6, 0, 14) }; p.Children.Add(uzi);
            p.Children.Add(Theme.Button("Сохранить", () =>
            {
                double value;
                string text = uzi.Text.Trim().Replace(',', '.');
                if (!date.SelectedDate.HasValue || !Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || value < 0 || value > 4) { MessageBox.Show(w, "Укажите дату и значение Узи от 0 до 4.", "Предыдущая оценка"); return; }
                assessment.Previous = new PreviousResult { Date = date.SelectedDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Uzi = Math.Round(value, 2), Source = "введено вручную" };
                w.DialogResult = true;
            }, true));
            w.Content = p;
            if (w.ShowDialog() == true) { Changed(); RefreshPrevious(); RefreshReportMissing(); }
        }
    }
}
