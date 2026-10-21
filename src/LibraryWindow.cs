using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace MaturityApp
{
    // One library, two sections: object templates (description, grades, methods) and
    // organization profiles (requisites and people). Unlimited files in one, optionally shared, folder.
    public class LibraryWindow : Window
    {
        readonly Assessment assessment;
        readonly Action applied;
        string folder;
        bool organizations;
        List<LibraryEntry> entries = new List<LibraryEntry>();
        readonly TextBox folderBox, searchBox, nameBox;
        readonly ListBox list;
        readonly TextBlock details, message, saveTitle, saveHint, applyNote;
        readonly StackPanel partsPanel;
        readonly CheckBox usePassport, useGrades, usePractice;
        readonly Button templatesTab, organizationsTab, applyButton, overwriteButton, renameButton, removeButton;

        public LibraryWindow(Assessment assessment, Action applied, bool organizations = false)
        {
            this.assessment = assessment; this.applied = applied; this.organizations = organizations; folder = TemplateLibrary.LoadFolder();
            Title = "Шаблоны и организации"; Width = 1000; Height = 680; MinWidth = 840; MinHeight = 540;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Background = Theme.Paper;
            Language = System.Windows.Markup.XmlLanguage.GetLanguage("ru-RU");
            Grid root = new Grid { Margin = new Thickness(18) }; Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel tabs = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            templatesTab = Theme.Button("Шаблоны объектов", () => SwitchSection(false)); organizationsTab = Theme.Button("Организации", () => SwitchSection(true));
            tabs.Children.Add(templatesTab); tabs.Children.Add(organizationsTab); root.Children.Add(tabs);

            StackPanel top = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; Grid.SetRow(top, 1);
            top.Children.Add(Theme.Text("Папка библиотеки. Укажите общую сетевую или облачную папку, чтобы шаблоны и профили видели коллеги.", 11, Theme.Muted));
            Grid folderRow = new Grid { Margin = new Thickness(0, 6, 0, 0) }; folderRow.ColumnDefinitions.Add(new ColumnDefinition()); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folderBox = new TextBox { Text = folder, Height = 32, Padding = new Thickness(7, 5, 7, 5) }; folderRow.Children.Add(folderBox);
            Button useFolder = Theme.Button("Использовать папку", () => ChangeFolder(folderBox.Text)); useFolder.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(useFolder, 1); folderRow.Children.Add(useFolder);
            Button openFolder = Theme.Button("Открыть", () => { try { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); } catch (Exception ex) { Say(ex.Message); } }); openFolder.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(openFolder, 2); folderRow.Children.Add(openFolder);
            top.Children.Add(folderRow); root.Children.Add(top);

            Grid body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); Grid.SetRow(body, 2); root.Children.Add(body);
            Grid left = new Grid { Margin = new Thickness(0, 0, 14, 0) }; left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition()); body.Children.Add(left);
            Grid searchField = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchBox = new TextBox { Height = 32, Padding = new Thickness(7, 5, 7, 5) }; searchField.Children.Add(searchBox);
            TextBlock searchHint = Theme.Text("Поиск по названию", 12, Theme.Muted); searchHint.Margin = new Thickness(9, 8, 0, 0); searchHint.IsHitTestVisible = false; searchField.Children.Add(searchHint);
            searchBox.TextChanged += (s, e) => { searchHint.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Fill(); }; left.Children.Add(searchField);
            list = new ListBox { BorderBrush = Theme.Line }; ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled); list.SelectionChanged += (s, e) => ShowSelected(); Grid.SetRow(list, 1); left.Children.Add(list);

            StackPanel right = new StackPanel(); ScrollViewer rightScroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(rightScroll, 1); body.Children.Add(rightScroll);
            StackPanel applyPanel = new StackPanel();
            applyPanel.Children.Add(Theme.Text("Выбранное", 15, null, true));
            details = Theme.Text("Выберите строку в списке.", 12, Theme.Muted); details.Margin = new Thickness(0, 6, 0, 10); applyPanel.Children.Add(details);
            partsPanel = new StackPanel();
            partsPanel.Children.Add(Theme.Text("Что подставить в текущую оценку:", 12));
            usePassport = new CheckBox { Content = "Описание объекта (состав, взаимодействия, СЗИ, технологии)", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) }; partsPanel.Children.Add(usePassport);
            useGrades = new CheckBox { Content = "Тип объекта, ответы анкеты, область оценки и цели", IsChecked = true, Margin = new Thickness(0, 4, 0, 0) }; partsPanel.Children.Add(useGrades);
            usePractice = new CheckBox { Content = "Требования, исходные данные, методы и инструменты", IsChecked = true, Margin = new Thickness(0, 4, 0, 10) }; partsPanel.Children.Add(usePractice);
            applyPanel.Children.Add(partsPanel);
            applyButton = Theme.Button("Подставить в текущую оценку", () => ApplySelected(SelectedParts()), true); applyButton.HorizontalAlignment = HorizontalAlignment.Left; applyPanel.Children.Add(applyButton);
            applyNote = Theme.Text("", 11, Theme.Muted); applyNote.Margin = new Thickness(0, 6, 0, 0); applyPanel.Children.Add(applyNote);
            right.Children.Add(Theme.Card(applyPanel, new Thickness(14)));

            StackPanel savePanel = new StackPanel();
            saveTitle = Theme.Text("", 15, null, true); savePanel.Children.Add(saveTitle);
            saveHint = Theme.Text("", 11, Theme.Muted); savePanel.Children.Add(saveHint);
            nameBox = new TextBox { Height = 32, Padding = new Thickness(7, 5, 7, 5), Margin = new Thickness(0, 5, 0, 8) }; savePanel.Children.Add(nameBox);
            WrapPanel saveButtons = new WrapPanel();
            saveButtons.Children.Add(Theme.Button("Сохранить как новый", () => SaveCurrentAsNew(nameBox.Text), true));
            overwriteButton = Theme.Button("Заменить выбранный", ConfirmOverwrite); saveButtons.Children.Add(overwriteButton);
            savePanel.Children.Add(saveButtons);
            WrapPanel manage = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            renameButton = Theme.Button("Переименовать выбранный", () => RenameSelected(nameBox.Text)); manage.Children.Add(renameButton);
            removeButton = Theme.Button("Удалить выбранный", ConfirmRemove); manage.Children.Add(removeButton);
            savePanel.Children.Add(manage);
            Border saveCard = Theme.Card(savePanel, new Thickness(14)); saveCard.Margin = new Thickness(0, 12, 0, 0); right.Children.Add(saveCard);

            message = Theme.Text("", 11, Theme.Muted); message.Margin = new Thickness(0, 10, 0, 0); Grid.SetRow(message, 3); root.Children.Add(message);
            SwitchSection(organizations);
        }

        void SwitchSection(bool orgs)
        {
            organizations = orgs;
            templatesTab.Background = orgs ? Brushes.White : Theme.Blue; templatesTab.Foreground = orgs ? Theme.Ink : Brushes.White;
            organizationsTab.Background = orgs ? Theme.Blue : Brushes.White; organizationsTab.Foreground = orgs ? Brushes.White : Theme.Ink;
            partsPanel.Visibility = orgs ? Visibility.Collapsed : Visibility.Visible;
            applyNote.Text = orgs ? "Подставляются реквизиты, подписанты, контакты и участники. Объект, ответы, цели и материалы не меняются."
                                  : "Организация, люди, дата, подтверждающие материалы и предыдущая оценка не меняются.";
            saveTitle.Text = orgs ? "Текущие реквизиты → профиль организации" : "Текущая оценка → шаблон объекта";
            saveHint.Text = orgs ? "Название профиля (обычно краткое наименование организации)" : "Название шаблона (например, «Поликлиника · ИСПДн УЗ-2» или «АСУ ТП цеха · ЗОКИИ 2»)";
            nameBox.Text = orgs ? (assessment.OrganizationShort.Trim().Length > 0 ? assessment.OrganizationShort.Trim() : assessment.Organization.Trim()) : assessment.SystemName.Trim();
            Refresh();
        }
        void Say(string text) { message.Text = text; }
        public List<LibraryEntry> Entries { get { return entries; } }
        public string Folder { get { return folder; } }
        public bool OrganizationsSection { get { return organizations; } }
        LibraryEntry Selected { get { ListBoxItem item = list.SelectedItem as ListBoxItem; return item == null ? null : (LibraryEntry)item.Tag; } }
        TemplateParts SelectedParts()
        {
            return (usePassport.IsChecked == true ? TemplateParts.Passport : 0) | (useGrades.IsChecked == true ? TemplateParts.Grades : 0) | (usePractice.IsChecked == true ? TemplateParts.Practice : 0);
        }

        public void Refresh(string select = null)
        {
            try { entries = TemplateLibrary.List(folder).Where(e => e.IsOrganization == organizations).ToList(); }
            catch (Exception ex) { entries = new List<LibraryEntry>(); Say("Папка недоступна: " + ex.Message); }
            Fill(select);
        }
        void Fill(string select = null)
        {
            string q = searchBox.Text.Trim(); list.Items.Clear();
            foreach (LibraryEntry e in entries.Where(x => q.Length == 0 || x.Name.IndexOf(q, StringComparison.CurrentCultureIgnoreCase) >= 0))
            {
                StackPanel row = new StackPanel { Margin = new Thickness(4, 5, 4, 5) };
                row.Children.Add(Theme.Text(e.Name, 13, e.IsValid ? Theme.Ink : Theme.Muted, true));
                row.Children.Add(Theme.Text(e.Summary, 11, Theme.Muted));
                ListBoxItem item = new ListBoxItem { Content = row, Tag = e }; list.Items.Add(item);
                if (select != null && String.Equals(e.Path, select, StringComparison.OrdinalIgnoreCase)) list.SelectedItem = item;
            }
            if (list.Items.Count > 0) Say("");
            else Say(entries.Count > 0 ? "Ничего не найдено." : organizations ? "Профилей пока нет. Заполните реквизиты и нажмите «Сохранить как новый»." : "Шаблонов пока нет. Заполните оценку и нажмите «Сохранить как новый».");
            ShowSelected();
        }
        void ShowSelected()
        {
            LibraryEntry e = Selected; bool editable = e != null && (e.Template != null || e.Profile != null);
            applyButton.IsEnabled = e != null && e.IsValid; overwriteButton.IsEnabled = renameButton.IsEnabled = editable; removeButton.IsEnabled = e != null;
            if (e == null) { details.Text = "Выберите строку в списке."; return; }
            if (!e.IsValid) { details.Text = "Файл не читается: " + e.Error; return; }
            if (e.Profile != null)
            {
                details.Text = e.Name + "\nОрганизация: " + (e.Profile.Organization.Length > 0 ? e.Profile.Organization : "не указана") + "\nПодписант: " + (e.Profile.ApproverName.Length > 0 ? e.Profile.ApproverName : "не указан") + "\nУчастников: " + e.Profile.Participants.Length + "\nФайл: " + Path.GetFileName(e.Path);
                return;
            }
            TemplateParts parts = e.Template != null ? AnswerTemplates.PartsOf(e.Template) : TemplateParts.Passport;
            usePassport.IsEnabled = (parts & TemplateParts.Passport) != 0; useGrades.IsEnabled = (parts & TemplateParts.Grades) != 0; usePractice.IsEnabled = (parts & TemplateParts.Practice) != 0;
            string text = e.Name + "\nСодержит: " + (e.Template != null ? AnswerTemplates.PartsText(parts) : "только описание объекта");
            if (e.Template != null)
            {
                int included = e.Template.Directions.Count(d => d.Included), answered = e.Template.Directions.Where(d => d.Included).Sum(d => d.Answers.Count(x => x.HasValue));
                text += "\nНаправлений в области: " + included + " из 21; ответов: " + answered + " из " + included * 8;
                if (e.Template.Objects != null && e.Template.Objects.Length > 0) text += "\nТип объекта: " + String.Join("; ", e.Template.Objects.Select(ObjectCatalog.Text));
                if (e.Template.Author.Length > 0 || e.Template.Created.Length > 0) text += "\nАвтор: " + (e.Template.Author.Length > 0 ? e.Template.Author : "—") + (e.Template.Created.Length > 0 ? " · создан " + e.Template.Created : "");
            }
            SystemPassport p = e.Passport ?? (e.Template != null ? e.Template.Passport : null);
            if (p != null) text += "\n" + SystemPassports.Description(p);
            details.Text = text + "\nФайл: " + Path.GetFileName(e.Path);
        }

        public void ChangeFolder(string path)
        {
            try { TemplateLibrary.SaveFolder(path); folder = path.Trim(); folderBox.Text = folder; Refresh(); Say("Папка библиотеки: " + folder); }
            catch (Exception ex) { Say("Не удалось использовать папку: " + ex.Message); }
        }
        public void UseFolderForSession(string path) { folder = path; folderBox.Text = path; Refresh(); } // tests: no settings write
        public void ShowOrganizations(bool value) { SwitchSection(value); }

        public bool ApplySelected(TemplateParts parts)
        {
            LibraryEntry e = Selected; if (e == null || !e.IsValid) return false;
            try
            {
                if (e.Profile != null) ProjectTools.ApplyProfile(assessment, e.Profile);
                else
                {
                    if (parts == TemplateParts.None) { Say("Отметьте, что подставить."); return false; }
                    if (e.Template != null) AnswerTemplates.Apply(assessment, e.Template, parts);
                    else if ((parts & TemplateParts.Passport) != 0) SystemPassports.Apply(assessment, e.Passport);
                }
                if (applied != null) applied();
                Say("Подставлено из «" + e.Name + "».");
                return true;
            }
            catch (Exception ex) { Say("Не удалось подставить: " + ex.Message); return false; }
        }
        public string SaveCurrentAsNew(string name)
        {
            try
            {
                name = (name ?? "").Trim();
                string path = organizations ? TemplateLibrary.SaveNewProfile(assessment, name, folder) : TemplateLibrary.SaveNew(AnswerTemplates.Create(assessment, name), folder);
                Refresh(path); Say("Сохранено: " + Path.GetFileName(path)); return path;
            }
            catch (Exception ex) { Say("Не удалось сохранить: " + ex.Message); return null; }
        }
        void ConfirmOverwrite()
        {
            LibraryEntry e = Selected; if (e == null) return;
            if (MessageBox.Show(this, "Заменить «" + e.Name + "» текущими данными? Прежнее содержимое будет потеряно.", "Замена", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK) OverwriteSelected();
        }
        public bool OverwriteSelected()
        {
            LibraryEntry e = Selected; if (e == null) return false;
            try
            {
                if (e.Profile != null) TemplateLibrary.OverwriteProfile(e, assessment); else TemplateLibrary.Overwrite(e, AnswerTemplates.Create(assessment, e.Name));
                Refresh(e.Path); Say("«" + e.Name + "» обновлён."); return true;
            }
            catch (Exception ex) { Say("Не удалось заменить: " + ex.Message); return false; }
        }
        public bool RenameSelected(string name)
        {
            LibraryEntry e = Selected; if (e == null) return false;
            try { string path = TemplateLibrary.Rename(e, name); Refresh(path); Say("Переименовано."); return true; }
            catch (Exception ex) { Say("Не удалось переименовать: " + ex.Message); return false; }
        }
        void ConfirmRemove()
        {
            LibraryEntry e = Selected; if (e == null) return;
            if (MessageBox.Show(this, "Удалить «" + e.Name + "»? Файл будет перенесён в подпапку «" + TemplateLibrary.TrashFolder + "», его можно вернуть вручную.", "Удаление", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) RemoveSelected();
        }
        public bool RemoveSelected()
        {
            LibraryEntry e = Selected; if (e == null) return false;
            try { TemplateLibrary.Remove(e); Refresh(); Say("Перенесено в «" + TemplateLibrary.TrashFolder + "»."); return true; }
            catch (Exception ex) { Say("Не удалось удалить: " + ex.Message); return false; }
        }
        public void Select(string path) { Fill(path); }
        public void RenderPreview(string path)
        {
            const int width = 1000, height = 700;
            FrameworkElement content = (FrameworkElement)Content;
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
