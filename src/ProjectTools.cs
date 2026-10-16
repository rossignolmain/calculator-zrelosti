using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace MaturityApp
{
    public class OrganizationProfile
    {
        public int Version = 1;
        public string Organization = "", OrganizationShort = "", OrganizationAddress = "", City = "";
        public string Assessor = "", AssessorPosition = "", ApproverName = "", ApproverPosition = "";
    }

    public static class ProjectTools
    {
        const int MaximumProfileBytes = 4 * 1024 * 1024;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly string[] ProfileFields = { "Organization", "OrganizationShort", "OrganizationAddress", "City", "Assessor", "AssessorPosition", "ApproverName", "ApproverPosition" };

        public static string[] Preflight(Assessment a)
        {
            return CollectPreflight(Snapshot(a));
        }

        static string[] CollectPreflight(Assessment a)
        {
            List<string> messages = new List<string>();
            if (String.IsNullOrWhiteSpace(a.Organization)) messages.Add("Не указана организация.");
            if (String.IsNullOrWhiteSpace(a.SystemName)) messages.Add("Не указана информационная система / объект оценки.");
            if (String.IsNullOrWhiteSpace(a.Assessor)) messages.Add("Не указано лицо, проводившее оценку.");
            if (!a.Directions.Any(d => d.Included)) messages.Add("Область оценки пуста: включите хотя бы одно направление.");
            foreach (DirectionAssessment d in a.Directions)
            {
                string label = d.Id + ". " + Data.DirectionNames[d.Id - 1];
                if (d.Included)
                {
                    int missing = d.Answers.Count(x => !x.HasValue);
                    if (missing > 0) messages.Add(label + ": не заполнено " + missing + " из 8 критериев.");
                }
                else if (String.IsNullOrWhiteSpace(d.Evidence))
                    messages.Add(label + ": не указано основание исключения (поле подтверждающих материалов).");
            }
            return messages.ToArray();
        }

        public static void SaveProfile(Assessment a, string path)
        {
            Assessment snapshot = Snapshot(a);
            OrganizationProfile profile = new OrganizationProfile {
                Organization = snapshot.Organization, OrganizationShort = snapshot.OrganizationShort,
                OrganizationAddress = snapshot.OrganizationAddress, City = snapshot.City,
                Assessor = snapshot.Assessor, AssessorPosition = snapshot.AssessorPosition,
                ApproverName = snapshot.ApproverName, ApproverPosition = snapshot.ApproverPosition
            };
            string json = new JavaScriptSerializer { MaxJsonLength = MaximumProfileBytes }.Serialize(profile);
            if (Utf8.GetByteCount(json) > MaximumProfileBytes) throw new InvalidDataException("Профиль организации слишком большой.");
            AtomicText(path, json);
        }

        public static OrganizationProfile LoadProfile(string path)
        {
            FileInfo info = new FileInfo(path);
            if (info.Length > MaximumProfileBytes) throw new InvalidDataException("Файл профиля слишком большой. Допустимо не более 4 МБ.");
            string json = File.ReadAllText(path, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer { MaxJsonLength = MaximumProfileBytes };
            Dictionary<string, object> document;
            OrganizationProfile profile;
            try
            {
                document = serializer.DeserializeObject(json) as Dictionary<string, object>;
                object version;
                if (document == null || !document.TryGetValue("Version", out version) || !(version is int) || (int)version != 1)
                    throw new InvalidDataException("Неподдерживаемый формат профиля. Нужен профиль организации версии 1.");
                foreach (string field in ProfileFields)
                {
                    object value;
                    if (document.TryGetValue(field, out value) && value != null && !(value is string))
                        throw new InvalidDataException("Поле профиля «" + field + "» должно содержать текст.");
                }
                profile = serializer.Deserialize<OrganizationProfile>(json);
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex) { throw new InvalidDataException("Не удалось прочитать профиль организации: некорректный JSON.", ex); }
            return NormalizeProfile(profile);
        }

        public static void ApplyProfile(Assessment a, OrganizationProfile profile)
        {
            if (a == null) throw new ArgumentNullException("a");
            OrganizationProfile p = NormalizeProfile(profile);
            // A profile never changes the evaluated system, date, scope, goals or answers.
            a.Organization = p.Organization; a.OrganizationShort = p.OrganizationShort;
            a.OrganizationAddress = p.OrganizationAddress; a.City = p.City;
            a.Assessor = p.Assessor; a.AssessorPosition = p.AssessorPosition;
            a.ApproverName = p.ApproverName; a.ApproverPosition = p.ApproverPosition;
        }

        static OrganizationProfile NormalizeProfile(OrganizationProfile p)
        {
            if (p == null || p.Version != 1) throw new InvalidDataException("Неподдерживаемый формат профиля. Нужен профиль организации версии 1.");
            return new OrganizationProfile {
                Organization = p.Organization ?? "", OrganizationShort = p.OrganizationShort ?? "",
                OrganizationAddress = p.OrganizationAddress ?? "", City = p.City ?? "",
                Assessor = p.Assessor ?? "", AssessorPosition = p.AssessorPosition ?? "",
                ApproverName = p.ApproverName ?? "", ApproverPosition = p.ApproverPosition ?? ""
            };
        }

        public static void ExportBundle(Assessment a, string path, bool includeWord, bool includeExcel)
        {
            Assessment snapshot = Snapshot(a);
            Summary summary = Calc.Summary(snapshot);
            if (summary.Count == 0) throw new InvalidDataException("Область оценки пуста. Для комплекта включите хотя бы одно направление.");
            string full = Path.GetFullPath(path), parent = Path.GetDirectoryName(full);
            Directory.CreateDirectory(parent);
            string scratch = Path.Combine(parent, ".maturity-bundle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            string project = Path.Combine(scratch, "01 Проект.uzi.json");
            string word = Path.Combine(scratch, "02 Отчёт.docx");
            string excel = Path.Combine(scratch, "03 Таблица.xlsx");
            string manifest = Path.Combine(scratch, "Состав комплекта.txt");
            string pending = Path.Combine(scratch, "bundle.tmp.zip");
            try
            {
                Store.Save(snapshot, project);
                if (includeWord) Reports.ExportWord(snapshot, word);
                if (includeExcel) Reports.ExportExcel(snapshot, excel);
                List<string> files = new List<string> { project };
                if (includeWord) files.Add(word);
                if (includeExcel) files.Add(excel);
                File.WriteAllText(manifest, Manifest(snapshot, summary, files.Select(Path.GetFileName).ToArray()), Utf8);
                files.Add(manifest);
                using (FileStream stream = new FileStream(pending, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                using (ZipArchive zip = new ZipArchive(stream, ZipArchiveMode.Create, false, Encoding.UTF8))
                {
                    foreach (string file in files)
                    {
                        ZipArchiveEntry entry = zip.CreateEntry(Path.GetFileName(file), CompressionLevel.Optimal);
                        using (Stream output = entry.Open())
                        using (FileStream input = File.OpenRead(file)) input.CopyTo(output);
                    }
                }
                ReplaceOrMove(pending, full);
            }
            finally
            {
                // Only the exact files created above are removed; no recursive deletion.
                foreach (string file in new[] { project, word, excel, manifest, pending }) TryDeleteFile(file);
                try { Directory.Delete(scratch, false); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }

        static string Manifest(Assessment a, Summary summary, string[] files)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine("КОМПЛЕКТ ОЦЕНКИ УРОВНЯ ЗРЕЛОСТИ");
            text.AppendLine("Сформирован: " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm zzz", CultureInfo.InvariantCulture));
            text.AppendLine("Организация: " + Value(a.Organization));
            text.AppendLine("Краткое наименование: " + Value(a.OrganizationShort));
            text.AppendLine("Адрес: " + Value(a.OrganizationAddress));
            text.AppendLine("Город: " + Value(a.City));
            text.AppendLine("Система / объект оценки: " + Value(a.SystemName));
            text.AppendLine("Дата оценки: " + a.Date);
            text.AppendLine("Номер отчёта: " + Value(a.DocumentNumber));
            text.AppendLine("Оценку выполнил: " + Value(a.Assessor));
            text.AppendLine("Должность оценщика: " + Value(a.AssessorPosition));
            text.AppendLine("Утверждающий: " + Value(a.ApproverName));
            text.AppendLine("Должность утверждающего: " + Value(a.ApproverPosition));
            text.AppendLine("Категория системы: " + (a.Category == 0 ? "не определена" : a.Category.ToString(CultureInfo.InvariantCulture)));
            text.AppendLine("Заполнение критериев: " + (summary.Complete ? "полное" : "ПРЕДВАРИТЕЛЬНЫЙ РЕЗУЛЬТАТ; есть незаполненные ответы"));
            text.AppendLine("Включено направлений: " + summary.Count + "; ответов: " + summary.Answered + " из " + summary.Total + ".");
            text.AppendLine("Показатель Узи: " + Calc.F(summary.Average, "0.00") + " — " + Calc.LevelName((int)Math.Floor(summary.Average)));
            text.AppendLine("Версия методики: " + Data.MethodDate);
            text.AppendLine("Источник: " + Data.SourceUrl);
            text.AppendLine(); text.AppendLine("СОСТАВ КОМПЛЕКТА");
            foreach (string file in files) text.AppendLine("- " + file);
            text.AppendLine("- Состав комплекта.txt");
            text.AppendLine(); text.AppendLine("ПРОВЕРКА ЗАПОЛНЕНИЯ ПЕРЕД ВЫГРУЗКОЙ");
            string[] messages = CollectPreflight(a);
            if (messages.Length == 0) text.AppendLine("Пропусков в основных реквизитах и критериях не обнаружено.");
            else foreach (string message in messages) text.AppendLine("- " + message);
            text.AppendLine(); text.AppendLine("JSON содержит редактируемый проект. Word и Excel сформированы из того же снимка данных. Данные не передавались в интернет.");
            return text.ToString();
        }

        static Assessment Snapshot(Assessment a)
        {
            if (a == null) throw new ArgumentNullException("a");
            return Store.Parse(Store.Serialize(a));
        }
        static string Value(string value) { return String.IsNullOrWhiteSpace(value) ? "Не указано" : value; }
        static void AtomicText(string path, string content)
        {
            string full = Path.GetFullPath(path), parent = Path.GetDirectoryName(full);
            Directory.CreateDirectory(parent);
            string temp = Path.Combine(parent, "." + Path.GetFileName(full) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try { File.WriteAllText(temp, content, Utf8); ReplaceOrMove(temp, full); }
            finally { TryDeleteFile(temp); }
        }
        static void ReplaceOrMove(string temporary, string destination)
        {
            if (File.Exists(destination)) File.Replace(temporary, destination, null);
            else File.Move(temporary, destination);
        }
        static void TryDeleteFile(string file)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
