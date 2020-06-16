using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;
using LayerGen35.DatabasePlugins;

namespace LayerGen35
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            if (args.Length > 0)
            {
                return RunFromCommandLine(args);
            }

            bool createdNew;
            using (new Mutex(true, "LayerGen3", out createdNew))
            {
                if (createdNew)
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    Application.Run(new MainForm());
                }
                else
                {
                    Process current = Process.GetCurrentProcess();
                    foreach (Process process in Process.GetProcessesByName(current.ProcessName))
                    {
                        if (process.Id != current.Id)
                        {
                            NativeMethods.ShowWindow(process.MainWindowHandle, 9);
                            NativeMethods.SetForegroundWindow(process.MainWindowHandle);
                            break;
                        }
                    }
                }
            }

            return 0;
        }

        private static int RunFromCommandLine(string[] args)
        {
            var handle = NativeMethods.GetConsoleWindow();

            if (handle == IntPtr.Zero)
            {
                NativeMethods.AllocConsole();
            }
            else
            {
                NativeMethods.ShowWindow(handle, 5);
            }

            string objects;
            Profile template = ParseCommandLineArguments(args, out objects);
            IDatabasePlugin databasePlugin = null;

            ShowTitle();

            if (template.DatabaseType == DatabaseTypes.Unknown)
            {
                Console.WriteLine(@"You must select a database type with the ""sql-plugin="" switch!");
                Console.WriteLine(@"    sql-plugin=SqlServer");
                Console.WriteLine(@"    sql-plugin=MySql");
                Console.WriteLine(@"    sql-plugin=Sqlite");
                Console.WriteLine(@"    sql-plugin=MsAccess");

                return 101;
            }

            if (template.DatabaseType == DatabaseTypes.SqlServer)
            {
                databasePlugin = new SqlServer
                {
                    DatabaseName = template.SqlServerDatabaseName,
                    DatabasePort = template.SqlServerPort,
                    DatabaseServer = template.SqlServerServerName,
                    OutputDirectory = template.OutputFolder,
                    Password = template.SqlServerPassword,
                    TrustedConnection = template.SqlServerTrustedConnection,
                    UserName = template.SqlServerUserName,
                    Objects = objects,
                    DefaultSchema = template.SqlServerDefaultSchema,
                    IncludeComments = template.IncludeComments,
                    ProgressBar = null,
                    DataNamespaceName = template.DataNameSpace,
                    BusinessNamespaceName = template.BusinessNameSpace,
                    AutoRightTrimStrings = template.AutomaticallyRightTrimData,
                    HasCustomConnectionString = template.SqlServerUseCustomConnectionString,
                    CustomConnectionString = template.SqlServerCustomConnectionString,
                    HasDynamicDataRetrieval = template.EnableDynamicDataRetrieval,
                    AllowSerialization = template.AllowSerialization,
                    PluralizationTemplate = template.PluralizationTemplate,
                    Language = template.Language
                };
            }

            if (template.DatabaseType == DatabaseTypes.MySql)
            {
                databasePlugin = new DatabasePlugins.MySql
                {
                    DatabaseName = template.MySqlDatabaseName,
                    DatabasePort = template.MySqlPort,
                    DatabaseServer = template.MySqlServerName,
                    OutputDirectory = template.OutputFolder,
                    Password = template.MySqlPassword,
                    UserName = template.MySqlUserName,
                    Objects = objects,
                    IncludeComments = template.IncludeComments,
                    ProgressBar = null,
                    DataNamespaceName = template.DataNameSpace,
                    BusinessNamespaceName = template.BusinessNameSpace,
                    AutoRightTrimStrings = template.AutomaticallyRightTrimData,
                    HasCustomConnectionString = template.SqlServerUseCustomConnectionString,
                    CustomConnectionString = template.SqlServerCustomConnectionString,
                    HasDynamicDataRetrieval = template.EnableDynamicDataRetrieval,
                    AllowSerialization = template.AllowSerialization,
                    PluralizationTemplate = template.PluralizationTemplate,
                    Language = template.Language
                };
            }

            if (template.DatabaseType == DatabaseTypes.Sqlite)
            {
                databasePlugin = new Sqlite
                {
                    DatabaseName = template.SqliteFileName,
                    OutputDirectory = template.OutputFolder,
                    Password = template.SqlitePassword,
                    Objects = objects,
                    IncludeComments = template.IncludeComments,
                    ProgressBar = null,
                    DataNamespaceName = template.DataNameSpace,
                    BusinessNamespaceName = template.BusinessNameSpace,
                    AutoRightTrimStrings = template.AutomaticallyRightTrimData,
                    HasCustomConnectionString = template.SqlServerUseCustomConnectionString,
                    CustomConnectionString = template.SqlServerCustomConnectionString,
                    HasDynamicDataRetrieval = template.EnableDynamicDataRetrieval,
                    AllowSerialization = template.AllowSerialization,
                    PluralizationTemplate = template.PluralizationTemplate,
                    Language = template.Language
                };
            }

            if (template.DatabaseType == DatabaseTypes.MsAccess)
            {
                databasePlugin = new MsAccess
                {
                    DatabaseName = template.AccessFileName,
                    OutputDirectory = template.OutputFolder,
                    Password = template.AccessPassword,
                    Objects = objects,
                    IncludeComments = template.IncludeComments,
                    ProgressBar = null,
                    DataNamespaceName = template.DataNameSpace,
                    BusinessNamespaceName = template.BusinessNameSpace,
                    AutoRightTrimStrings = template.AutomaticallyRightTrimData,
                    HasCustomConnectionString = template.SqlServerUseCustomConnectionString,
                    CustomConnectionString = template.SqlServerCustomConnectionString,
                    HasDynamicDataRetrieval = template.EnableDynamicDataRetrieval,
                    AllowSerialization = template.AllowSerialization,
                    PluralizationTemplate = template.PluralizationTemplate,
                    Language = template.Language
                };
            }

            try
            {
                if (databasePlugin != null) databasePlugin.CreateLayers();
            }
            catch
            {
                return 1;
            }

            return 0;
        }

        private static void ShowTitle()
        {
            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();

            Console.WriteLine(@"LayerGen {0}.{1}.{2}.{3}", assembly.GetName().Version.Major, assembly.GetName().Version.Minor, 
                assembly.GetName().Version.Build, assembly.GetName().Version.Revision);
            Console.WriteLine(@"Written by Alan Bryan (a.k.a. icemanind)");
            Console.WriteLine(@"--------------------------------------------------------------------");
        }

        private static Profile ParseCommandLineArguments(string[] args, out string objects)
        {
            objects = "";
            var template = new Profile
            {
                DatabaseType = DatabaseTypes.Unknown,
                SqlServerUseCustomConnectionString = false,
                MySqlUseCustomConnectionString = false,
                SqliteUseCustomConnectionString = false,
                AccessUseCustomConnectionString = false
            };


            foreach (string arg in args)
            {
                string lowerTrimmedArg = arg.Trim().ToLower();
                if (lowerTrimmedArg.StartsWith("sql-plugin="))
                {
                    template.DatabaseType = (DatabaseTypes)Enum.Parse(typeof(DatabaseTypes), arg.Remove(0, ("sql-plugin=").Length));
                }
                if (lowerTrimmedArg.StartsWith("output="))
                {
                    template.OutputFolder = arg.Remove(0, ("output=").Length);
                }

                if (lowerTrimmedArg.StartsWith("objects="))
                {
                    objects = arg.Remove(0, ("objects=").Length);
                }

                if (lowerTrimmedArg.StartsWith("include-comments="))
                {
                    template.IncludeComments = bool.Parse(arg.Remove(0, ("include-comments=").Length));
                }

                if (lowerTrimmedArg.StartsWith("data-namespace-name="))
                {
                    template.DataNameSpace = arg.Remove(0, ("data-namespace-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("business-namespace-name="))
                {
                    template.BusinessNameSpace = arg.Remove(0, ("business-namespace-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("pluralization-template="))
                {
                    template.PluralizationTemplate = arg.Remove(0, ("pluralization-template=").Length);
                }

                if (lowerTrimmedArg.StartsWith("enable-dynamic-data-retrieval="))
                {
                    template.EnableDynamicDataRetrieval = bool.Parse(arg.Remove(0, ("enable-dynamic-data-retrieval=").Length));
                }

                if (lowerTrimmedArg.StartsWith("automatically-right-trim-data="))
                {
                    template.AutomaticallyRightTrimData = bool.Parse(arg.Remove(0, ("automatically-right-trim-data=").Length));
                }

                if (lowerTrimmedArg.StartsWith("allow-serialization="))
                {
                    template.AllowSerialization = bool.Parse(arg.Remove(0, ("allow-serialization=").Length));
                }

                if (lowerTrimmedArg.StartsWith("sql-server-server-name="))
                {
                    template.SqlServerServerName = arg.Remove(0, ("sql-server-server-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sql-server-port="))
                {
                    template.SqlServerPort = int.Parse(arg.Remove(0, ("sql-server-port=").Length));
                }

                if (lowerTrimmedArg.StartsWith("sql-server-database-name="))
                {
                    template.SqlServerDatabaseName = arg.Remove(0, ("sql-server-database-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sql-server-default-schema="))
                {
                    template.SqlServerDefaultSchema = arg.Remove(0, ("sql-server-default-schema=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sql-server-trusted-connection="))
                {
                    template.SqlServerTrustedConnection = bool.Parse(arg.Remove(0, ("sql-server-trusted-connection=").Length));
                }

                if (lowerTrimmedArg.StartsWith("sql-server-username="))
                {
                    template.SqlServerUserName = arg.Remove(0, ("sql-server-username=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sql-server-password="))
                {
                    template.SqlServerPassword = arg.Remove(0, ("sql-server-password=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sql-server-custom-connection-string="))
                {
                    template.SqlServerUseCustomConnectionString = true;
                    template.SqlServerCustomConnectionString = arg.Remove(0, ("sql-server-custom-connection-string=").Length);
                }

                if (lowerTrimmedArg.StartsWith("mysql-server-database-name="))
                {
                    template.MySqlDatabaseName = arg.Remove(0, ("mysql-server-database-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("mysql-server-username="))
                {
                    template.MySqlUserName = arg.Remove(0, ("mysql-server-username=").Length);
                }

                if (lowerTrimmedArg.StartsWith("mysql-server-password="))
                {
                    template.MySqlPassword = arg.Remove(0, ("mysql-server-password=").Length);
                }

                if (lowerTrimmedArg.StartsWith("mysql-server-port="))
                {
                    template.MySqlPort = int.Parse(arg.Remove(0, ("mysql-server-port=").Length));
                }

                if (lowerTrimmedArg.StartsWith("mysql-server-server-name="))
                {
                    template.MySqlServerName = arg.Remove(0, ("mysql-server-server-name=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sqlite-password="))
                {
                    template.SqlitePassword = arg.Remove(0, ("sqlite-password=").Length);
                }

                if (lowerTrimmedArg.StartsWith("sqlite-filename="))
                {
                    template.SqliteFileName = arg.Remove(0, ("sqlite-filename=").Length);
                }

                if (lowerTrimmedArg.StartsWith("msaccess-filename="))
                {
                    template.AccessFileName = arg.Remove(0, ("msaccess-filename=").Length);
                }

                if (lowerTrimmedArg.StartsWith("msaccess-password="))
                {
                    template.AccessPassword = arg.Remove(0, ("msaccess-password=").Length);
                }

                if (lowerTrimmedArg.StartsWith("language="))
                {
                    if (lowerTrimmedArg.Remove(0, ("language=").Length) == "c#")
                    {
                        template.Language = Languages.CSharp;
                    }
                    if (lowerTrimmedArg.Remove(0, ("language=").Length) == "vbnet")
                    {
                        template.Language = Languages.VbNet;
                    }
                }
            }

            bool isCs = template.Language == Languages.CSharp;

            if (string.IsNullOrWhiteSpace(template.DataNameSpace))
            {
                if (template.DatabaseType == DatabaseTypes.SqlServer)
                {
                    template.DataNameSpace = @"DataLayer." + template.SqlServerDatabaseName.FirstCharToUpper();
                    template.DataNameSpace = isCs ? template.DataNameSpace.ConvertToValidCSharpNamespace() : template.DataNameSpace.ConvertToValidVbNamespace();
                }
            }

            if (string.IsNullOrWhiteSpace(template.BusinessNameSpace))
            {
                if (template.DatabaseType == DatabaseTypes.SqlServer)
                {
                    template.BusinessNameSpace = @"BusinessLayer." + template.SqlServerDatabaseName.FirstCharToUpper();
                    template.BusinessNameSpace = isCs ? template.BusinessNameSpace.ConvertToValidCSharpNamespace() : template.BusinessNameSpace.ConvertToValidVbNamespace();
                }
            }

            template.SqlServerTrustedConnection = string.IsNullOrWhiteSpace(template.SqlServerUserName);
            return template;
        }
    }
}
