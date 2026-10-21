using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MaturityApp
{
    // "Объект оценки": who is assessed and what the object consists of. Every list grows
    // with "+ Добавить", so any system — one workstation, a hospital network or an ICS — fits.
    public partial class MainWindow
    {
        TextBlock passportSummary, passportUnknown, objectTargetText;
        StackPanel objectRows, componentRows, connectionRows, toolRows, segmentRows;
        CheckBox noConnectionsBox;
        SystemPassport passportDraft;

        // The passport is created on the first edit, so opening the page never invents facts.
        SystemPassport Passport { get { return assessment.Passport ?? (passportDraft ?? (passportDraft = SystemPassports.CreateEmpty())); } }
        void PassportChanged()
        {
            if (building) return;
            if (assessment.Passport == null) { assessment.Passport = Passport; passportDraft = null; }
            Changed(); RefreshPassportPreview();
        }

        void BuildPassport()
        {
            passportDraft = null;
            StackPanel body = PageStack();
            TextBlock help = Theme.Text("Опишите, что оценивается. Все списки можно дополнять; незаполненное отмечается как «не уточнено». Описание не выставляет оценки зрелости.", 12, Theme.Muted); help.Margin = new Thickness(0, 0, 0, 14); body.Children.Add(help);

            StackPanel identity = new StackPanel();
            Grid names = PassportGrid(2);
            PassportText(names, 0, "Организация (оператор)", assessment.Organization, value => { assessment.Organization = value; Changed(); UpdateHeader(); });
            PassportText(names, 1, "Объект оценки — наименование системы или области оценки", assessment.SystemName, value => { assessment.SystemName = value; Changed(); UpdateHeader(); });
            identity.Children.Add(names);
            identity.Children.Add(SectionTitle("Тип объекта защиты", "Класс, уровень защищённости или категория задают рекомендуемый минимальный целевой уровень (п. 11 Методики) и основания требований."));
            objectRows = new StackPanel(); identity.Children.Add(objectRows); RebuildObjectRows();
            objectTargetText = Theme.Text("", 12, Theme.Blue, true); objectTargetText.Margin = new Thickness(0, 6, 0, 0); identity.Children.Add(objectTargetText);
            identity.Children.Add(AddRowButton("+ Добавить тип объекта", () => { assessment.Objects = assessment.Objects.Concat(new[] { new ProtectionObject { Kind = "gis" } }).ToArray(); ObjectsChanged(); RebuildObjectRows(); }));
            body.Children.Add(Theme.Card(identity, new Thickness(16)));

            StackPanel composition = new StackPanel();
            composition.Children.Add(SectionTitle("Состав объекта", "Рабочие места, серверы, сетевое и иное оборудование. Название можно выбрать из подсказок или ввести своё."));
            componentRows = new StackPanel(); composition.Children.Add(componentRows); RebuildComponentRows();
            composition.Children.Add(AddRowButton("+ Добавить компонент", () => { Passport.Components = Passport.Components.Concat(new[] { new PassportComponent() }).ToArray(); PassportChanged(); RebuildComponentRows(); }));
            body.Children.Add(Card(composition));

            StackPanel connections = new StackPanel();
            connections.Children.Add(SectionTitle("Внешние взаимодействия", "Другие системы и сети, с которыми взаимодействует объект: внешние ИС, сети операторов, корпоративная сеть."));
            noConnectionsBox = new CheckBox { Content = "Внешних взаимодействий нет", IsChecked = Passport.NoConnections, Margin = new Thickness(0, 0, 0, 8) };
            noConnectionsBox.Checked += (s, e) => { if (building) return; if (Passport.Connections.Length > 0 && MessageBox.Show(this, "Убрать указанные взаимодействия (" + Passport.Connections.Length + ")?", "Внешние взаимодействия", MessageBoxButton.OKCancel) != MessageBoxResult.OK) { noConnectionsBox.IsChecked = false; return; } Passport.Connections = new PassportConnection[0]; Passport.NoConnections = true; PassportChanged(); RebuildConnectionRows(); };
            noConnectionsBox.Unchecked += (s, e) => { if (building) return; Passport.NoConnections = false; PassportChanged(); RebuildConnectionRows(); };
            connections.Children.Add(noConnectionsBox);
            connectionRows = new StackPanel(); connections.Children.Add(connectionRows); RebuildConnectionRows();
            connections.Children.Add(AddRowButton("+ Добавить взаимодействие", () => { Passport.NoConnections = false; Passport.Connections = Passport.Connections.Concat(new[] { new PassportConnection() }).ToArray(); building = true; noConnectionsBox.IsChecked = false; building = false; PassportChanged(); RebuildConnectionRows(); }));
            body.Children.Add(Card(connections));

            StackPanel segments = new StackPanel();
            segments.Children.Add(SectionTitle("Сегменты сети", "Необязательно. Например: сегмент пользователей, серверный, технологический, ДМЗ."));
            segmentRows = new StackPanel(); segments.Children.Add(segmentRows); RebuildSegmentRows();
            segments.Children.Add(AddRowButton("+ Добавить сегмент", () => { Passport.Segments = Passport.Segments.Concat(new[] { new PassportSegment() }).ToArray(); PassportChanged(); RebuildSegmentRows(); }));
            body.Children.Add(Card(segments));

            StackPanel tools = new StackPanel();
            tools.Children.Add(SectionTitle("Средства защиты информации", "Фактически применяемые средства. Класс выбирается из списка, название — из подсказок или вводится вручную."));
            toolRows = new StackPanel(); tools.Children.Add(toolRows); RebuildToolRows();
            tools.Children.Add(AddRowButton("+ Добавить средство защиты", () => { Passport.Tools = Passport.Tools.Concat(new[] { new PassportTool { Class = "crypto" } }).ToArray(); PassportChanged(); RebuildToolRows(); }));
            body.Children.Add(Card(tools));

            StackPanel technology = new StackPanel();
            technology.Children.Add(SectionTitle("Применяемые технологии", "Ответ «Нет» позволяет исключить неприменимые направления оценки (п. 13 Методики) кнопкой на вкладке «Анкета»."));
            List<ComboBox> choices = new List<ComboBox>();
            Grid flags = PassportGrid(3); int index = 0;
            choices.Add(PassportBoolean(flags, index++, "Беспроводной доступ (Wi-Fi)", Passport.Wireless, v => Passport.Wireless = v));
            choices.Add(PassportBoolean(flags, index++, "Мобильные устройства", Passport.Mobile, v => Passport.Mobile = v));
            choices.Add(PassportBoolean(flags, index++, "Удалённый доступ (администрирование, подрядчики, работники)", Passport.RemoteAdministration, v => Passport.RemoteAdministration = v));
            choices.Add(PassportBoolean(flags, index++, "Собственная разработка ПО", Passport.OwnDevelopment, v => Passport.OwnDevelopment = v));
            choices.Add(PassportBoolean(flags, index++, "Искусственный интеллект", Passport.ArtificialIntelligence, v => Passport.ArtificialIntelligence = v));
            choices.Add(PassportBoolean(flags, index++, "Доменная инфраструктура", Passport.Domain, v => Passport.Domain = v));
            PassportBoolean(flags, index++, "Доступ в интернет", Passport.Internet, v => Passport.Internet = v);
            ComboBox isolated = PassportBoolean(flags, index++, "Wi-Fi отделён от сегментов объекта", Passport.WirelessIsolated, v => Passport.WirelessIsolated = v);
            Action syncIsolation = () => { isolated.IsEnabled = Passport.Wireless == true; isolated.ToolTip = isolated.IsEnabled ? null : "Уточняется, если беспроводной доступ применяется."; if (!isolated.IsEnabled && isolated.SelectedIndex != 0) { building = true; isolated.SelectedIndex = 0; building = false; Passport.WirelessIsolated = null; } };
            choices[0].SelectionChanged += (s, e) => syncIsolation(); syncIsolation();
            technology.Children.Add(flags);
            Button none = AddRowButton("Отметить первые пять как неприменяемые", () => {
                Passport.Wireless = Passport.Mobile = Passport.RemoteAdministration = Passport.OwnDevelopment = Passport.ArtificialIntelligence = false;
                building = true; foreach (ComboBox c in choices.Take(5)) c.SelectedIndex = 1; building = false; PassportChanged();
            });
            none.ToolTip = "Wi-Fi, мобильные устройства, удалённый доступ, собственная разработка и ИИ — «Нет». Домен и интернет не меняются.";
            technology.Children.Add(none);
            Grid notes = PassportGrid(1); notes.Margin = new Thickness(0, 10, 0, 0);
            PassportText(notes, 0, "Особенности объекта (площадки, режим работы, иное) — необязательно", Passport.Notes, value => { Passport.Notes = value; PassportChanged(); });
            technology.Children.Add(notes);
            body.Children.Add(Card(technology));

            StackPanel preview = new StackPanel();
            preview.Children.Add(Theme.Text("Итог описания", 15, null, true));
            passportSummary = Theme.Text("", 12); passportSummary.Margin = new Thickness(0, 8, 0, 6); preview.Children.Add(passportSummary);
            passportUnknown = Theme.Text("", 11, Theme.Muted); preview.Children.Add(passportUnknown);
            body.Children.Add(Card(preview));
            Button next = Theme.Button("Перейти к анкете →", () => { page = "quick"; ShowPage(); }, true); next.HorizontalAlignment = HorizontalAlignment.Left; next.Margin = new Thickness(0, 14, 0, 24); body.Children.Add(next);
            RefreshPassportPreview(); RefreshObjectTarget();
        }

        static Border Card(UIElement content) { Border b = Theme.Card(content, new Thickness(16)); b.Margin = new Thickness(0, 14, 0, 0); return b; }
        static StackPanel SectionTitle(string title, string hint)
        {
            StackPanel p = new StackPanel { Margin = new Thickness(0, 14, 0, 8) };
            p.Children.Add(Theme.Text(title, 15, null, true));
            TextBlock h = Theme.Text(hint, 11, Theme.Muted); h.Margin = new Thickness(0, 3, 0, 0); p.Children.Add(h); return p;
        }
        static Button AddRowButton(string text, Action action)
        {
            Button b = Theme.Button(text, action); b.HorizontalAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(10, 5, 10, 5); b.FontSize = 12; b.Margin = new Thickness(0, 6, 0, 0); b.Foreground = Theme.Blue; return b;
        }
        static Grid RowGrid(params double[] stars)
        {
            Grid g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            foreach (double s in stars) g.ColumnDefinitions.Add(new ColumnDefinition { Width = s > 0 ? new GridLength(s, GridUnitType.Star) : new GridLength(-s) });
            return g;
        }
        static void Put(Grid g, UIElement e, int column) { if (e is FrameworkElement) ((FrameworkElement)e).Margin = new Thickness(0, 0, 6, 0); Grid.SetColumn(e, column); g.Children.Add(e); }
        TextBox RowText(string value, string hint, Action<string> set)
        {
            TextBox box = new TextBox { Text = value, Height = 30, Padding = new Thickness(6, 4, 6, 4), ToolTip = hint, Tag = hint };
            box.TextChanged += (s, e) => { if (!building) { set(box.Text); PassportChanged(); } }; return box;
        }
        ComboBox RowEditable(string value, string[] suggestions, string hint, Action<string> set)
        {
            ComboBox c = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, Text = value, Height = 30, ToolTip = hint, Tag = hint };
            foreach (string s in suggestions) c.Items.Add(s);
            DependencyPropertyDescriptor.FromProperty(ComboBox.TextProperty, typeof(ComboBox)).AddValueChanged(c, (s, e) => { if (!building) { set(c.Text ?? ""); PassportChanged(); } });
            return c;
        }
        ComboBox RowChoice(CatalogItem[] items, string value, Action<string> set)
        {
            ComboBox c = new ComboBox { Height = 30 };
            foreach (CatalogItem item in items) c.Items.Add(new ComboBoxItem { Content = item.Text, Tag = item.Key });
            c.SelectedIndex = Math.Max(0, Array.FindIndex(items, x => x.Key == (value ?? "")));
            c.SelectionChanged += (s, e) => { if (!building && c.SelectedItem != null) { set((string)((ComboBoxItem)c.SelectedItem).Tag); PassportChanged(); } }; return c;
        }
        Button RemoveButton(Action remove)
        {
            Button b = new Button { Content = "✕", Width = 28, Height = 28, Background = Brushes.Transparent, Foreground = Theme.Muted, ToolTip = "Удалить строку" };
            b.Click += (s, e) => remove(); return b;
        }

        void RebuildObjectRows()
        {
            if (objectRows == null) return; objectRows.Children.Clear();
            if (assessment.Objects.Length == 0) { objectRows.Children.Add(Theme.Text("Не указан. Добавьте тип: ГИС, ИСПДн, значимый объект КИИ, АСУ ТП или иную систему. Если типов несколько, цель определяется по самому строгому.", 11, Theme.Muted)); return; }
            foreach (ProtectionObject item in assessment.Objects)
            {
                ProtectionObject o = item; Grid row = RowGrid(2.2, 1.2, 1.6, 1.6, -30);
                ComboBox kind = new ComboBox { Height = 30, Tag = "Тип объекта защиты" };
                foreach (ObjectKind k in ObjectCatalog.Kinds) kind.Items.Add(new ComboBoxItem { Content = k.Text, Tag = k.Key });
                kind.SelectedIndex = Array.FindIndex(ObjectCatalog.Kinds, k => k.Key == o.Kind);
                ComboBox level = new ComboBox { Height = 30, Tag = "Класс или категория" };
                Action fillLevels = () => {
                    level.Items.Clear(); ObjectKind k = ObjectCatalog.Find(o.Kind);
                    level.Items.Add(k.Levels.Length == 0 ? "—" : k.LevelLabel + ": не установлен");
                    foreach (string l in k.Levels) level.Items.Add(l);
                    level.IsEnabled = k.Levels.Length > 0; level.SelectedIndex = Math.Min(o.Level, k.Levels.Length);
                };
                building = true; fillLevels(); building = false;
                kind.SelectionChanged += (s, e) => {
                    if (building || kind.SelectedItem == null) return;
                    o.Kind = (string)((ComboBoxItem)kind.SelectedItem).Tag; o.Level = 0;
                    if (o.Kind != "ispdn") { o.PdCategories = new string[0]; o.Subjects = o.Volume = ""; }
                    ObjectsChanged(); RebuildObjectRows(); // the personal data block depends on the type
                };
                level.SelectionChanged += (s, e) => { if (building || level.SelectedIndex < 0) return; o.Level = level.SelectedIndex; ObjectsChanged(); };
                Put(row, kind, 0); Put(row, level, 1);
                TextBox name = new TextBox { Text = o.Name, Height = 30, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Наименование (если систем несколько)" }; name.TextChanged += (s, e) => { o.Name = name.Text; Changed(); }; Put(row, name, 2);
                TextBox doc = new TextBox { Text = o.Document, Height = 30, Padding = new Thickness(6, 4, 6, 4), ToolTip = "Акт классификации / категорирования: номер и дата" }; doc.TextChanged += (s, e) => { o.Document = doc.Text; Changed(); }; Put(row, doc, 3);
                Put(row, RemoveButton(() => { assessment.Objects = assessment.Objects.Where(x => !Object.ReferenceEquals(x, o)).ToArray(); ObjectsChanged(); RebuildObjectRows(); }), 4);
                objectRows.Children.Add(row);
                if (o.Kind == "ispdn") objectRows.Children.Add(PersonalDataRow(o));
            }
            objectRows.Children.Insert(0, HeaderRow(RowGrid(2.2, 1.2, 1.6, 1.6, -30), "Тип", "Класс / категория", "Наименование (необязательно)", "Акт классификации / категорирования"));
        }
        // ПП РФ № 1119: categories of personal data, whose data and how many subjects.
        UIElement PersonalDataRow(ProtectionObject o)
        {
            WrapPanel panel = new WrapPanel { Margin = new Thickness(12, 0, 0, 10) };
            panel.Children.Add(new TextBlock { Text = "Категории ПДн:", Foreground = Theme.Muted, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            foreach (CatalogItem item in ObjectCatalog.PdCategories)
            {
                CatalogItem current = item;
                CheckBox box = new CheckBox { Content = item.Text, IsChecked = o.PdCategories.Contains(item.Key), Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Tag = "pd:" + item.Key };
                box.Checked += (s, e) => { o.PdCategories = ObjectCatalog.PdCategories.Where(x => x.Key == current.Key || o.PdCategories.Contains(x.Key)).Select(x => x.Key).ToArray(); Changed(); RefreshPassportPreview(); };
                box.Unchecked += (s, e) => { o.PdCategories = o.PdCategories.Where(x => x != current.Key).ToArray(); Changed(); RefreshPassportPreview(); };
                panel.Children.Add(box);
            }
            ComboBox subjects = RowChoiceRaw(ObjectCatalog.Subjects, o.Subjects, v => { o.Subjects = v; Changed(); RefreshPassportPreview(); }); subjects.Width = 210; subjects.Tag = "Субъекты ПДн"; subjects.Margin = new Thickness(8, 0, 8, 0); panel.Children.Add(subjects);
            ComboBox volume = RowChoiceRaw(ObjectCatalog.Volumes, o.Volume, v => { o.Volume = v; Changed(); RefreshPassportPreview(); }); volume.Width = 210; volume.Tag = "Объём ПДн"; panel.Children.Add(volume);
            return panel;
        }
        ComboBox RowChoiceRaw(CatalogItem[] items, string value, Action<string> set)
        {
            ComboBox c = new ComboBox { Height = 30 };
            foreach (CatalogItem item in items) c.Items.Add(new ComboBoxItem { Content = item.Text, Tag = item.Key });
            c.SelectedIndex = Math.Max(0, Array.FindIndex(items, x => x.Key == (value ?? "")));
            c.SelectionChanged += (s, e) => { if (!building && c.SelectedItem != null) set((string)((ComboBoxItem)c.SelectedItem).Tag); }; return c;
        }
        void RebuildSegmentRows()
        {
            if (segmentRows == null) return; segmentRows.Children.Clear();
            if (Passport.Segments.Length == 0) { segmentRows.Children.Add(Theme.Text("Не указаны.", 11, Theme.Muted)); return; }
            segmentRows.Children.Add(HeaderRow(RowGrid(2, 2.4, 2.4, -30), "Сегмент", "Назначение (что в нём)", "Примечание (изоляция, VLAN, МЭ)"));
            foreach (PassportSegment item in Passport.Segments)
            {
                PassportSegment g = item; Grid row = RowGrid(2, 2.4, 2.4, -30);
                Put(row, RowEditable(g.Name, SystemPassports.SegmentSuggestions, "Сегмент", v => g.Name = v), 0);
                Put(row, RowText(g.Purpose, "Назначение сегмента", v => g.Purpose = v), 1);
                Put(row, RowText(g.Note, "Примечание к сегменту", v => g.Note = v), 2);
                Put(row, RemoveButton(() => { Passport.Segments = Passport.Segments.Where(x => !Object.ReferenceEquals(x, g)).ToArray(); PassportChanged(); RebuildSegmentRows(); }), 3);
                segmentRows.Children.Add(row);
            }
        }
        static Grid HeaderRow(Grid g, params string[] titles)
        {
            for (int i = 0; i < titles.Length; i++) { TextBlock t = Theme.Text(titles[i], 11, Theme.Muted); Grid.SetColumn(t, i); g.Children.Add(t); }
            g.Margin = new Thickness(0, 0, 0, 3); return g;
        }
        void ObjectsChanged()
        {
            ObjectCatalog.ApplyToAssessment(assessment);
            if (assessment.Objects.Length == 0 && assessment.Category == 0) assessment.Category = 1;
            Changed(); RefreshObjectTarget();
        }
        void RefreshObjectTarget()
        {
            if (objectTargetText == null) return;
            objectTargetText.Text = assessment.Objects.Length == 0 ? "" : assessment.Category == 0
                ? "Класс или категория не установлены — минимальный целевой уровень 1."
                : "Рекомендуемый минимальный целевой уровень по самому строгому типу: " + assessment.Category + ". Цель отдельных направлений можно повысить на вкладке «Анкета».";
        }

        void RebuildComponentRows()
        {
            if (componentRows == null) return; componentRows.Children.Clear();
            if (Passport.Components.Length == 0) { componentRows.Children.Add(Theme.Text("Пока пусто.", 11, Theme.Muted)); return; }
            componentRows.Children.Add(HeaderRow(RowGrid(2.4, -90, 3, -30), "Компонент", "Количество", "Примечание (назначение, ОС, размещение)"));
            foreach (PassportComponent item in Passport.Components)
            {
                PassportComponent c = item; Grid row = RowGrid(2.4, -90, 3, -30);
                Put(row, RowEditable(c.Kind, SystemPassports.ComponentSuggestions, "Компонент", v => c.Kind = v), 0);
                TextBox count = new TextBox { Text = c.Count.ToString(), Height = 30, Padding = new Thickness(6, 4, 6, 4), Tag = "Количество" };
                count.TextChanged += (s, e) => { int n; bool ok = Int32.TryParse(count.Text, out n) && n >= 0 && n <= 100000; count.BorderBrush = ok ? Theme.Line : Theme.B("#C44850"); if (ok && !building) { c.Count = n; PassportChanged(); } };
                Put(row, count, 1);
                Put(row, RowText(c.Note, "Примечание", v => c.Note = v), 2);
                Put(row, RemoveButton(() => { Passport.Components = Passport.Components.Where(x => !Object.ReferenceEquals(x, c)).ToArray(); PassportChanged(); RebuildComponentRows(); }), 3);
                componentRows.Children.Add(row);
            }
        }
        void RebuildConnectionRows()
        {
            if (connectionRows == null) return; connectionRows.Children.Clear();
            if (Passport.NoConnections) return;
            if (Passport.Connections.Length == 0) { connectionRows.Children.Add(Theme.Text("Не указаны. Добавьте взаимодействие или отметьте, что их нет.", 11, Theme.Muted)); return; }
            connectionRows.Children.Add(HeaderRow(RowGrid(2.2, 1.5, 1.5, 1.6, 1.7, -30), "Система или сеть", "Способ", "Канал", "Через что", "Примечание"));
            foreach (PassportConnection item in Passport.Connections)
            {
                PassportConnection c = item; Grid row = RowGrid(2.2, 1.5, 1.5, 1.6, 1.7, -30);
                Put(row, RowText(c.System, "Система или сеть", v => c.System = v), 0);
                Put(row, RowChoice(SystemPassports.Methods, c.Method, v => c.Method = v), 1);
                Put(row, RowChoice(SystemPassports.Channels, c.Channel, v => { c.Channel = v; if (v == "internet") Passport.Internet = true; }), 2);
                Put(row, RowEditable(c.Via, SystemPassports.ViaSuggestions, "Через что", v => c.Via = v), 3);
                Put(row, RowText(c.Note, "Примечание", v => c.Note = v), 4);
                Put(row, RemoveButton(() => { Passport.Connections = Passport.Connections.Where(x => !Object.ReferenceEquals(x, c)).ToArray(); PassportChanged(); RebuildConnectionRows(); }), 5);
                connectionRows.Children.Add(row);
            }
        }
        void RebuildToolRows()
        {
            if (toolRows == null) return; toolRows.Children.Clear();
            if (Passport.Tools.Length == 0) { toolRows.Children.Add(Theme.Text("Пока пусто.", 11, Theme.Muted)); return; }
            toolRows.Children.Add(HeaderRow(RowGrid(1.8, 2.2, 2.4, -30), "Класс", "Средство", "Примечание (версия, сертификат, где установлено)"));
            foreach (PassportTool item in Passport.Tools)
            {
                PassportTool t = item; Grid row = RowGrid(1.8, 2.2, 2.4, -30);
                string[] products; SystemPassports.ProductSuggestions.TryGetValue(t.Class, out products);
                ComboBox product = RowEditable(t.Product, products ?? new string[0], "Средство", v => t.Product = v);
                Put(row, RowChoice(SystemPassports.ToolClasses, t.Class, v => {
                    t.Class = v; string[] next; SystemPassports.ProductSuggestions.TryGetValue(v, out next);
                    string text = product.Text; product.Items.Clear(); foreach (string s in next ?? new string[0]) product.Items.Add(s); product.Text = text;
                }), 0);
                Put(row, product, 1);
                Put(row, RowText(t.Note, "Примечание", v => t.Note = v), 2);
                Put(row, RemoveButton(() => { Passport.Tools = Passport.Tools.Where(x => !Object.ReferenceEquals(x, t)).ToArray(); PassportChanged(); RebuildToolRows(); }), 3);
                toolRows.Children.Add(row);
            }
        }

        void RefreshPassportPreview()
        {
            if (passportSummary == null) return;
            passportSummary.Text = SystemPassports.Description(Passport) + (assessment.Objects.Length > 0 ? "\nТип объекта: " + String.Join("; ", assessment.Objects.Select(ObjectCatalog.Text)) + "." : "");
            string[] missing = SystemPassports.UnknownFields(Passport);
            passportUnknown.Text = missing.Length == 0 ? "Все характеристики уточнены." : "Не уточнено (" + missing.Length + "): " + String.Join("; ", missing) + ".";
        }
        static Grid PassportGrid(int columns)
        {
            Grid grid = new Grid(); for (int i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition()); return grid;
        }
        static StackPanel PassportField(Grid grid, int column, string label)
        {
            StackPanel panel = new StackPanel { Margin = new Thickness(0, 0, 12, 0) }; panel.Children.Add(Theme.Text(label, 11, Theme.Muted)); Grid.SetColumn(panel, column); grid.Children.Add(panel); return panel;
        }
        void PassportText(Grid grid, int column, string label, string value, Action<string> changed)
        {
            StackPanel panel = PassportField(grid, column, label); TextBox input = new TextBox { Text = value, Height = 34, MaxLength = 4000, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(7, 4, 7, 4), Tag = label };
            input.TextChanged += (s, e) => { if (!building) changed(input.Text); }; panel.Children.Add(input);
        }
        ComboBox PassportBoolean(Grid grid, int index, string label, bool? selectedValue, Action<bool?> changed)
        {
            int row = index / 3, column = index % 3; while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid field = PassportGrid(1); field.Margin = new Thickness(0, 0, 0, 10); Grid.SetRow(field, row); Grid.SetColumn(field, column); grid.Children.Add(field);
            StackPanel panel = PassportField(field, 0, label); ComboBox choice = new ComboBox { Height = 32, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6), Tag = label }; choice.Items.Add("Не уточнено"); choice.Items.Add("Нет"); choice.Items.Add("Да"); choice.SelectedIndex = !selectedValue.HasValue ? 0 : selectedValue.Value ? 2 : 1;
            choice.SelectionChanged += (s, e) => { if (building) return; changed(choice.SelectedIndex == 0 ? (bool?)null : choice.SelectedIndex == 2); PassportChanged(); }; panel.Children.Add(choice); return choice;
        }
    }
}
