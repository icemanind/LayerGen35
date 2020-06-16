//
// Bug fix: Last used profile will now properly select last used database engine.
// Bug fix: MySql and SQLite modules now pull from NuGet instead of having their own custom projects.
// Bug fix: Fixed tooltip text for pluralization template.
// Bug fix: Last used profile will now properly save the last used port of the database engine.
// Bug fix: Lots of code refactoring
// Bug fix: Added MS Access support
//

using System;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using MySql.Data.MySqlClient;

namespace LayerGen35
{
    public partial class MainForm : Form
    {
        private const int GwlStyle = -16;
        private const int WsDisabled = 0x08000000;
        
        private const string NewtonsoftJsonUrl = "http://www.newtonsoft.com/json";
        private const string ProfileSettingsFileName = "LayerGen35.settings.xml";

        private Profiles _profiles;

        public MainForm()
        {
            InitializeComponent();

            linkLabel1.Links.Add(new LinkLabel.Link { LinkData = NewtonsoftJsonUrl });

            gbProfile.Location = new Point(30, 70);
            gbProfile.Size = new Size(ClientSize.Width - 100, ClientSize.Height - 120);

            gbLanguageOptions.Parent = gbProfile;
            pnlMySql.Parent = gbProfile;
            pnlSqlite.Parent = gbProfile;
            pnlSqlServer.Parent = gbProfile;
            pnlOracle.Parent = gbProfile;
            groupBox1.Parent = gbProfile;
            gbSqlOptions.Parent = gbProfile;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
            path = (Path.GetDirectoryName(path) ?? "").Trim('\\');
            path = path + "\\" + ProfileSettingsFileName;

            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    var emptyProfiles = new Profiles();
                    sw.WriteLine(emptyProfiles.ToXml());
                }
            }

            string outputFolder = GetRegistryValue(RegistryFunctions.LayerGenKeys.OutputFolder);
            string includeComments = GetRegistryValue(RegistryFunctions.LayerGenKeys.IncludeComments);
            string language = GetRegistryValue(RegistryFunctions.LayerGenKeys.Language);
            string sqliteFileName = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqliteFileName);
            string sqlServer = GetRegistryValue(RegistryFunctions.LayerGenKeys.DatabaseEngine);
            string sqlServerName = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerName);
            string sqlServerPort = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerPort);
            string sqlServerDatabaseName = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerDatabaseName);
            string sqlServerDefaultSchema = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerDefaultSchema);
            string sqlServerTrustedConnection = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerTrustedConnection);
            string sqlServerUserName = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerUserName);
            string sqlServerPassword = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerPassword);
            string mysqlServerName = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlServerName);
            string mysqlServerPort = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlPort);
            string mysqlServerDatabaseName = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlDatabaseName);
            string mysqlServerUserName = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlUserName);
            string mysqlServerPassword = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlPassword);
            string accessFileName = GetRegistryValue(RegistryFunctions.LayerGenKeys.AccessFileName);
            string accessPassword = GetRegistryValue(RegistryFunctions.LayerGenKeys.AccessPassword);
            string oracleServerName = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleServerName);
            string oracleServerPort = GetRegistryValue(RegistryFunctions.LayerGenKeys.OraclePort);
            string oracleDatabaseName = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleDatabaseName);
            string oracleUsername = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleUserName);
            string oraclePassword = GetRegistryValue(RegistryFunctions.LayerGenKeys.OraclePassword);
            string oracleDefaultSchema = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleDefaultSchema);
            string customNamespaceNames = GetRegistryValue(RegistryFunctions.LayerGenKeys.CustomNamespaces);
            string dataNamespaceName = GetRegistryValue(RegistryFunctions.LayerGenKeys.DataNamespaceName);
            string businessNamespaceName = GetRegistryValue(RegistryFunctions.LayerGenKeys.BusinessNamespaceName);
            string customSqliteConnectString = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqliteCustomConnectString);
            string customSqliteConnectionString = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqliteCustomConnectionString);
            string sqlitePassword = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlitePassword);
            string customMySqlConnectString = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlCustomConnectString);
            string customMySqlConnectionString = GetRegistryValue(RegistryFunctions.LayerGenKeys.MySqlCustomConnectionString);
            string customSqlServerConnectString = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerCustomConnectString);
            string customSqlServerConnectionString = GetRegistryValue(RegistryFunctions.LayerGenKeys.SqlServerCustomConnectionString);
            string customOracleConnectString = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleCustomConnectString);
            string customOracleConnectionString = GetRegistryValue(RegistryFunctions.LayerGenKeys.OracleCustomConnectionString);
            string customAccessConnectString = GetRegistryValue(RegistryFunctions.LayerGenKeys.AccessCustomConnectString);
            string customAccessConnectionString = GetRegistryValue(RegistryFunctions.LayerGenKeys.AccessCustomConnectionString);
            string dynamicDataRetrieval = GetRegistryValue(RegistryFunctions.LayerGenKeys.DynamicDataRetrival);
            string automaticallyRightTrimStrings = GetRegistryValue(RegistryFunctions.LayerGenKeys.AutoRightTrimStrings);
            string allowSerialization = GetRegistryValue(RegistryFunctions.LayerGenKeys.AllowSerialization);
            string pluralizationTemplate = GetRegistryValue(RegistryFunctions.LayerGenKeys.PluralizationTemplate);
            string createAsyncMethods = GetRegistryValue(RegistryFunctions.LayerGenKeys.CreateAsyncMethods);
            string createWebApiClasses = GetRegistryValue(RegistryFunctions.LayerGenKeys.CreateWebApiClasses);
            string aspNetCore2 = GetRegistryValue(RegistryFunctions.LayerGenKeys.AspNetCore2);

            txtBusinessNamespace.Text = string.IsNullOrEmpty(businessNamespaceName) ? "" : businessNamespaceName;
            txtDataNamespace.Text = string.IsNullOrEmpty(dataNamespaceName) ? "" : dataNamespaceName;
            txtOutput.Text = string.IsNullOrEmpty(outputFolder) ? "" : outputFolder;
            txtSqliteFileName.Text = string.IsNullOrEmpty(sqliteFileName) ? "" : sqliteFileName;
            txtSqlitePassword.Text = string.IsNullOrEmpty(sqlitePassword) ? "" : sqlitePassword;
            txtMySqlServerName.Text = string.IsNullOrEmpty(mysqlServerName) ? "" : mysqlServerName;
            txtMySqlPort.Text = string.IsNullOrEmpty(mysqlServerPort) ? "3306" : mysqlServerPort;
            txtMySqlDatabaseName.Text = string.IsNullOrEmpty(mysqlServerDatabaseName) ? "" : mysqlServerDatabaseName;
            txtMySqlUserName.Text = string.IsNullOrEmpty(mysqlServerUserName) ? "" : mysqlServerUserName;
            txtMySqlPassword.Text = string.IsNullOrEmpty(mysqlServerPassword) ? "" : mysqlServerPassword;
            txtOracleServerName.Text = string.IsNullOrEmpty(oracleServerName) ? "" : oracleServerName;
            txtOraclePort.Text = string.IsNullOrEmpty(oracleServerPort) ? "1521" : oracleServerPort;
            txtOracleDatabaseName.Text = string.IsNullOrEmpty(oracleDatabaseName) ? "" : oracleDatabaseName;
            txtOracleUserName.Text = string.IsNullOrEmpty(oracleUsername) ? "" : oracleUsername;
            txtOraclePassword.Text = string.IsNullOrEmpty(oraclePassword) ? "" : oraclePassword;
            txtOracleDefaultSchema.Text = string.IsNullOrEmpty(oracleDefaultSchema) ? "" : oracleDefaultSchema;
            txtAccessFilename.Text = string.IsNullOrEmpty(accessFileName) ? "" : accessFileName;
            txtAccessPassword.Text = string.IsNullOrEmpty(accessPassword) ? "" : accessPassword;

            txtPluralizationTemplate.Text = string.IsNullOrEmpty(pluralizationTemplate) ? "{ObjectName}s" : pluralizationTemplate;

            if (string.IsNullOrEmpty(createAsyncMethods))
            {
                chkCreateAsyncMethods.Checked = true;
            }
            else
            {
                try
                {
                    chkCreateAsyncMethods.Checked = bool.Parse(createAsyncMethods);
                }
                catch
                {
                    chkCreateAsyncMethods.Checked = true;
                }
            }

            if (string.IsNullOrEmpty(aspNetCore2))
            {
                chkAspNetCore2.Checked = false;
            }
            else
            {
                try
                {
                    chkAspNetCore2.Checked = bool.Parse(aspNetCore2);
                }
                catch
                {
                    chkAspNetCore2.Checked = false;
                }
            }

            if (string.IsNullOrEmpty(createWebApiClasses))
            {
                chkCreateWebApiClasses.Checked = true;
            }
            else
            {
                try
                {
                    chkCreateWebApiClasses.Checked = bool.Parse(createWebApiClasses);
                }
                catch
                {
                    chkCreateWebApiClasses.Checked = true;
                }
            }

            if (string.IsNullOrEmpty(allowSerialization))
            {
                chkAllowSerialization.Checked = false;
            }
            else
            {
                try
                {
                    chkAllowSerialization.Checked = bool.Parse(allowSerialization);
                }
                catch
                {
                    chkAllowSerialization.Checked = false;
                }
            }

            if (string.IsNullOrEmpty(automaticallyRightTrimStrings))
            {
                chkAutomaticallyTrimStrings.Checked = true;
            }
            else
            {
                try
                {
                    chkAutomaticallyTrimStrings.Checked = bool.Parse(automaticallyRightTrimStrings);
                }
                catch
                {
                    chkAutomaticallyTrimStrings.Checked = true;
                }
            }

            if (string.IsNullOrEmpty(dynamicDataRetrieval))
            {
                chkEnableDynamicData.Checked = false;
            }
            else
            {
                try
                {
                    chkEnableDynamicData.Checked = bool.Parse(dynamicDataRetrieval);
                }
                catch
                {
                    chkEnableDynamicData.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customSqlServerConnectString))
            {
                chkSqlServerCustomConnectionString.Checked = false;
            }
            else
            {
                try
                {
                    chkSqlServerCustomConnectionString.Checked = bool.Parse(customSqlServerConnectString);
                }
                catch
                {
                    chkSqlServerCustomConnectionString.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customOracleConnectString))
            {
                chkOracleCustomConnectString.Checked = false;
            }
            else
            {
                try
                {
                    chkOracleCustomConnectString.Checked = bool.Parse(customOracleConnectString);
                }
                catch
                {
                    chkOracleCustomConnectString.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customMySqlConnectString))
            {
                chkMySqlCustomConnectionString.Checked = false;
            }
            else
            {
                try
                {
                    chkMySqlCustomConnectionString.Checked = bool.Parse(customMySqlConnectString);
                }
                catch
                {
                    chkMySqlCustomConnectionString.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customAccessConnectString))
            {
                chkAccessCustomConnectionString.Checked = false;
            }
            else
            {
                try
                {
                    chkAccessCustomConnectionString.Checked = bool.Parse(customAccessConnectString);
                }
                catch
                {
                    chkAccessCustomConnectionString.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customSqliteConnectString))
            {
                chkSqliteCustomConnectionString.Checked = false;
            }
            else
            {
                try
                {
                    chkSqliteCustomConnectionString.Checked = bool.Parse(customSqliteConnectString);
                }
                catch
                {
                    chkSqliteCustomConnectionString.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(customNamespaceNames))
            {
                chkCustomNamespaceNames.Checked = false;
            }
            else
            {
                try
                {
                    chkCustomNamespaceNames.Checked = bool.Parse(customNamespaceNames);
                }
                catch
                {
                    chkCustomNamespaceNames.Checked = false;
                }
            }
            if (string.IsNullOrEmpty(includeComments))
            {
                chkIncludeComments.Checked = true;
            }
            else
            {
                try
                {
                    chkIncludeComments.Checked = bool.Parse(includeComments);
                }
                catch
                {
                    chkIncludeComments.Checked = true;
                }
            }

            if (string.IsNullOrEmpty(language))
            {
                ddlLanguage.SelectedIndex = 0;
            }
            else
            {
                try
                {
                    ddlLanguage.SelectedIndex = int.Parse(language);
                }
                catch
                {
                    ddlLanguage.SelectedIndex = 0;
                }
            }
            if (string.IsNullOrEmpty(sqlServer))
            {
                ddlSqlServer.SelectedIndex = 0;
            }
            else
            {
                try
                {
                    ddlSqlServer.SelectedIndex = int.Parse(sqlServer);
                }
                catch
                {
                    ddlSqlServer.SelectedIndex = 0;
                }
            }

            txtSqlServerCustomConnectionString.Text = string.IsNullOrEmpty(customSqlServerConnectionString) ? "" : customSqlServerConnectionString;
            txtOracleConnectionString.Text = string.IsNullOrEmpty(customOracleConnectionString) ? "" : customOracleConnectionString;
            txtAccessConnectionString.Text = string.IsNullOrEmpty(customAccessConnectionString) ? "" : customAccessConnectionString;
            txtMySqlCustomConnectionString.Text = string.IsNullOrEmpty(customMySqlConnectionString) ? "" : customMySqlConnectionString;
            txtSqliteCustomConnectionString.Text = string.IsNullOrEmpty(customSqliteConnectionString) ? "" : customSqliteConnectionString;
            txtSqlServerName.Text = string.IsNullOrEmpty(sqlServerName) ? "" : sqlServerName;
            txtSqlServerPort.Text = string.IsNullOrEmpty(sqlServerPort) ? "1433" : sqlServerPort;
            txtDatabaseName.Text = string.IsNullOrEmpty(sqlServerDatabaseName) ? "" : sqlServerDatabaseName;
            txtSqlDefaultSchema.Text = string.IsNullOrEmpty(sqlServerDefaultSchema) ? "dbo" : sqlServerDefaultSchema;

            if (string.IsNullOrEmpty(sqlServerTrustedConnection))
            {
                chkSqlTrustedConnection.Checked = false;
            }
            else
            {
                try
                {
                    chkSqlTrustedConnection.Checked = bool.Parse(sqlServerTrustedConnection);
                }
                catch
                {
                    chkSqlTrustedConnection.Checked = false;
                }
            }

            txtSqlUserName.Text = string.IsNullOrEmpty(sqlServerUserName) ? "" : sqlServerUserName;
            txtSqlPassword.Text = string.IsNullOrEmpty(sqlServerPassword) ? "" : sqlServerPassword;

            txtSqlServerName.Focus();

            txtSqliteObjects.MaxLength = int.MaxValue;
            txtSqlServerObjects.MaxLength = int.MaxValue;
            txtMySqlObjects.MaxLength = int.MaxValue;

            RefreshNamespaces();

            ConfigureAccessConnectionString();
            ConfigureOracleConnectionString();
            ConfigureSqliteConnectionString();
            ConfigureMySqlConnectionString();
            ConfigureSqlServerConnectionString();

            using (StreamReader sr = File.OpenText(path))
            {
                _profiles = Profiles.FromXml(sr.ReadToEnd());
            }

            if (_profiles.All(z => z.ProfileName.ToLower() != "empty"))
            {
                var profile = new Profile { ProfileName = "Empty" };
                _profiles.Add(profile);
            }

            var lastUsedProfile = new Profile
            {
                ProfileName = "Last Used",
                AccessCustomConnectionString=txtAccessConnectionString.Text,
                AccessUseCustomConnectionString=chkAccessCustomConnectionString.Checked,
                AccessFileName=txtAccessFilename.Text,
                AccessPassword=txtAccessPassword.Text,
                AllowSerialization = chkAllowSerialization.Checked,
                AspNetCore2 = chkAspNetCore2.Checked,
                AutomaticallyRightTrimData = chkAutomaticallyTrimStrings.Checked,
                BusinessNameSpace = txtBusinessNamespace.Text,
                CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                CustomNameSpaces = chkCustomNamespaceNames.Checked,
                DataNameSpace = txtDataNamespace.Text,
                EnableDynamicDataRetrieval = chkEnableDynamicData.Checked,
                IncludeComments = chkIncludeComments.Checked,
                Language = ddlLanguage.SelectedIndex == 0 ? DatabasePlugins.Languages.CSharp : DatabasePlugins.Languages.VbNet,
                MySqlCustomConnectionString = txtMySqlCustomConnectionString.Text,
                MySqlDatabaseName = txtMySqlDatabaseName.Text,
                MySqlPassword = txtMySqlPassword.Text,
                MySqlServerName = txtMySqlServerName.Text,
                MySqlUseCustomConnectionString = chkMySqlCustomConnectionString.Checked,
                MySqlUserName = txtMySqlUserName.Text,
                OracleCustomConnectionString = txtOracleConnectionString.Text,
                OracleDatabaseName = txtOracleDatabaseName.Text,
                OracleDefaultSchema = txtOracleDefaultSchema.Text,
                OraclePassword = txtOraclePassword.Text,
                OracleServerName = txtOracleServerName.Text,
                OracleUseCustomConnectionString = chkOracleCustomConnectString.Checked,
                OracleUserName = txtOracleUserName.Text,
                OutputFolder = txtOutput.Text,
                PluralizationTemplate = txtPluralizationTemplate.Text,
                SqliteCustomConnectionString = txtSqliteCustomConnectionString.Text,
                SqliteFileName = txtSqliteFileName.Text,
                SqlitePassword = txtSqlitePassword.Text,
                SqliteUseCustomConnectionString = chkSqliteCustomConnectionString.Checked,
                SqlServerCustomConnectionString = txtSqlServerCustomConnectionString.Text,
                SqlServerDatabaseName = txtDatabaseName.Text,
                SqlServerDefaultSchema = txtSqlDefaultSchema.Text,
                SqlServerPassword = txtSqlPassword.Text,
                SqlServerServerName = txtSqlServerName.Text,
                SqlServerTrustedConnection = chkSqlTrustedConnection.Checked,
                SqlServerUseCustomConnectionString = chkCustomNamespaceNames.Checked,
                SqlServerUserName = txtSqlUserName.Text
            };

            try
            {
                lastUsedProfile.OraclePort = int.Parse(txtOraclePort.Text);
            }
            catch
            {
                lastUsedProfile.OraclePort = 1521;
            }

            try
            {
                lastUsedProfile.SqlServerPort = int.Parse(txtSqlServerPort.Text);
            }
            catch
            {
                lastUsedProfile.SqlServerPort = 1433;
            }

            try
            {
                lastUsedProfile.MySqlPort = int.Parse(txtMySqlPort.Text);
            }
            catch
            {
                lastUsedProfile.MySqlPort = 3306;
            }

            switch ((((string)ddlSqlServer.SelectedItem) ?? "sql server 2000-2014").ToLower())
            {
                case "ms access":
                    lastUsedProfile.DatabaseType = DatabasePlugins.DatabaseTypes.MsAccess;
                    break;
                case "oracle":
                    lastUsedProfile.DatabaseType = DatabasePlugins.DatabaseTypes.Oracle;
                    break;
                case "mysql":
                    lastUsedProfile.DatabaseType = DatabasePlugins.DatabaseTypes.MySql;
                    break;
                case "sqlite 3":
                    lastUsedProfile.DatabaseType = DatabasePlugins.DatabaseTypes.Sqlite;
                    break;
                default:
                    lastUsedProfile.DatabaseType = DatabasePlugins.DatabaseTypes.SqlServer;
                    break;
            }

            if (_profiles.Any(z => z.ProfileName.ToLower() == "last used"))
                _profiles.Remove(_profiles.First(z => z.ProfileName.ToLower() == "last used"));
            _profiles.Insert(0, lastUsedProfile);

            BindProfileDropDown();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.DatabaseEngine, ddlSqlServer.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OutputFolder, txtOutput.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.IncludeComments, chkIncludeComments.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.Language, ddlLanguage.SelectedIndex.ToString(CultureInfo.InvariantCulture));
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqliteFileName, txtSqliteFileName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlitePassword, txtSqlitePassword.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AccessFileName, txtAccessFilename.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AccessPassword, txtAccessPassword.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerName, txtSqlServerName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerPort, txtSqlServerPort.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerDatabaseName, txtDatabaseName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerDefaultSchema, txtSqlDefaultSchema.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerTrustedConnection, chkSqlTrustedConnection.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerUserName, txtSqlUserName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerPassword, txtSqlPassword.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlServerName, txtMySqlServerName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlPort, txtMySqlPort.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlDatabaseName, txtMySqlDatabaseName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlUserName, txtMySqlUserName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlPassword, txtMySqlPassword.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleServerName, txtOracleServerName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OraclePort, txtOraclePort.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleDatabaseName, txtOracleDatabaseName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleUserName, txtOracleUserName.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OraclePassword, txtOraclePassword.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleDefaultSchema, txtOracleDefaultSchema.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.CustomNamespaces, chkCustomNamespaceNames.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.DataNamespaceName, txtDataNamespace.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.BusinessNamespaceName, txtBusinessNamespace.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqliteCustomConnectString, chkSqliteCustomConnectionString.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqliteCustomConnectionString, txtSqliteCustomConnectionString.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AccessCustomConnectString, chkAccessCustomConnectionString.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AccessCustomConnectionString, txtAccessConnectionString.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlCustomConnectString, chkMySqlCustomConnectionString.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.MySqlCustomConnectionString, txtMySqlCustomConnectionString.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerCustomConnectString, chkSqlServerCustomConnectionString.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.SqlServerCustomConnectionString, txtSqlServerCustomConnectionString.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleCustomConnectString, chkOracleCustomConnectString.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.OracleCustomConnectionString, txtOracleConnectionString.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.DynamicDataRetrival, chkEnableDynamicData.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AutoRightTrimStrings, chkAutomaticallyTrimStrings.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AllowSerialization, chkAllowSerialization.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.PluralizationTemplate, txtPluralizationTemplate.Text);
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.CreateAsyncMethods, chkCreateAsyncMethods.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.CreateWebApiClasses, chkCreateWebApiClasses.Checked.ToString());
            RegistryFunctions.WriteToRegistry(Microsoft.Win32.RegistryHive.CurrentUser, RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, RegistryFunctions.LayerGenKeys.AspNetCore2, chkAspNetCore2.Checked.ToString());
        }

        private void chkSqlTrustedConnection_CheckedChanged(object sender, EventArgs e)
        {
            if (chkSqlTrustedConnection.Checked)
            {
                txtSqlUserName.Enabled = false;
                txtSqlPassword.Enabled = false;
                pbHelpSqlServerUserName.Visible = false;
                pbHelpSqlServerPassword.Visible = false;
            }
            else
            {
                txtSqlUserName.Enabled = true;
                txtSqlPassword.Enabled = true;
                pbHelpSqlServerUserName.Visible = true;
                pbHelpSqlServerPassword.Visible = true;
            }

            ConfigureSqlServerConnectionString();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            DialogResult dr = folderBrowserDialog1.ShowDialog();
            if (dr == DialogResult.OK)
            {
                txtOutput.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void btnSaveProfile_Click(object sender, EventArgs e)
        {
            var saveProfileDialog = new SaveProfileDialog();
            DialogResult dr = saveProfileDialog.ShowDialog();
            if (dr == DialogResult.OK)
            {
                var profile = new Profile
                {
                    ProfileName = saveProfileDialog.ProfileName,
                    AccessCustomConnectionString = txtAccessConnectionString.Text,
                    AccessUseCustomConnectionString = chkAccessCustomConnectionString.Checked,
                    AccessFileName = txtAccessFilename.Text,
                    AccessPassword = txtAccessPassword.Text,
                    AllowSerialization = chkAllowSerialization.Checked,
                    AspNetCore2 = chkAspNetCore2.Checked,
                    AutomaticallyRightTrimData = chkAutomaticallyTrimStrings.Checked,
                    BusinessNameSpace = txtBusinessNamespace.Text,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    CustomNameSpaces = chkCustomNamespaceNames.Checked,
                    DataNameSpace = txtDataNamespace.Text,
                    EnableDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    IncludeComments = chkIncludeComments.Checked,
                    MySqlCustomConnectionString = txtMySqlCustomConnectionString.Text,
                    MySqlDatabaseName = txtMySqlDatabaseName.Text,
                    MySqlPassword = txtMySqlPassword.Text,
                    MySqlServerName = txtMySqlServerName.Text,
                    MySqlUseCustomConnectionString = chkMySqlCustomConnectionString.Checked,
                    MySqlUserName = txtMySqlUserName.Text,
                    OracleCustomConnectionString = txtOracleConnectionString.Text,
                    OracleDatabaseName = txtOracleDatabaseName.Text,
                    OracleDefaultSchema = txtOracleDefaultSchema.Text,
                    OraclePassword = txtOraclePassword.Text,
                    OracleServerName = txtOracleServerName.Text,
                    OracleUseCustomConnectionString = chkOracleCustomConnectString.Checked,
                    OracleUserName = txtOracleUserName.Text,
                    OutputFolder = txtOutput.Text,
                    PluralizationTemplate = txtPluralizationTemplate.Text,
                    SqliteCustomConnectionString = txtSqliteCustomConnectionString.Text,
                    SqliteFileName = txtSqliteFileName.Text,
                    SqlitePassword = txtSqlitePassword.Text,
                    SqliteUseCustomConnectionString = chkSqliteCustomConnectionString.Checked,
                    SqlServerCustomConnectionString = txtSqlServerCustomConnectionString.Text,
                    SqlServerDatabaseName = txtDatabaseName.Text,
                    SqlServerDefaultSchema = txtSqlDefaultSchema.Text,
                    SqlServerPassword = txtSqlPassword.Text,
                    SqlServerServerName = txtSqlServerName.Text,
                    SqlServerTrustedConnection = chkSqlTrustedConnection.Checked,
                    SqlServerUseCustomConnectionString = chkCustomNamespaceNames.Checked,
                    SqlServerUserName = txtSqlUserName.Text
                };

                int mysqlPort;
                int sqlServerport;
                int oraclePort;
                if (int.TryParse(txtMySqlPort.Text, out mysqlPort))
                {
                    profile.MySqlPort = mysqlPort;
                }
                if (int.TryParse(txtSqlServerPort.Text, out sqlServerport))
                {
                    profile.SqlServerPort = sqlServerport;
                }
                if (int.TryParse(txtOraclePort.Text, out oraclePort))
                {
                    profile.OraclePort = oraclePort;
                }

                switch ((string)ddlSqlServer.SelectedItem)
                {
                    case "Oracle":
                        profile.DatabaseType = DatabasePlugins.DatabaseTypes.Oracle;
                        break;
                    case "SQLite 3":
                        profile.DatabaseType = DatabasePlugins.DatabaseTypes.Sqlite;
                        break;
                    case "MySQL":
                        profile.DatabaseType = DatabasePlugins.DatabaseTypes.MySql;
                        break;
                    default:
                        profile.DatabaseType = DatabasePlugins.DatabaseTypes.SqlServer;
                        break;
                }

                switch ((string)ddlLanguage.SelectedItem)
                {
                    case "VB.NET":
                        profile.Language = DatabasePlugins.Languages.VbNet;
                        break;
                    default:
                        profile.Language = DatabasePlugins.Languages.CSharp;
                        break;
                }

                _profiles.Add(profile);

                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                path = (Path.GetDirectoryName(path) ?? "").Trim('\\');
                path = path + "\\" + ProfileSettingsFileName;

                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(_profiles.ToXml());
                }

                using (StreamReader sr = File.OpenText(path))
                {
                    _profiles = Profiles.FromXml(sr.ReadToEnd());
                }

                BindProfileDropDown();
            }
        }

        private void SqlServerThread(object plugin)
        {
            var form = (DatabasePlugins.SqlServer)plugin;

            form.CreateLayers();

            if (((Form)form.ProgressBar.Parent).InvokeRequired)
            {
                Invoke((Action) (() => { CloseProgressForm(form); }));
            }
        }

        private void MySqlServerThread(object plugin)
        {
            var form = (DatabasePlugins.MySql)plugin;

            form.CreateLayers();

            if (((Form)form.ProgressBar.Parent).InvokeRequired)
            {
                Invoke((Action) (() => { CloseProgressForm(form); }));
            }
        }

        private void SqliteThread(object plugin)
        {
            var form = (DatabasePlugins.Sqlite)plugin;

            form.CreateLayers();

            if (((Form)form.ProgressBar.Parent).InvokeRequired)
            {
                Invoke((Action) (() => { CloseProgressForm(form); }));
            }
        }

        private void MsAccessThread(object plugin)
        {
            var form = (DatabasePlugins.MsAccess)plugin;

            form.CreateLayers();

            if (((Form) form.ProgressBar.Parent).InvokeRequired)
            {
                Invoke((Action) (() => { CloseProgressForm(form); }));
            }
        }

        private void OracleThread(object plugin)
        {
            var form = (DatabasePlugins.Oracle)plugin;

            form.CreateLayers();

            if (((Form) form.ProgressBar.Parent).InvokeRequired)
            {
                Invoke((Action) (() => { CloseProgressForm(form); }));
            }
        }

        private void CloseProgressForm(DatabasePlugins.IDatabasePlugin plugin)
        {
            ((Form)plugin.ProgressBar.Parent).Close();
            SetNativeEnabled(true);
            MessageBox.Show(this, @"LayerGen is Complete!");
        }

        private void btnCreateLayers_Click(object sender, EventArgs e)
        {
            string sqlServer = ((string) ddlSqlServer.SelectedItem).ToLower();

            if (sqlServer == "sql server 2000-2014")
            {
                if (string.IsNullOrEmpty(txtSqlServerObjects.Text) ||
                    string.IsNullOrEmpty(txtSqlServerObjects.Text.Trim()) || txtSqlServerObjects.Text.Trim() == ";")
                {
                    MessageBox.Show(@"You must select at least one object (table or view)!", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (sqlServer == "sqlite 3")
            {
                if (string.IsNullOrEmpty(txtSqliteObjects.Text) ||
                    string.IsNullOrEmpty(txtSqliteObjects.Text.Trim()) || txtSqliteObjects.Text.Trim() == ";")
                {
                    MessageBox.Show(@"You must select at least one object (table or view)!", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (sqlServer == "mysql")
            {
                if (string.IsNullOrEmpty(txtMySqlObjects.Text) ||
                    string.IsNullOrEmpty(txtMySqlObjects.Text.Trim()) || txtMySqlObjects.Text.Trim() == ";")
                {
                    MessageBox.Show(@"You must select at least one object (table or view)!", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (sqlServer == "oracle")
            {
                if (string.IsNullOrEmpty(txtOracleObjects.Text) ||
                    string.IsNullOrEmpty(txtOracleObjects.Text.Trim()) || txtOracleObjects.Text.Trim() == ";")
                {
                    MessageBox.Show(@"You must select at least one object (table or view)!", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (sqlServer == "ms access")
            {
                if (string.IsNullOrEmpty(txtAccessObjects.Text) ||
                    string.IsNullOrEmpty(txtAccessObjects.Text.Trim()) || txtAccessObjects.Text.Trim() == ";")
                {
                    MessageBox.Show(@"You must select at least one object (table or view)!", @"Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            if (txtPluralizationTemplate.Text == null || !txtPluralizationTemplate.Text.ToLower().Contains("{objectname}"))
            {
                MessageBox.Show(@"Your pluralization template must contain at least one {ObjectName} placeholder!", @"Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            try
            {
                new FileInfo(txtOutput.Text);
            }
            catch
            {
                MessageBox.Show(@"Invalid Output Path!", @"Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!Directory.Exists(txtOutput.Text))
            {
                DialogResult dr = MessageBox.Show(@"Directory does not exist. Create?", @"Directory not found", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (dr == DialogResult.No)
                {
                    return;
                }
                Directory.CreateDirectory(txtOutput.Text);
            }

            if (chkCreateWebApiClasses.Checked)
            {
                if (!Directory.Exists(Path.Combine(txtOutput.Text, "WebApi")))
                {
                    Directory.CreateDirectory(Path.Combine(txtOutput.Text, "WebApi"));
                }
            }

            var pw = new PleaseWaitForm { StartPosition = FormStartPosition.Manual };
            pw.Location = new Point(Location.X + (Width - pw.Width) / 2, Location.Y + (Height - pw.Height) / 2);
            pw.Show(this);
            SetNativeEnabled(false);

            pw.ProgressBar.Minimum = 0;
            pw.ProgressBar.Value = 0;
            pw.ProgressBar.Maximum = 100;

            if (sqlServer == "sql server 2000-2014")
            {
                var databasePlugin = new DatabasePlugins.SqlServer
                {
                    DatabaseName = txtDatabaseName.Text,
                    DatabasePort = int.Parse(txtSqlServerPort.Text),
                    DatabaseServer = txtSqlServerName.Text,
                    OutputDirectory = txtOutput.Text,
                    Password = txtSqlPassword.Text,
                    TrustedConnection = chkSqlTrustedConnection.Checked,
                    UserName = txtSqlUserName.Text,
                    Objects = txtSqlServerObjects.Text,
                    DefaultSchema = txtSqlDefaultSchema.Text,
                    IncludeComments = chkIncludeComments.Checked,
                    ProgressBar = pw.ProgressBar,
                    DataNamespaceName = txtDataNamespace.Text,
                    BusinessNamespaceName = txtBusinessNamespace.Text,
                    AutoRightTrimStrings = chkAutomaticallyTrimStrings.Checked,
                    HasCustomConnectionString = chkSqlServerCustomConnectionString.Checked,
                    CustomConnectionString = txtSqlServerCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' '),
                    HasDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    AllowSerialization = chkAllowSerialization.Checked,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    AspNetCore2 = chkCreateWebApiClasses.Checked && chkAspNetCore2.Checked,
                    PluralizationTemplate = txtPluralizationTemplate.Text
                };

                if ((string)ddlLanguage.SelectedItem == "VB.NET")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.VbNet;
                }
                if ((string)ddlLanguage.SelectedItem == "C#")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.CSharp;
                }

                var thread = new Thread(SqlServerThread);
                thread.Start(databasePlugin);
            }

            if (sqlServer == "sqlite 3")
            {
                var databasePlugin = new DatabasePlugins.Sqlite
                {
                    DatabaseName = txtSqliteFileName.Text,
                    Password = string.IsNullOrEmpty(txtSqlitePassword.Text.Trim()) ? null : txtSqlitePassword.Text.Trim(),
                    Objects = txtSqliteObjects.Text,
                    OutputDirectory = txtOutput.Text,
                    IncludeComments = chkIncludeComments.Checked,
                    ProgressBar = pw.ProgressBar,
                    DataNamespaceName = txtDataNamespace.Text,
                    AutoRightTrimStrings = chkAutomaticallyTrimStrings.Checked,
                    BusinessNamespaceName = txtBusinessNamespace.Text,
                    HasCustomConnectionString = chkSqliteCustomConnectionString.Checked,
                    CustomConnectionString = txtSqliteCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' '),
                    HasDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    AllowSerialization = chkAllowSerialization.Checked,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    AspNetCore2 = chkCreateWebApiClasses.Checked && chkAspNetCore2.Checked,
                    PluralizationTemplate = txtPluralizationTemplate.Text
                };

                if ((string)ddlLanguage.SelectedItem == "VB.NET")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.VbNet;
                }
                if ((string)ddlLanguage.SelectedItem == "C#")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.CSharp;
                }

                var thread = new Thread(SqliteThread);
                thread.Start(databasePlugin);
            }

            if (sqlServer == "ms access")
            {
                var databasePlugin = new DatabasePlugins.MsAccess
                {
                    DatabaseName = txtAccessFilename.Text,
                    Password = string.IsNullOrEmpty(txtAccessPassword.Text.Trim()) ? null : txtAccessPassword.Text.Trim(),
                    Objects = txtAccessObjects.Text,
                    OutputDirectory = txtOutput.Text,
                    IncludeComments = chkIncludeComments.Checked,
                    ProgressBar = pw.ProgressBar,
                    DataNamespaceName = txtDataNamespace.Text,
                    AutoRightTrimStrings = chkAutomaticallyTrimStrings.Checked,
                    BusinessNamespaceName = txtBusinessNamespace.Text,
                    HasCustomConnectionString = chkAccessCustomConnectionString.Checked,
                    CustomConnectionString = txtAccessConnectionString.Text.Replace('\r',' ').Replace('\n',' '),
                    HasDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    AllowSerialization = chkAllowSerialization.Checked,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    AspNetCore2 = chkCreateWebApiClasses.Checked && chkAspNetCore2.Checked,
                    PluralizationTemplate = txtPluralizationTemplate.Text
                };

                if ((string)ddlLanguage.SelectedItem == "VB.NET")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.VbNet;
                }
                if ((string)ddlLanguage.SelectedItem == "C#")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.CSharp;
                }

                var thread = new Thread(MsAccessThread);
                thread.Start(databasePlugin);
            }

            if (sqlServer == "mysql")
            {
                var databasePlugin = new DatabasePlugins.MySql
                {
                    DatabaseName = txtMySqlDatabaseName.Text,
                    Objects = txtMySqlObjects.Text,
                    OutputDirectory = txtOutput.Text,
                    IncludeComments = chkIncludeComments.Checked,
                    DatabaseServer = txtMySqlServerName.Text,
                    UserName = txtMySqlUserName.Text,
                    Password = txtMySqlPassword.Text,
                    AutoRightTrimStrings = chkAutomaticallyTrimStrings.Checked,
                    DatabasePort = int.Parse(txtMySqlPort.Text),
                    ProgressBar = pw.ProgressBar,
                    DataNamespaceName = txtDataNamespace.Text,
                    BusinessNamespaceName = txtBusinessNamespace.Text,
                    HasCustomConnectionString = chkMySqlCustomConnectionString.Checked,
                    CustomConnectionString = txtMySqlCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' '),
                    HasDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    AllowSerialization = chkAllowSerialization.Checked,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    AspNetCore2 = chkCreateWebApiClasses.Checked && chkAspNetCore2.Checked,
                    PluralizationTemplate = txtPluralizationTemplate.Text
                };

                if ((string)ddlLanguage.SelectedItem == "VB.NET")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.VbNet;
                }
                if ((string)ddlLanguage.SelectedItem == "C#")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.CSharp;
                }

                var thread = new Thread(MySqlServerThread);
                thread.Start(databasePlugin);
            }

            if (sqlServer == "oracle")
            {
                var databasePlugin = new DatabasePlugins.Oracle
                {
                    DatabaseName = txtOracleDatabaseName.Text,
                    DatabasePort = int.Parse(txtOraclePort.Text),
                    DatabaseServer = txtOracleServerName.Text,
                    OutputDirectory = txtOutput.Text,
                    Password = txtOraclePassword.Text,
                    UserName = txtOracleUserName.Text,
                    Objects = txtOracleObjects.Text,
                    DefaultSchema = txtOracleDefaultSchema.Text,
                    IncludeComments = chkIncludeComments.Checked,
                    ProgressBar = pw.ProgressBar,
                    DataNamespaceName = txtDataNamespace.Text,
                    BusinessNamespaceName = txtBusinessNamespace.Text,
                    AutoRightTrimStrings = chkAutomaticallyTrimStrings.Checked,
                    HasCustomConnectionString = chkOracleCustomConnectString.Checked,
                    CustomConnectionString = txtOracleConnectionString.Text.Replace('\r', ' ').Replace('\n', ' '),
                    HasDynamicDataRetrieval = chkEnableDynamicData.Checked,
                    AllowSerialization = chkAllowSerialization.Checked,
                    CreateAsyncMethods = chkCreateAsyncMethods.Checked,
                    CreateWebApiClasses = chkCreateWebApiClasses.Checked,
                    AspNetCore2 = chkCreateWebApiClasses.Checked && chkAspNetCore2.Checked,
                    PluralizationTemplate = txtPluralizationTemplate.Text
                };

                if ((string)ddlLanguage.SelectedItem == "VB.NET")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.VbNet;
                }
                if ((string)ddlLanguage.SelectedItem == "C#")
                {
                    databasePlugin.Language = DatabasePlugins.Languages.CSharp;
                }

                var thread = new Thread(OracleThread);
                thread.Start(databasePlugin);
            }
        }

        private void ddlSqlServer_SelectedIndexChanged(object sender, EventArgs e)
        {
            string sqlServer = (((string)ddlSqlServer.SelectedItem) ?? "sql server 2000-2014").ToLower();
            if (sqlServer == "sql server 2000-2014")
            {
                pnlSqlServer.Visible = true;
                pnlSqlite.Visible = false;
                pnlMySql.Visible = false;
                pnlOracle.Visible = false;
                pnlAccess.Visible = false;
                ConfigureSqlServerConnectionString();
            }
            else if (sqlServer == "sqlite 3")
            {
                pnlSqlServer.Visible = false;
                pnlSqlite.Visible = true;
                pnlMySql.Visible = false;
                pnlOracle.Visible = false;
                pnlAccess.Visible = false;
                ConfigureSqliteConnectionString();
            }
            else if (sqlServer == "mysql")
            {
                pnlSqlServer.Visible = false;
                pnlSqlite.Visible = false;
                pnlMySql.Visible = true;
                pnlOracle.Visible = false;
                pnlAccess.Visible = false;
                pnlMySql.BringToFront();
                ConfigureMySqlConnectionString();
            }
            else if (sqlServer == "oracle")
            {
                pnlSqlServer.Visible = false;
                pnlSqlite.Visible = false;
                pnlMySql.Visible = false;
                pnlOracle.Visible = true;
                pnlAccess.Visible = false;
                pnlOracle.BringToFront();
                ConfigureOracleConnectionString();
            }
            else if (sqlServer == "ms access")
            {
                pnlSqlServer.Visible = false;
                pnlSqlite.Visible = false;
                pnlMySql.Visible = false;
                pnlOracle.Visible = false;
                pnlAccess.Visible = true;
                pnlAccess.BringToFront();
                ConfigureAccessConnectionString();
            }
            RefreshNamespaces();
        }

        private void btnTablesBrowse_Click(object sender, EventArgs e)
        {
            var objectExplorer = new ObjectExplorer
            {
                DatabaseName = txtDatabaseName.Text,
                Port = int.Parse(txtSqlServerPort.Text),
                ServerName = txtSqlServerName.Text,
                Password = txtSqlPassword.Text,
                UserName = txtSqlUserName.Text,
                TrustedConnection = chkSqlTrustedConnection.Checked,
                DefaultSchema = txtSqlDefaultSchema.Text,
                SelectedObjects = txtSqlServerObjects.Text,
                HasCustomConnectionString = chkSqlServerCustomConnectionString.Checked,
                CustomConnectionString = txtSqlServerCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' ')
            };

            DialogResult dr = objectExplorer.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtSqlServerObjects.Text = objectExplorer.SelectedObjects;
            }
        }

        private void btnAccessObjectsBrowse_Click(object sender, EventArgs e)
        {
            var objectExplorer = new ObjectExplorerMsAccess
            {
                Filename = txtAccessFilename.Text,
                HasCustomConnectionString = chkAccessCustomConnectionString.Checked,
                CustomConnectionString = txtAccessConnectionString.Text.Replace('\r', ' ').Replace('\n', ' '),
                Password = txtAccessPassword.Text
            };

            DialogResult dr = objectExplorer.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtAccessObjects.Text = objectExplorer.SelectedObjects;
            }
        }

        private void btnSqliteObjectsBrowse_Click(object sender, EventArgs e)
        {
            var objectExplorer = new ObjectExplorerSqlite
            {
                Filename = txtSqliteFileName.Text,
                Password = string.IsNullOrEmpty(txtSqlitePassword.Text.Trim()) ? null : txtSqlitePassword.Text.Trim(),
                HasCustomConnectionString = chkSqliteCustomConnectionString.Checked,
                CustomConnectionString = txtSqliteCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' ')
            };

            DialogResult dr = objectExplorer.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtSqliteObjects.Text = objectExplorer.SelectedObjects;
            }
        }

        private void btnMySqlObjectsBrowse_Click(object sender, EventArgs e)
        {
            var objectExplorer = new ObjectExplorerMySql
            {
                DatabaseName = txtMySqlDatabaseName.Text,
                UserName = txtMySqlUserName.Text,
                Password = txtMySqlPassword.Text,
                ServerName = txtMySqlServerName.Text,
                ServerPort = uint.Parse(txtMySqlPort.Text),
                HasCustomConnectionString = chkMySqlCustomConnectionString.Checked,
                CustomConnectionString = txtMySqlCustomConnectionString.Text.Replace('\r', ' ').Replace('\n', ' ')
            };

            DialogResult dr = objectExplorer.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtMySqlObjects.Text = objectExplorer.SelectedObjects;
            }
        }

        private void btnSqliteFileNameBrowse_Click(object sender, EventArgs e)
        {
            DialogResult dr = openFileDialog1.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtSqliteFileName.Text = openFileDialog1.FileName;
            }
        }

        private void btnAccessFilenameBrowse_Click(object sender, EventArgs e)
        {
            DialogResult dr = openFileDialog2.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtAccessFilename.Text = openFileDialog2.FileName;
            }
        }

        private void RefreshNamespaces()
        {
            string sqlServer = (((string)ddlSqlServer.SelectedItem) ?? "sql server 2000-2014").ToLower();
            if (chkCustomNamespaceNames.Checked)
            {
                label20.ForeColor = Color.White;
                label21.ForeColor = Color.White;
                txtDataNamespace.Enabled = true;
                txtBusinessNamespace.Enabled = true;
                pbHelpBusinessNamespace.Visible = true;
                pbHelpDataNamespace.Visible = true;

                return;
            }
            pbHelpBusinessNamespace.Visible = false;
            pbHelpDataNamespace.Visible = false;

            label20.ForeColor = Color.FromArgb(165, 165, 165);
            label21.ForeColor = Color.FromArgb(165, 165, 165);
            txtDataNamespace.Enabled = false;
            txtBusinessNamespace.Enabled = false;

            bool isCs = ddlLanguage.SelectedIndex == 0;

            if (sqlServer == "sql server 2000-2014")
            {
                txtDataNamespace.Text = @"DataLayer." + txtDatabaseName.Text.FirstCharToUpper();
                txtDataNamespace.Text = isCs ? txtDataNamespace.Text.ConvertToValidCSharpNamespace() : txtDataNamespace.Text.ConvertToValidVbNamespace();
                txtBusinessNamespace.Text = @"BusinessLayer." + txtDatabaseName.Text.FirstCharToUpper();
                txtBusinessNamespace.Text = isCs ? txtBusinessNamespace.Text.ConvertToValidCSharpNamespace() : txtBusinessNamespace.Text.ConvertToValidVbNamespace();
            }
            if (sqlServer == "sqlite 3")
            {
                string file;
                try
                {
                    file = Path.GetFileNameWithoutExtension(txtSqliteFileName.Text);
                }
                catch
                {
                    file = "";
                }
                txtDataNamespace.Text = @"DataLayer." + file.FirstCharToUpper();
                txtDataNamespace.Text = isCs ? txtDataNamespace.Text.ConvertToValidCSharpNamespace() : txtDataNamespace.Text.ConvertToValidVbNamespace();
                txtBusinessNamespace.Text = @"BusinessLayer." + file.FirstCharToUpper();
                txtBusinessNamespace.Text = isCs ? txtBusinessNamespace.Text.ConvertToValidCSharpNamespace() : txtBusinessNamespace.Text.ConvertToValidVbNamespace();
            }
            if (sqlServer == "mysql")
            {
                txtDataNamespace.Text = @"DataLayer." + txtMySqlDatabaseName.Text.FirstCharToUpper();
                txtDataNamespace.Text = isCs ? txtDataNamespace.Text.ConvertToValidCSharpNamespace() : txtDataNamespace.Text.ConvertToValidVbNamespace();
                txtBusinessNamespace.Text = @"BusinessLayer." + txtMySqlDatabaseName.Text.FirstCharToUpper();
                txtBusinessNamespace.Text = isCs ? txtBusinessNamespace.Text.ConvertToValidCSharpNamespace() : txtBusinessNamespace.Text.ConvertToValidVbNamespace();
            }
            if (sqlServer == "oracle")
            {
                txtDataNamespace.Text = @"DataLayer." + txtOracleDatabaseName.Text.FirstCharToUpper();
                txtDataNamespace.Text = isCs ? txtDataNamespace.Text.ConvertToValidCSharpNamespace() : txtDataNamespace.Text.ConvertToValidVbNamespace();
                txtBusinessNamespace.Text = @"BusinessLayer." + txtOracleDatabaseName.Text.FirstCharToUpper();
                txtBusinessNamespace.Text = isCs ? txtBusinessNamespace.Text.ConvertToValidCSharpNamespace() : txtBusinessNamespace.Text.ConvertToValidVbNamespace();
            }
            if (sqlServer == "ms access")
            {
                string file;
                try
                {
                    file = Path.GetFileNameWithoutExtension(txtAccessFilename.Text);
                }
                catch
                {
                    file = "";
                }
                txtDataNamespace.Text = @"DataLayer." + file.FirstCharToUpper();
                txtDataNamespace.Text = isCs ? txtDataNamespace.Text.ConvertToValidCSharpNamespace() : txtDataNamespace.Text.ConvertToValidVbNamespace();
                txtBusinessNamespace.Text = @"BusinessLayer." + file.FirstCharToUpper();
                txtBusinessNamespace.Text = isCs ? txtBusinessNamespace.Text.ConvertToValidCSharpNamespace() : txtBusinessNamespace.Text.ConvertToValidVbNamespace();
            }

            if (chkCreateWebApiClasses.Checked)
            {
                chkAspNetCore2.Enabled = true;
                chkAspNetCore2.ForeColor = Color.FromArgb(255, 255, 255);
            }
            else
            {
                chkAspNetCore2.Enabled = true;
                chkAspNetCore2.ForeColor = Color.FromArgb(168, 168, 168);
            }
        }

        private void SetNativeEnabled(bool enabled)
        {
            NativeMethods.SetWindowLong(Handle, GwlStyle, NativeMethods.GetWindowLong(Handle, GwlStyle) &
                ~WsDisabled | (enabled ? 0 : WsDisabled));
        }

        private string GetRegistryValue(string valueName)
        {
            string error = "";
            return RegistryFunctions.RegValue(Microsoft.Win32.RegistryHive.CurrentUser,
                RegistryFunctions.LayerGenKeys.LayerGenSubKeyName, valueName, ref error);
        }

        private void chkCustomNamespaceNames_CheckedChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
        }

        private void txtMySqlDatabaseName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtDatabaseName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void txtOracleDatabaseName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void txtOraclePassword_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void txtOracleUserName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void txtOraclePort_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void chkOracleCustomConnectString_CheckedChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void txtOracleServerName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureOracleConnectionString();
        }

        private void txtAccessFilename_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureAccessConnectionString();
        }

        private void txtSqliteFileName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqliteConnectionString();
        }

        private void txtSqlitePassword_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqliteConnectionString();
        }

        private void ddlLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
        }

        private void chkSqliteCustomConnectionString_CheckedChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqliteConnectionString();
        }

        private void chkSqlServerCustomConnectionString_CheckedChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void chkMySqlCustomConnectionString_CheckedChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtMySqlServerName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtMySqlPort_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtMySqlUserName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtMySqlPassword_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureMySqlConnectionString();
        }

        private void txtSqlPassword_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void txtSqlServerName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void txtSqlUserName_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void txtSqlServerPort_TextChanged(object sender, EventArgs e)
        {
            RefreshNamespaces();
            ConfigureSqlServerConnectionString();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(e.Link.LinkData as string ?? "");
        }

        private void ConfigureSqliteConnectionString()
        {
            if (chkSqliteCustomConnectionString.Checked)
            {
                txtSqliteCustomConnectionString.Enabled = true;
                return;
            }

            txtSqliteCustomConnectionString.Enabled = false;

            try
            {
                var builder = new SQLiteConnectionStringBuilder
                {
                    DataSource = txtSqliteFileName.Text,
                    JournalMode = SQLiteJournalModeEnum.Memory,
                    Password = string.IsNullOrEmpty(txtSqlitePassword.Text.Trim()) ? null : txtSqlitePassword.Text.Trim()
                };

                txtSqliteCustomConnectionString.Text = builder.ConnectionString;
            }
            catch
            {
                txtSqliteCustomConnectionString.Text = "";
            }
        }

        private void ConfigureMySqlConnectionString()
        {
            if (chkMySqlCustomConnectionString.Checked)
            {
                txtMySqlCustomConnectionString.Enabled = true;
                return;
            }

            txtMySqlCustomConnectionString.Enabled = false;

            try
            {
                var builder = new MySqlConnectionStringBuilder
                {
                    UserID = txtMySqlUserName.Text,
                    Password = txtMySqlPassword.Text,
                    Database = txtMySqlDatabaseName.Text,
                    Server = txtMySqlServerName.Text,
                    ConvertZeroDateTime = true,
                    Port = uint.Parse(txtMySqlPort.Text)
                };

                txtMySqlCustomConnectionString.Text = builder.ConnectionString;
            }
            catch
            {
                txtMySqlCustomConnectionString.Text = "";
            }
        }

        private void BindProfileDropDown()
        {
            ddlProfile.DataBindings.Clear();
            ddlProfile.DataSource = null;

            ddlProfile.DataSource = _profiles;
            ddlProfile.DisplayMember = "ProfileName";
            ddlProfile.ValueMember = "ProfileName";
        }

        private void ConfigureOracleConnectionString()
        {
            if (chkOracleCustomConnectString.Checked)
            {
                txtOracleConnectionString.Enabled = true;
                return;
            }

            txtOracleConnectionString.Enabled = false;
#if ORACLE
            try
            {
                var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder();

                builder["Data Source"] = string.Format("(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={0})(PORT={1})))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME={2})))", txtOracleServerName.Text, txtOraclePort.Text, txtOracleDatabaseName.Text);
                builder["User ID"] = txtOracleUserName.Text;
                builder["Password"] = txtOraclePassword.Text;
                
                txtOracleConnectionString.Text = builder.ConnectionString;
            }
            catch
            {
                txtOracleConnectionString.Text = "";
            }
#endif
        }

        private void ConfigureAccessConnectionString()
        {
            if (chkAccessCustomConnectionString.Checked)
            {
                txtAccessConnectionString.Enabled = true;
                return;
            }

            txtAccessConnectionString.Enabled = false;

            try
            {
                var builder = new OleDbConnectionStringBuilder();
                builder["Provider"] = "Microsoft.ACE.OLEDB.12.0";
                builder["Data Source"] = txtAccessFilename.Text;
                if (!string.IsNullOrWhiteSpace(txtAccessPassword.Text))
                    builder["Database Password"] = txtAccessPassword.Text;

                txtAccessConnectionString.Text = builder.ConnectionString;
            }
            catch
            {
                txtAccessConnectionString.Text = "";
            }
        }

        private void ConfigureSqlServerConnectionString()
        {
            if (chkSqlServerCustomConnectionString.Checked)
            {
                txtSqlServerCustomConnectionString.Enabled = true;
                return;
            }

            txtSqlServerCustomConnectionString.Enabled = false;

            try
            {
                var builder = new SqlConnectionStringBuilder();
                builder["Data Source"] = txtSqlServerName.Text + "," + txtSqlServerPort.Text;
                builder["Integrated Security"] = chkSqlTrustedConnection.Checked;
                builder["Initial Catalog"] = txtDatabaseName.Text;
                if (!chkSqlTrustedConnection.Checked)
                {
                    builder["User ID"] = txtSqlUserName.Text;
                    builder["Password"] = txtSqlPassword.Text;
                }

                txtSqlServerCustomConnectionString.Text = builder.ConnectionString;
            }
            catch
            {
                txtSqlServerCustomConnectionString.Text = "";
            }
        }

        private void gbLanguageOptions_Paint(object sender, PaintEventArgs e)
        {
            const int startRow = 92;

            e.Graphics.DrawLine(new Pen(Color.White), 0, startRow, gbLanguageOptions.Width, startRow);
            e.Graphics.DrawLine(new Pen(Color.FromArgb(191, 191, 191)), 0, startRow + 1, gbLanguageOptions.Width, startRow + 1);
        }

        private void ddlProfile_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ddlProfile.SelectedItem == null)
                return;

            var profile = (Profile)ddlProfile.SelectedItem;
            btnDeleteProfile.Visible = true;
            if (profile.ProfileName.ToLower() == "empty" || profile.ProfileName.ToLower() == "last used")
                btnDeleteProfile.Visible = false;
            chkAllowSerialization.Checked = profile.AllowSerialization;
            chkCreateAsyncMethods.Checked = profile.CreateAsyncMethods;
            chkAspNetCore2.Checked = profile.AspNetCore2;
            chkAutomaticallyTrimStrings.Checked = profile.AutomaticallyRightTrimData;
            txtBusinessNamespace.Text = profile.BusinessNameSpace;
            chkCustomNamespaceNames.Checked = profile.CustomNameSpaces;
            txtDataNamespace.Text = profile.DataNameSpace;
            chkEnableDynamicData.Checked = profile.EnableDynamicDataRetrieval;
            chkIncludeComments.Checked = profile.IncludeComments;
            txtMySqlCustomConnectionString.Text = profile.MySqlCustomConnectionString;
            txtMySqlDatabaseName.Text = profile.MySqlDatabaseName;
            txtMySqlPassword.Text = profile.MySqlPassword;
            txtMySqlPort.Text = profile.MySqlPort.ToString();
            txtMySqlServerName.Text = profile.MySqlServerName;
            chkMySqlCustomConnectionString.Checked = profile.MySqlUseCustomConnectionString;
            txtMySqlUserName.Text = profile.MySqlUserName;
            txtOutput.Text = profile.OutputFolder;
            txtPluralizationTemplate.Text = profile.PluralizationTemplate;
            txtSqliteCustomConnectionString.Text = profile.SqliteCustomConnectionString;
            txtSqliteFileName.Text = profile.SqliteFileName;
            txtSqlitePassword.Text = profile.SqlitePassword;
            chkSqliteCustomConnectionString.Checked = profile.SqliteUseCustomConnectionString;
            txtSqlServerCustomConnectionString.Text = profile.SqlServerCustomConnectionString;
            txtDatabaseName.Text = profile.SqlServerDatabaseName;
            txtSqlDefaultSchema.Text = profile.SqlServerDefaultSchema;
            txtSqlPassword.Text = profile.SqlServerPassword;
            txtSqlServerPort.Text = profile.SqlServerPort.ToString();
            txtSqlServerName.Text = profile.SqlServerServerName;
            chkSqlTrustedConnection.Checked = profile.SqlServerTrustedConnection;
            chkCustomNamespaceNames.Checked = profile.SqlServerUseCustomConnectionString;
            txtSqlUserName.Text = profile.SqlServerUserName;
            txtOracleServerName.Text = profile.OracleServerName;
            txtOracleUserName.Text = profile.OracleUserName;
            txtOraclePassword.Text = profile.OraclePassword;
            chkOracleCustomConnectString.Checked = profile.OracleUseCustomConnectionString;
            txtOracleConnectionString.Text = profile.OracleCustomConnectionString;
            txtOracleDefaultSchema.Text = profile.OracleDefaultSchema;
            txtOracleDatabaseName.Text = profile.OracleDatabaseName;
            txtOraclePort.Text = profile.OraclePort.ToString();
            txtAccessConnectionString.Text = profile.AccessCustomConnectionString;
            txtAccessFilename.Text = profile.AccessFileName;
            txtAccessPassword.Text = profile.AccessPassword;
            chkAccessCustomConnectionString.Checked = profile.AccessUseCustomConnectionString;

            switch (profile.Language)
            {
                case DatabasePlugins.Languages.CSharp:
                    ddlLanguage.SelectedItem = "C#";
                    break;
                case DatabasePlugins.Languages.VbNet:
                    ddlLanguage.SelectedItem = "VB.NET";
                    break;
            }

            switch (profile.DatabaseType)
            {
                case DatabasePlugins.DatabaseTypes.MySql:
                    ddlSqlServer.SelectedItem = "MySQL";
                    break;
                case DatabasePlugins.DatabaseTypes.Sqlite:
                    ddlSqlServer.SelectedItem = "SQLite 3";
                    break;
                case DatabasePlugins.DatabaseTypes.SqlServer:
                    ddlSqlServer.SelectedItem = "SQL Server 2000-2014";
                    break;
            }
        }

        private void btnDeleteProfile_Click(object sender, EventArgs e)
        {
            var deleteProfileDialog = new DeleteProfileDialog();

            DialogResult dr = deleteProfileDialog.ShowDialog();

            if (dr == DialogResult.Yes)
            {
                _profiles.RemoveAt(ddlProfile.SelectedIndex);
                ddlProfile.SelectedIndex = 0;
                string path = System.Reflection.Assembly.GetExecutingAssembly().Location;
                path = (Path.GetDirectoryName(path) ?? "").Trim('\\');
                path = path + "\\" + ProfileSettingsFileName;

                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(_profiles.ToXml());
                }

                using (StreamReader sr = File.OpenText(path))
                {
                    _profiles = Profiles.FromXml(sr.ReadToEnd());
                }

                BindProfileDropDown();
            }
        }

        private void btnOracleObjectsBrowse_Click(object sender, EventArgs e)
        {
           var objectExplorer = new ObjectExplorerOracle
            {
                DatabaseName = txtOracleDatabaseName.Text,
                UserName = txtOracleUserName.Text,
                Password = txtOraclePassword.Text,
                DefaultSchema = txtOracleDefaultSchema.Text,
                ServerName = txtOracleServerName.Text,
                ServerPort = uint.Parse(txtOraclePort.Text),
                HasCustomConnectionString = chkOracleCustomConnectString.Checked,
                CustomConnectionString = txtOracleConnectionString.Text.Replace('\r', ' ').Replace('\n', ' ')
            };

            DialogResult dr = objectExplorer.ShowDialog();

            if (dr == DialogResult.OK)
            {
                txtOracleObjects.Text = objectExplorer.SelectedObjects;
            }
        }

        private void chkCreateWebApiClasses_Click(object sender, EventArgs e)
        {
            RefreshNamespaces();
        }
    }
}
