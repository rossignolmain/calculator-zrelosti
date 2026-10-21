using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MaturityApp
{
    // Exercises only this application's in-memory controls. No Window.Show,
    // Application.Run, native input, screenshots, external programs or user data.
    public static class UiVerification
    {
        public sealed class Result
        {
            public bool pass, headlessOnly = true;
            public int tests, assertions;
            public string startedAtUtc, finishedAtUtc;
            public List<string> completedTests = new List<string>();
            public List<string> errors = new List<string>();
            public List<string> generatedFiles = new List<string>();
        }
        static Result report;
        static string outputFolder;
        static readonly List<MainWindow> windows = new List<MainWindow>();
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static int Run(string[] args)
        {
            report = new Result { startedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
            outputFolder = Path.GetFullPath(args.Length > 1 ? args[1] : "work/ui-verification");
            Directory.CreateDirectory(outputFolder);
            try
            {
                Test("quick table starts with 168 unanswered cells and fits 1100px", DefaultsAndLayout);
                Test("one click records an explicit zero and a second click clears it", OneClickAnswers);
                Test("category changes targets without changing any answer", CategoryAndTargets);
                Test("exclusion preserves answers and disables answer controls", ScopePreservesAnswers);
                Test("copy source follows scope changes and undo is wired", CopyScopeAndUndo);
                Test("detailed view stays accessible and manual changes survive undo", DetailedAndUndo);
                Test("incomplete and search filters preserve answer data", Filters);
                Test("all pages including derived analysis preserve answers", AllPages);
                Test("mass criteria input protects existing grades and supports overwrite undo", MassValueInput);
                Test("mass visible scope and parameters never invent answers", MassVisibleScope);
                Test("object page describes any system with lists and never invents facts", ObjectPage);
                Test("object types set the minimum goal and lock the category field", ObjectTypesOnPage);
                Test("header offers generic commands and tabs", HeaderAndTabs);
                Test("fast bundle exports and updates the preset ZIP without dialogs", FastBundle);
                Test("opened bundle stays associated with ZIP for the next one click save", ReopenedBundle);
                Test("empty scope disables the fast bundle button", EmptyScope);
                Test("common row fills 168 cells in 8 clicks and keeps individual differences", CommonRow);
                Test("passport exclusion is an explicit command and keeps answers", PassportExclusion);
                Test("answer template carries grades, scope and goals but no requisites", AnswerTemplateRoundTrip);
                Test("report follows item 35 а-н and flags missing data", ReportStructure);
                Test("dynamics follow item 33 of the methodology", DynamicsRules);
                Test("template library saves, applies by parts, renames, replaces and trashes", Library);
                Test("old projects open and report data is validated", ReportDataCompatibility);
                Test("organization profile carries contacts and participants", ProfileWithPeople);
                Test("report data page edits the model", ReportPage);
            }
            finally
            {
                // Prevent the production close handler from asking to save test windows.
                foreach (MainWindow window in windows) Set(window, "dirty", false);
                report.finishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                report.pass = report.errors.Count == 0;
                string json = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(report);
                File.WriteAllText(Path.Combine(outputFolder, "ui-verification.json"), json, new UTF8Encoding(false));
            }
            return report.pass ? 0 : 1;
        }
        static void Test(string name, Action action)
        {
            report.tests++;
            try { action(); report.completedTests.Add(name); }
            catch (Exception ex)
            {
                while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
                report.errors.Add(name + ": " + ex.GetType().Name + ": " + ex.Message);
            }
        }
        static void Assert(bool condition, string message)
        {
            report.assertions++; if (!condition) throw new InvalidOperationException(message);
        }
        static MainWindow Create(Assessment model)
        {
            MainWindow window = new MainWindow(model, false); windows.Add(window); Layout(window);
            Assert(!window.IsVisible, "A test window became visible."); return window;
        }
        static void Layout(MainWindow window)
        {
            FrameworkElement content = (FrameworkElement)window.Content;
            content.Width = 1100; content.Height = 760;
            content.Measure(new Size(1100, 760)); content.Arrange(new Rect(0, 0, 1100, 760)); content.UpdateLayout();
        }
        static object Get(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            return field.GetValue(target);
        }
        static void Set(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, PrivateInstance);
            if (field == null) throw new MissingFieldException(target.GetType().Name, name);
            field.SetValue(target, value);
        }
        static object Call(object target, string name, params object[] args)
        {
            MethodInfo method = target.GetType().GetMethod(name, PrivateInstance);
            if (method == null) throw new MissingMethodException(target.GetType().Name, name);
            return method.Invoke(target, args);
        }
        static List<QuickRowView> Rows(MainWindow window) { return (List<QuickRowView>)Get(window, "quickRows"); }
        static void Click(Button button)
        {
            Assert(button.IsEnabled, "Attempted to invoke a disabled control.");
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        }
        static Button AnswerButton(QuickAnswerCell cell, int criterion, double value)
        {
            int index = Array.FindIndex(Data.Criteria[criterion].Options, x => x.Value == value);
            return ((Panel)cell.Child).Children.OfType<Button>().ElementAt(index);
        }
        static CheckBox Scope(QuickRowView row) { return row.Root.Children.OfType<CheckBox>().Single(); }
        static string Answers(Assessment a) { return String.Join("|", a.Directions.Select(d => String.Join(",", d.Answers.Select(x => x.HasValue ? x.Value.ToString("R", CultureInfo.InvariantCulture) : "_")))); }
        static IEnumerable<DependencyObject> Tree(DependencyObject root)
        {
            HashSet<DependencyObject> seen = new HashSet<DependencyObject>();
            Stack<DependencyObject> pending = new Stack<DependencyObject>(); pending.Push(root);
            while (pending.Count > 0)
            {
                DependencyObject current = pending.Pop(); if (!seen.Add(current)) continue; yield return current;
                foreach (object child in LogicalTreeHelper.GetChildren(current)) { DependencyObject d = child as DependencyObject; if (d != null) pending.Push(d); }
                if (current is Visual) for (int i = 0; i < VisualTreeHelper.GetChildrenCount(current); i++) pending.Push(VisualTreeHelper.GetChild(current, i));
            }
        }
        static Button CopyButton(MainWindow window) { return Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "").StartsWith("Повторить в пустых")); }
        static ComboBox Source(MainWindow window) { return (ComboBox)Get(window, "quickCopySource"); }
        static void Page(MainWindow window, string page) { Set(window, "page", page); Call(window, "ShowPage"); Layout(window); }

        static void DefaultsAndLayout()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a);
            Assert((string)Get(window, "page") == "quick", "Quick assessment is not the initial page.");
            Assert(Rows(window).Count == 21, "The quick table must contain 21 directions.");
            Assert(Tree((DependencyObject)window.Content).OfType<QuickAnswerCell>().Count() == 168 + 8, "The attached control tree must contain 168 answer cells and 8 common cells.");
            Assert(a.Directions.All(d => d.Answers.All(x => !x.HasValue)), "Creating or measuring the UI filled answers.");
            Assert(window.MinWidth <= 1100, "The declared minimum width exceeds 1100px.");
            QuickRowView first = Rows(window)[0]; FrameworkElement content = (FrameworkElement)window.Content;
            Rect targetBounds = first.Target.TransformToAncestor(content).TransformBounds(new Rect(new Point(), first.Target.RenderSize));
            Assert(targetBounds.Right <= content.ActualWidth && targetBounds.Left >= 0, "The target column is clipped at 1100px.");
        }
        static void OneClickAnswers()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a); QuickAnswerCell cell = Rows(window)[0].Cells[0];
            Button zero = AnswerButton(cell, 0, 0); Click(zero);
            Assert(a.Directions[0].Answers[0] == 0, "Clicking zero did not record an explicit answer.");
            Assert(Calc.Summary(a).Answered == 1, "Explicit zero was not counted as answered.");
            Click(zero); Assert(!a.Directions[0].Answers[0].HasValue, "The second click did not clear the answer.");
            Click(AnswerButton(cell, 0, .5)); Click(AnswerButton(cell, 0, 1));
            Assert(a.Directions[0].Answers[0] == 1, "Changing an answer did not replace the selected value.");
            Click(AnswerButton(Rows(window)[0].Cells[5], 5, .3));
            Assert(a.Directions[0].Answers[5] == .3, "The education criterion lost the 0.3 option.");
            Assert(Calc.Summary(a).Answered == 2, "A click changed an unrelated answer.");
        }
        static void CategoryAndTargets()
        {
            Assessment a = Assessment.Create(); a.Directions[3].Answers[2] = .5;
            MainWindow window = Create(a); string before = Answers(a);
            ComboBox category = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(x => x.Items.Count == 4 && x.Items[0] is string);
            category.SelectedIndex = 3;
            Assert(a.Category == 3 && a.Directions.All(d => d.Target == 3), "Category 3 did not set all goals to 3.");
            Assert(Answers(a) == before, "Changing category changed answers.");
            Assert(Rows(window).All(r => !((ComboBoxItem)r.Target.Items[0]).IsEnabled && !((ComboBoxItem)r.Target.Items[1]).IsEnabled), "Goals below the category minimum remain enabled.");
            Rows(window)[0].Target.SelectedIndex = 3;
            Assert(a.Directions[0].Target == 4 && a.Directions.Skip(1).All(d => d.Target == 3), "An individual target changed another direction.");
            category.SelectedIndex = 1;
            Assert(a.Directions.All(d => d.Target == 1) && Answers(a) == before, "Lowering category failed to reset goals while preserving answers.");
        }
        static void ScopePreservesAnswers()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[0] = .5; MainWindow window = Create(a); QuickRowView row = Rows(window)[0];
            Scope(row).IsChecked = false;
            Assert(!a.Directions[0].Included && a.Directions[0].Answers[0] == .5, "Excluding a direction deleted its answers.");
            Assert(row.Cells.All(c => !c.IsEnabled) && !row.Target.IsEnabled, "Excluded controls are still enabled.");
            Assert(Calc.Summary(a).Total == 160, "Excluded direction is still in the summary denominator.");
            Scope(row).IsChecked = true;
            Assert(a.Directions[0].Answers[0] == .5 && Calc.Summary(a).Total == 168, "Reincluding failed to restore the preserved answer/scope.");
        }
        static void CopyScopeAndUndo()
        {
            Assessment a = Assessment.Create(); foreach (DirectionAssessment d in a.Directions) d.Included = false;
            MainWindow window = Create(a);
            Assert(Source(window).Items.Count == 21, "Source choices must retain all direction IDs while scope changes.");
            Scope(Rows(window)[20]).IsChecked = true; Click(AnswerButton(Rows(window)[20].Cells[0], 0, 1));
            Scope(Rows(window)[0]).IsChecked = true; Source(window).SelectedIndex = 20;
            Assert(((ComboBoxItem)Source(window).SelectedItem).IsEnabled, "A newly included direction is missing from enabled sources.");
            Click(CopyButton(window));
            Assert(a.Directions[0].Answers[0] == 1 && a.Directions[20].Answers[0] == 1, "Copy did not use the newly included source.");
            Assert(a.Directions.Skip(1).Take(19).All(d => !d.Answers[0].HasValue), "Copy modified excluded directions.");
            Button undo = (Button)Get(window, "undoButton"); Assert(undo.Visibility == Visibility.Visible, "Undo is not offered after copying."); Click(undo);
            Assert(!a.Directions[0].Answers[0].HasValue && a.Directions[20].Answers[0] == 1, "Undo changed the source or failed to clear copied answers.");
        }
        static void DetailedAndUndo()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[0] = 1; MainWindow window = Create(a); Click(CopyButton(window));
            Rows(window)[1].Root.Children.OfType<Button>().Single().RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(window);
            Assert((string)Get(window, "page") == "assessment" && (int)Get(window, "selected") == 1, "The direction name did not open its detailed view.");
            List<RadioButton[]> radios = (List<RadioButton[]>)Get(window, "answerRadios");
            Assert(radios.Count == 8, "Detailed assessment no longer exposes eight criteria.");
            radios[0][1].IsChecked = true;
            Assert(a.Directions[1].Answers[0] == .5, "Detailed radio did not update the shared model.");
            Click((Button)Get(window, "undoButton"));
            Assert(a.Directions[1].Answers[0] == .5, "Undo discarded a manual detailed-view edit.");
            Assert(a.Directions.Skip(2).All(d => !d.Answers[0].HasValue), "Undo did not clear untouched copied answers.");
            Click((Button)Get(window, "navAssess")); Layout(window);
            Assert((string)Get(window, "page") == "quick" && Rows(window).Count == 21, "Returning to the matrix failed.");
        }
        static void Filters()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers = Enumerable.Repeat((double?)0, 8).ToArray(); MainWindow window = Create(a); string before = Answers(a);
            CheckBox pending = Tree((DependencyObject)window.Content).OfType<CheckBox>().Single(c => c.Content as string == "Только незаполненные"); pending.IsChecked = true;
            Assert(Rows(window)[0].Root.Visibility == Visibility.Collapsed && Rows(window)[1].Root.Visibility == Visibility.Visible, "Incomplete filter hides the wrong directions.");
            ((TextBox)Get(window, "search")).Text = "21";
            Assert(Rows(window).Count(r => r.Root.Visibility == Visibility.Visible) == 1 && Rows(window)[20].Root.Visibility == Visibility.Visible, "Direction search is incorrect.");
            Assert(Answers(a) == before, "Filtering changed answer data.");
        }
        static void AllPages()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a); string before = Answers(a);
            foreach (string page in new[] { "object", "assessment", "results", "settings", "analysis", "quick" }) { Page(window, page); Assert(!window.IsVisible, "Page rendering showed a window."); }
            Assert(Answers(a) == before, "Changing pages filled answers.");
            Assert(Rows(window).Count == 21, "The quick page failed after visiting legacy pages.");
        }
        static Button MassButton(MainWindow window, double value)
        {
            return ((List<Button>)Get(window, "massValueButtons")).Single(b => (double)b.Tag == value);
        }
        static T Tagged<T>(MainWindow window, string tag) where T : FrameworkElement { return Tree((DependencyObject)window.Content).OfType<T>().First(x => (x.Tag as string) == tag); }
        static Button Labeled(MainWindow window, string text) { return Tree((DependencyObject)window.Content).OfType<Button>().First(b => (b.Content as string ?? "") == text); }
        static void ObjectPage()
        {
            Assessment a = Assessment.Create(); a.Organization = "Оператор"; a.Directions[0].Answers[0] = 0; a.Directions[1].Included = false; a.Directions[0].Target = 4;
            MainWindow window = Create(a); string before = Store.Serialize(a); Page(window, "object");
            Assert(a.Passport == null && Store.Serialize(a) == before, "Opening the object page created facts.");
            string text = String.Join(" ", Tree((DependencyObject)window.Content).OfType<TextBlock>().Select(t => t.Text));
            Assert(!text.Contains("АРМ для доступа") && !text.Contains("Типовой АРМ") && !text.Contains("АИСТ") && !text.Contains("роутер"), "The object page still shows single-ARM wording.");
            Click(Labeled(window, "+ Добавить компонент")); Layout(window);
            Assert(a.Passport != null && a.Passport.Components.Length == 1, "Adding a component did not create a description row.");
            ComboBox kind = Tagged<ComboBox>(window, "Компонент"); kind.Text = "ПЛК / контроллер";
            TextBox count = Tagged<TextBox>(window, "Количество"); count.Text = "-5"; Assert(a.Passport.Components[0].Count == 1, "An invalid count was stored."); count.Text = "40";
            Assert(a.Passport.Components[0].Kind == "ПЛК / контроллер" && a.Passport.Components[0].Count == 40, "Component edits were not stored.");
            Click(Labeled(window, "+ Добавить компонент")); Layout(window); Click(Labeled(window, "+ Добавить компонент")); Layout(window);
            Assert(a.Passport.Components.Length == 3, "Components are not unlimited.");
            Click(Labeled(window, "+ Добавить взаимодействие")); Layout(window); Click(Labeled(window, "+ Добавить взаимодействие")); Layout(window);
            Assert(a.Passport.Connections.Length == 2, "Several external interactions cannot be added.");
            Tagged<TextBox>(window, "Система или сеть").Text = "Региональная система";
            Assert(a.Passport.Connections.Count(c => c.System == "Региональная система") == 1, "Connection system name was not stored in exactly one row.");
            Click(Labeled(window, "+ Добавить средство защиты")); Layout(window);
            Tagged<ComboBox>(window, "Средство").Text = "Любой продукт";
            Assert(a.Passport.Tools.Length == 1 && a.Passport.Tools[0].Product == "Любой продукт", "A free-text security product was not accepted.");
            Click(Labeled(window, "Отметить первые пять как неприменяемые"));
            Assert(a.Passport.Wireless == false && a.Passport.ArtificialIntelligence == false && !a.Passport.Domain.HasValue && !a.Passport.Internet.HasValue, "Marking technologies changed the wrong facts.");
            Assert(a.Directions[0].Answers[0] == 0 && !a.Directions[1].Included && a.Directions[0].Target == 4, "The object page changed maturity data.");
            Assessment none = Assessment.Create(); MainWindow w2 = Create(none); Page(w2, "object");
            Tree((DependencyObject)w2.Content).OfType<CheckBox>().First(c => (c.Content as string) == "Внешних взаимодействий нет").IsChecked = true;
            Assert(none.Passport != null && none.Passport.NoConnections && SystemPassports.UnknownFields(none.Passport).All(f => !f.StartsWith("Внешние взаимодействия")), "\"No external interactions\" is not recorded.");
        }
        static void ObjectTypesOnPage()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a); Page(window, "object");
            Click(Labeled(window, "+ Добавить тип объекта")); Layout(window);
            Assert(a.Objects.Length == 1 && a.Objects[0].Kind == "gis", "Adding an object type failed.");
            ComboBox level = Tagged<ComboBox>(window, "Класс или категория"); level.SelectedIndex = 2; // К2
            Assert(a.Objects[0].Level == 2 && a.Category == 2 && a.Directions.All(d => d.Target >= 2) && a.Requirements.Contains("fstec117"), "Object class did not set goal and requirements.");
            Page(window, "quick");
            ComboBox category = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(x => x.Items.Count == 4 && x.Items[0] is string);
            Assert(!category.IsEnabled && category.SelectedIndex == 2, "The category field is editable although object types define it.");
        }
        static void HeaderAndTabs()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a);
            string[] buttons = Tree((DependencyObject)window.Content).OfType<Button>().Select(b => b.Content as string ?? "").ToArray();
            foreach (string expected in new[] { "Новая оценка", "Открыть", "Сохранить", "Объект оценки", "Анкета", "Результат", "План улучшений", "Сведения для отчёта" })
                Assert(buttons.Contains(expected), "Missing command or tab: " + expected);
            Assert(buttons.Any(b => b.StartsWith("Отчёт")), "Missing the report command.");
            Assert(!buttons.Any(b => b.Contains("АРМ") || b.Contains("комплект")), "Specific or confusing commands remain: " + String.Join(", ", buttons.Where(b => b.Contains("АРМ") || b.Contains("комплект"))));
            Assert(window.Title.StartsWith("Оценка уровня зрелости"), "Window title is not generic.");
        }
        static CheckBox ReplaceMass(MainWindow window)
        {
            return Tree((DependencyObject)window.Content).OfType<CheckBox>().Single(b => (b.Content as string) == "Заменить введённые");
        }
        static void MassValueInput()
        {
            Assessment a = Assessment.Create(); a.Directions[0].Answers[0] = 0; a.Directions[20].Included = false;
            MainWindow window = Create(a); string before = Store.Serialize(a);
            Click(MassButton(window, .5));
            Assert(a.Directions[0].Answers[0] == 0 && a.Directions.Skip(1).Take(19).All(d => d.Answers[0] == .5), "Mass default input overwrote a zero or missed included rows.");
            Assert(a.Directions[20].Answers.All(x => !x.HasValue) && a.Directions.All(d => d.Answers.Skip(1).All(x => !x.HasValue)), "Mass single criterion touched exclusions or other criteria.");
            Call(window, "UndoCopiedAnswers"); Assert(Store.Serialize(a) == before, "Undo failed to restore the pre-mass assessment.");
            ReplaceMass(window).IsChecked = true; Assert(Store.Serialize(a) == before, "Selecting overwrite changed facts before a value was applied.");
            Click(MassButton(window, 1)); Assert(a.Directions.Take(20).All(d => d.Answers[0] == 1), "Explicit overwrite did not replace a known grade.");
            Call(window, "UndoCopiedAnswers"); Assert(Store.Serialize(a) == before, "Overwrite undo did not restore known grades.");
            ReplaceMass(window).IsChecked = false; Call(window, "SelectMassCriterion", 5);
            Assert(MassButton(window, .3).Visibility == Visibility.Visible, "Training does not expose 0.3."); Click(MassButton(window, .3)); Assert(a.Directions.Take(20).All(d => d.Answers[5] == .3), "Training mass grade was not applied.");
            Call(window, "UndoCopiedAnswers"); Call(window, "SelectMassCriterion", -1);
            Assert(MassButton(window, .3).Visibility == Visibility.Collapsed, "All-criteria mode exposes an invalid common 0.3 grade.");
            Click(MassButton(window, .5)); Assert(a.Directions.Take(20).All(d => d.Answers.All(x => x.HasValue)) && a.Directions[0].Answers[0] == 0, "Common grade did not fill all included criteria while preserving explicit zero.");
            Call(window, "UndoCopiedAnswers"); Assert(Store.Serialize(a) == before, "Common grade undo changed metadata, scope, targets or prior grades.");
        }
        static void MassVisibleScope()
        {
            Assessment a = Assessment.Create(); MainWindow window = Create(a); string before = Store.Serialize(a);
            Call(window, "SelectMassCriterion", -1); ReplaceMass(window).IsChecked = true;
            ComboBox scope = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => c.Items.Count == 2 && c.Items[0] is string && ((string)c.Items[0]).StartsWith("Все включённые")); scope.SelectedIndex = 1;
            ((TextBox)Get(window, "search")).Text = "21";
            Assert(Store.Serialize(a) == before, "Changing mass parameters or filters wrote answers without a command.");
            Click(MassButton(window, 1)); Assert(a.Directions[20].Answers.All(x => x == 1) && a.Directions.Take(20).All(d => d.Answers.All(x => !x.HasValue)), "Visible-only mass scope touched hidden rows.");
            Call(window, "UndoCopiedAnswers"); Assert(Store.Serialize(a) == before, "Visible-only mass undo failed.");
        }
        static void FastBundle()
        {
            Assessment a = Assessment.Create(); a.Organization = "Тестовая организация"; a.SystemName = "Тестовая система"; a.Assessor = "Тестовый составитель";
            MainWindow window = Create(a); string path = Path.Combine(outputFolder, "ui-bundle.zip"); Set(window, "bundlePath", path);
            Call(window, "SaveBundleFast", false);
            Assert(File.Exists(path), "Fast save did not create the preset bundle.");
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                Assert(zip.Entries.Count == 4 && zip.GetEntry("01 Оценка.uzi.json") != null && zip.GetEntry("02 Отчёт.docx") != null && zip.GetEntry("03 Таблица.xlsx") != null && zip.GetEntry("Состав архива.txt") != null, "Fast bundle does not contain the project and both reports.");
            }
            a.Directions[0].Answers[0] = 1; Call(window, "SaveBundleFast", false);
            using (ZipArchive zip = ZipFile.OpenRead(path))
            using (StreamReader reader = new StreamReader(zip.GetEntry("01 Оценка.uzi.json").Open(), Encoding.UTF8))
                Assert(Store.Parse(reader.ReadToEnd()).Directions[0].Answers[0] == 1, "A repeat fast save did not update the existing bundle.");
            Assert((string)Get(window, "bundlePath") == path, "The archive is not remembered for the next save.");
            report.generatedFiles.Add(path);
        }
        static void ReopenedBundle()
        {
            string path = Path.Combine(outputFolder, "ui-reopened-bundle.zip");
            Assessment initial = Assessment.Create(); initial.Organization = "Открытие комплекта"; initial.Directions[0].Answers[0] = 0;
            ProjectTools.ExportBundle(initial, path, false, false);
            Assessment loaded = ProjectTools.LoadAssessmentFile(path); MainWindow window = Create(loaded); window.SetProjectPath(path);
            Assert(Get(window, "projectPath") == null && (string)Get(window, "bundlePath") == path, "Opening ZIP incorrectly associates it with the JSON save path.");
            Assert(((Button)Get(window, "bundleButton")).IsEnabled, "Reports are not available for a reopened archive.");
            Click(AnswerButton(Rows(window)[0].Cells[0], 0, .5));
            Assert((bool)Get(window, "dirty"), "Editing a reopened bundle did not mark the assessment changed.");
            Assert((bool)Call(window, "SaveCurrent"), "\"Сохранить\" did not update the reopened archive.");
            Assert(!(bool)Get(window, "dirty") && ProjectTools.LoadAssessmentFile(path).Directions[0].Answers[0] == .5, "Bundle update lost answers or retained an unnecessary unsaved flag.");
            string project = Path.Combine(outputFolder, "separate.uzi.json"); window.SetProjectPath(project);
            Assert((string)Get(window, "projectPath") == project && Get(window, "bundlePath") == null, "Switching to a JSON file retained the old bundle association.");
            report.generatedFiles.Add(path);
        }
        static void EmptyScope()
        {
            Assessment a = Assessment.Create(); foreach (DirectionAssessment d in a.Directions) d.Included = false; MainWindow window = Create(a);
            Assert(!((Button)Get(window, "bundleButton")).IsEnabled, "Fast save is enabled for an empty assessment scope.");
            Assert(a.Directions.All(d => d.Answers.All(x => !x.HasValue)), "Empty-scope UI populated answers.");
        }
        static List<QuickAnswerCell> Common(MainWindow window) { return (List<QuickAnswerCell>)Get(window, "commonCells"); }
        static void CommonRow()
        {
            Assessment a = Assessment.Create(); a.Directions[20].Included = false; MainWindow window = Create(a);
            Assert(Common(window).Count == 8, "The common row must have eight cells.");
            Assert(Common(window).All(c => !c.IsVisible && c.ActualHeight == 0), "The common row must be collapsed by default.");
            Button toggle = Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "").StartsWith("Общий ответ для всех направлений"));
            Click(toggle); Layout(window);
            Assert(Common(window).All(c => c.ActualHeight > 20) && Rows(window)[0].Cells[0].ActualHeight > 20, "The expanded common row or the first direction row has no visible height.");
            Assert((string)toggle.Content == "Скрыть общий ответ ▴", "The toggle does not offer to hide the expanded row.");
            for (int j = 0; j < 8; j++) Click(AnswerButton(Common(window)[j], j, .5));
            Assert(a.Directions.Take(20).All(d => d.Answers.All(x => x == .5)), "Eight common clicks did not fill every included cell.");
            Assert(a.Directions[20].Answers.All(x => !x.HasValue), "The common row touched an excluded direction.");
            Click(AnswerButton(Rows(window)[3].Cells[0], 0, 0));
            Assert(Rows(window)[3].Cells[0].Background != Brushes.Transparent && Rows(window)[4].Cells[0].Background == Brushes.Transparent, "A differing grade is not highlighted.");
            Click(AnswerButton(Common(window)[0], 0, 1));
            Assert(a.Directions[3].Answers[0] == 0 && a.Directions.Take(20).Where(d => d.Id != 4).All(d => d.Answers[0] == 1), "Changing the common answer overwrote an individual difference or missed followers.");
            Assert(QuickInput.CommonValue(a, 0) == 1, "The common value is not the majority answer.");
            Click(AnswerButton(Common(window)[0], 0, 1));
            Assert(a.Directions[3].Answers[0] == 0, "A repeated common click cleared or overwrote answers.");
            Click(AnswerButton(Common(window)[0], 0, .5));
            Call(window, "UndoCopiedAnswers");
            Assert(a.Directions[3].Answers[0] == 0 && a.Directions.Take(20).Where(d => d.Id != 4).All(d => d.Answers[0] == 1), "Undo of a common change failed.");
            Assert(Calc.Summary(a).Answered == 160, "Common row changed the number of answers unexpectedly.");
        }
        static void PassportExclusion()
        {
            Assessment a = Assessment.Create(); a.Passport = SystemPassports.CreateEmpty(); a.Directions[7].Answers[0] = 1;
            MainWindow window = Create(a);
            Func<Button> find = () => Tree((DependencyObject)window.Content).OfType<Button>().SingleOrDefault(b => (b.Content as string ?? "").StartsWith("Исключить неприменимые"));
            Assert(find() == null, "Unknown technologies offered an exclusion.");
            a.Passport.Mobile = false; a.Passport.Wireless = false; a.Passport.RemoteAdministration = false; a.Passport.OwnDevelopment = false; a.Passport.ArtificialIntelligence = false; a.Passport.Domain = false;
            Page(window, "quick");
            Assert(a.Directions.All(d => d.Included), "Opening the page excluded directions without a command.");
            Button exclude = find(); Assert(exclude != null && (string)exclude.Content == "Исключить неприменимые направления: 8, 9, 10, 13, 19", "Passport exclusion offers the wrong directions.");
            Click(exclude); Layout(window);
            int[] off = a.Directions.Where(d => !d.Included).Select(d => d.Id).ToArray();
            Assert(String.Join(",", off) == "8,9,10,13,19", "Passport exclusion changed the wrong scope.");
            Assert(a.Directions[7].Evidence.Contains("п. 13") && a.Directions[9].Evidence.Contains("Wi-Fi"), "Passport exclusion did not record the basis of exclusion.");
            Assert(a.Directions[7].Answers[0] == 1, "Exclusion deleted an answer.");
            Assert(find() == null, "The exclusion is still offered after it was applied.");
        }
        static void AnswerTemplateRoundTrip()
        {
            Assessment a = Assessment.Create(); a.Organization = "Организация А"; a.Category = 2; foreach (DirectionAssessment d in a.Directions) d.Target = 2;
            a.Directions[0].Answers = new double?[] { 1, .5, 0, 1, .5, .3, 0, 1 }; a.Directions[12].Included = false; a.Directions[2].Target = 4; a.Directions[0].Evidence = "Документ А";
            a.Passport = SystemPassports.CreateEmpty();
            string path = Path.Combine(outputFolder, "ui-template.answers.json");
            AnswerTemplates.Save(AnswerTemplates.Create(a, "Школы", TemplateParts.Grades), path);
            string json = File.ReadAllText(path, Encoding.UTF8);
            Assert(!json.Contains("Организация А") && !json.Contains("Документ А") && !json.Contains("Workstations"), "The template leaked requisites, evidence or passport.");
            Assessment b = Assessment.Create(); b.Organization = "Организация Б"; b.Directions[0].Evidence = "Документ Б";
            AnswerTemplate t = AnswerTemplates.Load(path); AnswerTemplates.Apply(b, t);
            Assert(t.Name == "Школы", "Template name was lost.");
            Assert(b.Category == 2 && b.Directions[2].Target == 4 && !b.Directions[12].Included && b.Directions[0].Answers[5] == .3 && !b.Directions[1].Answers[0].HasValue, "Template did not restore grades, scope, category or goals.");
            Assert(b.Organization == "Организация Б" && b.Directions[0].Evidence == "Документ Б", "Applying a template changed requisites or evidence.");
            bool rejected = false;
            try { AnswerTemplates.Parse(json.Replace("0.3", "0.7")); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "An invalid grade in a template was accepted.");
            rejected = false;
            try { AnswerTemplates.Parse(Store.Serialize(a)); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "A project file was accepted as an answer template.");
            report.generatedFiles.Add(path);
        }
        static Assessment FullReportAssessment()
        {
            Assessment a = Assessment.Create(); a.Organization = "МБОУ «Школа № 1»"; a.SystemName = "ГИС «Электронная школа»"; a.Assessor = "Иванов И. И."; a.AssessorPosition = "Специалист по ЗИ";
            a.AssessorOrganization = "МБОУ «Школа № 1»"; a.AssessorContact = "+7 000 000-00-00"; a.ApproverName = "Петров П. П."; a.ApproverPosition = "Директор";
            a.Objects = new[] { new ProtectionObject { Kind = "gis", Level = 3 }, new ProtectionObject { Kind = "ispdn", Level = 3 } };
            a.Requirements = new[] { "fstec117", "fstec21" }; a.SourceData = new[] { "a", "g", "k" }; a.Methods = new[] { "documents", "interview" };
            a.Participants = new[] { new ReportPerson { Role = "source", Name = "Сидорова С. С.", Position = "Заместитель директора", Organization = "МБОУ «Школа № 1»", Contact = "s@school.example" } };
            a.PlanResponsible = "Ответственный за ЗИ"; a.PlanDeadline = "до 31.12.2027";
            foreach (DirectionAssessment d in a.Directions) d.Answers = new double?[] { 1, 1, .5, .5, 1, .5, 0, .5 };
            a.Directions[18].Included = false; a.Directions[18].Evidence = "ИИ не применяется.";
            a.Directions[0].Target = 4; // one unmet goal, so the plan (п. 35 м, н) has rows
            return a;
        }
        static string WordXml(string path)
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
            using (StreamReader r = new StreamReader(zip.GetEntry("word/document.xml").Open(), Encoding.UTF8)) return r.ReadToEnd();
        }
        static void ReportStructure()
        {
            Assessment a = FullReportAssessment();
            Assert(ReportFacts.Missing(a).Length == 0, "A fully described assessment still reports missing data: " + String.Join("; ", ReportFacts.Missing(a)));
            string word = Path.Combine(outputFolder, "report-full.docx"); Reports.ExportWord(a, word); string xml = WordXml(word);
            foreach (string letter in new[] { "а", "б", "в", "г", "д", "е", "ж", "з", "и", "к", "л", "м", "н" })
                Assert(xml.Contains("(п. 35 " + letter + ")"), "Word report misses item 35 " + letter + ".");
            Assert(xml.Contains("Подписи (п. 36)") && xml.Contains("УТВЕРЖДАЮ"), "Signatures or approval are missing.");
            Assert(!xml.Contains("Отчёт неполон"), "A complete report is flagged as incomplete.");
            Assert(xml.Contains("Сидорова С. С.") && xml.Contains("s@school.example") && xml.Contains("+7 000 000-00-00"), "People or contacts are missing from the report.");
            Assert(xml.Contains("приказ ФСТЭК России от 11.04.2025 № 117") && xml.Contains("ИИ не применяется."), "Requirements or the exclusion basis are missing.");
            Assert(xml.Contains("Узи = ΣУзитi / N = (") && xml.Contains(") / 20 = "), "The Uzi calculation is not shown with N = 20.");
            Assert(xml.Contains("до 31.12.2027") && xml.Contains("Ответственный за ЗИ"), "Plan owner or deadline is missing.");
            Assert(xml.Contains("Предыдущая оценка уровня зрелости не проводилась"), "Absent previous assessment is not stated.");
            string excel = Path.Combine(outputFolder, "report-full.xlsx"); Reports.ExportExcel(a, excel);
            using (ZipArchive zip = ZipFile.OpenRead(excel))
            using (StreamReader r = new StreamReader(zip.GetEntry("xl/workbook.xml").Open(), Encoding.UTF8))
            { string book = r.ReadToEnd(); Assert(book.Contains("Сведения по п. 35") && book.Contains("Dij и Pзн"), "Excel misses the item 35 or Dij sheets."); }
            Assessment empty = Assessment.Create(); string emptyWord = Path.Combine(outputFolder, "report-empty.docx"); Reports.ExportWord(empty, emptyWord);
            Assert(WordXml(emptyWord).Contains("Отчёт неполон"), "An empty report is not flagged as incomplete.");
            report.generatedFiles.Add(word); report.generatedFiles.Add(excel);
        }
        static void DynamicsRules()
        {
            Assessment old = FullReportAssessment(); Assessment now = FullReportAssessment();
            now.Previous = ReportFacts.FromAssessment(old, "test");
            Assert(ReportFacts.Dynamics(now).Kind == "zero", "Identical assessments must give zero dynamics.");
            now.Directions[0].Answers[6] = 1; now.Directions[0].Answers[5] = 1; now.Directions[0].Answers[7] = 1; now.Directions[0].Answers[2] = 1; now.Directions[0].Answers[3] = 1;
            Assert(Calc.Direction(now.Directions[0]).Level > Calc.Direction(old.Directions[0]).Level, "Test setup did not raise a level.");
            Assert(ReportFacts.Dynamics(now).Kind == "positive", "Growth of Uzi must give positive dynamics (п. 33 а).");
            now.Directions[1].Answers[6] = null; now.Directions[1].Answers[2] = 0;
            Assert(ReportFacts.Dynamics(now).Kind == "negative" && ReportFacts.Dynamics(now).CriterionDecreases.Count > 0, "Any decrease must give negative dynamics (п. 33 г).");
            Assessment manual = FullReportAssessment(); manual.Previous = new PreviousResult { Date = "2025-10-01", Uzi = 0.5, Source = "вручную" };
            Assert(ReportFacts.Dynamics(manual).Kind == "positive", "A manually entered lower Uzi must give positive dynamics.");
            string json = Store.Serialize(now); Assessment back = Store.Parse(json);
            Assert(back.Previous != null && back.Previous.Directions.Length == 21 && back.Previous.Source == "test", "Previous result does not survive saving.");
        }
        static void Library()
        {
            string folder = Path.Combine(outputFolder, "library"); if (Directory.Exists(folder)) Directory.Delete(folder, true); Directory.CreateDirectory(folder);
            Assessment source = FullReportAssessment(); source.Passport = SystemPassports.CreateEmpty(); source.Passport.Connections = new[] { new PassportConnection { System = "Внешняя система", Channel = "vpn" } }; source.Objects = new[] { new ProtectionObject { Kind = "ispdn", Level = 2, Name = "Своя ИС", Document = "Акт № 1" } }; source.Category = 2; foreach (DirectionAssessment d in source.Directions) d.Target = 2;
            string first = TemplateLibrary.SaveNew(AnswerTemplates.Create(source, "Поликлиника · ИСПДн УЗ-2"), folder);
            string second = TemplateLibrary.SaveNew(AnswerTemplates.Create(source, "Поликлиника · ИСПДн УЗ-2"), folder);
            Assert(first != second && File.Exists(first) && File.Exists(second), "Two templates with one name overwrote each other.");
            string json = File.ReadAllText(first, Encoding.UTF8);
            Assert(!json.Contains("Сидорова") && !json.Contains("МБОУ") && !json.Contains("ИИ не применяется"), "A template leaked people, organization or evidence.");
            SystemPassports.SaveTemplate(SystemPassports.CreateEmpty(), Path.Combine(folder, "Старая основа.arm.json"));
            TemplateLibrary.SaveNewProfile(source, "Профиль", folder);
            File.WriteAllText(Path.Combine(folder, "Битый.answers.json"), "{oops", Encoding.UTF8);
            List<LibraryEntry> entries = TemplateLibrary.List(folder);
            Assert(entries.Count == 5 && entries.Count(e => e.IsValid) == 4 && entries.Any(e => e.Passport != null) && entries.Count(e => e.IsOrganization) == 1, "Library listing is wrong (templates, legacy basis, profile, broken file).");

            Assessment target = Assessment.Create(); target.Organization = "Другая школа"; target.Directions[0].Evidence = "Свой документ";
            LibraryWindow window = new LibraryWindow(target, null); window.UseFolderForSession(folder);
            window.Select(first);
            Assert(window.ApplySelected(TemplateParts.Grades), "Applying grades failed.");
            Assert(target.Category == 2 && target.Directions[0].Answers[0] == 1 && !target.Directions[18].Included && target.Passport == null && target.Requirements.Length == 0, "Grades-only apply touched other parts or missed grades.");
            Assert(target.Organization == "Другая школа" && target.Directions[0].Evidence == "Свой документ" && target.Participants.Length == 0, "Template changed organization, evidence or people.");
            window.Select(first); Assert(window.ApplySelected(TemplateParts.All), "Applying all parts failed.");
            Assert(target.Passport != null && target.Passport.Connections[0].System == "Внешняя система" && target.Objects.Length == 1 && target.Objects[0].Level == 2 && target.Objects[0].Document == "" && target.Requirements.Contains("fstec117") && target.Methods.Contains("interview"), "Full apply missed passport or practice.");

            target.Directions[2].Answers[0] = 0; window.Select(first); Assert(window.OverwriteSelected(), "Overwrite failed.");
            Assert(AnswerTemplates.Load(first).Directions[2].Answers[0] == 0 && AnswerTemplates.Load(first).Name == "Поликлиника · ИСПДн УЗ-2", "Overwrite lost the change or the name.");
            window.Select(second); Assert(window.RenameSelected("Поликлиника"), "Rename failed.");
            Assert(TemplateLibrary.List(folder).Any(e => e.Name == "Поликлиника" && e.Path.EndsWith("Поликлиника.answers.json")), "Rename did not change name and file.");
            window.Select(first); Assert(window.RemoveSelected(), "Remove failed.");
            Assert(!File.Exists(first) && Directory.GetFiles(Path.Combine(folder, TemplateLibrary.TrashFolder)).Length == 1, "Removed template was not moved to the trash folder.");
            string fresh = window.SaveCurrentAsNew("Новый тип"); Assert(fresh != null && File.Exists(fresh), "Saving the current assessment as a new template failed.");
            window.ShowOrganizations(true);
            Assert(window.Entries.Count == 1 && window.Entries[0].Profile != null, "The organizations section does not list profiles only.");
            window.Select(window.Entries[0].Path); Assessment empty = Assessment.Create();
            LibraryWindow orgWindow = new LibraryWindow(empty, null, true); orgWindow.UseFolderForSession(folder); orgWindow.Select(orgWindow.Entries[0].Path);
            Assert(orgWindow.ApplySelected(TemplateParts.All) && empty.Organization == source.Organization && empty.Participants.Length == source.Participants.Length && empty.Directions.All(d => d.Answers.All(x => !x.HasValue)), "Applying an organization profile failed or touched grades.");
            Assert(!window.IsVisible && !orgWindow.IsVisible, "The library window became visible in a headless test.");
        }
        static void ReportDataCompatibility()
        {
            Assessment legacy = Assessment.Create();
            string json = Store.Serialize(legacy);
            foreach (string field in new[] { "AssessmentKind", "ExternalLicense", "AssessorOrganization", "AssessorContact", "Participants", "Requirements", "SourceData", "Methods", "Instruments", "ToolsNote", "SourceDataNote", "Previous", "PlanResponsible", "PlanDeadline" })
            {
                int at = json.IndexOf(",\"" + field + "\":", StringComparison.Ordinal); Assert(at > 0, "Field not serialized: " + field);
                int end = json.IndexOf(",\"", at + 2, StringComparison.Ordinal); if (end < 0) end = json.LastIndexOf('}');
                json = json.Remove(at, end - at);
            }
            Assessment opened = Store.Parse(json);
            Assert(opened.Participants.Length == 0 && opened.Requirements.Length == 0 && opened.AssessmentKind == "internal" && opened.Previous == null, "A 1.5 project did not open with empty report data.");
            bool rejected = false;
            try { Assessment bad = Assessment.Create(); bad.Participants = new[] { new ReportPerson { Role = "boss" } }; Store.Parse(Store.Serialize(bad)); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "An unknown participant role was accepted.");
            rejected = false;
            try { Assessment bad = Assessment.Create(); bad.Methods = new[] { "magic" }; Store.Parse(Store.Serialize(bad)); } catch (InvalidDataException) { rejected = true; }
            Assert(rejected, "An unknown method was accepted.");
        }
        static void ProfileWithPeople()
        {
            Assessment a = FullReportAssessment(); string path = Path.Combine(outputFolder, "profile.org.json");
            ProjectTools.SaveProfile(a, path);
            Assessment b = Assessment.Create(); b.Directions[0].Answers[0] = .5;
            ProjectTools.ApplyProfile(b, ProjectTools.LoadProfile(path));
            Assert(b.AssessorContact == a.AssessorContact && b.Participants.Length == 1 && b.Participants[0].Name == "Сидорова С. С.", "Profile lost contacts or participants.");
            Assert(b.Directions[0].Answers[0] == .5 && b.Requirements.Length == 0, "Profile changed grades or practice.");
        }
        static void ReportPage()
        {
            Assessment a = Assessment.Create(); a.Organization = "Оператор"; MainWindow window = Create(a); Page(window, "settings");
            CheckBox requirement = Tree((DependencyObject)window.Content).OfType<CheckBox>().Single(c => (c.Tag as string) == "report:fstec239");
            requirement.IsChecked = true; Assert(a.Requirements.SequenceEqual(new[] { "fstec239" }), "Ticking a requirement did not update the model.");
            CheckBox method = Tree((DependencyObject)window.Content).OfType<CheckBox>().Single(c => (c.Tag as string) == "report:interview");
            method.IsChecked = true; method.IsChecked = false; Assert(a.Methods.Length == 0, "Unticking a method did not update the model.");
            Click(Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "") == "+ Предоставлял исходные данные")); Layout(window);
            Assert(a.Participants.Length == 1 && a.Participants[0].Role == "source" && a.Participants[0].Organization == "Оператор", "Adding a participant did not prefill the operator.");
            Click(Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "") == "Организация = оператор")); Layout(window);
            Assert(a.AssessorOrganization == "Оператор", "Assessor organization shortcut failed.");
            Call(window, "ApplyPrevious", FullReportAssessment(), "old.zip"); Layout(window);
            Assert(a.Previous != null && a.SourceData.Contains("zh") && a.Methods.Contains("previous"), "Loading a previous assessment did not record it as analysed source data.");
        }
    }
}
