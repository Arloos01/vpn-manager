using System;
using System.IO;
using System.Reflection;

namespace VPNTool
{
    /// <summary>
    /// Gestión centralizada de rutas portables para VPNTool (Objetivo 1).
    /// Almacena todos los datos internos (data, config, cache, logs, temp) dentro de una única
    /// carpeta oculta en Documentos (.vpnmanager), garantizando CERO rastros en el Escritorio.
    /// </summary>
    public static class PortablePaths
    {
        private static string _baseDir;
        private static string _appContainerDir;
        private static string _dataDir;
        private static string _configDir;
        private static string _cacheDir;
        private static string _logsDir;
        private static string _tempDir;

        public static string BaseDir
        {
            get { if (_baseDir == null) Initialize(); return _baseDir; }
        }

        public static string AppContainerDir
        {
            get { if (_appContainerDir == null) Initialize(); return _appContainerDir; }
        }

        public static string DataDir
        {
            get { if (_dataDir == null) Initialize(); return _dataDir; }
        }

        public static string ConfigDir
        {
            get { if (_configDir == null) Initialize(); return _configDir; }
        }

        public static string CacheDir
        {
            get { if (_cacheDir == null) Initialize(); return _cacheDir; }
        }

        public static string LogsDir
        {
            get { if (_logsDir == null) Initialize(); return _logsDir; }
        }

        public static string TempDir
        {
            get { if (_tempDir == null) Initialize(); return _tempDir; }
        }

        public static string RealDocuments { get; private set; }
        public static string RealUserProfile { get; private set; }
        public static string RealLocalAppData { get; private set; }

        public static void Initialize()
        {
            // 1. Obtener rutas reales del sistema antes de cualquier redirección
            try
            {
                RealUserProfile = Environment.GetEnvironmentVariable("USERPROFILE");
                RealLocalAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
                RealDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                if (string.IsNullOrEmpty(RealDocuments) && !string.IsNullOrEmpty(RealUserProfile))
                {
                    RealDocuments = Path.Combine(RealUserProfile, "Documents");
                }
            }
            catch { }

            // 2. Ruta física real del ejecutable
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    _baseDir = Path.GetDirectoryName(loc);
                }
            }
            catch { }

            if (string.IsNullOrEmpty(_baseDir))
            {
                _baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            }

            // 3. Contenedor único y oculto en Documentos (.vpnmanager)
            string container = null;
            try
            {
                if (!string.IsNullOrEmpty(RealDocuments))
                {
                    container = Path.Combine(RealDocuments, ".vpnmanager");
                }
            }
            catch { }

            if (string.IsNullOrEmpty(container))
            {
                container = Path.Combine(_baseDir, ".vpnmanager");
            }

            _appContainerDir = container;
            EnsureHiddenDirectory(_appContainerDir);

            _dataDir = Path.Combine(_appContainerDir, "data");
            _configDir = Path.Combine(_appContainerDir, "config");
            _cacheDir = Path.Combine(_appContainerDir, "cache");
            _logsDir = Path.Combine(_appContainerDir, "logs");
            _tempDir = Path.Combine(_appContainerDir, "temp");

            EnsureDirectory(_dataDir);
            EnsureDirectory(_configDir);
            EnsureDirectory(_cacheDir);
            EnsureDirectory(_logsDir);
            EnsureDirectory(_tempDir);

            // 4. Limpiar carpetas sueltas creadas previamente junto al ejecutable (Escritorio, etc.)
            CleanLooseFoldersNearExe(_baseDir);

            // 5. Redirección de variables temporales del proceso hacia la subcarpeta temp oculta
            try
            {
                Environment.SetEnvironmentVariable("TEMP", _tempDir, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("TMP", _tempDir, EnvironmentVariableTarget.Process);
            }
            catch { }

            // 6. Limpieza de archivos temporales huérfanos
            CleanTempDirectory();
        }

        private static void EnsureDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }
            catch { }
        }

        private static void EnsureHiddenDirectory(string path)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                DirectoryInfo di = new DirectoryInfo(path);
                if ((di.Attributes & FileAttributes.Hidden) == 0)
                {
                    di.Attributes |= FileAttributes.Hidden;
                }
            }
            catch { }
        }

        /// <summary>
        /// Si VPN-Manager.exe se ejecutó en el Escritorio o en una carpeta normal,
        /// elimina las carpetas sueltas (data, config, cache, logs, temp) que se hayan creado visibles.
        /// </summary>
        private static void CleanLooseFoldersNearExe(string baseDir)
        {
            if (string.IsNullOrEmpty(baseDir)) return;

            try
            {
                string normBase = Path.GetFullPath(baseDir).TrimEnd('\\', '/');
                // No tocar si es la carpeta de código fuente que tiene "src"
                if (Directory.Exists(Path.Combine(normBase, "src"))) return;

                string[] looseFolders = new string[] { "data", "config", "cache", "logs", "temp" };
                foreach (string f in looseFolders)
                {
                    string target = Path.Combine(normBase, f);
                    if (Directory.Exists(target))
                    {
                        try { Directory.Delete(target, true); } catch { }
                    }
                }
            }
            catch { }
        }

        public static void CleanTempDirectory()
        {
            try
            {
                if (Directory.Exists(_tempDir))
                {
                    foreach (string file in Directory.GetFiles(_tempDir))
                    {
                        try { File.Delete(file); } catch { }
                    }
                    foreach (string dir in Directory.GetDirectories(_tempDir))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch { }
        }
    }
}
