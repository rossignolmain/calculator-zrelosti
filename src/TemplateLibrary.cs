using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public class LibraryEntry
    {
        public string Path, Name, Error = "";
        public DateTime Modified;
        public AnswerTemplate Template;   // full template (.answers.json)
        public SystemPassport Passport;   // legacy passport-only basis (.arm.json)
        public bool IsValid { get { return Template != null || Passport != null; } }
        public string Summary
        {
            get
            {
                if (!IsValid) return "Не читается: " + Error;
                string parts = Template != null ? AnswerTemplates.PartsText(AnswerTemplates.PartsOf(Template)) : "только паспорт (основа АРМ)";
                string who = Template != null && Template.Author.Length > 0 ? " · " + Template.Author : "";
                return parts + who + " · " + Modified.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);
            }
        }
    }

    public class LibrarySettings { public string TemplateFolder = ""; }

    // Unlimited templates, one small file each, in a folder that can be shared
    // (network share or synced cloud folder). Last writer wins for the same file;
    // new templates never collide because file names are made unique.
    public static class TemplateLibrary
    {
        public const string TemplateExtension = ".answers.json", PassportExtension = ".arm.json", TrashFolder = "_Удалённые";

        public static string DataFolder { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Данные"); } }
        public static string DefaultFolder { get { return Path.Combine(DataFolder, "Шаблоны оценок"); } }
        static string SettingsPath { get { return Path.Combine(DataFolder, "Настройки.json"); } }

        public static string LoadFolder()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    LibrarySettings s = new JavaScriptSerializer().Deserialize<LibrarySettings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                    if (s != null && !String.IsNullOrWhiteSpace(s.TemplateFolder)) return s.TemplateFolder;
                }
            }
            catch { } // unreadable settings fall back to the local default
            return DefaultFolder;
        }
        public static void SaveFolder(string folder)
        {
            if (String.IsNullOrWhiteSpace(folder)) throw new InvalidDataException("Укажите папку библиотеки.");
            folder = folder.Trim();
            Directory.CreateDirectory(folder); // also proves the folder is reachable and writable enough to create
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(SettingsPath, new JavaScriptSerializer().Serialize(new LibrarySettings { TemplateFolder = folder }), new UTF8Encoding(false));
        }

        public static List<LibraryEntry> List(string folder)
        {
            List<LibraryEntry> entries = new List<LibraryEntry>();
            if (!Directory.Exists(folder)) return entries;
            foreach (string path in Directory.GetFiles(folder, "*.json"))
            {
                string file = System.IO.Path.GetFileName(path);
                bool template = file.EndsWith(TemplateExtension, StringComparison.OrdinalIgnoreCase), arm = file.EndsWith(PassportExtension, StringComparison.OrdinalIgnoreCase);
                if (!template && !arm) continue;
                LibraryEntry e = new LibraryEntry { Path = path, Modified = File.GetLastWriteTime(path), Name = file.Substring(0, file.Length - (template ? TemplateExtension.Length : PassportExtension.Length)) };
                try
                {
                    if (template) { e.Template = AnswerTemplates.Load(path); if (e.Template.Name.Length > 0) e.Name = e.Template.Name; }
                    else e.Passport = SystemPassports.LoadTemplate(path);
                }
                catch (Exception ex) { e.Error = ex.Message; }
                entries.Add(e);
            }
            return entries.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static string SaveNew(AnswerTemplate t, string folder)
        {
            if (String.IsNullOrWhiteSpace(t.Name)) throw new InvalidDataException("Укажите название шаблона.");
            Directory.CreateDirectory(folder);
            string path = UniquePath(folder, SafeName(t.Name), TemplateExtension);
            AnswerTemplates.Save(t, path);
            return path;
        }
        public static void Overwrite(LibraryEntry entry, AnswerTemplate t)
        {
            if (entry == null || entry.Template == null) throw new InvalidDataException("Заменить можно только шаблон оценки (.answers.json).");
            t.Name = entry.Template.Name.Length > 0 ? entry.Template.Name : entry.Name;
            AnswerTemplates.Save(t, entry.Path);
        }
        public static string Rename(LibraryEntry entry, string name)
        {
            if (entry == null || entry.Template == null) throw new InvalidDataException("Переименовать можно только шаблон оценки (.answers.json).");
            if (String.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Укажите новое название.");
            string folder = System.IO.Path.GetDirectoryName(entry.Path);
            entry.Template.Name = name.Trim();
            string target = UniquePath(folder, SafeName(name), TemplateExtension, entry.Path);
            AnswerTemplates.Save(entry.Template, entry.Path);
            if (!String.Equals(target, entry.Path, StringComparison.OrdinalIgnoreCase)) File.Move(entry.Path, target);
            return target;
        }
        // Moves the file to a trash subfolder instead of deleting it, so a shared library is recoverable.
        public static string Remove(LibraryEntry entry)
        {
            string folder = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(entry.Path), TrashFolder);
            Directory.CreateDirectory(folder);
            string file = System.IO.Path.GetFileName(entry.Path);
            string target = System.IO.Path.Combine(folder, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + " " + file);
            File.Move(entry.Path, target);
            return target;
        }

        static string SafeName(string name)
        {
            string safe = new string(name.Trim().Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
            if (safe.Length > 80) safe = safe.Substring(0, 80).Trim();
            return safe.Length == 0 ? "Шаблон" : safe;
        }
        static string UniquePath(string folder, string name, string extension, string allowed = null)
        {
            string path = System.IO.Path.Combine(folder, name + extension);
            for (int i = 2; File.Exists(path) && !String.Equals(path, allowed, StringComparison.OrdinalIgnoreCase); i++)
                path = System.IO.Path.Combine(folder, name + " (" + i + ")" + extension);
            return path;
        }
    }
}
