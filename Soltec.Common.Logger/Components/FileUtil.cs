using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Web;
using System.IO;
using System.Reflection;


namespace Soltec.Common.Logger
{
    public class FileUtil
    {
        public static object syncObj = new object();
        public static string localLogPath = Settings.Default.LocalLogPath;
        public static string nameSettPathToSave = Settings.Default.NameSettPathToSave;
        private static long logRotateSize = Settings.Default.LogRotateSize;
        private static bool esLinux = Settings.Default.EsLinux;

        public static void WriteInLogFile(string linetoWrite)
        {
            lock (syncObj)
            {
                CheckLogSize();

                string fechaLog = "(" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + ")"; //fecha en que se escribio la incidencia

                string logPath = localLogPath;

                string logFilePath = string.Empty;
                if (!esLinux)
                {
                    if (!localLogPath.Contains(":") && Assembly.GetExecutingAssembly() != null)
                        logPath = Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), localLogPath); //ruta obtenida desde el app config
                    logFilePath = Path.Combine(logPath, nameSettPathToSave);
                }
                else
                {
                    logFilePath = "/logs/" + nameSettPathToSave;
                }

                if (!System.IO.Directory.Exists(logPath))
                    System.IO.Directory.CreateDirectory(logPath);

                CreateOrWriteFile(logFilePath, fechaLog + "-" + linetoWrite);
            }
        }
        public static void CheckLogSize()
        {
            string logPath = localLogPath;
            string logFilePath = string.Empty;
            if (!esLinux)
            {
                if (!localLogPath.Contains(":") && Assembly.GetExecutingAssembly() != null)
                    logPath = Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), localLogPath); //ruta obtenida desde el app config

                logFilePath = Path.Combine(logPath, nameSettPathToSave);
            } else
            {
                logFilePath = "/logs/" + nameSettPathToSave;
            }
            //string logFilePath = Path.Combine(logPath, nameSettPathToSave);

            long logSize;
            long configSize;

            System.IO.FileInfo logFile = null;
            try
            {
                logFile = new System.IO.FileInfo(logFilePath);
                logSize = logFile.Length;
                configSize = logRotateSize;
                if (logSize >= configSize)
                {
                    //renombramos el archivo
                    String newLogName = logFilePath.Replace(".txt", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss").Replace(' ', '_').Replace(':', '-'));
                    System.IO.File.Move(logFilePath, newLogName + ".txt");
                }

            }
            catch (System.IO.FileNotFoundException)
            {
                if (!System.IO.Directory.Exists(logPath))
                    System.IO.Directory.CreateDirectory(logPath);

                CreateOrWriteFile(logFilePath, "Creación del Log->" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            }
            catch (System.IO.IOException e)
            {
                throw new Exception("Error al rotar el log:" + e.Message);
            }
        }

        public static string ReadFile(string file)
        {


            var response = string.Empty;
            if (System.IO.File.Exists(file))
            {
                string[] lines = System.IO.File.ReadAllLines(file);

                if (lines.Count() > 0)
                    response = lines[0];
            }

            //Logger.Log("File reader size {0}.", response.Length);
            return response;

        }

       
        private static void CreateOrWriteFile(string FileName, string text)
        {
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(FileName, true))
            {
                file.WriteLine(text);
            }
        }

        public static bool SaveFile(string idFolderName, string fileName, byte[] fileData, bool overWrite, ref string error)
        {
            string logPath = localLogPath;
            string logFilePath = string.Empty;
            if (!esLinux)
            {
                if (!localLogPath.Contains(":"))
                    logPath = Path.Combine(System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), localLogPath); //ruta obtenida desde el app config

                logFilePath = Path.Combine(logPath, nameSettPathToSave);
            } else
            {
                logFilePath = "/logs/" + nameSettPathToSave;
            }


            string pathToSave = nameSettPathToSave.Trim();



            bool result = false;
            string filePath;
            //Validate Root directory from webconfig and add slash if is missing. 
            pathToSave = ValidateDirectory(pathToSave);
            //Validate directory with idFolderName
            pathToSave = pathToSave + idFolderName;
            pathToSave = ValidateDirectory(pathToSave);
            //concat. Path and Filename
            filePath = pathToSave + fileName.Trim();


            try
            {
                FileStream fs;
                BinaryWriter bw;
                if (overWrite)
                {
                    fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                }
                else
                {
                    fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write);
                }
                bw = new BinaryWriter(fs);
                bw.Write(fileData);
                bw.Flush();
                bw.Close();
                fs.Close();
                bw = null;
                fs.Dispose();

                result = true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                result = false;
            }

            return result;
        }

        public static bool SaveFileGral(string directory, string fileName, byte[] fileData, bool overWrite, ref string error)
        {
            //get configuration from web.config


            bool result = false;
            string filePath;
            ValidateDirectory(directory);
            //concat. Path and Filename


            filePath = Path.Combine(directory, fileName);


            try
            {
                FileStream fs;
                BinaryWriter bw;
                if (overWrite)
                {
                    fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                }
                else
                {
                    fs = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write);
                }
                bw = new BinaryWriter(fs);
                bw.Write(fileData);
                bw.Flush();
                bw.Close();
                fs.Close();
                bw = null;
                fs.Dispose();

                result = true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                result = false;
            }

            return result;
        }

        public static string ValidateDirectory(string pathToSave)
        {
            char lastchar = pathToSave[pathToSave.Length - 1];
            if (!lastchar.Equals("\\"))
            {
                pathToSave = pathToSave + "\\";
            }
            //Check if the directory exists
            if (!System.IO.Directory.Exists(pathToSave))
            {
                System.IO.Directory.CreateDirectory(pathToSave);
            }
            return pathToSave;
        }

    }
}