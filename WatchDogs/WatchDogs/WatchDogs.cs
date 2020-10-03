using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace WatchDogs
{
    public partial class WatchDogs : ServiceBase
    {
        private static List<DNA> WOF;

        private static AutoResetEvent msgQueueDataEvent = null;
        private static ManualResetEvent msgQueueProcessingThreadExitEvent = null;
        private static Task msgQueueProcessingTask;
        private static ISchedulerFactory sf = null;
        private static IScheduler scheduler = null;
        private static int NumberOfCpuCores;
        private static Regex rxNumberOfCores = new Regex(@".*NumberOfCores(.*\r{0,1}\n{0,1})+(?<cores>\d+).*", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace);
        private static object obj;
        private static Task primaryComparisonTask;
        private static Task lastInQueueCheckTask;
        private static List<FileSystemWatcher> watchers;
        private static List<DNA> up_msg_queue = new List<DNA>();

        //private static FileSystemWatcher alphaWatcher;
        private static object _createdFiles = new object();

        private static long fileIdCounter;
        public WatchDogs()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            CheckFilesAndDirs();
            try
            {
                var tempArr = File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList();
                if (tempArr.Any())
                {
                    string lastRecord = tempArr.Last();
                    if (lastRecord.Any() && lastRecord != "")
                    {
                        fileIdCounter = int.Parse(lastRecord.Substring(lastRecord.LastIndexOf(";") + 1)) + 1;
                    }
                }
                else
                    fileIdCounter = 1;
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
            }
            msgQueueDataEvent = new AutoResetEvent(false);
            msgQueueProcessingThreadExitEvent = new ManualResetEvent(false);
            msgQueueProcessingTask = new Task(() => MsgQueueProcessingTask(), TaskCreationOptions.LongRunning);
            msgQueueProcessingTask.Start();

            WOF = WOFFill();
            NumberOfCpuCores = GetNumberOfCpuCores();
            obj = new object();

            //Запуск основных наблюдателей
            List<string> listPaths = File.ReadAllLines(Properties.Settings.Default.PathToBackupsDirsFile).ToList();
            watchers = new List<FileSystemWatcher>();
            for (int i = 0; i < listPaths.Count; i++)
            {
                if (File.Exists(listPaths[i]))
                {
                    watchers.Add(new FileSystemWatcher()
                    {
                        Path = Path.GetDirectoryName(listPaths[i]),
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        Filter = Path.GetFileName(listPaths[i]),
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = false
                    });
                }
                else if (Directory.Exists(listPaths[i]))
                {
                    watchers.Add(new FileSystemWatcher()
                    {
                        Path = listPaths[i],
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                        Filter = "",
                        EnableRaisingEvents = true,
                        IncludeSubdirectories = true
                    });
                }
                watchers[i].Created += new FileSystemEventHandler(FileStatusChanged);
                watchers[i].Changed += new FileSystemEventHandler(FileStatusChanged);
                watchers[i].Deleted += new FileSystemEventHandler(FileStatusChanged);
                watchers[i].Renamed += new RenamedEventHandler(OnRenamed);
                watchers[i].Error += new ErrorEventHandler(AlphaWatcher_Error);
            }

            ////Запуск проверки на файлы которые не были доставлены в очередь при падении службы
            lastInQueueCheckTask = new Task(() => LastInQueueCheck());
            lastInQueueCheckTask.Start();

            ////Запуск проверки файлов которые были изменены/созданы/удалены когда служба была выключена
            primaryComparisonTask = new Task(() => PrimaryComparison());
            primaryComparisonTask.Start();

            List<string> exceptDirs = File.ReadAllLines(Properties.Settings.Default.PathToExceptDirs).ToList();
            foreach (var dirPath in File.ReadAllLines(Properties.Settings.Default.PathToBackupsDirsFile))
            {
                if (exceptDirs.Find(f => f == dirPath) != null)
                    continue;
                CreateDirectoryStructure(dirPath);
            }
            try { ConfigureScheduler(); } catch (Exception ex) { EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error); }

        }

        protected override void OnStop()
        {
            watchers.ForEach(f => { f.Dispose(); f = null; });

            if (primaryComparisonTask != null)
            {
                primaryComparisonTask.Wait(10000);
            }
            if (lastInQueueCheckTask != null)
            {
                lastInQueueCheckTask.Wait(10000);
            }

            if (msgQueueProcessingThreadExitEvent != null) msgQueueProcessingThreadExitEvent.Set();
            if (msgQueueDataEvent != null) msgQueueDataEvent.Set();
            if (msgQueueProcessingTask != null)
            {
                msgQueueProcessingTask.Wait(10000);
            }
            msgQueueDataEvent.Dispose();
            msgQueueProcessingThreadExitEvent.Dispose();
            EventLog.WriteEntry("WatchDogs", "Service Successfully stopped", EventLogEntryType.Information);
        }

        private static void CheckFilesAndDirs()
        {
            if (!Directory.Exists(Properties.Settings.Default.AllBackupsPath))
                Directory.CreateDirectory(Properties.Settings.Default.AllBackupsPath);

            if (!Directory.Exists(Properties.Settings.Default.WatchDogsProperties))
                Directory.CreateDirectory(Properties.Settings.Default.WatchDogsProperties);

            if (!File.Exists(Properties.Settings.Default.PathToBackupsDirsFile))
                File.Create(Properties.Settings.Default.PathToBackupsDirsFile);

            if (!File.Exists(Properties.Settings.Default.PathToExceptDirs))
                File.Create(Properties.Settings.Default.PathToExceptDirs);

            if (!File.Exists(Properties.Settings.Default.PathToLastFilesInQueue))
                File.Create(Properties.Settings.Default.PathToLastFilesInQueue);

            if (!File.Exists(Properties.Settings.Default.PathToListFiles))
                File.Create(Properties.Settings.Default.PathToListFiles);
        }

        private static void PrimaryComparison()
        {
            //backupsDirs Директории под резервное копирование
            List<string> backupsDirs = File.ReadAllLines(Properties.Settings.Default.PathToBackupsDirsFile).ToList();
            List<DNA> dirsFiles = new List<DNA>();
            List<string> exceptDirs = File.ReadAllLines(Properties.Settings.Default.PathToExceptDirs).ToList();

            //Получаем файлы из всех директорий в dirsFiles
            foreach (var path in backupsDirs.ToList())
            {
                if (exceptDirs.Find(f => f == path) != null)
                {
                    continue;
                }
                if (File.Exists(path))
                {
                    FileInfo fInfo = new FileInfo(path);
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    file.size = fInfo.Length;
                    file.extension = fInfo.Extension;
                    file.changed = fInfo.LastWriteTime;
                    file.created = fInfo.CreationTime;
                    lock (obj)
                    {
                        dirsFiles.Add(file);
                    }
                    backupsDirs.Remove(path);
                }
                else if (Directory.Exists(path))
                {
                    lock (obj)
                        dirsFiles.AddRange(DirSearch(path));
                }
                else
                {
                    EventLog.WriteEntry("WatchDogs", "PrimaryComparison Function.\nУказанный путь в файле PathToBackupsDirsFile на резервное копирование не существует.\n" + path, EventLogEntryType.Warning);
                }
            }

            dirsFiles = dirsFiles.Distinct().ToList();

            if (File.Exists(Properties.Settings.Default.PathToListFiles))
            {
                //Получаем массив состоящий из путей сохранённых файлов
                List<string> backupFilesPaths = File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList();
                //Исключаем из списка файлов все файлы которые были уже помещены в резерв.
                try
                {
                    foreach (var fileNameChangedId in backupFilesPaths)
                    {
                        int index1 = fileNameChangedId.IndexOf(';');
                        int index2 = fileNameChangedId.LastIndexOf(';');
                        string subs = fileNameChangedId.Substring(index1 + 1, index2 - index1 - 1);
                        string fn = Path.GetFileName(fileNameChangedId.Substring(0, index1));
                        DateTime date = DateTime.Parse(subs);
                        foreach (var file in dirsFiles.ToList())
                        {
                            if (file.changed.ToString() == date.ToString() && file.name == fn)
                                dirsFiles.Remove(file);
                            if (file.changed.ToString() != date.ToString() && Path.Combine(file.path, file.name) == fileNameChangedId.Substring(0, index1))
                                dirsFiles[dirsFiles.IndexOf(file)].eventStatus = EventStatus.Changed;
                        }
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                }
            }

            //Отправляем оставшиеся файлы в очередь для помещения в резерв
            if (dirsFiles.Any())
            {
                dirsFiles.ForEach(f => { f.id = fileIdCounter; Interlocked.Increment(ref fileIdCounter); });
                lock (up_msg_queue)
                {
                    up_msg_queue.AddRange(dirsFiles);
                    msgQueueDataEvent.Set();
                }
            }
        }

        private static List<DNA> WOFFill()
        {
            List<DNA> retValue = new List<DNA>();
            foreach (var fPath in File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList())
            {
                try
                {
                    string path = fPath.Substring(0, fPath.IndexOf(';'));
                    FileInfo fInfo = new FileInfo(path);
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    file.size = fInfo.Length;
                    file.extension = fInfo.Extension;
                    file.changed = fInfo.LastWriteTime;
                    file.created = fInfo.CreationTime;
                    string s = fPath.Substring(fPath.LastIndexOf(';') + 1);
                    file.id = long.Parse(s);
                    retValue.Add(file);
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                }
            }
            return retValue;
        }

        private static List<DNA> DirSearch(string sDir)
        {
            List<DNA> files = new List<DNA>();
            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    FileInfo fInfo = new FileInfo(f);
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    file.size = fInfo.Length;
                    file.extension = fInfo.Extension;
                    file.changed = fInfo.LastWriteTime;
                    file.created = fInfo.CreationTime;
                    files.Add(file);
                }
                foreach (string d in Directory.GetDirectories(sDir))
                {
                    files.AddRange(DirSearch(d));
                }
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
            }
            return files;
        }

        private static void CreateDirectoryStructure(string sDir)
        {
            List<DNA> files = new List<DNA>();
            DirectoryInfo dirInfo = new DirectoryInfo(sDir);
            try
            {
                if (File.Exists(sDir))
                {
                    string path = Path.Combine(Properties.Settings.Default.AllBackupsPath, Path.GetDirectoryName(sDir).Remove(0, 3));
                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);
                }
                else if (Directory.Exists(sDir))
                {
                    if (!Directory.Exists(Path.Combine(Properties.Settings.Default.AllBackupsPath, dirInfo.FullName.Remove(0, 3))))
                        Directory.CreateDirectory(Path.Combine(Properties.Settings.Default.AllBackupsPath, dirInfo.FullName.Remove(0, 3)));

                    foreach (string d in dirInfo.GetDirectories().ToList().Select(s => s.FullName).ToList())
                    {
                        string path = Path.Combine(Properties.Settings.Default.AllBackupsPath, d.Remove(0, 3));
                        if (!Directory.Exists(path))
                            Directory.CreateDirectory(path);

                        CreateDirectoryStructure(d);
                    }
                }

            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
            }
        }

        private static void MsgQueueProcessingTask()
        {
            while (msgQueueDataEvent.WaitOne(-1))
            {
                List<DNA> q;

                lock (up_msg_queue)
                {
                    if (!up_msg_queue.Any())
                    {
                        if (msgQueueProcessingThreadExitEvent.WaitOne(0))
                            break;
                    }
                    q = up_msg_queue;
                    up_msg_queue = new List<DNA>();
                }
                //Запись последних изменнёных файлов в ТХТ файл
                using (StreamWriter sw = new StreamWriter(Properties.Settings.Default.PathToLastFilesInQueue, true))
                {
                    foreach (var file in q.ToList())
                    {
                        sw.WriteLine(Path.Combine(file.path, file.name) + ';' + file.changed);
                    }
                }

                if (q.Where(w => w.eventStatus == EventStatus.Created).Any())
                {
                    foreach (var e in q.ToList())
                    {
                        try
                        {
                            if (WOF.Find(f => f == e) != null)
                            {
                                continue;
                            }
                            string pathToFolder = Path.Combine(Properties.Settings.Default.AllBackupsPath, e.path.Remove(0, 3));
                            string fullName = e.name;
                            if (e.name.Length > 65)
                            {
                                e.fullName = e.name;
                                e.name = e.name.Remove(64);
                            }

                            string path = Path.Combine(Path.Combine(pathToFolder, e.name), "Version 1");

                            if (Directory.Exists(Path.Combine(pathToFolder, e.name)))
                            {
                                if (e.created == e.changed)
                                    continue;
                                path = Path.Combine(Path.Combine(pathToFolder, e.name), "Version " + (Directory.GetFiles(Path.Combine(pathToFolder, e.name)).Length + 1).ToString());
                                if (Directory.Exists(path + ".zip"))
                                    continue;
                                else
                                    Directory.CreateDirectory(path);
                            }
                            else
                                Directory.CreateDirectory(path);


                            string fp = e.path + "\\" + fullName;

                            File.Copy(fp, Path.Combine(path, e.name));

                            ZipFile.CreateFromDirectory(path, path + ".zip");
                            if (fullName.Length > 65)
                                if (!File.Exists(Path.Combine(Path.Combine(pathToFolder, e.name), "Full_File_Name") + ".txt"))
                                    File.WriteAllText(Path.Combine(Path.Combine(pathToFolder, e.name), "Full_File_Name") + ".txt", fullName);

                            Directory.Delete(path, true);
                        }
                        catch (Exception ex)
                        {
                            q.Remove(e);
                            EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                        }
                    }
                }
                if (q.Where(w => w.eventStatus == EventStatus.Changed).Any())
                {
                    foreach (var e in q.ToList())
                    {
                        try
                        {
                            string pathToFolder = Path.Combine(Properties.Settings.Default.AllBackupsPath, e.path.Remove(0, 3));
                            string fullName = e.name;
                            if (e.name.Length > 65)
                            {
                                e.fullName = e.name;
                                e.name = e.name.Remove(64);
                            }

                            string path = Path.Combine(Path.Combine(pathToFolder, e.name), "Version 1");

                            if (Directory.Exists(Path.Combine(pathToFolder, e.name)))
                            {
                                if (e.created == e.changed)
                                    continue;
                                path = Path.Combine(Path.Combine(pathToFolder, e.name), "Version " + (Directory.GetFiles(Path.Combine(pathToFolder, e.name)).Length + 1).ToString());
                                if (Directory.Exists(path + ".zip"))
                                    continue;
                                else
                                    Directory.CreateDirectory(path);
                            }
                            else
                                Directory.CreateDirectory(path);

                            string fp = e.path + "\\" + fullName;

                            File.Copy(fp, Path.Combine(path, e.name));

                            ZipFile.CreateFromDirectory(path, path + ".zip");
                            if (fullName.Length > 65)
                                if (!File.Exists(Path.Combine(Path.Combine(pathToFolder, e.name), "Full_File_Name") + ".txt"))
                                    File.WriteAllText(Path.Combine(Path.Combine(pathToFolder, e.name), "Full_File_Name") + ".txt", fullName);

                            List<string> files = File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList();
                            foreach (var filePath in files.ToList())
                            {
                                string s = filePath.Substring(0, filePath.IndexOf(';'));
                                if (s == Path.Combine(e.path, e.name))
                                {
                                    files.Remove(filePath);
                                    File.WriteAllLines(Properties.Settings.Default.PathToListFiles, files);
                                }
                            }

                            Directory.Delete(path, true);
                        }
                        catch (Exception ex)
                        {
                            q.Remove(e);
                            EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                        }
                    }
                }

                //Добавление в список файлов которые были архивированы
                using (StreamWriter sw = new StreamWriter(Properties.Settings.Default.PathToListFiles, true))
                {
                    foreach (var file in q.ToList())
                    {
                        sw.WriteLine(Path.Combine(file.path, file.fullName != null ? file.fullName : file.name) + ";" + file.changed + ";" + file.id);
                    }
                }

                lock (WOF)
                {
                    WOF.AddRange(q);
                }
                #region Удаление записи о успешно зарезервированном файле
                List<string> ls = File.ReadAllLines(Properties.Settings.Default.PathToLastFilesInQueue).ToList();
                ls = ls.Distinct().ToList();
                foreach (var file in q.ToList())
                {
                    ls.Remove(Path.Combine(file.path, file.fullName != null ? file.fullName : file.name) + ';' + file.changed);
                }
                File.WriteAllLines(Properties.Settings.Default.PathToLastFilesInQueue, ls);
                #endregion
            }
        }

        private static void LastInQueueCheck()
        {
            List<string> ls = File.ReadAllLines(Properties.Settings.Default.PathToLastFilesInQueue).ToList();

            List<DNA> files = new List<DNA>();

            foreach (var pathFile in ls)
            {
                try
                {
                    FileInfo fInfo = new FileInfo(pathFile.Substring(0, pathFile.IndexOf(';')));
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    file.size = fInfo.Length;
                    file.extension = fInfo.Extension;
                    file.changed = DateTime.Parse(pathFile.Substring(pathFile.IndexOf(';') + 1));
                    file.created = fInfo.CreationTime;
                    files.Add(file);
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                }
            }

            lock (up_msg_queue)
            {
                if (files.Count > 0)
                {
                    up_msg_queue.AddRange(files);
                    msgQueueDataEvent.Set();
                }
            }
        }

        public static void FilesOutOfDates()
        {
            string path = Properties.Settings.Default.AllBackupsPath;
            List<DNA> backupFiles = DirSearch(path);
            DateTime dateTime = DateTime.Now;
            foreach (var file in backupFiles)
            {
                if (dateTime.Subtract(file.created).Days >= Properties.Settings.Default.NumberOfDaysOfFilesExistence)
                    File.Delete(Path.Combine(file.path, file.name));
            }
        }

        private static void FileStatusChanged(object source, FileSystemEventArgs e)
        {
            Regex regAppData = new Regex(@"\w\:\\(users)\\(.*)\\(appdata)");
            Regex regWindowsTemp = new Regex(@"\w\:\\(windows)\\(temp)");
            Regex regPrefetch = new Regex(@"\w\:\\(windows)\\(prefetch)");
            string lPath = e.FullPath.ToLower();

            if (!regAppData.Match(lPath).Success && !regWindowsTemp.Match(lPath).Success && !regPrefetch.Match(lPath).Success)
            {
                if (File.Exists(e.FullPath))
                {
                    FileInfo fInfo = new FileInfo(e.FullPath);
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    if (e.ChangeType != WatcherChangeTypes.Deleted)
                    {
                        file.size = fInfo.Length;
                    }
                    file.extension = fInfo.Extension;
                    file.changed = fInfo.LastWriteTime;
                    file.created = fInfo.CreationTime;
                    switch (e.ChangeType)
                    {
                        case WatcherChangeTypes.Created:
                            try
                            {
                                lock (up_msg_queue)
                                {
                                    file.id = fileIdCounter;
                                    Interlocked.Increment(ref fileIdCounter);
                                    file.eventStatus = EventStatus.Created;
                                    up_msg_queue.Add(file);
                                    msgQueueDataEvent.Set();
                                }
                            }
                            catch (Exception ex)
                            {
                                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                            }
                            break;

                        case WatcherChangeTypes.Changed:
                            try
                            {
                                lock (up_msg_queue)
                                {
                                    file.id = WOF.Find(f => f.name == file.name && f.created == file.created).id;
                                    file.eventStatus = EventStatus.Changed;
                                    up_msg_queue.Add(file);
                                    msgQueueDataEvent.Set();
                                }
                            }
                            catch (Exception ex)
                            {
                                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                            }
                            break;

                        default:
                            break;
                    }
                }
                else if (Directory.Exists(e.FullPath))
                {
                    Directory.CreateDirectory(Path.Combine(Properties.Settings.Default.AllBackupsPath, e.FullPath.Remove(0, 3)));
                }
                else if (e.ChangeType == WatcherChangeTypes.Deleted)
                {
                    string backupPath = Path.Combine(Properties.Settings.Default.AllBackupsPath, e.FullPath.Remove(0, 3));
                    if (File.Exists(backupPath))
                    {
                        List<string> files = Directory.GetDirectories(Path.GetDirectoryName(backupPath)).ToList().Where(w => w.Contains("DELETED_")).ToList();
                        int cnt = 1;

                        if (e.Name.Length > 55)
                        {
                            for (int i = 1; i < files.Count; i++)
                            {
                                if (files.Find(f => f == "DELETED_" + i + "_" + e.Name.Remove(55)) != null)
                                {
                                    cnt++;
                                }
                            }
                            File.Move(backupPath, Path.Combine(Path.GetDirectoryName(backupPath), "DELETED_" + cnt + "_" + e.Name.Remove(55)));
                        }
                        else
                        {
                            for (int i = 1; i < files.Count; i++)
                            {
                                if (files.Find(f => f == "DELETED_" + i + "_" + e.Name) != null)
                                {
                                    cnt++;
                                }
                            }
                            File.Move(backupPath, Path.Combine(Path.GetDirectoryName(backupPath), "DELETED_" + cnt + "_" + e.Name));
                        }
                    }
                    else if (Directory.Exists(backupPath))
                    {
                        List<string> files = Directory.GetDirectories(Path.GetDirectoryName(backupPath)).ToList().Where(w => w.Contains("DELETED_")).ToList();
                        int cnt = 1;

                        if (e.Name.Length > 55)
                        {
                            for (int i = 0; i < files.Count; i++)
                            {
                                if (files.Find(f => Path.GetDirectoryName(f) == "DELETED_" + i + 1 + "_" + e.Name.Remove(55)) != null)
                                {
                                    cnt++;
                                }
                            }
                            Directory.Move(backupPath, Path.Combine(Path.GetDirectoryName(backupPath), "DELETED_" + cnt + "_" + e.Name.Remove(55)));
                        }
                        else
                        {
                            for (int i = 0; i < files.Count; i++)
                            {
                                if (files.Find(f => Path.GetFileName(f) == "DELETED_" + (i + 1).ToString() + "_" + e.Name) != null)
                                {
                                    cnt++;
                                }
                            }
                            Directory.Move(backupPath, Path.Combine(Path.GetDirectoryName(backupPath), "DELETED_" + cnt + "_" + e.Name));
                        }
                    }
                    
                    List<string> backUpFiles = File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList();
                    foreach (var filePath in backUpFiles.ToList())
                    {
                        string s = filePath.Substring(0, filePath.IndexOf(';'));
                        if (s == e.FullPath)
                        {
                            backUpFiles.Remove(filePath);
                            File.WriteAllLines(Properties.Settings.Default.PathToListFiles, backUpFiles);
                        }
                    }
                }
            }
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            if (File.Exists(e.FullPath))
            {
                try
                {
                    FileInfo fInfo = new FileInfo(e.FullPath);
                    DNA file = new DNA();
                    file.name = fInfo.Name;
                    file.path = fInfo.DirectoryName;
                    file.size = fInfo.Length;
                    file.extension = fInfo.Extension;
                    file.changed = fInfo.LastWriteTime;
                    file.created = fInfo.CreationTime;
                    file.id = WOF.Find(f => f.name == e.OldName).id;
                    file.eventStatus = EventStatus.Changed;

                    lock (WOF)
                    {
                        WOF.Remove(WOF.Find(f => f.id == file.id));
                        WOF.Add(file);
                    }
                    List<string> files = File.ReadAllLines(Properties.Settings.Default.PathToListFiles).ToList();
                    foreach (var filePath in files.ToList())
                    {
                        string s = filePath.Substring(0, filePath.IndexOf(';'));
                        if (s == e.OldFullPath)
                        {
                            files.Remove(filePath);
                            File.WriteAllLines(Properties.Settings.Default.PathToListFiles, files);
                        }
                    }
                    lock (up_msg_queue)
                    {
                        up_msg_queue.Add(file);
                        msgQueueDataEvent.Set();
                    }
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                }
            }
            else if (Directory.Exists(e.FullPath))
            {
                try
                {
                    Directory.Move(Path.Combine(Properties.Settings.Default.AllBackupsPath, e.OldFullPath.Remove(0, 3)),
                                   Path.Combine(Properties.Settings.Default.AllBackupsPath, e.FullPath.Remove(0, 3)));
                }
                catch (Exception ex)
                {
                    EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
                }
            }
        }

        private static void AlphaWatcher_Error(object sender, ErrorEventArgs e)
        {

        }

        private static int GetNumberOfCpuCores()
        {
            int cores = 1;
            string fileName = Path.Combine(Environment.SystemDirectory, "wbem", "wmic.exe");
            string arguments = @"cpu get NumberOfCores";

            Process process = new Process
            {
                StartInfo = {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            process.Start();

            StreamReader output = process.StandardOutput;
            var m = rxNumberOfCores.Match(output.ReadToEnd());
            process.WaitForExit();
            int exitCode = process.ExitCode;
            process.Close();

            if (m.Groups["cores"].Success)
            {
                cores = int.Parse(m.Groups["cores"].Value);
            }

            return cores;
        }

        public static async void ConfigureScheduler()
        {
            try
            {
                sf = new StdSchedulerFactory();
                scheduler = await StdSchedulerFactory.GetDefaultScheduler();

                IJobDetail job = JobBuilder.Create<EverydayNtfyJob>()
                    .WithIdentity("eveningJob", "group1")
                    .Build();

                ITrigger trigger = TriggerBuilder.Create()
                    .WithIdentity("eveningTrigger", "group1")
                    .StartNow()
                    .WithCronSchedule("0 0 19 * * ?")
                    .Build();
                await scheduler.ScheduleJob(job, trigger);

                if (!scheduler.IsStarted) await scheduler.Start();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
            }
        }

    }

    public class EverydayNtfyJob : IJob
    {
        public void EverydayNtfy()
        {
            try
            {
                WatchDogs.FilesOutOfDates();
            }
            catch (Exception ex)
            {
                EventLog.WriteEntry("WatchDogs", ex.Message + "\n" + ex.StackTrace, EventLogEntryType.Error);
            }
        }
        Task IJob.Execute(IJobExecutionContext context)
        {
            return Task.Factory.StartNew(() =>
            {
                EverydayNtfy();
            });
        }
    }

    public enum EventStatus
    {
        Created,
        Changed,
        Renamed,
        None
    }
    class DNA
    {
        public string name { get; set; }
        public string fullName { get; set; }
        public string path { get; set; }
        public long size { get; set; }
        public string extension { get; set; }
        public DateTime changed { get; set; }
        public DateTime created { get; set; }
        public long id { get; set; }
        public EventStatus eventStatus;
    }

}
