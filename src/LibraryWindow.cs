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
    // Template library: unlimited named templates in one (optionally shared) folder.
    public class LibraryWindow : Window
    {
        readonly Assessment assessment;
        readonly Action applied;
        string folder;
        List<LibraryEntry> entries = new List<LibraryEntry>();
        readonly TextBox folderBox, searchBox, nameBox;
        readonly ListBox list;
        readonly TextBlock details, message;
        readonly CheckBox usePassport, useGrades, usePractice;
        readonly Button applyButton, overwriteButton, renameButton, removeButton;

        public LibraryWindow(Assessment assessment, Action applied)
        {
            this.assessment = assessment; this.applied = applied; folder = TemplateLibrary.LoadFolder();
            Title = "Библиотека шаблонов"; Width = 980; Height = 640; MinWidth = 820; MinHeight = 520;
            WindowStartupLocation = WindowStartupLocation.CenterOwner; FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Background = Theme.Paper;
            Grid root = new Grid { Margin = new Thickness(18) }; Content = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel top = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            top.Children.Add(Theme.Text("Папка библиотеки — можно указать общую сетевую или облачную папку, тогда шаблоны видят все коллеги", 11, Theme.Muted));
            Grid folderRow = new Grid { Margin = new Thickness(0, 6, 0, 0) }; folderRow.ColumnDefinitions.Add(new ColumnDefinition()); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            folderBox = new TextBox { Text = folder, Height = 32, Padding = new Thickness(7, 5, 7, 5) }; folderRow.Children.Add(folderBox);
            Button useFolder = Theme.Button("Использовать папку", () => ChangeFolder(folderBox.Text)); useFolder.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(useFolder, 1); folderRow.Children.Add(useFolder);
            Button openFolder = Theme.Button("Открыть", () => { try { Directory.CreateDirectory(folder); Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true }); } catch (Exception ex) { Say(ex.Message); } }); openFolder.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(openFolder, 2); folderRow.Children.Add(openFolder);
            top.Children.Add(folderRow); root.Children.Add(top);

            Grid body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) }); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); Grid.SetRow(body, 1); root.Children.Add(body);
            Grid left = new Grid { Margin = new Thickness(0, 0, 14, 0) }; left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); left.RowDefinitions.Add(new RowDefinition()); body.Children.Add(left);
            Grid searchField = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchBox = new TextBox { Height = 32, Padding = new Thickness(7, 5, 7, 5) }; searchField.Children.Add(searchBox);
            TextBlock searchHint = Theme.Text("Поиск по названию", 12, Theme.Muted); searchHint.Margin = new Thickness(9, 8, 0, 0); searchHint.IsHitTestVisible = false; searchField.Children.Add(searchHint);
            searchBox.TextChanged += (s, e) => { searchHint.Visibility = searchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; Fill(); }; left.Children.Add(searchField);
            list = new ListBox { BorderBrush = Theme.Line }; ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled); list.SelectionChanged += (s, e) => ShowSelected(); Grid.SetRow(list, 1); left.Children.Add(list);

            StackPanel right = new StackPanel(); ScrollViewer rightScroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetColumn(rightScroll, 1); body.Children.Add(rightScroll);
            StackPanel applyPanel = new StackPanel();
            applyPanel.Children.Add(Theme.Text("Выбранный шаблон", 15, null, true));
            details = Theme.Text("Выберите шаблон в списке.", 12, Theme.Muted); details.Margin = new Thickness(0, 6, 0, 10); applyPanel.Children.Add(details);
            applyPanel.Children.Add(Theme.Text("Что подставить в текущую оценку:", 12));
            usePassport = new CheckBox { Content = "Паспорт объекта", IsChecked = true, Margin = new Thickness(0, 6, 0, 0) }; applyPanel.Children.Add(usePassport);
            useGrades = new CheckBox { Content = "Оценки, область, категория и цели", IsChecked = true, Margin = new Thickness(0, 4, 0, 0) }; applyPanel.Children.Add(useGrades);
            usePractice = new CheckBox { Content = "Требования, исходные данные, методы и инструменты", IsChecked = true, Margin = new Thickness(0, 4, 0, 10) }; applyPanel.Children.Add(usePractice);
            applyButton = Theme.Button("Подставить в текущую оценку", () => ApplySelected(SelectedParts()), true); applyButton.HorizontalAlignment = HorizontalAlignment.Left; applyPanel.Children.Add(applyButton);
            applyPanel.Children.Add(Theme.Text("Организация, люди, дата, подтверждающие материалы и предыдущая оценка не меняются.", 11, Theme.Muted));
            right.Children.Add(Theme.Card(applyPanel, new Thickness(14)));

            StackPanel savePanel = new StackPanel();
            savePanel.Children.Add(Theme.Text("Текущая оценка → шаблон", 15, null, true));
            savePanel.Children.Add(Theme.Text("Название (например, «Школа · АИСТ · ГИС К3»)", 11, Theme.Muted));
            nameBox = new TextBox { Height = 32, Padding = new Thickness(7, 5, 7, 5), Margin = new Thickness(0, 5, 0, 8), Text = DefaultName() }; savePanel.Children.Add(nameBox);
            WrapPanel saveButtons = new WrapPanel();
            saveButtons.Children.Add(Theme.Button("Сохранить как новый", () => SaveCurrentAsNew(nameBox.Text), true));
            overwriteButton = Theme.Button("Заменить выбранный", ConfirmOverwrite); saveButtons.Children.Add(overwriteButton);
            savePanel.Children.Add(saveButtons);
            WrapPanel manage = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
            renameButton = Theme.Button("Переименовать выбранный", () => RenameSelected(nameBox.Text)); manage.Children.Add(renameButton);
            removeButton = Theme.Button("Удалить выбранный", ConfirmRemove); manage.Children.Add(removeButton);
            savePanel.Children.Add(manage);
            Border saveCard = Theme.Card(savePanel, new Thickness(14)); saveCard.Margin = new Thickness(0, 12, 0, 0); right.Children.Add(saveCard);

            message = Theme.Text("", 11, Theme.Muted); message.Margin = new Thickness(0, 10, 0, 0); Grid.SetRow(message, 2); root.Children.Add(message);
            Refresh();
        }

        string DefaultName()
        {
            string system = assessment.Passport != null && !String.IsNullOrWhiteSpace(assessment.Passport.ExternalSystem) ? assessment.Passport.ExternalSystem.Trim() : assessment.SystemName.Trim();
            return system.Length == 0 ? "Новый шаблон" : system;
        }
        void Say(string text) { message.Text = text; }
        public List<LibraryEntry> Entries { get { return entries; } }
        public string Folder { get { return folder; } }
        LibraryEntry Selected { get { ListBoxItem item = list.SelectedItem as ListBoxItem; return item == null ? null : (LibraryEntry)item.Tag; } }
        TemplateParts SelectedParts()
        {
            return (usePassport.IsChecked == true ? TemplateParts.Passport : 0) | (useGrades.IsChecked == true ? TemplateParts.Grades : 0) | (usePractice.IsChecked == true ? TemplateParts.Practice : 0);
        }

        public void Refresh(string select = null)
        {
            try { entries = TemplateLibrary.List(folder); }
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
            if (list.Items.Count > 0) Say(""); // callers report their own result after refreshing
            else Say(entries.Count == 0 ? "В папке пока нет шаблонов. Заполните оценку и нажмите «Сохранить как новый»." : "Ничего не найдено.");
            ShowSelected();
        }
        void ShowSelected()
        {
            LibraryEntry e = Selected; bool template = e != null && e.Template != null;
            applyButton.IsEnabled = e != null && e.IsValid; overwriteButton.IsEnabled = renameButton.IsEnabled = template; removeButton.IsEnabled = e != null;
            if (e == null) { details.Text = "Выберите шаблон в списке."; return; }
            if (!e.IsValid) { details.Text = "Файл не читается: " + e.Error; return; }
            TemplateParts parts = template ? AnswerTemplates.PartsOf(e.Template) : TemplateParts.Passport;
            usePassport.IsEnabled = (parts & TemplateParts.Passport) != 0; useGrades.IsEnabled = (parts & TemplateParts.Grades) != 0; usePractice.IsEnabled = (parts & TemplateParts.Practice) != 0;
            string text = e.Name + "\nСодержит: " + (template ? AnswerTemplates.PartsText(parts) : "только паспорт (основа АРМ)");
            if (template)
            {
                int included = e.Template.Directions.Count(d => d.Included), answered = e.Template.Directions.Where(d => d.Included).Sum(d => d.Answers.Count(x => x.HasValue));
                text += "\nНаправлений в области: " + included + " из 21; ответов: " + answered + " из " + included * 8 + "; категория: " + (e.Template.Category == 0 ? "не определена" : e.Template.Category.ToString());
                if (e.Template.Author.Length > 0 || e.Template.Created.Length > 0) text += "\nАвтор: " + (e.Template.Author.Length > 0 ? e.Template.Author : "—") + (e.Template.Created.Length > 0 ? " · создан " + e.Template.Created : "");
            }
            if (e.Passport != null || template && e.Template.Passport != null) text += "\nВнешняя система: " + ((e.Passport ?? e.Template.Passport).ExternalSystem.Length > 0 ? (e.Passport ?? e.Template.Passport).ExternalSystem : "не указана");
            details.Text = text + "\nФайл: " + Path.GetFileName(e.Path);
        }

        public void ChangeFolder(string path)
        {
            try { TemplateLibrary.SaveFolder(path); folder = path.Trim(); folderBox.Text = folder; Refresh(); Say("Папка библиотеки: " + folder); }
            catch (Exception ex) { Say("Не удалось использовать папку: " + ex.Message); }
        }
        public void UseFolderForSession(string path) { folder = path; folderBox.Text = path; Refresh(); } // tests: no settings write

        public bool ApplySelected(TemplateParts parts)
        {
            LibraryEntry e = Selected; if (e == null || !e.IsValid) return false;
            if (parts == TemplateParts.None) { Say("Отметьте, что подставить."); return false; }
            try
            {
                if (e.Template != null) AnswerTemplates.Apply(assessment, e.Template, parts);
                else if ((parts & TemplateParts.Passport) != 0) SystemPassports.Apply(assessment, e.Passport);
                if (applied != null) applied();
                Say("Подставлено из «" + e.Name + "». Исправьте отличия этого объекта.");
                return true;
            }
            catch (Exception ex) { Say("Не удалось применить шаблон: " + ex.Message); return false; }
        }
        public string SaveCurrentAsNew(string name)
        {
            try
            {
                string path = TemplateLibrary.SaveNew(AnswerTemplates.Create(assessment, (name ?? "").Trim()), folder);
                Refresh(path); Say("Сохранён новый шаблон: " + Path.GetFileName(path)); return path;
            }
            catch (Exception ex) { Say("Не удалось сохранить: " + ex.Message); return null; }
        }
        void ConfirmOverwrite()
        {
            LibraryEntry e = Selected; if (e == null) return;
            if (MessageBox.Show(this, "Заменить шаблон «" + e.Name + "» текущей оценкой? Прежнее содержимое шаблона будет потеряно.", "Замена шаблона", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK) OverwriteSelected();
        }
        public bool OverwriteSelected()
        {
            LibraryEntry e = Selected; if (e == null) return false;
            try { TemplateLibrary.Overwrite(e, AnswerTemplates.Create(assessment, e.Name)); Refresh(e.Path); Say("Шаблон «" + e.Name + "» обновлён."); return true; }
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
            if (MessageBox.Show(this, "Удалить шаблон «" + e.Name + "»? Файл будет перенесён в подпапку «" + TemplateLibrary.TrashFolder + "», его можно вернуть вручную.", "Удаление шаблона", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) RemoveSelected();
        }
        public bool RemoveSelected()
        {
            LibraryEntry e = Selected; if (e == null) return false;
            try { TemplateLibrary.Remove(e); Refresh(); Say("Шаблон перенесён в «" + TemplateLibrary.TrashFolder + "»."); return true; }
            catch (Exception ex) { Say("Не удалось удалить: " + ex.Message); return false; }
        }
        public void Select(string path) { Fill(path); }
        public void RenderPreview(string path)
        {
            // The root grid has an 18px margin; render the whole 980 x 680 client area.
            const int width = 980, height = 680;
            FrameworkElement content = (FrameworkElement)Content;
            content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
            System.Windows.Media.Imaging.RenderTargetBitmap bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(content);
            System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
