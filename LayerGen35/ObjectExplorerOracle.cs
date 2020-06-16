using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace LayerGen35
{
    public partial class ObjectExplorerOracle : Form
    {
        public string ServerName { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string DatabaseName { get; set; }
        public string DefaultSchema { get; set; }
        public uint ServerPort { get; set; }
        public string CustomConnectionString { get; set; }
        public bool HasCustomConnectionString { get; set; }

        public string SelectedObjects { get; private set; }

        private string ConnectionString
        {
            get
            {
                if (HasCustomConnectionString)
                    return CustomConnectionString;
#if ORACLE
                var builder = new Oracle.ManagedDataAccess.Client.OracleConnectionStringBuilder();

                builder["Data Source"] = string.Format("(DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST={0})(PORT={1})))(CONNECT_DATA=(SERVER=DEDICATED)(SERVICE_NAME={2})))", ServerName, ServerPort, DatabaseName);
                builder["User ID"] = UserName;
                builder["Password"] = Password;

                return builder.ConnectionString;
#else
                return "";
#endif
            }
        }
        public ObjectExplorerOracle()
        {
            InitializeComponent();
        }

        private void ObjectExplorerOracle_Load(object sender, EventArgs e)
        {
            List<LgObject> objectList = GetTablesAndViews();

            foreach (LgObject obj in objectList.OrderBy(z => z.ObjectName))
            {
                if (obj.IsView)
                {
                    clbViews.Items.Add(obj.ObjectName);
                }
                else
                {
                    clbTables.Items.Add(obj.ObjectName);
                }
            }

            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, false);
            }

            for (int i = 0; i < clbViews.Items.Count; i++)
            {
                clbViews.SetItemChecked(i, false);
            }
        }

        private List<LgObject> GetTablesAndViews()
        {
            var lgObjects = new List<LgObject>();
#if ORACLE
            using (var connection = new Oracle.ManagedDataAccess.Client.OracleConnection(ConnectionString))
            {
                connection.Open();
                using (DataTable tables = connection.GetSchema("Tables"))
                {
                    foreach (DataRow row in tables.Rows.Cast<DataRow>())
                    {
                        if (!string.Equals(((string)row["OWNER"]), DefaultSchema, StringComparison.CurrentCultureIgnoreCase))
                            continue;
                        var obj = new LgObject
                        {
                            IsView = false,
                            ObjectName = (string) row["TABLE_NAME"]
                        };

                        lgObjects.Add(obj);
                    }
                }

                using (DataTable views = connection.GetSchema("Views"))
                {
                    foreach (DataRow row in views.Rows)
                    {
                        if (!string.Equals(((string)row["OWNER"]), DefaultSchema, StringComparison.CurrentCultureIgnoreCase))
                            continue;
                        var obj = new LgObject
                        {
                            IsView = true,
                            ObjectName = (string) row["VIEW_NAME"]
                        };

                        lgObjects.Add(obj);
                    }
                }
            }
#endif
            return lgObjects;
        }

        private void btnTablesCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, true);
            }
        }

        private void btnViewsCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbViews.Items.Count; i++)
            {
                clbViews.SetItemChecked(i, true);
            }
        }

        private void btnTablesDecheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, false);
            }
        }

        private void btnViewsDecheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbViews.Items.Count; i++)
            {
                clbViews.SetItemChecked(i, false);
            }
        }

        private void btnDone_Click(object sender, EventArgs e)
        {
            SelectedObjects = "";

            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                if (clbTables.GetItemChecked(i))
                    SelectedObjects = SelectedObjects + clbTables.Items[i] + ";";
            }
            for (int i = 0; i < clbViews.Items.Count; i++)
            {
                if (clbViews.GetItemChecked(i))
                    SelectedObjects = SelectedObjects + clbViews.Items[i] + ";";
            }
            SelectedObjects = SelectedObjects.TrimEnd(';');
            DialogResult = DialogResult.OK;
        }
    }
}
