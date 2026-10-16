using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MaturityApp
{
    public partial class MainWindow
    {
        void BuildAnalysis()
        {
            AssessmentAnalysis data = Analysis.Build(assessment);
            StackPanel body = PageStack(); body.Children.Add(Metrics());
            if (!data.HasScope) { body.Children.Add(Theme.Card(Theme.Text("Включите хотя бы одно направление на вкладке «Оценка». Аналитика появится автоматически.", 14))); return; }
            int unknown = data.Deficits.Count(d => d.Type == "assess"), known = data.Deficits.Count(d => d.Type == "improve");
            StackPanel summary = new StackPanel(); summary.Children.Add(Theme.Text("По тем же ответам: " + known + " подтверждённых дефицитов · " + unknown + " неоценённых критериев", 16, null, true));
            TextBlock intro = Theme.Text("Сводка и черновик плана рассчитаны автоматически. Неоценённые условия отделены от подтверждённых недостатков. Ответственных и сроки можно заполнить в выгруженной таблице.", 12, Theme.Muted); intro.Margin = new Thickness(0, 9, 0, 0); summary.Children.Add(intro); body.Children.Add(Theme.Card(summary));

            AddAnalysisHeading(body, "Профиль по восьми критериям", "Среднее считается только по данным с выбранной оценкой. Оно не заменяет показатель Узи; рядом показана полнота заполнения.");
            Grid profile = AnalysisGrid(new double[] { 2.2, 1, 1, 1, 1, 1.2 });
            TableRow(profile, new[] { "Критерий", "Среднее 0–1", "Ответы", "Пусто", "Оценка 0", "Ниже цели" }, 0, true, null);
            int line = 1;
            foreach (CriterionProfile criterion in data.Criteria)
                TableRow(profile, new[] { criterion.Name, criterion.Average.HasValue ? Calc.F(criterion.Average.Value, "0.00") : "—", criterion.Answered + "/" + criterion.Included, criterion.Missing.ToString(), criterion.ZeroCount.ToString(), criterion.KnownDeficits.ToString() }, line++, false, null);
            body.Children.Add(Theme.Card(profile, new Thickness(8)));

            AddAnalysisHeading(body, "Общие действия для нескольких направлений", Analysis.PriorityExplanation);
            if (data.GroupedActions.Length == 0) body.Children.Add(Theme.Card(Theme.Text("По введённым ответам целей достигли все включённые направления. Поддерживайте процессы и подтверждающие материалы.", 13)));
            else
            {
                StackPanel allGroups = new StackPanel();
                foreach (GroupedAction action in data.GroupedActions)
                {
                    StackPanel group = new StackPanel();
                    group.Children.Add(Theme.Text(action.Priority + ". " + action.Action + ": " + action.CriterionName + " · направлений: " + action.DirectionCount, 14, null, true));
                    TextBlock description = Theme.Text(action.ConditionText, 12, Theme.Muted); description.Margin = new Thickness(0, 8, 0, 8); group.Children.Add(description);
                    group.Children.Add(Theme.Text("Направления: " + String.Join(", ", action.DirectionIds) + (action.Required.HasValue ? " · условие цели: оценка не ниже " + Calc.F(action.Required.Value) : " · без установленного минимума на этой цели"), 11, Theme.Blue));
                    Border card = Theme.Card(group, new Thickness(16)); card.Margin = new Thickness(0, 0, 0, 10); allGroups.Children.Add(card);
                }
                Expander groups = new Expander { Header = "Показать общий план: " + data.GroupedActions.Length + " групп действий", Content = allGroups, IsExpanded = true, Margin = new Thickness(0, 0, 0, 15) }; body.Children.Add(groups);
            }

            AddAnalysisHeading(body, "Что нужно для следующего и целевого уровня", "Это сценарий по обязательным минимумам таблицы 3. Он не изменяет ответы и не подтверждает выполнение мероприятий. Для неизвестных условий сначала нужна оценка.");
            Grid roadmap = AnalysisGrid(new double[] { 3, .7, .7, 1.2, 1.3, 1.2 });
            TableRow(roadmap, new[] { "Направление", "Сейчас", "Цель", "Улучшить для цели", "Оценить для цели", "До следующего" }, 0, true, null); line = 1;
            foreach (DirectionRoadmap direction in data.Directions.Where(d => d.Included))
            {
                int id = direction.DirectionId;
                string next = direction.Next == null ? "Уровень 4" : "ур. " + direction.Next.Level + ": " + direction.Next.KnownChanges + " улучш., " + direction.Next.UnknownChanges + " оцен.";
                TableRow(roadmap, new[] { id + ". " + direction.DirectionName, direction.CurrentLevel.ToString(), direction.TargetLevel.ToString(), direction.Target.KnownChanges.ToString(), direction.Target.UnknownChanges.ToString(), next }, line++, false, () => { selected = id - 1; page = "assessment"; ShowPage(); });
            }
            body.Children.Add(new Expander { Header = "Показать маршруты по " + data.Included + " направлениям", Content = Theme.Card(roadmap, new Thickness(8)), Margin = new Thickness(0, 0, 0, 18) });

            DeficitRecord[] tasks = data.Deficits.Where(d => d.CriterionIndex.HasValue).OrderBy(d => d.DirectionId).ThenBy(d => d.CriterionIndex).ToArray();
            Grid plan = AnalysisGrid(new double[] { 2.2, 1.1, .65, .65, .8, 3 });
            TableRow(plan, new[] { "Направление", "Критерий", "Сейчас", "Для цели", "Действие", "Условие / мероприятие" }, 0, true, null); line = 1;
            foreach (DeficitRecord task in tasks)
            {
                int id = task.DirectionId;
                TableRow(plan, new[] { id + ". " + task.DirectionName, task.CriterionName, task.Current.HasValue ? Calc.F(task.Current.Value) : "Не оценено", task.Required.HasValue ? Calc.F(task.Required.Value) : "—", task.Action, task.ConditionText }, line++, false, () => { selected = id - 1; page = "assessment"; ShowPage(); });
            }
            body.Children.Add(new Expander { Header = "Подробный черновик плана: " + tasks.Length + " пунктов", Content = Theme.Card(plan, new Thickness(8)), Margin = new Thickness(0, 0, 0, 20) });
            TextBlock exported = Theme.Text("В Word добавлены профиль и план, в Excel — три новых листа: «Профиль критериев», «План улучшений», «Общие дефициты». Они входят в обычный комплект, дополнительных полей для их создания нет.", 12, Theme.Muted); exported.Margin = new Thickness(0, 0, 0, 14); body.Children.Add(exported);
            Button save = Theme.Button("Сохранить комплект с аналитикой", () => SaveBundleFast(false), true); save.HorizontalAlignment = HorizontalAlignment.Left; save.Margin = new Thickness(0, 0, 0, 24); body.Children.Add(save);
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
