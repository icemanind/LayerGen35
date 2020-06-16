using System;
using Microsoft.Win32;

namespace LayerGen35
{
    public class RegistryFunctions
    {
        public class LayerGenKeys
        {
            public const string LayerGenSubKeyName = "Software\\Icemanind\\LayerGen35";
            public const string DatabaseEngine = "SqlServer";
            public const string OutputFolder = "OutputFolder";
            public const string IncludeComments = "IncludeComments";
            public const string Language = "Language";
            public const string CustomNamespaces = "CustomNamespaceNames";
            public const string DataNamespaceName = "DataNamespaceName";
            public const string BusinessNamespaceName = "BusinessNamespaceName";
            public const string CreateAsyncMethods = "CreateAsyncMethods";
            public const string CreateWebApiClasses = "CreateWebApiClasses";
            public const string AspNetCore2 = "AspNetCore2";
            public const string DynamicDataRetrival = "DynamicDataRetrieval";
            public const string AutoRightTrimStrings = "AutoRightTrimStrings";
            public const string AllowSerialization = "AllowSerialization";
            public const string PluralizationTemplate = "PluralizationTemplate";

            public const string SqliteFileName = "SqliteFileName";
            public const string SqlitePassword = "SqlitePassword";
            public const string SqliteCustomConnectString = "CustomSqliteConnectString";
            public const string SqliteCustomConnectionString = "CustomSqliteConnectionString";

            public const string SqlServerName = "SqlServerName";
            public const string SqlServerPort = "SqlServerPort";
            public const string SqlServerDatabaseName = "SqlDatabaseName";
            public const string SqlServerDefaultSchema = "SqlDefaultSchema";
            public const string SqlServerTrustedConnection = "SqlTrustedConnection";
            public const string SqlServerUserName = "SqlUserName";
            public const string SqlServerPassword = "SqlPassword";
            public const string SqlServerCustomConnectString = "CustomSqlServerConnectString";
            public const string SqlServerCustomConnectionString = "CustomSqlServerConnectionString";

            public const string MySqlServerName = "MySqlServerName";
            public const string MySqlPort = "MySqlServerPort";
            public const string MySqlDatabaseName = "MySqlDatabaseName";
            public const string MySqlUserName = "MySqlUserName";
            public const string MySqlPassword = "MySqlPassword";
            public const string MySqlCustomConnectString = "CustomMySqlConnectString";
            public const string MySqlCustomConnectionString = "CustomMySqlConnectionString";

            public const string OracleServerName = "OracleServerName";
            public const string OraclePort = "OracleServerPort";
            public const string OracleDatabaseName = "OracleDatabaseName";
            public const string OracleUserName = "OracleUserName";
            public const string OraclePassword = "OraclePassword";
            public const string OracleDefaultSchema = "OracleDefaultSchema";
            public const string OracleCustomConnectString = "CustomOracleServerConnectString";
            public const string OracleCustomConnectionString = "CustomOracleServerConnectionString";

            public const string AccessFileName = "AccessFileName";
            public const string AccessPassword = "AccessPassword";
            public const string AccessCustomConnectString = "CustomAccessConnectString";
            public const string AccessCustomConnectionString = "CustomAccessConnectionString";
        }

        public static string RegValue(RegistryHive hive, string key, string valueName, ref string errInfo)
        {
            RegistryKey objParent = null;
            string sAns = "";
            switch (hive)
            {
                case RegistryHive.ClassesRoot:
                    objParent = Registry.ClassesRoot;
                    break;
                case RegistryHive.CurrentConfig:
                    objParent = Registry.CurrentConfig;
                    break;
                case RegistryHive.CurrentUser:
                    objParent = Registry.CurrentUser;
                    break;
                case RegistryHive.DynData:
#pragma warning disable 618
                    objParent = Registry.DynData;
#pragma warning restore 618
                    break;
                case RegistryHive.LocalMachine:
                    objParent = Registry.LocalMachine;
                    break;
                case RegistryHive.PerformanceData:
                    objParent = Registry.PerformanceData;
                    break;
                case RegistryHive.Users:
                    objParent = Registry.Users;
                    break;
            }

            try
            {
                if (objParent == null)
                    return "";
                RegistryKey objSubkey = objParent.OpenSubKey(key);
                //if can't be found, object is not initialized
                if ((objSubkey != null))
                {
                    sAns = (string) (objSubkey.GetValue(valueName));
                }


            }
            catch (Exception ex)
            {
                errInfo = ex.Message;

            }
            finally
            {
                //if no error but value is empty, populate errinfo
                if (string.IsNullOrEmpty(errInfo) & string.IsNullOrEmpty(sAns))
                {
                    errInfo = "No value found for requested registry key";
                }
            }
            return sAns;
        }

        public static bool WriteToRegistry(RegistryHive parentKeyHive, string subKeyName, string valueName, object value)
        {
            RegistryKey objParentKey = null;
            bool bAns;

            try
            {
                switch (parentKeyHive)
                {
                    case RegistryHive.ClassesRoot:
                        objParentKey = Registry.ClassesRoot;
                        break;
                    case RegistryHive.CurrentConfig:
                        objParentKey = Registry.CurrentConfig;
                        break;
                    case RegistryHive.CurrentUser:
                        objParentKey = Registry.CurrentUser;
                        break;
                    case RegistryHive.DynData:
#pragma warning disable 618
                        objParentKey = Registry.DynData;
#pragma warning restore 618
                        break;
                    case RegistryHive.LocalMachine:
                        objParentKey = Registry.LocalMachine;
                        break;
                    case RegistryHive.PerformanceData:
                        objParentKey = Registry.PerformanceData;
                        break;
                    case RegistryHive.Users:
                        objParentKey = Registry.Users;

                        break;
                }

                if (objParentKey == null)
                    return false;

                RegistryKey objSubKey = objParentKey.OpenSubKey(subKeyName, true) ??
                                        objParentKey.CreateSubKey(subKeyName);

                if (objSubKey == null)
                    return false;
                objSubKey.SetValue(valueName, value);
                bAns = true;
            }
            catch (Exception)
            {
                bAns = false;

            }

            return bAns;
        }
    }
}
