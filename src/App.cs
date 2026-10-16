using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MaturityApp
{
    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                if (args.Length > 0 && args[0] == "--self-test") return Verification.Run(args);
                Application app = new Application();
                Theme.Install(app);
                if (args.Length > 0 && args[0] == "--ui-self-test") return UiVerification.Run(args);
                if (args.Length > 0 && args[0] == "--render-preview")
                {
                    string folder = args[1]; Directory.CreateDirectory(folder);
                    Assessment demo = Assessment.Create();
                    demo.Organization = "Пример организации"; demo.SystemName = "Информационная система";
                    demo.Assessor = "Ответственный за информационную безопасность";
                    demo.OrganizationShort = "Пример"; demo.City = "Город";
                    demo.ApproverPosition = "Руководитель организации";
                    demo.Directions[0].Answers = new double?[] { 1, 1, .5, .5, 1, .5, 0, .5 };
                    demo.Directions[0].Evidence = "Положение о защите информации, утверждённые регламенты и журнал плановых проверок.";
                    MainWindow window = new MainWindow(demo, false);
                    window.RenderPreview("quick", Path.Combine(folder, "Оценка.png"));
                    window.RenderPreview("quick", Path.Combine(folder, "Оценка компактно.png"), 1280, 740);
                    window.RenderPreview("assessment", Path.Combine(folder, "Подробно.png"));
                    window.RenderPreview("results", Path.Combine(folder, "Результаты.png"));
                    window.RenderPreview("settings", Path.Combine(folder, "Параметры.png"));
                    ExportReviewWindow review = new ExportReviewWindow(demo, true);
                    review.RenderPreview(Path.Combine(folder, "Комплект.png"));
                    app.Shutdown(); return 0;
                }
                Assessment initial = null;
                string input = args.FirstOrDefault(x => !x.StartsWith("--") && File.Exists(x));
                if (input != null) initial = ProjectTools.LoadAssessmentFile(input);
                MainWindow main = new MainWindow(initial, true);
                if (input != null) main.SetProjectPath(input);
                app.Run(main); return 0;
            }
            catch (Exception ex)
            {
                string message = ex.GetType().Name + ": " + ex.Message;
                if (args.Any(x => x.StartsWith("--")))
                {
                    try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagnostics.txt"), ex.ToString()); } catch { }
                }
                else MessageBox.Show(message, "Не удалось открыть калькулятор", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
    public static class Theme
    {
        public static readonly Brush Ink = B("#1B2A41"), Muted = B("#6B778B"), Blue = B("#2864D7"), Line = B("#E2E7EF"), Paper = B("#F3F6FB");
        public static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
        public static void Install(Application app)
        {
            app.Resources = (ResourceDictionary)XamlReader.Parse(@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
              <Style TargetType='Button'>
                <Setter Property='Padding' Value='14,9'/><Setter Property='Margin' Value='0,0,8,0'/><Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='#1B2A41'/><Setter Property='BorderThickness' Value='0'/><Setter Property='FontSize' Value='13'/><Setter Property='Cursor' Value='Hand'/><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='bd' Background='{TemplateBinding Background}' CornerRadius='7' Padding='{TemplateBinding Padding}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.78'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='bd' Property='Opacity' Value='0.6'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#2864D7'/><Setter TargetName='bd' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
              </Style>
              <Style TargetType='TextBox'><Setter Property='Padding' Value='10,8'/><Setter Property='BorderBrush' Value='#D9E0EA'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Background' Value='White'/><Setter Property='Foreground' Value='#1B2A41'/><Setter Property='FontSize' Value='13'/><Setter Property='VerticalContentAlignment' Value='Center'/></Style>
              <Style TargetType='RadioButton'><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#1B2A41'/><Setter Property='Cursor' Value='Hand'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='RadioButton'><Grid Background='Transparent'><Grid.ColumnDefinitions><ColumnDefinition Width='24'/><ColumnDefinition Width='*'/></Grid.ColumnDefinitions><Border Width='16' Height='16' CornerRadius='8' Background='White' BorderBrush='#9AA9BD' BorderThickness='1' VerticalAlignment='Center'><Ellipse x:Name='dot' Fill='#2864D7' Margin='3' Visibility='Collapsed'/></Border><ContentPresenter Grid.Column='1' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='dot' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='0.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
              <Style TargetType='CheckBox'><Setter Property='FontSize' Value='13'/><Setter Property='Foreground' Value='#1B2A41'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Cursor' Value='Hand'/></Style>
              <Style TargetType='ToolTip'><Setter Property='MaxWidth' Value='420'/></Style>
            </ResourceDictionary>");
        }
        public static TextBlock Text(string text, double size = 13, Brush brush = null, bool bold = false)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = brush ?? Ink, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        }
        public static Border Card(UIElement child, Thickness? padding = null)
        {
            return new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = padding ?? new Thickness(20), Child = child };
        }
        public static Button Button(string text, Action action, bool primary = false)
        {
            Button b = new Button { Content = text, Background = primary ? Blue : Brushes.White, Foreground = primary ? Brushes.White : Ink };
            b.Click += (s, e) => action(); return b;
        }
        public static Brush LevelColor(int level)
        {
            return B(new[] { "#C44850", "#C47A26", "#B08B1C", "#278458", "#2189B0" }[Math.Max(0, Math.Min(4, level))]);
        }
    }
    public partial class MainWindow : Window
    {
        Assessment assessment;
        readonly bool live;
        readonly string autoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Данные", "Автосохранение.json");
        readonly DispatcherTimer saveTimer;
        string projectPath, bundlePath, page = "quick", quickSearch = "";
        int selected;
        bool dirty, building, autoOk, chartRadar = true, onlyIncomplete;
        Grid shell, center;
        TextBlock headerTitle, headerSub, status, sidebarProgress;
        ProgressBar globalProgress;
        TextBox search;
        Button navAssess, navResults, navSettings;
        TextBlock directionLevel, directionP, directionProgress, remainingText, overallValue, overallLabel, answeredValue, countValue;
        StackPanel recommendationPanel;
        readonly List<RadioButton[]> answerRadios = new List<RadioButton[]>();
        readonly List<Border[]> answerBorders = new List<Border[]>();
        readonly List<QuickRowView> quickRows = new List<QuickRowView>();
        TextBlock quickScore, quickNotice, quickHint;
        Button bundleButton, undoButton;
        ComboBox quickCopySource;
        List<AnswerChange> copiedAnswers;
        public MainWindow(Assessment initial, bool runtime)
        {
            live = runtime; assessment = initial ?? Assessment.Create();
            string recoveryError = null;
            if (initial == null && live && File.Exists(autoPath))
            {
                try { assessment = Store.Load(autoPath); autoOk = true; dirty = true; }
                catch
                {
                    assessment = Assessment.Create();
                    recoveryError = "Автокопия повреждена. Создана новая оценка; исходная автокопия сохранена для восстановления.";
                    try { File.Copy(autoPath, Path.Combine(Path.GetDirectoryName(autoPath), "Автосохранение-повреждено-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json"), false); }
                    catch { recoveryError = "Не удалось прочитать автокопию: " + autoPath + ". Сохраните её отдельно перед изменениями."; }
                }
            }
            Title = "Уровень зрелости · Калькулятор Узи";
            MinWidth = 1100; MinHeight = 720;
            Width = Math.Max(MinWidth, Math.Min(1450, SystemParameters.WorkArea.Width - 40));
            Height = Math.Max(MinHeight, Math.Min(960, SystemParameters.WorkArea.Height - 40));
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 13;
            Background = Theme.Paper;
            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            saveTimer.Tick += (s, e) => { saveTimer.Stop(); AutoSave(); };
            BuildShell(); ShowPage();
            if (autoOk) status.Text = "Восстановлена последняя оценка · данные на этом компьютере";
            if (recoveryError != null) status.Text = recoveryError;
            Closing += (s, e) =>
            {
                if (!dirty) return;
                if (projectPath != null)
                {
                    MessageBoxResult saveChoice = MessageBox.Show(this, "Сохранить изменения в проекте «" + Path.GetFileName(projectPath) + "»?", "Изменённая оценка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (saveChoice == MessageBoxResult.Cancel || saveChoice == MessageBoxResult.Yes && !SaveProject(false)) { e.Cancel = true; return; }
                }
                else if (bundlePath != null)
                {
                    MessageBoxResult saveChoice = MessageBox.Show(this, "Обновить комплект «" + Path.GetFileName(bundlePath) + "» перед закрытием?", "Изменённая оценка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                    if (saveChoice == MessageBoxResult.Cancel || saveChoice == MessageBoxResult.Yes && !SaveBundleFast(false)) { e.Cancel = true; return; }
                }
                saveTimer.Stop(); AutoSave();
                if (autoOk) return;
                MessageBoxResult answer = MessageBox.Show(this, "Автосохранение недоступно. Сохранить оценку в выбранный файл перед закрытием?", "Сохранение оценки", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (answer == MessageBoxResult.Cancel) e.Cancel = true;
                if (answer == MessageBoxResult.Yes && !SaveProject(false)) e.Cancel = true;
            };
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Save, (s, e) => { if (projectPath == null && bundlePath != null) SaveBundleFast(false); else SaveProject(false); }));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.Open, (s, e) => OpenProject()));
            CommandBindings.Add(new CommandBinding(ApplicationCommands.New, (s, e) => NewProject()));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Save, Key.S, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(ApplicationCommands.New, Key.N, ModifierKeys.Control));
            InputBindings.Add(new KeyBinding(new RelayCommand(() => SaveProject(true)), Key.S, ModifierKeys.Control | ModifierKeys.Shift));
        }
        public void SetProjectPath(string path)
        {
            bool bundle = String.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase);
            projectPath = bundle ? null : path; bundlePath = bundle ? path : null; dirty = false; UpdateNumbers(); UpdateHeader();
        }
        void BuildShell()
        {
            shell = new Grid { Background = Theme.Paper };
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition());
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = shell;
            Grid header = new Grid { Margin = new Thickness(24, 18, 20, 14) };
            header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); shell.Children.Add(header);
            StackPanel title = new StackPanel(); headerTitle = Theme.Text("Оценка зрелости", 24, null, true);
            headerSub = Theme.Text("Данные сохраняются автоматически", 12, Theme.Muted); headerSub.Margin = new Thickness(0, 5, 10, 0); title.Children.Add(headerTitle); title.Children.Add(headerSub); header.Children.Add(title);
            StackPanel actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(actions, 1); header.Children.Add(actions);
            actions.Children.Add(Theme.Button("Открыть", OpenProject));
            Button more = Theme.Button("Ещё  ▾", () => { }); ContextMenu menu = new ContextMenu();
            MenuItem create = new MenuItem { Header = "Новая оценка" }; create.Click += (s, e) => NewProject(); menu.Items.Add(create);
            MenuItem save = new MenuItem { Header = "Сохранить файл проекта (.uzi.json)" }; save.Click += (s, e) => SaveProject(false); menu.Items.Add(save);
            MenuItem saveAs = new MenuItem { Header = "Сохранить проект как…" }; saveAs.Click += (s, e) => SaveProject(true); menu.Items.Add(saveAs);
            menu.Items.Add(new Separator());
            MenuItem word = new MenuItem { Header = "Только отчёт Word (.docx)" }; word.Click += (s, e) => Export(true); menu.Items.Add(word);
            MenuItem excel = new MenuItem { Header = "Только таблица Excel (.xlsx)" }; excel.Click += (s, e) => Export(false); menu.Items.Add(excel);
            MenuItem bundleAs = new MenuItem { Header = "Сохранить комплект как…" }; bundleAs.Click += (s, e) => SaveBundleFast(true); menu.Items.Add(bundleAs);
            MenuItem custom = new MenuItem { Header = "Выбрать состав комплекта…" }; custom.Click += (s, e) => ExportBundle(); menu.Items.Add(custom);
            more.ContextMenu = menu; more.Click += (s, e) => { menu.PlacementTarget = more; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; }; actions.Children.Add(more);
            bundleButton = Theme.Button("Сохранить комплект", () => SaveBundleFast(false), true);
            bundleButton.ToolTip = "Проект, Word и Excel одним действием. При первом сохранении выберите файл; следующие нажатия обновят его."; actions.Children.Add(bundleButton);
            Grid tabs = new Grid { Margin = new Thickness(24, 0, 24, 12) }; tabs.ColumnDefinitions.Add(new ColumnDefinition()); tabs.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); Grid.SetRow(tabs, 1); shell.Children.Add(tabs);
            StackPanel nav = new StackPanel { Orientation = Orientation.Horizontal };
            navAssess = Nav("Оценка", "quick"); navResults = Nav("Результат", "results"); navSettings = Nav("Данные для отчёта", "settings"); nav.Children.Add(navAssess); nav.Children.Add(navResults); nav.Children.Add(navSettings); tabs.Children.Add(nav);
            StackPanel progress = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(progress, 1); tabs.Children.Add(progress);
            sidebarProgress = Theme.Text("Заполнено 0 из 168", 12, Theme.Muted); progress.Children.Add(sidebarProgress);
            globalProgress = new ProgressBar { Minimum = 0, Maximum = 100, Width = 145, Height = 5, Margin = new Thickness(12, 0, 0, 0), Foreground = Theme.Blue, Background = Theme.B("#DDE5F1"), BorderThickness = new Thickness(0) }; progress.Children.Add(globalProgress);
            center = new Grid { Margin = new Thickness(24, 0, 24, 0) }; Grid.SetRow(center, 2); shell.Children.Add(center);
            Grid footer = new Grid { Margin = new Thickness(24, 8, 24, 10) }; footer.ColumnDefinitions.Add(new ColumnDefinition()); footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); Grid.SetRow(footer, 3); shell.Children.Add(footer);
            status = Theme.Text("Автосохранение включено · данные на этом компьютере", 11, Theme.Muted); footer.Children.Add(status);
            undoButton = Theme.Button("Отменить заполнение", UndoCopiedAnswers); undoButton.Foreground = Theme.Blue; undoButton.FontSize = 11; undoButton.Padding = new Thickness(8, 4, 8, 4); undoButton.Visibility = Visibility.Collapsed; Grid.SetColumn(undoButton, 1); footer.Children.Add(undoButton);
        }
        Button Nav(string label, string destination)
        {
            Button b = new Button { Content = label, Background = Brushes.Transparent, Foreground = Theme.Muted, Padding = new Thickness(16, 9, 16, 9), Margin = new Thickness(0, 0, 7, 0) };
            b.Click += (s, e) => { page = destination; ShowPage(); }; return b;
        }
        void FilterDirections()
        {
            string q = quickSearch.Trim();
            foreach (QuickRowView row in quickRows)
            {
                DirectionAssessment d = row.Model;
                bool match = q.Length == 0 || (d.Id + " " + Data.DirectionNames[d.Id - 1]).IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0;
                row.Root.Visibility = match && (!onlyIncomplete || d.Included && Calc.Direction(d).Answered < 8) ? Visibility.Visible : Visibility.Collapsed;
            }
        }
        void ShowPage()
        {
            building = true; center.Children.Clear(); answerRadios.Clear(); answerBorders.Clear(); quickRows.Clear(); quickScore = quickNotice = quickHint = null; quickCopySource = null;
            directionLevel = directionP = directionProgress = remainingText = overallValue = overallLabel = answeredValue = countValue = null; recommendationPanel = null;
            bool assessing = page == "quick" || page == "assessment";
            navAssess.Background = assessing ? Theme.Blue : Brushes.Transparent; navAssess.Foreground = assessing ? Brushes.White : Theme.Muted;
            navResults.Background = page == "results" ? Theme.Blue : Brushes.Transparent; navResults.Foreground = page == "results" ? Brushes.White : Theme.Muted;
            navSettings.Background = page == "settings" ? Theme.Blue : Brushes.Transparent; navSettings.Foreground = page == "settings" ? Brushes.White : Theme.Muted;
            headerTitle.Text = page == "quick" ? "Оценка зрелости" : page == "assessment" ? "Подробная оценка" : page == "results" ? "Результат оценки" : "Данные для отчёта";
            if (page == "quick") BuildQuickAssessment(); else if (page == "assessment") BuildAssessment(); else if (page == "results") BuildResults(); else BuildSettings();
            building = false; UpdateNumbers(); UpdateHeader();
        }
        StackPanel PageStack()
        {
            StackPanel p = new StackPanel();
            center.Children.Add(new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 12, 0) }); return p;
        }
        Grid Metrics()
        {
            Grid grid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int i = 0; i < 3; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            TextBlock[] values = new TextBlock[3];
            string[] labels = { "ТЕКУЩИЙ ПОКАЗАТЕЛЬ УЗИ", "ЗАПОЛНЕНИЕ АНКЕТЫ", "НАПРАВЛЕНИЯ В ОЦЕНКЕ" };
            for (int i = 0; i < 3; i++)
            {
                StackPanel stack = new StackPanel(); stack.Children.Add(Theme.Text(labels[i], 10, Theme.Muted, true));
                values[i] = Theme.Text("—", 30, i == 0 ? Theme.Blue : Theme.Ink, true); values[i].Margin = new Thickness(0, 6, 0, 2); stack.Children.Add(values[i]);
                if (i == 0) { overallLabel = Theme.Text("Предварительный расчёт", 11, Theme.Muted); stack.Children.Add(overallLabel); }
                else stack.Children.Add(Theme.Text(i == 1 ? "Пустой ответ отличается от оценки 0" : "Галочка в таблице включает направление", 11, Theme.Muted));
                Border card = Theme.Card(stack, new Thickness(18, 15, 16, 15)); card.Margin = new Thickness(0, 0, i < 2 ? 12 : 0, 0); Grid.SetColumn(card, i); grid.Children.Add(card);
            }
            overallValue = values[0]; answeredValue = values[1]; countValue = values[2]; return grid;
        }
        void BuildAssessment()
        {
            Grid outer = new Grid(); outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); outer.RowDefinitions.Add(new RowDefinition()); center.Children.Add(outer);
            outer.Children.Add(Metrics());
            Grid content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition()); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(278) }); Grid.SetRow(content, 1); outer.Children.Add(content);
            StackPanel form = new StackPanel();
            ScrollViewer scroll = new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 14, 0) }; content.Children.Add(scroll);
            DirectionAssessment d = assessment.Directions[selected];
            TextBlock overline = Theme.Text("НАПРАВЛЕНИЕ " + d.Id.ToString("00") + " / 21", 11, Theme.Blue, true); overline.Margin = new Thickness(0, 0, 0, 7); form.Children.Add(overline);
            TextBlock heading = Theme.Text(Data.DirectionNames[selected], 23, null, true); heading.LineHeight = 29; heading.Margin = new Thickness(0, 0, 0, 10); form.Children.Add(heading);
            TextBlock help = Theme.Text("Выберите описание, которое подтверждается фактической работой и документами. Баллы и уровень пересчитываются сразу.", 12, Theme.Muted); help.Margin = new Thickness(0, 0, 0, 18); form.Children.Add(help);
            if (!d.Included)
            {
                StackPanel excluded = new StackPanel(); excluded.Children.Add(Theme.Text("Направление исключено из расчёта", 16, null, true));
                TextBlock h = Theme.Text("Ответы сохранены. Включите направление, если соответствующие технологии применяются в вашей системе.", 13, Theme.Muted); h.Margin = new Thickness(0, 8, 0, 14); excluded.Children.Add(h);
                excluded.Children.Add(Theme.Button("Включить в область оценки", () => { d.Included = true; Changed(); ShowPage(); }, true));
                form.Children.Add(Theme.Card(excluded));
            }
            for (int j = 0; j < 8; j++)
            {
                int criterionIndex = j; Criterion c = Data.Criteria[j];
                StackPanel criterion = new StackPanel();
                Grid top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                top.Children.Add(Theme.Text((j + 1) + ". " + c.Name, 16, null, true)); TextBlock weight = Theme.Text("вес " + Calc.F(c.Weight), 11, Theme.Muted); Grid.SetColumn(weight, 1); top.Children.Add(weight); criterion.Children.Add(top);
                TextBlock desc = Theme.Text(c.Description, 11, Theme.Muted); desc.Margin = new Thickness(0, 5, 0, 12); criterion.Children.Add(desc);
                RadioButton[] radios = new RadioButton[c.Options.Length]; Border[] borders = new Border[c.Options.Length];
                for (int k = 0; k < c.Options.Length; k++)
                {
                    int optionIndex = k; AnswerOption option = c.Options[k];
                    Grid optionContent = new Grid(); optionContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(31) }); optionContent.ColumnDefinitions.Add(new ColumnDefinition());
                    TextBlock v = Theme.Text(Calc.F(option.Value), 13, Theme.Blue, true); optionContent.Children.Add(v);
                    TextBlock txt = Theme.Text(option.Text, 12.5); txt.LineHeight = 18; Grid.SetColumn(txt, 1); optionContent.Children.Add(txt);
                    RadioButton radio = new RadioButton { Content = optionContent, GroupName = "d" + d.Id + "c" + j, IsChecked = d.Answers[j].HasValue && Math.Abs(d.Answers[j].Value - option.Value) < 1e-9, Padding = new Thickness(0), IsEnabled = d.Included };
                    Border optionBorder = new Border { CornerRadius = new CornerRadius(7), BorderThickness = new Thickness(1), BorderBrush = Theme.Line, Padding = new Thickness(10, 11, 10, 11), Margin = new Thickness(0, 0, 0, 6), Child = radio };
                    radio.Checked += (s, e) => { if (building) return; ForgetCopiedAnswer(d.Id, criterionIndex); d.Answers[criterionIndex] = c.Options[optionIndex].Value; Changed(); UpdateNumbers(); };
                    radios[k] = radio; borders[k] = optionBorder; criterion.Children.Add(optionBorder);
                }
                answerRadios.Add(radios); answerBorders.Add(borders);
                Button clear = Theme.Button("Снять ответ", () => { ForgetCopiedAnswer(d.Id, criterionIndex); d.Answers[criterionIndex] = null; building = true; foreach (RadioButton rb in answerRadios[criterionIndex]) rb.IsChecked = false; building = false; Changed(); UpdateNumbers(); });
                clear.FontSize = 11; clear.Foreground = Theme.Muted; clear.Padding = new Thickness(0, 5, 5, 0); clear.HorizontalAlignment = HorizontalAlignment.Left; clear.IsEnabled = d.Included; criterion.Children.Add(clear);
                Border card = Theme.Card(criterion, new Thickness(18)); card.Margin = new Thickness(0, 0, 0, 14); form.Children.Add(card);
            }
            StackPanel evidence = new StackPanel(); evidence.Children.Add(Theme.Text("Подтверждающие материалы и комментарии", 15, null, true));
            TextBlock evidenceHelp = Theme.Text("Укажите регламенты, журналы, результаты проверок и основания для исключения направления.", 12, Theme.Muted); evidenceHelp.Margin = new Thickness(0, 6, 0, 10); evidence.Children.Add(evidenceHelp);
            TextBox notes = new TextBox { Text = d.Evidence, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            notes.TextChanged += (s, e) => { d.Evidence = notes.Text; Changed(); }; evidence.Children.Add(notes);
            Border evcard = Theme.Card(evidence); evcard.Margin = new Thickness(0, 2, 0, 18); form.Children.Add(evcard);
            StackPanel next = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            Button previous = Theme.Button("← Предыдущее", () => { selected--; ShowPage(); }); previous.IsEnabled = selected > 0; next.Children.Add(previous);
            next.Children.Add(Theme.Button(selected == 20 ? "К результатам →" : "Следующее →", () => { if (selected < 20) selected++; else page = "results"; ShowPage(); }, true)); form.Children.Add(next);
            StackPanel aside = new StackPanel { Margin = new Thickness(14, 0, 0, 0) }; ScrollViewer asideScroll = new ScrollViewer { Content = aside, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) }; Grid.SetColumn(asideScroll, 1); content.Children.Add(asideScroll);
            StackPanel score = new StackPanel(); score.Children.Add(Theme.Text("УРОВЕНЬ НАПРАВЛЕНИЯ", 10, Theme.Muted, true)); directionLevel = Theme.Text("0", 42, Theme.Blue, true); directionLevel.Margin = new Thickness(0, 6, 0, 0); score.Children.Add(directionLevel);
            directionProgress = Theme.Text("", 12, Theme.Muted); score.Children.Add(directionProgress); directionP = Theme.Text("", 12); directionP.Margin = new Thickness(0, 12, 0, 14); score.Children.Add(directionP);
            score.Children.Add(Theme.Text("Целевой уровень", 12, null, true));
            StackPanel targets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 12) };
            for (int target = 1; target <= 4; target++)
            {
                int value = target; Button b = Theme.Button(target.ToString(), () => { d.Target = value; Changed(); ShowPage(); }, d.Target == target); b.Width = 40; b.Padding = new Thickness(0, 9, 0, 9); b.Margin = new Thickness(0, 0, 6, 0); b.IsEnabled = target >= Math.Max(1, assessment.Category); targets.Children.Add(b);
            }
            score.Children.Add(targets); score.Children.Add(Theme.Text("Минимум для категории: " + Math.Max(1, assessment.Category) + ". Цель можно повысить до 4.", 11, Theme.Muted));
            aside.Children.Add(Theme.Card(score, new Thickness(16)));
            StackPanel hint = new StackPanel(); hint.Children.Add(Theme.Text("Что нужно до цели", 15, null, true)); recommendationPanel = new StackPanel { Margin = new Thickness(0, 12, 0, 0) }; hint.Children.Add(recommendationPanel);
            Border hintCard = Theme.Card(hint, new Thickness(16)); hintCard.Margin = new Thickness(0, 14, 0, 14); aside.Children.Add(hintCard);
            aside.Children.Add(Theme.Text("P = Σ (вес × оценка). Уровень определяется по порогу суммы и обязательным минимумам таблицы 3. Неотвеченные критерии временно дают 0.", 11, Theme.Muted));
        }
        void UpdateNumbers()
        {
            Summary summary = Calc.Summary(assessment);
            sidebarProgress.Text = "Заполнено " + summary.Answered + " из " + summary.Total;
            globalProgress.Value = summary.Total == 0 ? 0 : 100.0 * summary.Answered / summary.Total;
            if (overallValue != null) overallValue.Text = summary.Count == 0 ? "—" : Calc.F(summary.Average, "0.00");
            if (overallLabel != null) overallLabel.Text = summary.Count == 0 ? "Выберите область оценки" : (summary.Complete ? "" : "Предварительно · ") + Calc.LevelName((int)Math.Floor(summary.Average));
            if (answeredValue != null) answeredValue.Text = summary.Answered + " / " + summary.Total;
            if (countValue != null) countValue.Text = summary.Count + " из 21";
            if (bundleButton != null) { bundleButton.IsEnabled = summary.Count > 0; bundleButton.Content = bundlePath == null ? "Сохранить комплект" : "Обновить комплект"; }
            if (undoButton != null) undoButton.Visibility = copiedAnswers != null && copiedAnswers.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (quickScore != null) quickScore.Text = summary.Count == 0 ? "Узи —" : "Узи " + Calc.F(summary.Average, "0.00");
            if (quickNotice != null) quickNotice.Text = summary.Count == 0 ? "Включите направления галочками в таблице" : summary.Complete ? "Анкета заполнена · целей достигнуто: " + summary.Met + "/" + summary.Count : "Предварительно · осталось ответов: " + (summary.Total - summary.Answered);
            foreach (QuickRowView row in quickRows)
            {
                DirectionResult result = Calc.Direction(row.Model);
                foreach (QuickAnswerCell cell in row.Cells) cell.Refresh();
                row.Level.Text = row.Model.Included ? result.Level + "\n" + result.Answered + "/8" : "—";
                row.Level.Foreground = result.Complete ? Theme.LevelColor(result.Level) : Theme.Muted; row.Target.IsEnabled = row.Model.Included;
            }
            if (quickCopySource != null)
            {
                foreach (ComboBoxItem item in quickCopySource.Items) item.IsEnabled = assessment.Directions[(int)item.Tag - 1].Included;
                ComboBoxItem itemSelected = quickCopySource.SelectedItem as ComboBoxItem;
                if (itemSelected == null || !itemSelected.IsEnabled) quickCopySource.SelectedIndex = Array.FindIndex(assessment.Directions, d => d.Included);
            }
            FilterDirections();
            if (directionLevel != null)
            {
                DirectionAssessment d = assessment.Directions[selected]; DirectionResult r = Calc.Direction(d);
                directionLevel.Text = d.Included ? r.Level.ToString() : "—"; directionLevel.Foreground = Theme.LevelColor(r.Level);
                directionProgress.Text = d.Included ? Calc.LevelName(r.Level) + "\n" + r.Answered + " из 8 ответов" + (r.Complete ? "" : " · предварительно") : "Исключено из расчёта";
                directionP.Text = "Взвешенная сумма P = " + Calc.F(r.Score, "0.000");
                recommendationPanel.Children.Clear();
                if (!d.Included) recommendationPanel.Children.Add(Theme.Text("Направление не учитывается в общем показателе.", 12, Theme.Muted));
                else foreach (string rec in r.Recommendations) { TextBlock t = Theme.Text("•  " + rec, 11.5, Theme.Muted); t.Margin = new Thickness(0, 0, 0, 12); recommendationPanel.Children.Add(t); }
                for (int j = 0; j < answerBorders.Count; j++)
                    for (int k = 0; k < answerBorders[j].Length; k++)
                    {
                        bool check = answerRadios[j][k].IsChecked == true;
                        answerBorders[j][k].Background = check ? Theme.B("#ECF3FF") : Brushes.White;
                        answerBorders[j][k].BorderBrush = check ? Theme.B("#89AEF0") : Theme.Line;
                    }
            }
            if (remainingText != null) remainingText.Text = summary.Count == 0 ? "Область оценки пуста. Включите хотя бы одно направление в таблице." : summary.Complete ? "Анкета заполнена. Целевой уровень достигнут по " + summary.Met + " из " + summary.Count + " направлений." : "Предварительный результат: осталось ответить на " + (summary.Total - summary.Answered) + " вопросов. Неотвеченные критерии временно учтены как 0.";
        }
        void BuildResults()
        {
            StackPanel body = PageStack(); body.Children.Add(Metrics());
            Summary sum = Calc.Summary(assessment);
            StackPanel note = new StackPanel(); remainingText = Theme.Text("", 13, sum.Complete ? Theme.B("#278458") : Theme.B("#926415"), true); note.Children.Add(remainingText);
            TextBlock formula = Theme.Text("Узи = сумма текущих уровней направлений / число включённых направлений. Цель проверяется отдельно по каждому направлению.", 12, Theme.Muted); formula.Margin = new Thickness(0, 7, 0, 0); note.Children.Add(formula);
            Border notice = Theme.Card(note, new Thickness(18, 14, 18, 14)); notice.Background = sum.Complete ? Theme.B("#F0F9F4") : Theme.B("#FFF9ED"); notice.Margin = new Thickness(0, 0, 0, 20); body.Children.Add(notice);
            Grid charts = new Grid(); charts.ColumnDefinitions.Add(new ColumnDefinition()); charts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(264) });
            StackPanel plot = new StackPanel(); plot.Children.Add(Theme.Text("Профиль зрелости", 18, null, true));
            TextBlock legend = Theme.Text("Синий — текущий уровень    Оранжевый — цель    × — исключено", 11, Theme.Muted); legend.Margin = new Thickness(0, 8, 0, 12); plot.Children.Add(legend);
            StackPanel chartModes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            Button radarMode = Theme.Button("Радар", () => { chartRadar = true; ShowPage(); }, chartRadar); radarMode.Padding = new Thickness(10, 5, 10, 5); radarMode.FontSize = 11; chartModes.Children.Add(radarMode);
            Button barsMode = Theme.Button("Столбцы", () => { chartRadar = false; ShowPage(); }, !chartRadar); barsMode.Padding = new Thickness(10, 5, 10, 5); barsMode.FontSize = 11; chartModes.Children.Add(barsMode); plot.Children.Add(chartModes);
            plot.Children.Add(chartRadar ? (FrameworkElement)new RadarProfile(assessment) : ProfileChart()); Border plotCard = Theme.Card(plot); plotCard.Margin = new Thickness(0, 0, 16, 0); charts.Children.Add(plotCard);
            StackPanel levels = new StackPanel(); levels.Children.Add(Theme.Text("Шкала уровней", 16, null, true));
            for (int i = 0; i < 5; i++) { TextBlock l = Theme.Text(i + "  " + Calc.LevelName(i), 12, Theme.LevelColor(i), true); l.Margin = new Thickness(0, 13, 0, 0); levels.Children.Add(l); }
            TextBlock lb = Theme.Text("Приоритеты определяются разрывом с целью и обязательными минимумами. Высокая сумма P сама по себе не гарантирует нужный уровень.", 11, Theme.Muted); lb.Margin = new Thickness(0, 18, 0, 0); levels.Children.Add(lb);
            Border levelCard = Theme.Card(levels, new Thickness(17)); Grid.SetColumn(levelCard, 1); charts.Children.Add(levelCard); body.Children.Add(charts);
            TextBlock tableTitle = Theme.Text("Результаты по направлениям", 20, null, true); tableTitle.Margin = new Thickness(0, 25, 0, 12); body.Children.Add(tableTitle);
            Grid table = new Grid(); double[] widths = { 38, 0, 70, 65, 60, 105 };
            foreach (double width in widths) table.ColumnDefinitions.Add(new ColumnDefinition { Width = width == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(width) });
            string[] labels = { "№", "Направление", "P", "Уровень", "Цель", "Статус" }; TableRow(table, labels, 0, true, null);
            for (int i = 0; i < 21; i++)
            {
                DirectionAssessment d = assessment.Directions[i]; DirectionResult r = Calc.Direction(d); int idx = i;
                string state = !d.Included ? "Исключено" : !r.Complete ? r.Answered + "/8 ответов" : r.Gap == 0 ? "Достигнута" : "Разрыв: " + r.Gap;
                TableRow(table, new[] { d.Id.ToString(), Data.DirectionNames[i], d.Included ? Calc.F(r.Score, "0.000") : "—", d.Included ? r.Level.ToString() : "—", d.Included ? d.Target.ToString() : "—", state }, i + 1, false, () => { selected = idx; page = "assessment"; ShowPage(); });
            }
            body.Children.Add(Theme.Card(table, new Thickness(8)));
            TextBlock recTitle = Theme.Text("Рекомендации для достижения целей", 20, null, true); recTitle.Margin = new Thickness(0, 24, 0, 12); body.Children.Add(recTitle);
            bool any = false;
            foreach (DirectionAssessment d in assessment.Directions.Where(x => x.Included).OrderByDescending(x => Calc.Direction(x).Gap).ThenBy(x => x.Id))
            {
                DirectionResult r = Calc.Direction(d); if (r.Complete && r.Gap == 0) continue; any = true;
                StackPanel recs = new StackPanel(); recs.Children.Add(Theme.Text(d.Id + ". " + Data.DirectionNames[d.Id - 1], 14, null, true));
                foreach (string rec in r.Recommendations) { TextBlock t = Theme.Text("•  " + rec, 12, Theme.Muted); t.Margin = new Thickness(0, 9, 0, 0); recs.Children.Add(t); }
                Border card = Theme.Card(recs, new Thickness(18)); card.Margin = new Thickness(0, 0, 0, 12); body.Children.Add(card);
            }
            if (!any) body.Children.Add(Theme.Card(Theme.Text(sum.Count == 0 ? "Включите направления галочками на вкладке «Оценка»." : "Целевые уровни достигнуты по всем включённым направлениям.", 13)));
            StackPanel exports = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 24) };
            exports.Children.Add(Theme.Button("Сохранить комплект", () => SaveBundleFast(false), true)); exports.Children.Add(Theme.Button("Отчёт Word", () => Export(true))); exports.Children.Add(Theme.Button("Таблица Excel", () => Export(false))); body.Children.Add(exports);
        }
        FrameworkElement ProfileChart()
        {
            // A responsive WPF chart; no browser or online components.
            Grid grid = new Grid { Height = 220 };
            grid.RowDefinitions.Add(new RowDefinition()); grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) });
            Grid axis = new Grid(); grid.Children.Add(axis);
            for (int i = 0; i <= 4; i++) { TextBlock t = Theme.Text((4 - i).ToString(), 10, Theme.Muted); t.VerticalAlignment = i == 0 ? VerticalAlignment.Top : i == 4 ? VerticalAlignment.Bottom : VerticalAlignment.Top; t.Margin = new Thickness(0, i == 0 || i == 4 ? 0 : 49 * i - 8, 0, 0); axis.Children.Add(t); }
            for (int i = 0; i < 21; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                DirectionAssessment d = assessment.Directions[i]; DirectionResult r = Calc.Direction(d);
                Grid column = new Grid { Margin = new Thickness(2, 0, 2, 0), ToolTip = d.Id + ". " + Data.DirectionNames[i] + (d.Included ? "\nУровень " + r.Level + ", цель " + d.Target : "\nИсключено") };
                Grid.SetColumn(column, i + 1); grid.Children.Add(column);
                for (int l = 0; l <= 4; l++) { Border line = new Border { Height = 1, Background = Theme.Line, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, l * 49) }; column.Children.Add(line); }
                Border bar = new Border { Height = d.Included ? Math.Max(2, 49 * r.Level) : 2, Background = d.Included ? Theme.Blue : Theme.B("#CED7E4"), VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(3, 3, 0, 0), MaxWidth = 20 }; column.Children.Add(bar);
                if (d.Included) { Border target = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(2), BorderBrush = Theme.B("#E59B34"), Background = Brushes.White, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, d.Target * 49 - 4.5) }; column.Children.Add(target); }
                TextBlock num = Theme.Text(d.Included ? d.Id.ToString() : "×", 9, Theme.Muted); num.HorizontalAlignment = HorizontalAlignment.Center; Grid.SetRow(num, 1); Grid.SetColumn(num, i + 1); grid.Children.Add(num);
            }
            return grid;
        }
        void TableRow(Grid table, string[] values, int row, bool header, Action action)
        {
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int col = 0; col < values.Length; col++)
            {
                TextBlock text = Theme.Text(values[col], header ? 10 : 11.5, header ? Theme.Muted : Theme.Ink, header); text.Margin = new Thickness(6, 10, 6, 10);
                Border cell = new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, 1), Background = header ? Theme.B("#F5F7FB") : Brushes.White, Child = text };
                if (action != null) { cell.Cursor = Cursors.Hand; cell.MouseLeftButtonUp += (s, e) => action(); cell.ToolTip = "Открыть направление"; }
                Grid.SetRow(cell, row); Grid.SetColumn(cell, col); table.Children.Add(cell);
            }
        }
        void BuildSettings()
        {
            StackPanel body = PageStack();
            TextBlock intro = Theme.Text("Эти сведения попадут в документы. Для самого расчёта заполнять их необязательно.", 13, Theme.Muted); intro.Margin = new Thickness(0, 0, 0, 18); body.Children.Add(intro);
            StackPanel metadata = new StackPanel(); metadata.Children.Add(Theme.Text("Сведения об оценке", 18, null, true));
            Grid fields = new Grid { Margin = new Thickness(0, 16, 0, 0) }; fields.ColumnDefinitions.Add(new ColumnDefinition()); fields.ColumnDefinitions.Add(new ColumnDefinition());
            AddField(fields, 0, 0, "Организация", assessment.Organization, value => assessment.Organization = value);
            AddField(fields, 0, 1, "Информационная система / объект оценки", assessment.SystemName, value => assessment.SystemName = value);
            AddField(fields, 1, 0, "Кто провёл оценку", assessment.Assessor, value => assessment.Assessor = value);
            StackPanel dateField = new StackPanel { Margin = new Thickness(0, 0, 0, 14) }; dateField.Children.Add(Theme.Text("Дата оценки", 12, Theme.Muted));
            DatePicker date = new DatePicker { SelectedDate = DateTime.ParseExact(assessment.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture), Margin = new Thickness(0, 7, 0, 0), Height = 34, FontSize = 13 };
            date.SelectedDateChanged += (s, e) => { if (date.SelectedDate.HasValue) { assessment.Date = date.SelectedDate.Value.ToString("yyyy-MM-dd"); Changed(); } else date.SelectedDate = DateTime.ParseExact(assessment.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture); };
            dateField.Children.Add(date); Grid.SetRow(dateField, 1); Grid.SetColumn(dateField, 1); fields.Children.Add(dateField); metadata.Children.Add(fields);
            Grid additional = new Grid { Margin = new Thickness(0, 12, 0, 0) }; additional.ColumnDefinitions.Add(new ColumnDefinition()); additional.ColumnDefinitions.Add(new ColumnDefinition());
            AddField(additional, 0, 0, "Краткое название организации", assessment.OrganizationShort, value => assessment.OrganizationShort = value);
            AddField(additional, 0, 1, "Номер документа", assessment.DocumentNumber, value => assessment.DocumentNumber = value);
            AddField(additional, 1, 0, "Город", assessment.City, value => assessment.City = value);
            AddField(additional, 1, 1, "Адрес организации", assessment.OrganizationAddress, value => assessment.OrganizationAddress = value);
            AddField(additional, 2, 0, "Должность составителя", assessment.AssessorPosition, value => assessment.AssessorPosition = value);
            AddField(additional, 2, 1, "Должность утверждающего", assessment.ApproverPosition, value => assessment.ApproverPosition = value);
            AddField(additional, 3, 0, "ФИО утверждающего", assessment.ApproverName, value => assessment.ApproverName = value);
            Expander requisites = new Expander { Header = Theme.Text("Реквизиты для документов и подписи", 14, null, true), Content = additional, Margin = new Thickness(0, 0, 0, 15), IsExpanded = new[] { assessment.OrganizationShort, assessment.OrganizationAddress, assessment.City, assessment.AssessorPosition, assessment.ApproverName, assessment.ApproverPosition, assessment.DocumentNumber }.Any(x => !String.IsNullOrEmpty(x)) };
            metadata.Children.Add(requisites);
            metadata.Children.Add(Theme.Text("Общие замечания", 12, Theme.Muted));
            TextBox notes = new TextBox { Text = assessment.Notes, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 70, MaxHeight = 160, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 7, 0, 0) }; notes.TextChanged += (s, e) => { assessment.Notes = notes.Text; Changed(); }; metadata.Children.Add(notes);
            StackPanel profiles = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            profiles.Children.Add(Theme.Button("Загрузить реквизиты", LoadProfile)); profiles.Children.Add(Theme.Button("Сохранить реквизиты", SaveProfile)); metadata.Children.Add(profiles);
            TextBlock profileHelp = Theme.Text("Реквизиты и подписанты хранятся отдельно от оценки. Их загрузка не изменяет систему, дату, ответы и цели.", 11, Theme.Muted); profileHelp.Margin = new Thickness(0, 10, 0, 0); metadata.Children.Add(profileHelp);
            body.Children.Add(Theme.Card(metadata));
            StackPanel about = new StackPanel(); about.Children.Add(Theme.Text("О расчёте", 18, null, true));
            TextBlock rules = Theme.Text("21 базовое направление × 8 критериев. D = вес × оценка; P = сумма D. Текущий уровень — максимальный уровень с выполненным порогом P и обязательными минимумами таблицы 3. Итог Узи округляется до двух знаков. Интервал 0,2 ≤ P < 0,225 трактуется как уровень 0, как в исходном калькуляторе.\n\nНезаполненные ответы временно учитываются как 0; до полного заполнения итог помечается как предварительный. Если все направления исключены, показатель не рассчитывается. Данные не передаются в интернет.\n\nИсточник алгоритма и формулировок: 117fstec.credos.ru/maturity. Методика от " + Data.MethodDate + ". Это самостоятельное приложение; результат зависит от введённых данных.", 12, Theme.Muted); rules.Margin = new Thickness(0, 12, 0, 0); about.Children.Add(rules);
            Expander aboutCard = new Expander { Header = "Методика и справка", Content = about, Margin = new Thickness(0, 18, 0, 24) }; body.Children.Add(aboutCard);
        }
        void AddField(Grid grid, int row, int column, string label, string value, Action<string> setter)
        {
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            StackPanel field = new StackPanel { Margin = new Thickness(0, 0, column == 0 ? 18 : 0, 14) }; field.Children.Add(Theme.Text(label, 12, Theme.Muted));
            TextBox box = new TextBox { Text = value, Margin = new Thickness(0, 7, 0, 0) }; box.TextChanged += (s, e) => { setter(box.Text); Changed(); UpdateHeader(); }; field.Children.Add(box);
            Grid.SetRow(field, row); Grid.SetColumn(field, column); grid.Children.Add(field);
        }
        void UpdateHeader()
        {
            headerSub.Text = page == "quick" ? "Автокопия включена · 21 направление × 8 критериев" : assessment.Organization.Length > 0 ? assessment.Organization + (assessment.SystemName.Length > 0 ? " · " + assessment.SystemName : "") : assessment.SystemName.Length > 0 ? assessment.SystemName : "Сведения для отчёта можно заполнить по необходимости";
            headerSub.TextTrimming = TextTrimming.CharacterEllipsis; headerSub.TextWrapping = TextWrapping.NoWrap;
            string activePath = projectPath ?? bundlePath;
            Title = "Уровень зрелости · " + (activePath == null ? "Новая оценка" : Path.GetFileNameWithoutExtension(activePath)) + (dirty ? " *" : "");
        }
        void Changed()
        {
            if (building) return; dirty = true; autoOk = false; UpdateHeader();
            if (!live) return;
            status.Text = "Есть изменения · сохранение на этом компьютере…"; saveTimer.Stop(); saveTimer.Start();
        }
        void AutoSave()
        {
            if (!live) return;
            try { Store.Save(assessment, autoPath); autoOk = true; status.Text = "Автосохранено " + DateTime.Now.ToString("HH:mm") + " · данные на этом компьютере"; }
            catch (Exception ex) { autoOk = false; status.Text = "Автосохранение недоступно · используйте «Сохранить» · " + ex.Message; }
        }
        bool SaveProject(bool saveAs)
        {
            string path = projectPath;
            if (saveAs || path == null)
            {
                SaveFileDialog dialog = new SaveFileDialog { Title = "Сохранить оценку", Filter = "Проект оценки (*.uzi.json)|*.uzi.json|JSON (*.json)|*.json", FileName = DefaultName() + ".uzi.json", AddExtension = true, DefaultExt = ".uzi.json" };
                if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName;
            }
            try { Store.Save(assessment, path); projectPath = path; dirty = false; AutoSave(); status.Text = "Сохранено: " + path; UpdateHeader(); return true; }
            catch (Exception ex) { ShowError("Не удалось сохранить проект", ex); return false; }
        }
        bool PreserveCurrent()
        {
            if (!dirty) return true;
            MessageBoxResult choice = MessageBox.Show(this, "Сохранить изменения в текущей оценке перед переходом?", "Текущая оценка", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return choice == MessageBoxResult.No || choice == MessageBoxResult.Yes && (projectPath == null && bundlePath != null ? SaveBundleFast(false) : SaveProject(false));
        }
        void NewProject()
        {
            if (!PreserveCurrent()) return;
            saveTimer.Stop(); assessment = Assessment.Create(); projectPath = bundlePath = null; copiedAnswers = null; dirty = false; autoOk = false; selected = 0; quickSearch = ""; onlyIncomplete = false; page = "quick"; ShowPage(); AutoSave();
        }
        void OpenProject()
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Открыть оценку или комплект", Filter = "Оценки и комплекты (*.uzi.json;*.json;*.zip)|*.uzi.json;*.json;*.zip|Все файлы (*.*)|*.*" };
            if (dialog.ShowDialog(this) != true) return;
            Assessment loaded;
            try { loaded = ProjectTools.LoadAssessmentFile(dialog.FileName); } catch (Exception ex) { ShowError("Не удалось открыть оценку", ex); return; }
            if (!PreserveCurrent()) return;
            saveTimer.Stop(); assessment = loaded; copiedAnswers = null; dirty = false; selected = 0; quickSearch = ""; onlyIncomplete = false; page = "quick"; ShowPage(); SetProjectPath(dialog.FileName); AutoSave(); status.Text = "Открыто: " + dialog.FileName;
        }
        string DefaultName()
        {
            string name = "Оценка зрелости" + (assessment.Organization.Length > 0 ? " - " + assessment.Organization : "") + " - " + assessment.Date;
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_'); return name.Length > 150 ? name.Substring(0, 150) : name;
        }
        void Export(bool word)
        {
            Summary s = Calc.Summary(assessment);
            if (s.Count == 0) { MessageBox.Show(this, "Для отчёта включите хотя бы одно направление в таблице на вкладке «Оценка».", "Область оценки пуста", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            SaveFileDialog dialog = new SaveFileDialog { Title = word ? "Сформировать отчёт Word" : "Выгрузить таблицу Excel", Filter = word ? "Документ Word (*.docx)|*.docx" : "Книга Excel (*.xlsx)|*.xlsx", FileName = DefaultName() + (word ? ".docx" : ".xlsx"), AddExtension = true, DefaultExt = word ? ".docx" : ".xlsx" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                if (word) Reports.ExportWord(assessment, dialog.FileName); else Reports.ExportExcel(assessment, dialog.FileName);
                status.Text = "Отчёт сохранён: " + dialog.FileName + (s.Complete ? "" : " · предварительный результат");
            }
            catch (Exception ex) { ShowError("Не удалось сформировать отчёт", ex); }
            finally { Mouse.OverrideCursor = null; }
        }
        void ExportBundle()
        {
            if (Calc.Summary(assessment).Count == 0) { MessageBox.Show(this, "Включите хотя бы одно направление в таблице на вкладке «Оценка».", "Область оценки пуста", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            ExportReviewWindow review = new ExportReviewWindow(assessment, true) { Owner = this };
            if (review.ShowDialog() != true) { if (review.GoToSettings) { page = "settings"; ShowPage(); } return; }
            SaveFileDialog dialog = new SaveFileDialog { Title = "Сохранить комплект оценки", Filter = "Архив комплекта (*.zip)|*.zip", FileName = DefaultName() + ".zip", AddExtension = true, DefaultExt = ".zip" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                Mouse.OverrideCursor = Cursors.Wait;
                ProjectTools.ExportBundle(assessment, dialog.FileName, review.IncludeWord, review.IncludeExcel);
                status.Text = "Комплект сохранён: " + dialog.FileName;
            }
            catch (Exception ex) { ShowError("Не удалось сохранить комплект", ex); }
            finally { Mouse.OverrideCursor = null; }
        }
        bool SaveBundleFast(bool chooseNewPath = false)
        {
            if (Calc.Summary(assessment).Count == 0) { status.Text = "Включите хотя бы одно направление в таблице."; return false; }
            string path = chooseNewPath ? null : bundlePath;
            if (path == null)
            {
                SaveFileDialog dialog = new SaveFileDialog { Title = "Сохранить комплект: проект + Word + Excel", Filter = "Комплект оценки (*.zip)|*.zip", FileName = DefaultName() + ".zip", AddExtension = true, DefaultExt = ".zip" };
                if (bundlePath != null) { dialog.InitialDirectory = Path.GetDirectoryName(bundlePath); dialog.FileName = Path.GetFileName(bundlePath); }
                else if (projectPath != null) dialog.InitialDirectory = Path.GetDirectoryName(projectPath);
                if (dialog.ShowDialog(this) != true) return false; path = dialog.FileName;
            }
            try
            {
                Mouse.OverrideCursor = Cursors.Wait; saveTimer.Stop(); AutoSave();
                ProjectTools.ExportBundle(assessment, path, true, true); bundlePath = path; if (projectPath == null) dirty = false; UpdateNumbers(); UpdateHeader();
                status.Text = "Комплект сохранён: " + path + (Calc.Summary(assessment).Complete ? "" : " · предварительный результат");
                return true;
            }
            catch (Exception ex) { ShowError("Не удалось сохранить комплект", ex); return false; }
            finally { Mouse.OverrideCursor = null; }
        }
        void SaveProfile()
        {
            string name = "Реквизиты" + (assessment.OrganizationShort.Length > 0 ? " - " + assessment.OrganizationShort : assessment.Organization.Length > 0 ? " - " + assessment.Organization : "");
            foreach (char ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            if (name.Length > 140) name = name.Substring(0, 140);
            SaveFileDialog dialog = new SaveFileDialog { Title = "Сохранить реквизиты организации", Filter = "Реквизиты организации (*.org.json)|*.org.json", FileName = name + ".org.json", DefaultExt = ".org.json", AddExtension = true };
            if (dialog.ShowDialog(this) != true) return;
            try { ProjectTools.SaveProfile(assessment, dialog.FileName); status.Text = "Реквизиты сохранены: " + dialog.FileName; }
            catch (Exception ex) { ShowError("Не удалось сохранить реквизиты", ex); }
        }
        void LoadProfile()
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Загрузить реквизиты организации", Filter = "Реквизиты организации (*.org.json)|*.org.json|JSON (*.json)|*.json" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                OrganizationProfile profile = ProjectTools.LoadProfile(dialog.FileName);
                ProjectTools.ApplyProfile(assessment, profile); Changed(); ShowPage(); status.Text = "Реквизиты загружены · ответы и параметры оценки сохранены";
            }
            catch (Exception ex) { ShowError("Не удалось загрузить реквизиты", ex); }
        }
        void ShowError(string title, Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Error); }
        public void RenderPreview(string targetPage, string path, int width = 1450, int height = 920)
        {
            page = targetPage; ShowPage();
            FrameworkElement content = (FrameworkElement)Content;
            content.Width = width; content.Height = height;
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
            RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
    public class RelayCommand : ICommand
    {
        readonly Action action;
        public RelayCommand(Action action) { this.action = action; }
        public bool CanExecute(object parameter) { return true; }
        public void Execute(object parameter) { action(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
    public class ExportReviewWindow : Window
    {
        public bool IncludeWord = true, IncludeExcel = true, GoToSettings;
        public ExportReviewWindow(Assessment assessment, bool bundle)
        {
            Title = bundle ? "Состав комплекта оценки" : "Проверка перед выгрузкой";
            Width = 690; MinWidth = 560; MinHeight = 420;
            Height = Math.Max(MinHeight, Math.Min(760, SystemParameters.WorkArea.Height - 48));
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); Background = Theme.Paper;
            Grid frame = new Grid { Margin = new Thickness(26) }; frame.RowDefinitions.Add(new RowDefinition()); frame.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Content = frame;
            StackPanel root = new StackPanel();
            frame.Children.Add(new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) });
            root.Children.Add(Theme.Text(Title, 23, null, true));
            TextBlock intro = Theme.Text(bundle ? "Все файлы будут сформированы из одной версии оценки и сохранены в общем ZIP-архиве." : "Проверьте сведения перед созданием отчёта. При неполной анкете результат будет помечен как предварительный.", 12, Theme.Muted); intro.Margin = new Thickness(0, 9, 0, 17); root.Children.Add(intro);
            Summary summary = Calc.Summary(assessment);
            TextBlock totals = Theme.Text("Направлений: " + summary.Count + " · ответов: " + summary.Answered + " из " + summary.Total + "\n" + (summary.Complete ? "Анкета заполнена" : "Предварительный результат"), 13, summary.Complete ? Theme.B("#278458") : Theme.B("#926415"), true);
            root.Children.Add(Theme.Card(totals, new Thickness(14)));
            if (bundle)
            {
                StackPanel files = new StackPanel(); files.Children.Add(Theme.Text("Состав архива", 15, null, true));
                files.Children.Add(new CheckBox { Content = "Проект оценки (.uzi.json) — включён всегда", IsChecked = true, IsEnabled = false, Margin = new Thickness(0, 12, 0, 9) });
                CheckBox word = new CheckBox { Content = "Отчёт Word (.docx)", IsChecked = true, Margin = new Thickness(0, 0, 0, 9) }; word.Checked += (s, e) => IncludeWord = true; word.Unchecked += (s, e) => IncludeWord = false; files.Children.Add(word);
                CheckBox excel = new CheckBox { Content = "Таблица Excel (.xlsx)", IsChecked = true }; excel.Checked += (s, e) => IncludeExcel = true; excel.Unchecked += (s, e) => IncludeExcel = false; files.Children.Add(excel);
                Border filesCard = Theme.Card(files, new Thickness(16)); filesCard.Margin = new Thickness(0, 14, 0, 0); root.Children.Add(filesCard);
            }
            string[] issues = ProjectTools.Preflight(assessment);
            TextBlock issuesTitle = Theme.Text(issues.Length == 0 ? "Пропусков не найдено" : "Перед выгрузкой проверьте", 15, null, true); issuesTitle.Margin = new Thickness(0, 20, 0, 10); root.Children.Add(issuesTitle);
            if (issues.Length > 0)
            {
                StackPanel lines = new StackPanel();
                foreach (string issue in issues) { TextBlock line = Theme.Text("•  " + issue, 12, Theme.Muted); line.Margin = new Thickness(0, 0, 7, 10); lines.Children.Add(line); }
                root.Children.Add(new ScrollViewer { Content = lines, MaxHeight = 190, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
                TextBlock advisory = Theme.Text("Можно продолжить с текущими данными или вернуться к заполнению.", 11, Theme.Muted); advisory.Margin = new Thickness(0, 9, 0, 0); root.Children.Add(advisory);
            }
            StackPanel buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
            buttons.Children.Add(Theme.Button("Отмена", () => { DialogResult = false; }));
            buttons.Children.Add(Theme.Button("К параметрам", () => { GoToSettings = true; DialogResult = false; }));
            buttons.Children.Add(Theme.Button(bundle ? "Выбрать файл…" : "Продолжить", () => { DialogResult = true; }, true)); Grid.SetRow(buttons, 1); frame.Children.Add(buttons);
        }
        public void RenderPreview(string path)
        {
            FrameworkElement content = (FrameworkElement)Content;
            content.Measure(new Size(690, 720)); content.Arrange(new Rect(0, 0, 690, 720)); content.UpdateLayout();
            DrawingVisual visual = new DrawingVisual(); using (DrawingContext context = visual.RenderOpen()) { context.DrawRectangle(Theme.Paper, null, new Rect(0, 0, 690, 720)); }
            RenderTargetBitmap bitmap = new RenderTargetBitmap(690, 720, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Render(content);
            PngBitmapEncoder encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
    public class RadarProfile : FrameworkElement
    {
        readonly Assessment assessment;
        public RadarProfile(Assessment a) { assessment = a; Height = 270; }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double radius = Math.Max(20, Math.Min(ActualWidth / 2 - 32, ActualHeight / 2 - 25));
            Point center = new Point(ActualWidth / 2, ActualHeight / 2);
            Pen grid = new Pen(Theme.Line, 1);
            for (int ring = 1; ring <= 4; ring++)
            {
                Point[] points = Enumerable.Range(0, 21).Select(i => Polar(center, radius * ring / 4, i)).ToArray();
                dc.DrawGeometry(null, grid, Polygon(points));
                DrawText(dc, ring.ToString(), new Point(center.X + 3, center.Y - radius * ring / 4 + 1), Theme.Muted, 9);
            }
            for (int i = 0; i < 21; i++)
            {
                dc.DrawLine(grid, center, Polar(center, radius, i));
                Point label = Polar(center, radius + 19, i);
                DrawText(dc, assessment.Directions[i].Included ? (i + 1).ToString() : "×", new Point(label.X - 7, label.Y - 7), assessment.Directions[i].Included ? Theme.Muted : Theme.B("#B5BFCE"), 10);
            }
            Point[] targets = assessment.Directions.Select((d, i) => Polar(center, radius * (d.Included ? d.Target : 0) / 4, i)).ToArray();
            Pen targetPen = new Pen(Theme.B("#D89430"), 1.7) { DashStyle = DashStyles.Dash };
            dc.DrawGeometry(null, targetPen, Polygon(targets));
            Point[] current = assessment.Directions.Select((d, i) => Polar(center, radius * (d.Included ? Calc.Direction(d).Level : 0) / 4, i)).ToArray();
            dc.DrawGeometry(Theme.B("#302864D7"), new Pen(Theme.Blue, 2), Polygon(current));
            for (int i = 0; i < 21; i++) if (assessment.Directions[i].Included) dc.DrawEllipse(Theme.Blue, null, current[i], 2.4, 2.4);
        }
        static Point Polar(Point center, double radius, int i) { double angle = -Math.PI / 2 + 2 * Math.PI * i / 21; return new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle)); }
        static StreamGeometry Polygon(Point[] points)
        {
            StreamGeometry geometry = new StreamGeometry(); using (StreamGeometryContext context = geometry.Open()) { context.BeginFigure(points[0], true, true); context.PolyLineTo(points.Skip(1).ToArray(), true, true); } geometry.Freeze(); return geometry;
        }
        void DrawText(DrawingContext dc, string text, Point point, Brush color, double size)
        {
            dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("ru-RU"), FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
        }
    }
}
