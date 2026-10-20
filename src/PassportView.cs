using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace MaturityApp
{
    public partial class MainWindow
    {
        readonly string armTemplatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Данные", "Типовой АРМ.arm.json");
        TextBlock passportDescription, passportTopology, passportUnknown;
        ComboBox passportChannelChoice, passportInternetChoice;
        TextBlock passportReference;
        Button passportReferenceLink;

        void NewArmProject()
        {
            if (!PreserveCurrent()) return;
            SystemPassport basis = SystemPassports.CreateSingleArm();
            string notice = "Создан новый АРМ: введите организацию и уточните отличия от типового состава.";
            if (live && File.Exists(armTemplatePath))
            {
                try { basis = SystemPassports.LoadTemplate(armTemplatePath); notice = "Применён ваш типовой состав. Реквизиты организации и оценки нужно указать для нового объекта."; }
                catch (Exception ex) { notice = "Создана стандартная основа. Сохранённый типовой состав не прочитан: " + ex.Message; }
            }
            saveTimer.Stop(); assessment = Assessment.Create(); SystemPassports.Apply(assessment, basis);
            assessment.SystemName = ArmObjectName(basis.ExternalSystem);
            projectPath = bundlePath = null; copiedAnswers = null; dirty = true; autoOk = false; selected = 0;
            quickSearch = ""; onlyIncomplete = false; massCriterion = 0; massOverwrite = massVisibleOnly = false;
            page = "object"; ShowPage(); AutoSave(); status.Text = notice;
        }

        static string ArmObjectName(string external)
        {
            return String.IsNullOrWhiteSpace(external) ? "АРМ доступа к внешней информационной системе" : "АРМ доступа к «" + external.Trim() + "»";
        }

        void ApplySingleArmBasis()
        {
            SystemPassports.Apply(assessment, SystemPassports.CreateSingleArm());
            if (String.IsNullOrWhiteSpace(assessment.SystemName)) assessment.SystemName = ArmObjectName("");
            Changed(); ShowPage(); status.Text = "Добавлена основа: 1 АРМ, 1 роутер, без коммутатора и сервера. Уточните состав; оценки и область расчёта сохранены.";
        }

        void ApplyArmTemplate(SystemPassport loaded)
        {
            string oldExternal = assessment.Passport == null ? "" : assessment.Passport.ExternalSystem;
            bool generated = String.IsNullOrWhiteSpace(assessment.SystemName) || assessment.SystemName == ArmObjectName(oldExternal);
            SystemPassports.Apply(assessment, loaded);
            if (generated) assessment.SystemName = ArmObjectName(assessment.Passport == null ? "" : assessment.Passport.ExternalSystem);
        }

        void BuildPassport()
        {
            StackPanel body = PageStack();
            TextBlock lead = Theme.Text("Один АРМ для доступа к внешней системе", 23, null, true); lead.Margin = new Thickness(0, 0, 0, 8); body.Children.Add(lead);
            TextBlock help = Theme.Text("Технический состав можно повторять в разных организациях. Ответы о зрелости, цели и применимость направлений проверяются для каждого объекта отдельно.", 12, Theme.Muted); help.Margin = new Thickness(0, 0, 0, 16); body.Children.Add(help);
            if (assessment.Passport == null)
            {
                StackPanel start = new StackPanel(); start.Children.Add(Theme.Text("Добавьте типовую основу к этой оценке: 1 компьютер и 1 обычный роутер; коммутатор и сервер по умолчанию отсутствуют. Остальные характеристики предстоит уточнить.", 14));
                Button apply = Theme.Button("Применить основу «Один АРМ»", ApplySingleArmBasis, true); apply.HorizontalAlignment = HorizontalAlignment.Left; apply.Margin = new Thickness(0, 14, 0, 0); start.Children.Add(apply); body.Children.Add(Theme.Card(start)); return;
            }
            SystemPassport p = assessment.Passport;
            StackPanel identity = new StackPanel();
            Grid names = PassportGrid(2);
            PassportText(names, 0, "Организация", assessment.Organization, value => { assessment.Organization = value; PassportChanged(); });
            PassportExternalSystem(names, 1, p.ExternalSystem, value => {
                string oldGenerated = ArmObjectName(p.ExternalSystem); bool generated = String.IsNullOrWhiteSpace(assessment.SystemName) || assessment.SystemName == oldGenerated;
                p.ExternalSystem = value; if (generated) assessment.SystemName = ArmObjectName(value); PassportChanged();
            }); identity.Children.Add(names);
            TextBlock title = Theme.Text("Состав оборудования · исправьте только отличия", 14, null, true); title.Margin = new Thickness(0, 15, 0, 9); identity.Children.Add(title);
            Grid counts = PassportGrid(4);
            PassportNumber(counts, 0, "Компьютеры / АРМ", p.Workstations, 1, value => p.Workstations = value);
            PassportNumber(counts, 1, "Роутеры", p.Routers, 0, value => p.Routers = value);
            PassportNumber(counts, 2, "Коммутаторы", p.Switches, 0, value => p.Switches = value);
            PassportNumber(counts, 3, "Серверы", p.Servers, 0, value => p.Servers = value);
            identity.Children.Add(counts); body.Children.Add(Theme.Card(identity, new Thickness(16)));

            StackPanel connection = new StackPanel { Margin = new Thickness(0, 14, 0, 14) };
            Grid links = PassportGrid(2);
            PassportChoice(links, 0, "Способ работы с внешней ИС", new[] { "Не уточнялось", "Через браузер", "Отдельная программа / клиент" }, new[] { "unknown", "browser", "client" }, p.Connection, value => { p.Connection = value; PassportChanged(); });
            passportChannelChoice = PassportChoice(links, 1, "Канал подключения", new[] { "Не уточнялось", "Интернет", "VPN / защищённый канал", "Изолированный / выделенный канал" }, new[] { "unknown", "internet", "vpn", "isolated" }, p.Channel, value => { p.Channel = value; if (value == "internet") p.Internet = true; SyncPassportInternet(); PassportChanged(); }); connection.Children.Add(links);
            Grid protection = PassportGrid(1); protection.Margin = new Thickness(0, 12, 0, 0);
            PassportText(protection, 0, "Средства защиты: названия антивируса, VPN-клиента, других СЗИ", p.Protection, value => { p.Protection = value; PassportChanged(); }); connection.Children.Add(protection); body.Children.Add(Theme.Card(connection, new Thickness(16)));
            body.Children.Add(BuildProtectionComposer(p));
            StackPanel reference = new StackPanel { Margin = new Thickness(0, 8, 0, 0) }; passportReference = Theme.Text("", 12); reference.Children.Add(passportReference);
            passportReferenceLink = Theme.Button("Открыть официальный источник", () => { string url = ConnectionCatalog.Find(p.ExternalSystem).Url; if (url.Length > 0) try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { ShowError("Не удалось открыть источник", ex); } }); passportReferenceLink.HorizontalAlignment = HorizontalAlignment.Left; passportReferenceLink.Margin = new Thickness(0, 10, 0, 0); reference.Children.Add(passportReferenceLink);
            body.Children.Add(new Expander { Header = "Справка о вариантах подключения · источник и дата проверки", Content = Theme.Card(reference, new Thickness(16)), Margin = new Thickness(0, 0, 0, 14) });

            StackPanel extra = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
            TextBlock extraHelp = Theme.Text("Одна кнопка ниже указывает отсутствие домена, Wi-Fi, мобильных устройств, удалённого администрирования, собственной разработки ПО и ИИ. Сама она не исключает направления: для этого на вкладке «Оценка» появится кнопка «Исключить по паспорту».", 12, Theme.Muted); extra.Children.Add(extraHelp);
            List<ComboBox> technologyChoices = new List<ComboBox>();
            Button simple = Theme.Button("Эти шесть технологий не используются", () => {
                p.Domain = p.Wireless = p.Mobile = p.RemoteAdministration = p.OwnDevelopment = p.ArtificialIntelligence = false;
                bool previousBuilding = building; building = true; foreach (ComboBox item in technologyChoices) item.SelectedIndex = 1; building = previousBuilding; PassportChanged();
            }); simple.HorizontalAlignment = HorizontalAlignment.Left; simple.Margin = new Thickness(0, 10, 0, 10); extra.Children.Add(simple);
            Grid technologies = PassportGrid(3); int techRow = 0;
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Домен", p.Domain, value => p.Domain = value));
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Wi-Fi / беспроводной доступ", p.Wireless, value => p.Wireless = value));
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Мобильные устройства", p.Mobile, value => p.Mobile = value));
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Удалённое администрирование АРМ", p.RemoteAdministration, value => p.RemoteAdministration = value));
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Собственная разработка ПО", p.OwnDevelopment, value => p.OwnDevelopment = value));
            technologyChoices.Add(PassportBoolean(technologies, techRow++, "Использование ИИ", p.ArtificialIntelligence, value => p.ArtificialIntelligence = value));
            passportInternetChoice = PassportBoolean(technologies, techRow++, "Выход в интернет", p.Internet, value => p.Internet = value);
            SyncPassportInternet();
            extra.Children.Add(technologies);
            Grid notes = PassportGrid(1); notes.Margin = new Thickness(0, 11, 0, 0); PassportText(notes, 0, "Особенности объекта / примечание к составу", p.Notes, value => { p.Notes = value; PassportChanged(); }); extra.Children.Add(notes);
            body.Children.Add(new Expander { Header = "Дополнительные технологии и особенности · уточнить одним действием", Content = Theme.Card(extra, new Thickness(16)), Margin = new Thickness(0, 0, 0, 14) });

            StackPanel preview = new StackPanel(); preview.Children.Add(Theme.Text("Автоматически из этих же данных", 16, null, true));
            passportDescription = Theme.Text("", 13); passportDescription.Margin = new Thickness(0, 10, 0, 10); preview.Children.Add(passportDescription);
            passportTopology = Theme.Text("", 14, Theme.Blue, true); passportTopology.Margin = new Thickness(0, 0, 0, 10); preview.Children.Add(passportTopology);
            passportUnknown = Theme.Text("", 11, Theme.Muted); preview.Children.Add(passportUnknown); body.Children.Add(Theme.Card(preview, new Thickness(16)));

            WrapPanel commands = new WrapPanel { Margin = new Thickness(0, 15, 0, 8) };
            commands.Children.Add(Theme.Button("Перейти к оценке", () => { page = "quick"; ShowPage(); }, true));
            commands.Children.Add(Theme.Button("Запомнить основу для новых АРМ", RememberArmTemplate));
            commands.Children.Add(Theme.Button("Загрузить основу…", LoadArmTemplate));
            commands.Children.Add(Theme.Button("Сохранить основу как…", SaveArmTemplateAs)); body.Children.Add(commands);
            TextBlock repeated = Theme.Text("Запоминается только технический состав. Следующий «Новый АРМ» получит эту основу; организация, ФИО, подтверждающие материалы и оценки будут пустыми. Паспорт, состав и схема входят в Word и Excel автоматически.", 11, Theme.Muted); repeated.Margin = new Thickness(0, 0, 0, 20); body.Children.Add(repeated);
            RefreshPassportPreview();
        }

        void PassportChanged()
        {
            if (building) return; Changed(); UpdateHeader(); RefreshPassportPreview();
        }
        void SyncPassportInternet()
        {
            if (passportInternetChoice == null || assessment.Passport == null) return;
            SystemPassport p = assessment.Passport; bool prior = building; building = true;
            passportInternetChoice.SelectedIndex = !p.Internet.HasValue ? 0 : p.Internet.Value ? 2 : 1;
            passportInternetChoice.IsEnabled = p.Channel != "internet";
            passportInternetChoice.ToolTip = p.Channel == "internet" ? "Доступ в интернет следует из выбранного канала. Чтобы изменить это свойство, сначала выберите другой канал подключения." : null;
            building = prior;
        }
        void RefreshPassportPreview()
        {
            if (assessment.Passport == null || passportDescription == null) return;
            passportDescription.Text = SystemPassports.Description(assessment.Passport); passportTopology.Text = SystemPassports.Topology(assessment.Passport);
            string[] missing = SystemPassports.UnknownFields(assessment.Passport);
            passportUnknown.Text = missing.Length == 0 ? "Все характеристики паспорта уточнены. Оценки зрелости заполняются отдельно." : "Не уточнено (" + missing.Length + "): " + String.Join("; ", missing) + ".";
            if (passportReference != null) { ConnectionReference entry = ConnectionCatalog.Find(assessment.Passport.ExternalSystem); passportReference.Text = entry.Text + "\nПроверка открытых источников: " + ConnectionCatalog.CheckedDate + ". Справка не подставляет фактические оценки и установленное ПО."; passportReferenceLink.IsEnabled = entry.Url.Length > 0; }
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
        void PassportNumber(Grid grid, int column, string label, int value, int minimum, Action<int> changed)
        {
            StackPanel panel = PassportField(grid, column, label); TextBox input = new TextBox { Text = value.ToString(), Height = 32, MaxLength = 4, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(7, 4, 7, 4), Tag = label };
            input.TextChanged += (s, e) => { if (building) return; int parsed; bool valid = Int32.TryParse(input.Text, out parsed) && parsed >= minimum && parsed <= 9999; input.BorderBrush = valid ? Theme.Line : Theme.B("#C44850"); input.ToolTip = valid ? null : "Введите целое число от " + minimum + " до 9999. До исправления сохраняется последнее корректное значение."; if (valid) { changed(parsed); PassportChanged(); } }; panel.Children.Add(input);
        }
        ComboBox PassportChoice(Grid grid, int column, string label, string[] names, string[] values, string selectedValue, Action<string> changed)
        {
            StackPanel panel = PassportField(grid, column, label); ComboBox choice = new ComboBox { Height = 33, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6), Tag = label };
            for (int i = 0; i < names.Length; i++) choice.Items.Add(new ComboBoxItem { Content = names[i], Tag = values[i] }); choice.SelectedIndex = Math.Max(0, Array.IndexOf(values, selectedValue));
            choice.SelectionChanged += (s, e) => { if (!building && choice.SelectedItem != null) changed((string)((ComboBoxItem)choice.SelectedItem).Tag); }; panel.Children.Add(choice); return choice;
        }
        void PassportExternalSystem(Grid grid, int column, string value, Action<string> changed)
        {
            StackPanel panel = PassportField(grid, column, "К какой внешней системе подключаются");
            ComboBox choice = new ComboBox { IsEditable = true, IsTextSearchEnabled = false, StaysOpenOnEdit = true, Text = value, Height = 34, Padding = new Thickness(6), Margin = new Thickness(0, 6, 0, 0), Tag = "К какой внешней системе подключаются" };
            foreach (string system in new[] { "ППО АИСТ ГБД", "ФИС ФРДО", "АС СЭП", "ФИС ГИА", "Суперсервис «Поступление в вуз онлайн»", "ИС ЕПТ", "РМИАС", "ЕГИСЗ", "ТФОМС", "ФИС ГНА" }) choice.Items.Add(system);
            DependencyPropertyDescriptor.FromProperty(ComboBox.TextProperty, typeof(ComboBox)).AddValueChanged(choice, (s, e) => { if (!building) changed(choice.Text ?? ""); });
            choice.ToolTip = "Выберите частую систему или введите своё название. Выбор названия не утверждает наличие СЗИ и не выставляет оценки."; panel.Children.Add(choice);
        }
        UIElement BuildProtectionComposer(SystemPassport p)
        {
            StackPanel content = new StackPanel { Margin = new Thickness(0, 9, 0, 0) };
            content.Children.Add(Theme.Text("Варианты из вашего примера подключения к АИСТ. Выберите фактически установленные компоненты. Название ИС само по себе не подтверждает требования оператора или наличие этих средств.", 12, Theme.Muted));
            Grid choices = PassportGrid(4); choices.Margin = new Thickness(0, 12, 0, 12);
            ComboBox vpn = ProtectionChoice(choices, 0, "Криптозащита / сеть", new[] { "Не уточнено", "ViPNet" });
            ComboBox nsd = ProtectionChoice(choices, 1, "Защита от НСД", new[] { "Не уточнено", "Secret Net Studio 8", "Dallas Lock 8.0" });
            ComboBox antivirus = ProtectionChoice(choices, 2, "Антивирус", new[] { "Не уточнено", "В составе Secret Net Studio", "Dr.Web", "Kaspersky" });
            ComboBox boot = ProtectionChoice(choices, 3, "СДЗ", new[] { "Не уточнено", "ПАК «Соболь»", "СДЗ Dallas Lock", "ПК ViPNet SafeBoot" });
            content.Children.Add(choices);
            nsd.SelectionChanged += (s, e) => { ComboBoxItem builtIn = (ComboBoxItem)antivirus.Items[1]; builtIn.IsEnabled = nsd.SelectedIndex == 1; if (!builtIn.IsEnabled && antivirus.SelectedIndex == 1) antivirus.SelectedIndex = 0; }; ((ComboBoxItem)antivirus.Items[1]).IsEnabled = false;
            Button apply = Theme.Button("Вставить выбранный набор в паспорт", () => {
                string[] labels = { "Криптозащита / сеть", "Защита от НСД", "Антивирус", "СДЗ" }; ComboBox[] controls = { vpn, nsd, antivirus, boot };
                p.Protection = String.Join("; ", controls.Select((control, i) => labels[i] + ": " + (control.SelectedIndex == 0 ? "не уточнено" : ((ComboBoxItem)control.SelectedItem).Content.ToString())));
                Changed(); ShowPage(); status.Text = "Выбранный набор внесён в паспорт. Запомните основу, чтобы повторять этот состав для новых организаций.";
            }); apply.HorizontalAlignment = HorizontalAlignment.Left; content.Children.Add(apply);
            return new Expander { Header = "Выбрать СЗИ из списка · ViPNet, SNS, Dallas Lock, антивирус, СДЗ", Content = Theme.Card(content, new Thickness(16)), Margin = new Thickness(0, 0, 0, 14) };
        }
        static ComboBox ProtectionChoice(Grid grid, int column, string label, string[] values)
        {
            StackPanel panel = PassportField(grid, column, label); ComboBox choice = new ComboBox { Height = 33, Padding = new Thickness(6), Margin = new Thickness(0, 6, 0, 0), Tag = "СЗИ: " + label };
            foreach (string value in values) choice.Items.Add(new ComboBoxItem { Content = value }); choice.SelectedIndex = 0; panel.Children.Add(choice); return choice;
        }
        ComboBox PassportBoolean(Grid grid, int index, string label, bool? selectedValue, Action<bool?> changed)
        {
            int row = index / 3, column = index % 3; while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid field = PassportGrid(1); field.Margin = new Thickness(0, 0, 0, 10); Grid.SetRow(field, row); Grid.SetColumn(field, column); grid.Children.Add(field);
            StackPanel panel = PassportField(field, 0, label); ComboBox choice = new ComboBox { Height = 32, Margin = new Thickness(0, 6, 0, 0), Padding = new Thickness(6), Tag = label }; choice.Items.Add("Не уточнялось"); choice.Items.Add("Нет"); choice.Items.Add("Да"); choice.SelectedIndex = !selectedValue.HasValue ? 0 : selectedValue.Value ? 2 : 1;
            choice.SelectionChanged += (s, e) => { if (building) return; changed(choice.SelectedIndex == 0 ? (bool?)null : choice.SelectedIndex == 2); PassportChanged(); }; panel.Children.Add(choice); return choice;
        }
        void RememberArmTemplate()
        {
            try { SystemPassports.SaveTemplate(assessment.Passport, armTemplatePath); status.Text = "Основа запомнена. Кнопка «Новый АРМ» применит её к новой организации без переноса оценок и реквизитов."; }
            catch (Exception ex) { ShowError("Не удалось запомнить основу", ex); }
        }
        void LoadArmTemplate()
        {
            OpenFileDialog dialog = new OpenFileDialog { Title = "Загрузить техническую основу АРМ", Filter = "Основа АРМ (*.arm.json)|*.arm.json|JSON (*.json)|*.json" }; if (dialog.ShowDialog(this) != true) return;
            try { SystemPassport loaded = SystemPassports.LoadTemplate(dialog.FileName); ApplyArmTemplate(loaded); Changed(); ShowPage(); status.Text = "Загружен только технический состав. Организация, ответы, цели и область оценки сохранены."; }
            catch (Exception ex) { ShowError("Не удалось загрузить основу", ex); }
        }
        void SaveArmTemplateAs()
        {
            SaveFileDialog dialog = new SaveFileDialog { Title = "Сохранить техническую основу", Filter = "Основа АРМ (*.arm.json)|*.arm.json", FileName = "Типовой АРМ.arm.json", AddExtension = true, DefaultExt = ".arm.json" }; if (dialog.ShowDialog(this) != true) return;
            try { SystemPassports.SaveTemplate(assessment.Passport, dialog.FileName); status.Text = "Техническая основа сохранена: " + dialog.FileName; }
            catch (Exception ex) { ShowError("Не удалось сохранить основу", ex); }
        }
    }
}
