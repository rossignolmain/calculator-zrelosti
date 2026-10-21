using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MaturityApp
{
    public partial class MainWindow
    {
        StackPanel planRows;

        void BuildAnalysis()
        {
            AssessmentAnalysis data = Analysis.Build(assessment);
            StackPanel body = PageStack(); body.Children.Add(Metrics());
            if (!data.HasScope) { body.Children.Add(Theme.Card(Theme.Text("Включите хотя бы одно направление на вкладке «Анкета». План появится автоматически.", 14))); return; }
            int unknown = data.Deficits.Count(d => d.Type == "assess"), known = data.Deficits.Count(d => d.Type == "improve");

            StackPanel planCard = new StackPanel();
            planCard.Children.Add(Theme.Text("Мероприятия по достижению целевых уровней (п. 35 н)", 17, null, true));
            TextBlock intro = Theme.Text("Сформированы из ответов анкеты: " + known + " недостатков к устранению, " + unknown + " видов требований ещё не оценено. Укажите ответственного и срок; общие значения подставляются во все пустые строки.", 12, Theme.Muted); intro.Margin = new Thickness(0, 6, 0, 10); planCard.Children.Add(intro);
            Grid defaults = new Grid(); defaults.ColumnDefinitions.Add(new ColumnDefinition()); defaults.ColumnDefinitions.Add(new ColumnDefinition());
            AddField(defaults, 0, 0, "Ответственный по умолчанию", assessment.PlanResponsible, value => { assessment.PlanResponsible = value; RefreshPlanHints(); });
            AddField(defaults, 0, 1, "Срок по умолчанию (например, «до 31.12.2027»)", assessment.PlanDeadline, value => { assessment.PlanDeadline = value; RefreshPlanHints(); });
            planCard.Children.Add(defaults);
            planRows = new StackPanel(); planCard.Children.Add(planRows); RebuildPlanRows(data);
            body.Children.Add(Theme.Card(planCard, new Thickness(16)));

            AddAnalysisHeading(body, "Общие действия для нескольких направлений", Analysis.PriorityExplanation);
            if (data.GroupedActions.Length == 0) body.Children.Add(Theme.Card(Theme.Text("Целевые уровни достигнуты по всем включённым направлениям.", 13)));
            else
            {
                StackPanel allGroups = new StackPanel();
                foreach (GroupedAction action in data.GroupedActions)
                {
                    StackPanel group = new StackPanel();
                    group.Children.Add(Theme.Text(action.Priority + ". " + action.Action + ": " + action.CriterionName + " · направлений: " + action.DirectionCount, 14, null, true));
                    TextBlock description = Theme.Text(action.ConditionText, 12, Theme.Muted); description.Margin = new Thickness(0, 8, 0, 8); group.Children.Add(description);
                    group.Children.Add(Theme.Text("Направления: " + String.Join(", ", action.DirectionIds) + (action.Required.HasValue ? " · для цели нужно не ниже " + Calc.F(action.Required.Value) : ""), 11, Theme.Blue));
                    Border card = Theme.Card(group, new Thickness(16)); card.Margin = new Thickness(0, 0, 0, 10); allGroups.Children.Add(card);
                }
                body.Children.Add(new Expander { Header = "Показать общие действия: " + data.GroupedActions.Length, Content = allGroups, Margin = new Thickness(0, 0, 0, 15) });
            }

            AddAnalysisHeading(body, "Профиль по видам требований", "Средняя оценка по заполненным ответам. Это вспомогательный показатель, он не заменяет Узи.");
            Grid profile = AnalysisGrid(new double[] { 2.2, 1, 1, 1, 1, 1.2 });
            TableRow(profile, new[] { "Вид требования", "Среднее 0–1", "Ответы", "Пусто", "Оценка 0", "Ниже цели" }, 0, true, null);
            int line = 1;
            foreach (CriterionProfile criterion in data.Criteria)
                TableRow(profile, new[] { criterion.Name, criterion.Average.HasValue ? Calc.F(criterion.Average.Value, "0.00") : "—", criterion.Answered + "/" + criterion.Included, criterion.Missing.ToString(), criterion.ZeroCount.ToString(), criterion.KnownDeficits.ToString() }, line++, false, null);
            body.Children.Add(new Expander { Header = "Показать профиль", Content = Theme.Card(profile, new Thickness(8)), Margin = new Thickness(0, 0, 0, 15) });

            AddAnalysisHeading(body, "Что нужно для следующего и целевого уровня", "Расчёт по обязательным значениям таблицы 3. Он не меняет ответы.");
            Grid roadmap = AnalysisGrid(new double[] { 3, .7, .7, 1.2, 1.3, 1.2 });
            TableRow(roadmap, new[] { "Направление", "Сейчас", "Цель", "Улучшить для цели", "Оценить для цели", "До следующего" }, 0, true, null); line = 1;
            foreach (DirectionRoadmap direction in data.Directions.Where(d => d.Included))
            {
                int id = direction.DirectionId;
                string next = direction.Next == null ? "Уровень 4" : "ур. " + direction.Next.Level + ": " + direction.Next.KnownChanges + " улучш., " + direction.Next.UnknownChanges + " оцен.";
                TableRow(roadmap, new[] { id + ". " + direction.DirectionName, direction.CurrentLevel.ToString(), direction.TargetLevel.ToString(), direction.Target.KnownChanges.ToString(), direction.Target.UnknownChanges.ToString(), next }, line++, false, () => { selected = id - 1; page = "assessment"; ShowPage(); });
            }
            body.Children.Add(new Expander { Header = "Показать по " + data.Included + " направлениям", Content = Theme.Card(roadmap, new Thickness(8)), Margin = new Thickness(0, 0, 0, 24) });
        }

        void RebuildPlanRows(AssessmentAnalysis data)
        {
            planRows.Children.Clear();
            DeficitRecord[] rows = ReportFacts.PlanRows(assessment, data);
            int unanswered = data.Deficits.Count(d => d.Type == "assess");
            if (unanswered > 0) { TextBlock note = Theme.Text("Не оценено видов требований: " + unanswered + ". Заполните анкету — до этого план неполный.", 12, Theme.B("#926415")); note.Margin = new Thickness(0, 8, 0, 4); planRows.Children.Add(note); }
            if (rows.Length == 0) { planRows.Children.Add(unanswered > 0 ? Theme.Text("Мероприятий по заполненным ответам пока нет.", 12, Theme.Muted) : Theme.Text("Мероприятия не требуются: цели достигнуты. По п. 35 Методики при достижении целей определяются новые целевые уровни.", 12)); return; }
            Grid head = RowGrid(2.3, 3.4, 1.6, 1.1); head.Margin = new Thickness(0, 6, 0, 4);
            string[] titles = { "Направление и вид требования", "Мероприятие", "Ответственный", "Срок" };
            for (int i = 0; i < titles.Length; i++) { TextBlock t = Theme.Text(titles[i], 11, Theme.Muted); Grid.SetColumn(t, i); head.Children.Add(t); }
            planRows.Children.Add(head);
            foreach (DeficitRecord task in rows)
            {
                DeficitRecord d = task; Grid row = RowGrid(2.3, 3.4, 1.6, 1.1); row.Margin = new Thickness(0, 0, 0, 8);
                TextBlock what = Theme.Text(d.DirectionId + ". " + d.DirectionName + "\n" + (d.CriterionName ?? "Сумма баллов направления") + " · " + (d.Type == "assess" ? "оценить" : d.Type == "score" ? "сумма баллов" : "улучшить"), 11.5); Put(row, what, 0);
                TextBlock action = Theme.Text(d.ConditionText ?? d.Action ?? "", 11.5, Theme.Muted); Put(row, action, 1);
                PlanItem current = assessment.PlanItems.FirstOrDefault(x => x.Key == d.Key);
                Put(row, PlanBox(current == null ? "" : current.Responsible, assessment.PlanResponsible, v => SetPlan(d.Key, v, null), "responsible"), 2);
                Put(row, PlanBox(current == null ? "" : current.Deadline, assessment.PlanDeadline, v => SetPlan(d.Key, null, v), "deadline"), 3);
                planRows.Children.Add(row);
            }
            RefreshPlanHints();
        }
        TextBox PlanBox(string value, string fallback, Action<string> set, string kind)
        {
            TextBox box = new TextBox { Text = value, Height = 30, Padding = new Thickness(6, 4, 6, 4), VerticalAlignment = VerticalAlignment.Top, Tag = "plan:" + kind };
            box.TextChanged += (s, e) => { if (!building) { set(box.Text); RefreshPlanHints(); } }; return box;
        }
        void SetPlan(string key, string responsible, string deadline)
        {
            PlanItem item = assessment.PlanItems.FirstOrDefault(x => x.Key == key);
            if (item == null) { item = new PlanItem { Key = key }; assessment.PlanItems = assessment.PlanItems.Concat(new[] { item }).ToArray(); }
            if (responsible != null) item.Responsible = responsible;
            if (deadline != null) item.Deadline = deadline;
            assessment.PlanItems = assessment.PlanItems.Where(x => x.Responsible.Length > 0 || x.Deadline.Length > 0).ToArray();
            Changed();
        }
        // An empty row shows the default as a hint, so the user sees what goes into the report.
        void RefreshPlanHints()
        {
            if (planRows == null) return;
            foreach (TextBox box in planRows.Children.OfType<Grid>().SelectMany(g => g.Children.OfType<TextBox>()))
            {
                string fallback = (string)box.Tag == "plan:responsible" ? assessment.PlanResponsible : assessment.PlanDeadline;
                box.ToolTip = box.Text.Length == 0 ? (fallback.Length > 0 ? "Будет указано: " + fallback : "Не указано") : null;
                box.Background = box.Text.Length == 0 && fallback.Length == 0 ? Theme.B("#FFF6E8") : System.Windows.Media.Brushes.White;
            }
        }
        static Grid AnalysisGrid(double[] weights)
        {
            Grid grid = new Grid(); foreach (double weight in weights) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(weight, GridUnitType.Star) }); return grid;
        }
        static void AddAnalysisHeading(StackPanel body, string title, string description)
        {
            TextBlock heading = Theme.Text(title, 19, null, true); heading.Margin = new Thickness(0, 23, 0, 8); body.Children.Add(heading);
            TextBlock help = Theme.Text(description, 12, Theme.Muted); help.Margin = new Thickness(0, 0, 0, 12); body.Children.Add(help);
        }
    }
}
