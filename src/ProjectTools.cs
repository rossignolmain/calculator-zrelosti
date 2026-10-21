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
        // 1.6: contacts and people for item 35 в, г of the methodology. Optional in older profiles.
        public string AssessorOrganization = "", AssessorContact = "";
        public ReportPerson[] Participants = new ReportPerson[0];
    }

    public static class ProjectTools
    {
        const int MaximumProfileBytes = 4 * 1024 * 1024;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        static readonly string[] ProfileFields = { "Organization", "OrganizationShort", "OrganizationAddress", "City", "Assessor", "AssessorPosition", "ApproverName", "ApproverPosition", "AssessorOrganization", "AssessorContact" };

        public static Assessment LoadAssessmentFile(string path)
        {
            if (!String.Equals(Path.GetExtension(path), ".zip", StringComparison.OrdinalIgnoreCase)) return Store.Load(path);
            const int maximumProjectBytes = 4 * 1024 * 1024;
            try
            {
                using (FileStream file = File.OpenRead(path))
                using (ZipArchive archive = new ZipArchive(file, ZipArchiveMode.Read, false, Encoding.UTF8))
                {
                    ZipArchiveEntry project = null;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!String.Equals(entry.FullName, "01 Проект.uzi.json", StringComparison.Ordinal)) continue;
                        if (project != null) throw new InvalidDataException("В комплекте найдено несколько файлов «01 Проект.uzi.json».");
                        project = entry;
                    }
                    if (project == null) throw new InvalidDataException("В комплекте отсутствует файл «01 Проект.uzi.json».");
                    if (project.Length < 0 || project.Length > maximumProjectBytes) throw new InvalidDataException("Проект внутри комплекта превышает допустимый размер 4 МБ.");
                    using (Stream input = project.Open())
                    using (MemoryStream data = new MemoryStream())
                    {
                        byte[] buffer = new byte[8192];
                        int total = 0, read;
                        // Read at most the limit plus one byte, even if the ZIP directory lies about entry.Length.
                        while ((read = input.Read(buffer, 0, Math.Min(buffer.Length, maximumProjectBytes - total + 1))) > 0)
                        {
                            total += read;
                            if (total > maximumProjectBytes) throw new InvalidDataException("Проект внутри комплекта превышает допустимый размер 4 МБ.");
                            data.Write(buffer, 0, read);
                        }
                        if (total != project.Length) throw new InvalidDataException("Размер проекта внутри комплекта не соответствует данным архива.");
                        string json = new UTF8Encoding(false, true).GetString(data.ToArray());
                        if (json.Length > 0 && json[0] == '\uFEFF') json = json.Substring(1);
                        return Store.Parse(json);
                    }
                }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception ex) { throw new InvalidDataException("Не удалось открыть комплект: архив или содержащийся в нём проект повреждён.", ex); }
        }

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
                ApproverName = snapshot.ApproverName, ApproverPosition = snapshot.ApproverPosition,
                AssessorOrganization = snapshot.AssessorOrganization, AssessorContact = snapshot.AssessorContact, Participants = snapshot.Participants
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
            a.AssessorOrganization = p.AssessorOrganization; a.AssessorContact = p.AssessorContact; a.Participants = p.Participants;
        }

        static OrganizationProfile NormalizeProfile(OrganizationProfile p)
        {
            if (p == null || p.Version != 1) throw new InvalidDataException("Неподдерживаемый формат профиля. Нужен профиль организации версии 1.");
            return new OrganizationProfile {
                Organization = p.Organization ?? "", OrganizationShort = p.OrganizationShort ?? "",
                OrganizationAddress = p.OrganizationAddress ?? "", City = p.City ?? "",
                Assessor = p.Assessor ?? "", AssessorPosition = p.AssessorPosition ?? "",
                ApproverName = p.ApproverName ?? "", ApproverPosition = p.ApproverPosition ?? "",
                AssessorOrganization = p.AssessorOrganization ?? "", AssessorContact = p.AssessorContact ?? "",
                Participants = NormalizePeople(p.Participants)
            };
        }
        static ReportPerson[] NormalizePeople(ReportPerson[] people)
        {
            Assessment check = Assessment.Create(); check.Participants = people ?? new ReportPerson[0];
            ReportFacts.Normalize(check); // same role and size checks as a project
            return check.Participants.Select(x => new ReportPerson { Role = x.Role, Name = x.Name, Position = x.Position, Organization = x.Organization, Contact = x.Contact }).ToArray();
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
