namespace LayerGen35
{
    public class Profile
    {
        public string OutputFolder { get; set; }
        public DatabasePlugins.Languages Language { get; set; }
        public bool IncludeComments { get; set; }
        public bool CustomNameSpaces { get; set; }
        public string DataNameSpace { get; set; }
        public string BusinessNameSpace { get; set; }
        public string PluralizationTemplate { get; set; }
        public bool EnableDynamicDataRetrieval { get; set; }
        public bool AutomaticallyRightTrimData { get; set; }
        public bool AllowSerialization { get; set; }
        public bool CreateAsyncMethods { get; set; }
        public bool CreateWebApiClasses { get; set; }
        public bool AspNetCore2 { get; set; }

        public DatabasePlugins.DatabaseTypes DatabaseType { get; set; }

        public string SqlServerServerName { get; set; }
        public int SqlServerPort { get; set; }
        public string SqlServerDatabaseName { get; set; }
        public string SqlServerDefaultSchema { get; set; }
        public bool SqlServerTrustedConnection { get; set; }
        public string SqlServerUserName { get; set; }
        public string SqlServerPassword { get; set; }
        public bool SqlServerUseCustomConnectionString { get; set; }
        public string SqlServerCustomConnectionString { get; set; }

        public string OracleServerName { get; set; }
        public int OraclePort { get; set; }
        public string OracleDatabaseName { get; set; }
        public string OracleDefaultSchema { get; set; }
        public string OracleUserName { get; set; }
        public string OraclePassword { get; set; }
        public bool OracleUseCustomConnectionString { get; set; }
        public string OracleCustomConnectionString { get; set; }

        public string SqliteFileName { get; set; }
        public string SqlitePassword { get; set; }
        public bool SqliteUseCustomConnectionString { get; set; }
        public string SqliteCustomConnectionString { get; set; }

        public string AccessFileName { get; set; }
        public string AccessPassword { get; set; }
        public bool AccessUseCustomConnectionString { get; set; }
        public string AccessCustomConnectionString { get; set; }

        public string MySqlServerName { get; set; }
        public int MySqlPort { get; set; }
        public string MySqlDatabaseName { get; set; }
        public string MySqlUserName { get; set; }
        public string MySqlPassword { get; set; }
        public bool MySqlUseCustomConnectionString { get; set; }
        public string MySqlCustomConnectionString { get; set; }

        public string ProfileName { get; set; }

        public Profile()
        {
            OutputFolder = "";
            Language = DatabasePlugins.Languages.CSharp;
            IncludeComments = true;
            CustomNameSpaces = false;
            DataNameSpace = "";
            BusinessNameSpace = "";
            PluralizationTemplate = "{ObjectName}s";
            EnableDynamicDataRetrieval = true;
            AutomaticallyRightTrimData = false;
            AllowSerialization = false;
            CreateAsyncMethods = true;
            AspNetCore2 = false;
            CreateWebApiClasses = true;
            DatabaseType = DatabasePlugins.DatabaseTypes.SqlServer;

            AccessFileName = "";
            AccessPassword = "";
            AccessUseCustomConnectionString = false;
            AccessCustomConnectionString = "";

            SqlServerServerName = "";
            SqlServerPort = 1433;
            SqlServerDatabaseName = "";
            SqlServerDefaultSchema = "dbo";
            SqlServerTrustedConnection = true;
            SqlServerUserName = "";
            SqlServerPassword = "";
            SqlServerUseCustomConnectionString = false;
            SqlServerCustomConnectionString = "";

            SqliteFileName = "";
            SqlitePassword = "";
            SqliteUseCustomConnectionString = false;
            SqliteCustomConnectionString = "";

            MySqlServerName = "";
            MySqlPort = 3306;
            MySqlDatabaseName = "";
            MySqlUserName = "";
            MySqlPassword = "";
            MySqlUseCustomConnectionString = false;
            MySqlCustomConnectionString = "";

            OracleServerName = "";
            OraclePort = 1521;
            OracleDatabaseName = "";
            OracleUserName = "";
            OraclePassword = "";
            OracleDefaultSchema = "";
            OracleUseCustomConnectionString = false;
            OracleCustomConnectionString = "";

            ProfileName = "";
        }
    }
}
