﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LayerGen35
{
    public partial class ObjectExplorerMsAccess : Form
    {
        public string Filename { get; set; }
        public string Password { get; set; }
        public string SelectedObjects { get; private set; }
        public string CustomConnectionString { get; set; }
        public bool HasCustomConnectionString { get; set; }

        private string ConnectionString
        {
            get
            {
                if (HasCustomConnectionString)
                    return CustomConnectionString;

                var builder = new OleDbConnectionStringBuilder();
                builder["Provider"] = "Microsoft.ACE.OLEDB.12.0";
                builder["Data Source"] = Filename;
                if (!string.IsNullOrWhiteSpace(Password))
                    builder["Database Password"] = Password;

                return builder.ConnectionString;
            }
        }
        
        public ObjectExplorerMsAccess()
        {
            InitializeComponent();
        }

        private void ObjectExplorerMsAccess_Load(object sender, EventArgs e)
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

            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();
                using (DataTable tables = connection.GetSchema("Tables"))
                {
                    foreach (DataRow row in tables.Rows.Cast<DataRow>().Where(row => ((string) row["TABLE_TYPE"]).ToLower() == "table"))
                    {
                        var obj = new LgObject
                        {
                            IsView = false,
                            ObjectName = (string)row["TABLE_NAME"]
                        };

                        lgObjects.Add(obj);
                    }

                    foreach (DataRow row in tables.Rows.Cast<DataRow>().Where(row => ((string)row["TABLE_TYPE"]).ToLower() == "view"))
                    {
                        var obj = new LgObject
                        {
                            IsView = true,
                            ObjectName = (string)row["TABLE_NAME"]
                        };

                        lgObjects.Add(obj);
                    }
                }
            }
            return lgObjects;
        }

        private void btnTablesCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, true);
            }
        }

        private void btnTablesDecheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbTables.Items.Count; i++)
            {
                clbTables.SetItemChecked(i, false);
            }
        }

        private void btnViewsCheckAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < clbViews.Items.Count; i++)
            {
                clbViews.SetItemChecked(i, true);
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
