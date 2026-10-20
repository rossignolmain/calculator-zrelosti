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
                Test("adding an ARM passport preserves maturity and keeps unknown facts", ArmPassport);
                Test("new ARM starts a separate organization and empty assessment", NewArm);
                Test("ARM connection controls agree and security choices require explicit apply", ArmConnectionAndProtection);
                Test("fast bundle exports and updates the preset ZIP without dialogs", FastBundle);
                Test("opened bundle stays associated with ZIP for the next one click save", ReopenedBundle);
                Test("empty scope disables the fast bundle button", EmptyScope);
                Test("common row fills 168 cells in 8 clicks and keeps individual differences", CommonRow);
                Test("passport exclusion is an explicit command and keeps answers", PassportExclusion);
                Test("answer template carries grades, scope and goals but no requisites", AnswerTemplateRoundTrip);
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
        static void ArmPassport()
        {
            Assessment a = Assessment.Create(); a.Organization = "Существующая организация"; a.SystemName = "Существующий объект";
            a.Directions[0].Answers[0] = 0; a.Directions[1].Included = false; a.Directions[0].Target = 4; a.Directions[0].Evidence = "Проверенный документ";
            MainWindow window = Create(a); string before = Store.Serialize(a); Page(window, "object");
            Assert(a.Passport == null && Store.Serialize(a) == before, "Opening the object page created unconfirmed facts.");
            Click(Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "").StartsWith("Применить основу")));
            Assert(a.Passport != null && a.Passport.Workstations == 1 && a.Passport.Routers == 1 && a.Passport.Switches == 0 && a.Passport.Servers == 0, "The explicit ARM basis did not set the expected equipment.");
            Assert(!a.Passport.Wireless.HasValue && !a.Passport.Internet.HasValue && !a.Passport.Mobile.HasValue && !a.Passport.OwnDevelopment.HasValue, "Applying the base asserted unobserved technologies.");
            string afterPassport = Store.Serialize(a); SystemPassport p = a.Passport; a.Passport = null; Assert(Store.Serialize(a) == before, "The technical basis changed organization, maturity, goals, scope or evidence."); a.Passport = p;
            Click(Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string ?? "").StartsWith("Эти шесть технологий")));
            Assert(p.Domain == false && p.Wireless == false && p.Mobile == false && p.RemoteAdministration == false && p.OwnDevelopment == false && p.ArtificialIntelligence == false, "The explicit no-extra-technologies command missed a property.");
            Assert(!p.Internet.HasValue, "No-extra-technologies guessed Internet availability.");
            a.Passport = null; Assert(Store.Serialize(a) == before, "No-extra-technologies altered a maturity answer or scope."); a.Passport = p;
            ComboBox wifi = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "Wi-Fi / беспроводной доступ"); wifi.SelectedIndex = 2;
            Assert(p.Wireless == true, "An individual exception cannot override the simple basis.");
            ComboBox external = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(t => (t.Tag as string) == "К какой внешней системе подключаются"); external.Text = "Внешняя ИС <&>";
            Assert(p.ExternalSystem == external.Text && a.SystemName == "Существующий объект", "External system input overwrote a custom object name.");
            TextBox routers = Tree((DependencyObject)window.Content).OfType<TextBox>().Single(t => (t.Tag as string) == "Роутеры"); routers.Text = "-1";
            Assert(p.Routers == 1, "An invalid count changed the saved valid fact."); routers.Text = "2"; Assert(p.Routers == 2, "A corrected count was not committed.");
            Page(window, "quick"); Assert(a.Directions[0].Answers[0] == 0 && !a.Directions[1].Included && a.Directions[0].Target == 4, "Returning to assessment changed maturity data.");
        }
        static void NewArm()
        {
            Assessment old = Assessment.Create(); old.Organization = "Предыдущая организация"; old.Assessor = "Предыдущий сотрудник"; old.Directions[0].Answers[0] = 1;
            MainWindow window = Create(old); Call(window, "NewArmProject"); Assessment fresh = (Assessment)Get(window, "assessment");
            Assert(!Object.ReferenceEquals(fresh, old) && fresh.Passport != null && (string)Get(window, "page") == "object", "New ARM did not create an independent project.");
            Assert(fresh.Organization == "" && fresh.Assessor == "" && fresh.Directions.All(d => d.Answers.All(x => !x.HasValue)), "New ARM carried organization or maturity facts across organizations.");
            Assert(old.Organization == "Предыдущая организация" && old.Directions[0].Answers[0] == 1, "New ARM mutated the old project snapshot.");
            ComboBox external = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(t => (t.Tag as string) == "К какой внешней системе подключаются"); external.Text = "Внешняя ИС";
            Assert(fresh.SystemName == "АРМ доступа к «Внешняя ИС»", "The generated object title did not follow the external system name.");
            SystemPassport another = SystemPassports.CreateSingleArm(); another.ExternalSystem = "Другая ИС"; Call(window, "ApplyArmTemplate", another);
            Assert(fresh.SystemName == "АРМ доступа к «Другая ИС»", "Loading a basis left the previously generated system title.");
            fresh.SystemName = "Особое название объекта"; another.ExternalSystem = "Третья ИС"; Call(window, "ApplyArmTemplate", another);
            Assert(fresh.SystemName == "Особое название объекта", "Loading a basis overwrote a custom object title.");
            Assert(!(bool)Get(window, "live") && !window.IsVisible, "The test used runtime user data or showed a window.");
        }
        static void ArmConnectionAndProtection()
        {
            Assessment a = Assessment.Create(); a.Passport = SystemPassports.CreateSingleArm(); a.Directions[0].Answers[0] = 0;
            MainWindow window = Create(a); Page(window, "object");
            ComboBox channel = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "Канал подключения");
            ComboBox internet = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "Выход в интернет");
            Assert(internet.SelectedIndex == 0 && internet.IsEnabled, "An unknown Internet fact was silently filled."); channel.SelectedIndex = 1;
            Assert(a.Passport.Internet == true && internet.SelectedIndex == 2 && !internet.IsEnabled, "Internet channel and visible Internet fact disagree.");
            channel.SelectedIndex = 2; Assert(internet.IsEnabled, "Internet remains locked for a VPN channel."); internet.SelectedIndex = 1;
            Assert(a.Passport.Channel == "vpn" && a.Passport.Internet == false, "A private VPN cannot be described without Internet.");
            string beforeProtection = Store.Serialize(a);
            ComboBox vpn = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "СЗИ: Криптозащита / сеть");
            ComboBox nsd = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "СЗИ: Защита от НСД");
            ComboBox av = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "СЗИ: Антивирус");
            ComboBox boot = Tree((DependencyObject)window.Content).OfType<ComboBox>().Single(c => (c.Tag as string) == "СЗИ: СДЗ");
            vpn.SelectedIndex = 1; nsd.SelectedIndex = 1; av.SelectedIndex = 1; boot.SelectedIndex = 1;
            Assert(Store.Serialize(a) == beforeProtection, "Selecting components wrote installed protection before applying it.");
            nsd.SelectedIndex = 2; Assert(av.SelectedIndex == 0 && !((ComboBoxItem)av.Items[1]).IsEnabled, "SNS built-in antivirus stayed selected with Dallas Lock.");
            av.SelectedIndex = 2;
            Click(Tree((DependencyObject)window.Content).OfType<Button>().Single(b => (b.Content as string) == "Вставить выбранный набор в паспорт"));
            Assert(a.Passport.Protection.Contains("ViPNet") && a.Passport.Protection.Contains("Dallas Lock 8.0") && a.Passport.Protection.Contains("Dr.Web") && a.Passport.Protection.Contains("Соболь"), "The selected security composition was not generated.");
            Assert(a.Directions[0].Answers[0] == 0 && a.Directions.Sum(d => d.Answers.Count(x => x.HasValue)) == 1, "Security composition inferred a maturity grade.");
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
                Assert(zip.Entries.Count == 4 && zip.GetEntry("01 Проект.uzi.json") != null && zip.GetEntry("02 Отчёт.docx") != null && zip.GetEntry("03 Таблица.xlsx") != null && zip.GetEntry("Состав комплекта.txt") != null, "Fast bundle does not contain the project and both reports.");
            }
            a.Directions[0].Answers[0] = 1; Call(window, "SaveBundleFast", false);
            using (ZipArchive zip = ZipFile.OpenRead(path))
            using (StreamReader reader = new StreamReader(zip.GetEntry("01 Проект.uzi.json").Open(), Encoding.UTF8))
                Assert(Store.Parse(reader.ReadToEnd()).Directions[0].Answers[0] == 1, "A repeat fast save did not update the existing bundle.");
            Assert(((Button)Get(window, "bundleButton")).Content as string == "Обновить комплект", "The fast save button did not change to update mode.");
            report.generatedFiles.Add(path);
        }
        static void ReopenedBundle()
        {
            string path = Path.Combine(outputFolder, "ui-reopened-bundle.zip");
            Assessment initial = Assessment.Create(); initial.Organization = "Открытие комплекта"; initial.Directions[0].Answers[0] = 0;
            ProjectTools.ExportBundle(initial, path, false, false);
            Assessment loaded = ProjectTools.LoadAssessmentFile(path); MainWindow window = Create(loaded); window.SetProjectPath(path);
            Assert(Get(window, "projectPath") == null && (string)Get(window, "bundlePath") == path, "Opening ZIP incorrectly associates it with the JSON save path.");
            Assert(((Button)Get(window, "bundleButton")).Content as string == "Обновить комплект", "Reopened ZIP is not ready for a one click update.");
            Click(AnswerButton(Rows(window)[0].Cells[0], 0, .5));
            Assert((bool)Get(window, "dirty"), "Editing a reopened bundle did not mark the assessment changed.");
            Assert((bool)Call(window, "SaveBundleFast", false), "Updating a reopened bundle failed.");
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
            Assert(Common(window).All(c => c.ActualHeight > 20) && Rows(window)[0].Cells[0].ActualHeight > 20, "The common row or the first direction row has no visible height.");
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
            Assessment a = Assessment.Create(); a.Passport = SystemPassports.CreateSingleArm(); a.Directions[7].Answers[0] = 1;
            MainWindow window = Create(a);
            Func<Button> find = () => Tree((DependencyObject)window.Content).OfType<Button>().SingleOrDefault(b => (b.Content as string ?? "").StartsWith("Исключить по паспорту"));
            Assert(find() == null, "Unknown technologies offered an exclusion.");
            a.Passport.Mobile = false; a.Passport.Wireless = false; a.Passport.RemoteAdministration = false; a.Passport.OwnDevelopment = false; a.Passport.ArtificialIntelligence = false; a.Passport.Domain = false;
            Page(window, "quick");
            Assert(a.Directions.All(d => d.Included), "Opening the page excluded directions without a command.");
            Button exclude = find(); Assert(exclude != null && (string)exclude.Content == "Исключить по паспорту: 8, 9, 10, 13, 19", "Passport exclusion offers the wrong directions.");
            Click(exclude); Layout(window);
            int[] off = a.Directions.Where(d => !d.Included).Select(d => d.Id).ToArray();
            Assert(String.Join(",", off) == "8,9,10,13,19", "Passport exclusion changed the wrong scope.");
            Assert(a.Directions[7].Answers[0] == 1, "Exclusion deleted an answer.");
            Assert(find() == null, "The exclusion is still offered after it was applied.");
        }
        static void AnswerTemplateRoundTrip()
        {
            Assessment a = Assessment.Create(); a.Organization = "Организация А"; a.Category = 2; foreach (DirectionAssessment d in a.Directions) d.Target = 2;
            a.Directions[0].Answers = new double?[] { 1, .5, 0, 1, .5, .3, 0, 1 }; a.Directions[12].Included = false; a.Directions[2].Target = 4; a.Directions[0].Evidence = "Документ А";
            a.Passport = SystemPassports.CreateSingleArm();
            string path = Path.Combine(outputFolder, "ui-template.answers.json");
            AnswerTemplates.Save(AnswerTemplates.Create(a, "Школы"), path);
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
    }
}
