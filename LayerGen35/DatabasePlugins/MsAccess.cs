using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace LayerGen35.DatabasePlugins
{
    public class MsAccess : IDatabasePlugin
    {
        private delegate void SetTextCallback(int percentage);

        private int _progressNdx;

        /// <summary>
        /// Gets the type of the database.
        /// </summary>
        /// <value>The type of the database.</value>
        public DatabaseTypes DatabaseType
        {
            get { return DatabaseTypes.MsAccess; }
        }

        /// <summary>
        /// Gets or sets the language.
        /// </summary>
        /// <value>The language.</value>
        public Languages Language { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this instance has dynamic data retrieval.
        /// </summary>
        /// <value><c>true</c> if this instance has dynamic data retrieval; otherwise, <c>false</c>.</value>
        public bool HasDynamicDataRetrieval { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to [automatically right trim strings].
        /// </summary>
        /// <value><c>true</c> if [automatic right trim strings]; otherwise, <c>false</c>.</value>
        public bool AutoRightTrimStrings { get; set; }
        public bool CreateAsyncMethods { get; set; }
        public bool CreateWebApiClasses { get; set; }
        public bool AspNetCore2 { get; set; }
        public bool AllowSerialization { get; set; }
        public string DatabaseName { get; set; }
        public string OutputDirectory { get; set; }
        public string DatabaseServer { get; set; }
        public int DatabasePort { get; set; }
        public bool HasCustomConnectionString { get; set; }
        public string Objects { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether this is a [trusted connection].
        /// </summary>
        /// <value><c>true</c> if [trusted connection]; otherwise, <c>false</c>.</value>
        public bool TrustedConnection { get; set; }
        /// <summary>
        /// Gets or sets a value indicating whether to [include comments].
        /// </summary>
        /// <value><c>true</c> if [include comments]; otherwise, <c>false</c>.</value>
        public bool IncludeComments { get; set; }
        /// <summary>
        /// Gets or sets the progress bar to update.
        /// </summary>
        /// <value>The progress bar.</value>
        public ProgressBar ProgressBar { get; set; }
        /// <summary>
        /// Gets or sets the name of the data namespace.
        /// </summary>
        /// <value>The name of the data namespace.</value>
        public string DataNamespaceName { get; set; }
        /// <summary>
        /// Gets or sets the name of the business namespace.
        /// </summary>
        /// <value>The name of the business namespace.</value>
        public string BusinessNamespaceName { get; set; }
        /// <summary>
        /// Gets or sets the custom connection string.
        /// </summary>
        /// <value>The custom connection string.</value>
        public string CustomConnectionString { get; set; }
        /// <summary>
        /// Gets or sets the pluralization template.
        /// </summary>
        /// <value>The pluralization template.</value>
        public string PluralizationTemplate { get; set; }

        private string ConnectionString
        {
            get
            {
                if (HasCustomConnectionString)
                    return CustomConnectionString;

                var builder = new OleDbConnectionStringBuilder();
                builder["Provider"] = "Microsoft.ACE.OLEDB.12.0";
                builder["Data Source"] = DatabaseName;
                if (!string.IsNullOrWhiteSpace(Password))
                    builder["Database Password"] = Password;

                return builder.ConnectionString;
            }
        }

        private void UpdateProgress(int ndx, int total)
        {
            if (ProgressBar == null)
                return;
            int percentage = (int)((double)ndx / total * 100);

            if (ProgressBar.InvokeRequired)
            {
                SetTextCallback updateProgressDelegate = UpdateProgressBar;
                ProgressBar.Invoke(updateProgressDelegate, percentage);
            }
            else
            {
                UpdateProgressBar(percentage);
            }
        }

        private void UpdateProgressBar(int percentage)
        {
            if (ProgressBar == null)
                return;
            ProgressBar.Value = percentage;
        }

        /// <summary>
        /// Creates the layers.
        /// </summary>
        public void CreateLayers()
        {
            _progressNdx = 0;
            UpdateProgress(0, 1);

            if (Language == Languages.CSharp)
            {
                CreateCsDataLayers();
                CreateCsBusinessLayers();
                CreateCsUniversalFile();
            }
            if (Language == Languages.VbNet)
            {
                CreateVbDataLayers();
                CreateVbBusinessLayers();
                CreateVbUniversalFile();
            }
        }

        private void CreateVbDataLayers()
        {
            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var dataLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.DataLayer.MsAccessVbNet.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            dataLayerTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                string objName = objectName.Trim();
                bool isView = IsView(objName);

                List<Field> fields = MapFields(objName);
                if (!HasPrimaryKey(fields) && !isView)
                {
                    continue;
                }

                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Data.vb"))
                {
                    dataLayerTemplate = dataLayerTemplate.Replace("{1}", Common.GetSafeVbName(Common.GetVbPropertyName(objName)));

                    var fieldsPart = new StringBuilder();
                    foreach (var field in fields)
                    {
                        fieldsPart.AppendLine("        Private " + field.SafeVbFieldName + " As " + field.VbDataType);
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{2}", fieldsPart.ToString());

                    dataLayerTemplate = dataLayerTemplate.Replace("{3}", objName);

                    var fieldListPart = new StringBuilder();
                    foreach (var field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        fieldListPart.Append("[" + field.FieldName + "],");
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{4}", fieldListPart.ToString().TrimEnd(','));
                    dataLayerTemplate.Replace("{45}", fieldListPart.ToString().TrimEnd(','));

                    var fieldListAllPart = new StringBuilder();
                    foreach (var field in fields.OrderBy(z => z.FieldName))
                    {
                        fieldListAllPart.Append("[" + field.FieldName + "],");
                    }
                    dataLayerTemplate.Replace("{44}", fieldListAllPart.ToString().TrimEnd(','));

                    var valPart = new StringBuilder();
                    int i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        valPart.Append("@val" + i + ",");
                        i++;
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{5}", valPart.ToString().TrimEnd(','));
                    if (!isView)
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{6}", fields.First(z => z.IsPrimaryKey).FieldName);
                    }

                    var propertiesPart = new StringBuilder();
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        if (!string.IsNullOrEmpty(field.Description))
                        {
                            propertiesPart.Append("        ''' <summary>" + Environment.NewLine);
                            propertiesPart.Append("        ''' " + field.Description + Environment.NewLine);
                            propertiesPart.Append("        ''' </summary>" + Environment.NewLine);
                        }
                        propertiesPart.Append("        Public Overridable Property " + field.SafeVbPropertyName + "() As " + field.VbDataType + Environment.NewLine);
                        propertiesPart.Append("            Get" + Environment.NewLine);
                        propertiesPart.Append("                Return " + field.SafeVbFieldName + Environment.NewLine);
                        propertiesPart.Append("            End Get" + Environment.NewLine);

                        if (field.IsIdentity && (!isView))
                        {
                            propertiesPart.Append("            Protected Set(value As " + field.VbDataType + ")" + Environment.NewLine);
                            propertiesPart.Append("                " + field.SafeVbFieldName + " = value" + Environment.NewLine);
                            propertiesPart.Append("                _layerGenIsDirty = True" + Environment.NewLine);
                            propertiesPart.Append("            End Set" + Environment.NewLine);
                        }
                        else if ((!field.IsComputedField) && (!isView))
                        {
                            propertiesPart.Append("            Set(value As " + field.VbDataType + ")" + Environment.NewLine);
                            propertiesPart.Append("                " + field.SafeVbFieldName + " = value" + Environment.NewLine);
                            propertiesPart.Append("                _layerGenIsDirty = True" + Environment.NewLine);
                            if (field.VbDataType == "DateTime")
                                propertiesPart.Append("                If value = DateTime.MinValue Then SetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ") Else UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                            else if (field.CsDataType == "string")
                            {
                                propertiesPart.AppendLine("                If value Is Nothing Then SetNull(" +
                                                      BusinessNamespaceName + "." +
                                                      Common.GetSafeVbName(Common.GetVbPropertyName(objName)) +
                                                      ".Fields." + field.SafeVbPropertyName + ") Else UnsetNull(" +
                                                      BusinessNamespaceName + "." +
                                                      Common.GetSafeVbName(Common.GetVbPropertyName(objName)) +
                                                      ".Fields." + field.SafeVbPropertyName + ")");
                            }
                            else
                            {
                                propertiesPart.AppendLine("                UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")");
                            }
                            propertiesPart.Append("            End Set" + Environment.NewLine);
                        }

                        if (field.IsComputedField || isView)
                        {
                            propertiesPart.Append("            Protected Set(value As " + field.VbDataType + ")" + Environment.NewLine);
                            propertiesPart.Append("                " + field.SafeVbFieldName + " = value" + Environment.NewLine);
                            propertiesPart.Append("                _layerGenIsDirty = True" + Environment.NewLine);
                            propertiesPart.Append("            End Set" + Environment.NewLine);
                        }
                        propertiesPart.Append("        End Property" + Environment.NewLine + Environment.NewLine);
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{7}", propertiesPart.ToString());

                    if (!isView)
                    {
                        string pkVbName = fields.First(z => z.IsPrimaryKey).SafeVbFieldName;
                        dataLayerTemplate.Replace("{8}", pkVbName);
                        if (fields.First(z => z.IsPrimaryKey).TextBased)
                        {
                            dataLayerTemplate.Replace("{88}", "'\" & " + pkVbName + " & \"'\"");
                        }
                        else
                        {
                            dataLayerTemplate.Replace("{88}", "\" & " + pkVbName);
                        }
                    }

                    //var nullDictionaryPart = new StringBuilder("            _nullDictionary = New Dictionary(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields, Boolean)() From { _" + Environment.NewLine);
                    var nullDictionaryPart = new StringBuilder("            _nullDictionary = New Dictionary(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields, Boolean)()" + Environment.NewLine);
                    foreach (var field in fields)
                    {
                        nullDictionaryPart.AppendLine("            _nullDictionary.Add(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ", True)");
                        //nullDictionaryPart.Append("                {" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ", True}, _" + Environment.NewLine);
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{9}", nullDictionaryPart + Environment.NewLine);

                    //var internalNamePart = new StringBuilder("            _internalNameDictionary = New Dictionary(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields, String)() From { _" + Environment.NewLine);
                    var internalNamePart = new StringBuilder("            _internalNameDictionary = New Dictionary(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields, String)()" + Environment.NewLine);
                    foreach (var field in fields)
                    {
                        internalNamePart.AppendLine("            _internalNameDictionary.Add(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ", \"" + field.FieldName + "\")");
                        //internalNamePart.Append("                {" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ", \"" + field.FieldName + "\"}, _" + Environment.NewLine);
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{10}", internalNamePart + Environment.NewLine);
                    //dataLayerTemplate = dataLayerTemplate.Replace("{10}", internalNamePart.ToString().TrimEnd(Environment.NewLine.ToCharArray()).TrimEnd('_').TrimEnd(' ').TrimEnd(',') + " _" + Environment.NewLine + "            }");

                    var fillPart = new StringBuilder();
                    foreach (Field field in fields)
                    {
                        fillPart.Append("            If HasField(_internalNameDictionary(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + "), dr) Then" + Environment.NewLine);
                        fillPart.Append("                If IsDBNull(dr(_internalNameDictionary(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + "))) Then" + Environment.NewLine);
                        fillPart.Append("                    SetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                        fillPart.Append("                Else" + Environment.NewLine);
                        fillPart.Append("                    " + field.SafeVbPropertyName + " = DirectCast(dr(_internalNameDictionary(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")), " + field.VbDataType + ")" + Environment.NewLine);
                        if (field.TextBased && AutoRightTrimStrings)
                        {
                            fillPart.AppendLine("                    " + field.SafeVbPropertyName + " = " + field.SafeVbPropertyName + ".TrimEnd()");
                        }
                        fillPart.Append("                    UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                        fillPart.Append("                End If" + Environment.NewLine);
                        fillPart.Append("            Else" + Environment.NewLine);
                        fillPart.Append("                _isReadOnly = True" + Environment.NewLine);
                        fillPart.Append("                SetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                        fillPart.Append("            End If" + Environment.NewLine + Environment.NewLine);
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{11}", fillPart.ToString());

                    var getPart = new StringBuilder();
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        if (field.IsPrimaryKey)
                        {
                            getPart.Append("                            " + field.SafeVbPropertyName + " = DirectCast(reader(\"" + field.FieldName + "\"), " + field.VbDataType + ")" + Environment.NewLine);
                            getPart.Append("                            UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                        }
                        else
                        {
                            getPart.Append("                            If (Not HasField(\"" + field.FieldName + "\", reader)) OrElse reader.IsDBNull(reader.GetOrdinal(\"" + field.FieldName + "\")) Then" + Environment.NewLine);
                            getPart.Append("                                SetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                            getPart.Append("                            Else" + Environment.NewLine);
                            getPart.Append("                                " + field.SafeVbPropertyName + " = DirectCast(reader(\"" + field.FieldName + "\"), " + field.VbDataType + ")" + Environment.NewLine);
                            if (field.TextBased && AutoRightTrimStrings)
                            {
                                getPart.AppendLine("                                " + field.SafeVbPropertyName + " = " + field.SafeVbPropertyName + ".TrimEnd()");
                            }
                            getPart.Append("                                UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                            getPart.Append("                            End If" + Environment.NewLine);
                        }
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{12}", getPart.ToString());

                    var resetToDefaultPart = new StringBuilder();
                    foreach (var z in fields)
                    {
                        if (!z.IsIdentity)
                            resetToDefaultPart.Append("            _nullDictionary(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + z.SafeVbPropertyName + ") = True" + Environment.NewLine);
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{13}", resetToDefaultPart.ToString());

                    var savePart = new StringBuilder();

                    i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        savePart.Append("                            If IsNull(" + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Fields." + field.SafeVbPropertyName + ") Then" + Environment.NewLine);
                        savePart.Append("                                Dim param As New OleDbParameter(\"@val" + i + "\", OleDbType." + field.SqlDbType + ")" + Environment.NewLine);
                        if (field.CanBeNull)
                            savePart.Append("                                param.Value = DBNull.Value" + Environment.NewLine);
                        else savePart.Append("                                param.Value = " + GetDefaultVbValue(field) + Environment.NewLine);
                        savePart.Append("                                command.Parameters.Add(param)" + Environment.NewLine);
                        savePart.Append("                            Else" + Environment.NewLine);
                        savePart.Append("                                Dim param As New OleDbParameter(\"@val" + i + "\", OleDbType." + field.SqlDbType + ")" + Environment.NewLine);
                        savePart.Append("                                param.Value = " + field.SafeVbFieldName + Environment.NewLine);
                        savePart.Append("                                command.Parameters.Add(param)" + Environment.NewLine);
                        savePart.Append("                            End If" + Environment.NewLine);
                        i++;
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{14}", savePart.ToString());

                    var str1 = new StringBuilder("                            ");

                    if (!isView)
                    {
                        Field f = fields.First(z => z.IsPrimaryKey);
                        if (f.IsIdentity && Common.GetVbConversionFunction(f.VbDataType) == "CType")
                        {
                            str1.Append(f.SafeVbFieldName + " = " + Common.GetVbConversionFunction(f.VbDataType) + "(obj, " + f.VbDataType + ")" + Environment.NewLine);
                        }
                        else if (f.IsIdentity)
                        {
                            str1.Append(f.SafeVbFieldName + " = " + Common.GetVbConversionFunction(f.VbDataType) + "(obj)" + Environment.NewLine);
                        }
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{15}", str1.ToString());

                    str1.Clear();
                    str1.Append("                    Const cmdString As String = \"UPDATE [\" & LayerGenTableName & \"] SET ");
                    i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        str1.Append("[" + field.FieldName + "]=@val" + i + ",");
                        i++;
                    }
                    dataLayerTemplate = dataLayerTemplate.Replace("{16}", str1.ToString().TrimEnd(',') + " WHERE \" & LayerGenPrimaryKey & \"=@val" + i + "\"" + Environment.NewLine);

                    dataLayerTemplate = dataLayerTemplate.Replace("{17}", "                            command.Parameters.AddWithValue(\"@val" + i + "\", _oldPrimaryKeyValue)" + Environment.NewLine);
                    if (!isView)
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{18}", "        Private _oldPrimaryKeyValue As " + fields.First(z => z.IsPrimaryKey).VbDataType + Environment.NewLine);
                        dataLayerTemplate = dataLayerTemplate.Replace("{19}", fields.First(z => z.IsPrimaryKey).VbDataType);

                        if (fields.First(z => z.IsPrimaryKey).TextBased || fields.First(z => z.IsPrimaryKey).CsDataType.ToLower() == "guid")
                        {
                            dataLayerTemplate = dataLayerTemplate.Replace("{20}", "            Dim sql As String = \"SELECT \" & strFields & \" FROM [\" & LayerGenTableName & \"] WHERE \" & LayerGenPrimaryKey & \"=@val1\"" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "                    command.Parameters.AddWithValue(\"@val1\", id)");
                        }
                        else
                        {
                            dataLayerTemplate = dataLayerTemplate.Replace("{20}", "            Dim sql As String = \"SELECT \" & strFields & \" FROM [\" & LayerGenTableName & \"] WHERE \" & LayerGenPrimaryKey & \"=\" & id" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "");
                        }
                    }

                    var fkDataGetBy = new StringBuilder();
                    if (!isView)
                    {
                        var fkProperties = new StringBuilder();
                        var fkFields = new StringBuilder();
                        string sqlDataType = "";

                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkFields.Append("        Private _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " As " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + Environment.NewLine);

                            fkProperties.Append("        Public ReadOnly Property F" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "() As " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + Environment.NewLine);
                            fkProperties.Append("            Get" + Environment.NewLine);
                            fkProperties.Append("                If _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " Is Nothing Then" + Environment.NewLine);
                            fkProperties.Append("                    _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " = New " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + "(" + Common.GetVbFieldName(key.ForeignColumnName) + ")" + Environment.NewLine);
                            fkProperties.Append("                End If" + Environment.NewLine);
                            fkProperties.Append("                Return _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + Environment.NewLine);
                            fkProperties.Append("            End Get" + Environment.NewLine);
                            fkProperties.Append("        End Property" + Environment.NewLine + Environment.NewLine);

                            fkDataGetBy.Append("        Friend Shared Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "(fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetBy.Append(f.VbDataType + ") As DataTable" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetBy.Append("            Using connection As New OleDbConnection()" + Environment.NewLine);
                            fkDataGetBy.Append("                connection.ConnectionString = Universal.GetConnectionString()" + Environment.NewLine);
                            fkDataGetBy.Append("                Using command As New OleDbCommand()" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandText = \"SELECT * FROM [" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetBy.AppendLine("                    Dim parameter As New OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ")");
                            fkDataGetBy.AppendLine("                    parameter.Value = fkId");
                            fkDataGetBy.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetBy.Append("                    command.Parameters.Add(New OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetBy.Append("                        .Value = fkId _" + Environment.NewLine);
                            //fkDataGetBy.Append("                    })" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                    connection.Open()" + Environment.NewLine);
                            fkDataGetBy.Append("                    Using adapter As New OleDbDataAdapter()" + Environment.NewLine);
                            fkDataGetBy.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetBy.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetBy.Append("                            adapter.Fill(ds)" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetBy.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetBy.Append("                            End If" + Environment.NewLine);
                            fkDataGetBy.Append("                        End Using" + Environment.NewLine);
                            fkDataGetBy.Append("                    End Using" + Environment.NewLine);
                            fkDataGetBy.Append("                End Using" + Environment.NewLine);
                            fkDataGetBy.Append("            End Using" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetBy.Append("        End Function" + Environment.NewLine);
                        }

                        dataLayerTemplate = dataLayerTemplate.Replace("{22}", fkFields.ToString());
                        dataLayerTemplate = dataLayerTemplate.Replace("{21}", fkProperties.ToString());
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{23}", fkDataGetBy.ToString());

                    if (isView)
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{18}", "");
                        dataLayerTemplate = dataLayerTemplate.Replace("{21}", "");
                        dataLayerTemplate = dataLayerTemplate.Replace("{22}", "");
                        RemoveTemplateComments(ref dataLayerTemplate);
                    }
                    else
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{/*}", "");
                        dataLayerTemplate = dataLayerTemplate.Replace("{*/}", "");
                    }

                    var equalPart = new StringBuilder();
                    if (!isView)
                    {
                        Field primaryKeyField = fields.First(z => z.IsPrimaryKey);
                        dataLayerTemplate.Replace("{25}", primaryKeyField.SafeVbPropertyName);
                    }
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        equalPart.Append("            Dim cls" + field.SafeVbFieldName.Remove(0, 1) + " As Byte() = ObjectToByteArray(cls." + field.SafeVbPropertyName + ")" + Environment.NewLine);
                    }
                    equalPart.Append(Environment.NewLine);

                    var tmpEqual = new StringBuilder();

                    equalPart.Append("            Dim clsArray As Byte() = New Byte(");

                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        tmpEqual.Append("cls" + field.SafeVbFieldName.Remove(0, 1) + ".Length + ");
                        if (tmpEqual.Length >= 110)
                        {
                            equalPart.Append(tmpEqual + " _ " + Environment.NewLine + "                       ");
                            tmpEqual.Clear();
                        }
                    }
                    equalPart.Append(tmpEqual);
                    equalPart.ReplaceAllText(equalPart.ToString().TrimEnd(' ', '\r', '\n', '+', '_'));
                    equalPart.Append(" - 1) {}" + Environment.NewLine);

                    tmpEqual.Clear();
                    tmpEqual.Append("0");

                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        equalPart.Append("            Array.Copy(cls" + field.SafeVbFieldName.Remove(0, 1));
                        equalPart.Append(", 0, clsArray, " + tmpEqual + ", cls" + field.SafeVbFieldName.Remove(0, 1) + ".Length)" + Environment.NewLine);
                        tmpEqual.Append(" + cls" + field.SafeVbFieldName.Remove(0, 1) + ".Length");
                        int teLength = tmpEqual.ToString().Split(Environment.NewLine.ToCharArray()).Length;
                        if (teLength > 0)
                        {
                            string te = tmpEqual.ToString().Split(Environment.NewLine.ToCharArray())[teLength - 1];
                            if (te.Length >= 85)
                            {
                                tmpEqual.Append(" _ " + Environment.NewLine + "                        ");
                            }
                        }
                    }

                    equalPart.Append(Environment.NewLine);
                    equalPart.Append("            Return clsArray" + Environment.NewLine);

                    dataLayerTemplate.Replace("{24}", equalPart.ToString());
                    dataLayerTemplate.Replace("{26}", DataNamespaceName);
                    dataLayerTemplate.Replace("{27}", BusinessNamespaceName);
                    dataLayerTemplate.Replace("{29}", ((!isView) && (fields.First(z => z.IsPrimaryKey).VbDataType.ToLower()) == "guid") ? ".ToString()" : "");

                    var serializationCode = new StringBuilder();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        <Serializable> _");
                        serializationCode.AppendLine("        Public Class " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)));

                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                                serializationCode.AppendLine("            Private " + field.SafeVbFieldName + " As System.Nullable(Of " + field.VbDataType + ")");
                            else
                                serializationCode.AppendLine("            Private " + field.SafeVbFieldName + " As " + field.VbDataType);
                        }
                        serializationCode.AppendLine("            Private _serializationIsUpdate As Boolean");
                        serializationCode.AppendLine("            Private _serializationConnectionString As String");
                        serializationCode.AppendLine();

                        foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                                serializationCode.AppendLine("            Public Property " + field.SafeVbPropertyName + "() As System.Nullable(Of " + field.VbDataType + ")");
                            else
                                serializationCode.AppendLine("            Public Property " + field.SafeVbPropertyName + "() As " + field.VbDataType);
                            serializationCode.AppendLine("                Get");
                            serializationCode.AppendLine("                    Return " + field.SafeVbFieldName);
                            serializationCode.AppendLine("                End Get");
                            if (field.IsValueType)
                                serializationCode.AppendLine("                Set(value As System.Nullable(Of " + field.VbDataType + "))");
                            else
                                serializationCode.AppendLine("                Set(value As " + field.VbDataType + ")");
                            serializationCode.AppendLine("                    " + field.SafeVbFieldName + " = value");
                            serializationCode.AppendLine("                End Set");
                            serializationCode.AppendLine("            End Property");
                        }
                        serializationCode.AppendLine("            ''' <summary>");
                        serializationCode.AppendLine("            ''' Set this to true if <see cref=\"Save()\"></see> should do an update.");
                        serializationCode.AppendLine("            ''' Otherwise, set to false to force <see cref=\"Save()\"></see> to do an insert.");
                        serializationCode.AppendLine("            ''' </summary>");
                        serializationCode.AppendLine("            Public Property SerializationIsUpdate() As Boolean");
                        serializationCode.AppendLine("                Get");
                        serializationCode.AppendLine("                    Return _serializationIsUpdate");
                        serializationCode.AppendLine("                End Get");
                        serializationCode.AppendLine("                Set(value As Boolean)");
                        serializationCode.AppendLine("                    _serializationIsUpdate = value");
                        serializationCode.AppendLine("                End Set");
                        serializationCode.AppendLine("            End Property");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            ''' <summary>");
                        serializationCode.AppendLine("            ''' The connection string used to connect to the database.");
                        serializationCode.AppendLine("            ''' </summary>");
                        serializationCode.AppendLine("            Public Property SerializationConnectionString() As String");
                        serializationCode.AppendLine("                Get");
                        serializationCode.AppendLine("                    Return _serializationConnectionString");
                        serializationCode.AppendLine("                End Get");
                        serializationCode.AppendLine("                Set(value As String)");
                        serializationCode.AppendLine("                    _serializationConnectionString = value");
                        serializationCode.AppendLine("                End Set");
                        serializationCode.AppendLine("            End Property");
                        serializationCode.AppendLine("        End Class");
                    }

                    dataLayerTemplate.Replace("{32}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Converts an instance of an object to a string format");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"format\">Specifies if it should convert to XML, BSON or JSON</param>");
                        serializationCode.AppendLine("        ''' <returns>The object, converted to a string representation</returns>");
                        serializationCode.AppendLine("        Public Function ToString(format As " + BusinessNamespaceName + ".SerializationFormats) As String");
                        serializationCode.AppendLine("            Dim " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + " As New " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + "()");
                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            If IsNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ") Then");
                                serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = DirectCast(Nothing, System.Nullable(Of " + field.VbDataType + "))");
                                serializationCode.AppendLine("            Else");
                                serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = " + field.SafeVbFieldName);
                                serializationCode.AppendLine("            End If");
                                //serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = If(IsNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + "), DirectCast(Nothing, System.Nullable(Of " + field.VbDataType + ")), " + field.SafeVbFieldName + ")");
                            }
                            else if (field.CsDataType == "string")
                            {
                                serializationCode.AppendLine("            If IsNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ") Then");
                                serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = Nothing");
                                serializationCode.AppendLine("            Else");
                                serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = " + field.SafeVbFieldName);
                                serializationCode.AppendLine("            End If");
                                //serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = If(IsNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + "), Nothing, " + field.SafeVbFieldName + ")");
                            }
                            else
                            {
                                serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = " + field.SafeVbFieldName);
                            }
                        }
                        serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ".SerializationIsUpdate = _layerGenIsUpdate");
                        serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ".SerializationConnectionString = _connectionString");
                        serializationCode.AppendLine("            If format = " + BusinessNamespaceName + ".SerializationFormats.Json Then");
                        serializationCode.AppendLine("                Return Newtonsoft.Json.JsonConvert.SerializeObject(" + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ")");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If format = " + BusinessNamespaceName + ".SerializationFormats.Xml Then");
                        serializationCode.AppendLine("                Dim xType As New System.Xml.Serialization.XmlSerializer(" + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ".GetType())");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                Using sw As New StringWriter()");
                        serializationCode.AppendLine("                    xType.Serialize(sw, " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ")");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                    Return sw.ToString()");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If format = " + BusinessNamespaceName + ".SerializationFormats.BsonBase64 Then");
                        serializationCode.AppendLine("                Using ms As New System.IO.MemoryStream");
                        serializationCode.AppendLine("                    Using writer As New Newtonsoft.Json.Bson.BsonWriter(ms)");
                        serializationCode.AppendLine("                        Dim serializer As New Newtonsoft.Json.JsonSerializer()");
                        serializationCode.AppendLine("                        serializer.Serialize(writer, " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ")");
                        serializationCode.AppendLine("                    End Using");
                        serializationCode.AppendLine("                    Return Convert.ToBase64String(ms.ToArray())");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Return \"\"");
                        serializationCode.AppendLine("        End Function");
                    }

                    dataLayerTemplate.Replace("{33}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        Protected Shared Function BsonTo" + Common.GetVbPropertyName(objName) + "(bson As String) As " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Dim z As " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)));
                        serializationCode.AppendLine("            Dim data As Byte() = Convert.FromBase64String(bson)");
                        serializationCode.AppendLine("            Using ms As New System.IO.MemoryStream(data)");
                        serializationCode.AppendLine("                Using reader As New Newtonsoft.Json.Bson.BsonReader(ms)");
                        serializationCode.AppendLine("                    Dim serializer As New Newtonsoft.Json.JsonSerializer");
                        serializationCode.AppendLine("                    z = serializer.Deserialize(Of " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + ")(reader)");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End Using");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim tmp As New " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + "()");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + ".HasValue Then");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName + ".Value");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                            else
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + " Is Nothing Then");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName);
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim decryptor As New " + BusinessNamespaceName + ".Encryption64()");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = True");
                        serializationCode.AppendLine("            Return tmp");

                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        Protected Shared Function XmlTo" + Common.GetVbPropertyName(objName) + "(xml As String) As " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Dim xType As New System.Xml.Serialization.XmlSerializer(GetType(" + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + "))");
                        serializationCode.AppendLine("            Dim z As " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)));
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Using sr As New StringReader(xml)");
                        serializationCode.AppendLine("                z = DirectCast(xType.Deserialize(sr), " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + ")");
                        serializationCode.AppendLine("            End Using");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim tmp As New " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + "()");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + ".HasValue Then");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName + ".Value");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                            else
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + " Is Nothing Then");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName);
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim decryptor As New " + BusinessNamespaceName + ".Encryption64()");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = True");
                        serializationCode.AppendLine("            Return tmp");

                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        Protected Shared Function JsonTo" + Common.GetVbPropertyName(objName) + "(json As String) As " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Dim z As " + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + " = Newtonsoft.Json.JsonConvert.DeserializeObject(Of Serializable" + Common.GetVbPropertyName(objName) + ")(json)");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim tmp As New " + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + "()");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + ".HasValue Then");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName + ".Value");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                            else
                            {
                                serializationCode.AppendLine("            If z." + field.SafeVbPropertyName + " Is Nothing Then");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            Else ");
                                serializationCode.AppendLine("                tmp." + field.SafeVbFieldName + " = z." + field.SafeVbPropertyName);
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Fields." + field.SafeVbPropertyName + ")");
                                serializationCode.AppendLine("            End If");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Dim decryptor As New " + BusinessNamespaceName + ".Encryption64()");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = True");
                        serializationCode.AppendLine("            Return tmp");

                        serializationCode.AppendLine("        End Function");
                    }

                    dataLayerTemplate.Replace("{34}", serializationCode.ToString());

                    Common.DoComments(ref dataLayerTemplate, "'", IncludeComments);
                    sw.Write(dataLayerTemplate);
                }
            }
        }

        private void CreateVbBusinessLayers()
        {
            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var businessLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.BusinessLayer.MsAccessVbNet.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            businessLayerTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                string objName = objectName.Trim();
                List<Field> fields = MapFields(objName);
                bool isView = IsView(objName);
                if (!HasPrimaryKey(fields) && !isView)
                {
                    continue;
                }

                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Business.vb"))
                {
                    var enumsPart = new StringBuilder();
                    foreach (Field field in fields)
                    {
                        if (!string.IsNullOrEmpty(field.Description))
                        {
                            enumsPart.Append("            ''' <summary>" + Environment.NewLine);
                            enumsPart.Append("            ''' " + field.Description + Environment.NewLine);
                            enumsPart.Append("            ''' </summary>" + Environment.NewLine);
                        }
                        enumsPart.Append("            " + field.SafeVbPropertyName + Environment.NewLine);
                    }
                    businessLayerTemplate.Replace("{3}", enumsPart.ToString());

                    businessLayerTemplate.Replace("{0}", Common.GetSafeVbName(Common.GetVbPropertyName(objName)));
                    businessLayerTemplate.Replace("{35}", Common.GetSafeVbName(Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate)));
                    businessLayerTemplate.Replace("{99}", objName);
                    if (!isView)
                    {
                        businessLayerTemplate.Replace("{1}", fields.First(z => z.IsPrimaryKey).VbDataType);
                    }

                    if (isView)
                    {
                        RemoveTemplateComments(ref businessLayerTemplate);
                    }
                    else
                    {
                        businessLayerTemplate.Replace("{/*}", "");
                        businessLayerTemplate.Replace("{*/}", "");
                    }

                    var fkBusinessGetBy = new StringBuilder();
                    if (!isView)
                    {
                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkBusinessGetBy.Append("        Public Sub GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "(fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    fkBusinessGetBy.Append(f.VbDataType + ")" + Environment.NewLine);
                                    break;
                                }
                            }

                            fkBusinessGetBy.Append("            Dim dt As DataTable = " + DataNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "(fkId)" + Environment.NewLine);
                            fkBusinessGetBy.Append("            If dt IsNot Nothing Then" + Environment.NewLine);
                            fkBusinessGetBy.Append("                Load(dt.Rows)" + Environment.NewLine);
                            fkBusinessGetBy.Append("            End If" + Environment.NewLine);
                            fkBusinessGetBy.Append("        End Sub" + Environment.NewLine);
                        }
                    }

                    businessLayerTemplate.Replace("{2}", fkBusinessGetBy.ToString());

                    businessLayerTemplate.Replace("{26}", DataNamespaceName);
                    businessLayerTemplate.Replace("{27}", BusinessNamespaceName);

                    var serializationCode = new StringBuilder();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " from a base64 encoded BSON string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"bson\">The base64 encoded BSON string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromBson(bson As String) As " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate));
                        serializationCode.AppendLine("            Dim zc As List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + ")");
                        serializationCode.AppendLine("            Dim data As Byte() = Convert.FromBase64String(bson)");
                        serializationCode.AppendLine("            Dim tmp As New " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + "()");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Using ms As New System.IO.MemoryStream(data)");
                        serializationCode.AppendLine("                Using reader As New Newtonsoft.Json.Bson.BsonReader(ms)");
                        serializationCode.AppendLine("                    reader.ReadRootValueAsArray = True");
                        serializationCode.AppendLine("                    Dim serializer As New Newtonsoft.Json.JsonSerializer()");
                        serializationCode.AppendLine("                    zc = serializer.Deserialize(Of List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + "))(reader)");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End Using");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            For Each z As " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + " In zc");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetVbPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)))");
                        serializationCode.AppendLine("            Next");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If zc.Count > 0 Then");
                        serializationCode.AppendLine("                Dim decryptor As New Encryption64()");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zc(0).SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Return tmp");
                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " from an XML string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"xml\">The XML string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromXml(xml As String) As " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate));
                        serializationCode.AppendLine("            Dim xType As New System.Xml.Serialization.XmlSerializer(GetType(List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + ")))");
                        serializationCode.AppendLine("            Dim zc As List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + ")");
                        serializationCode.AppendLine("            Dim tmp As New " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + "()");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Using sr As New System.IO.StringReader(xml)");
                        serializationCode.AppendLine("                zc = DirectCast(xType.Deserialize(sr), List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + "))");
                        serializationCode.AppendLine("            End Using");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            For Each z As " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + " In zc");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetVbPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)))");
                        serializationCode.AppendLine("            Next");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If zc.Count > 0 Then");
                        serializationCode.AppendLine("                Dim decryptor As New Encryption64()");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zc(0).SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Return tmp");
                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " from a JSON string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"json\">The JSON string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromJson(json As String) As " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate));
                        serializationCode.AppendLine("            Dim zs As List(Of " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + ") = Newtonsoft.Json.JsonConvert.DeserializeObject(Of List(Of " + DataNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".Serializable" + Common.GetVbPropertyName(objName) + "))(json)");
                        serializationCode.AppendLine("            Dim tmp As New " + Common.GetPluralizedName(Common.GetVbPropertyName(objName), PluralizationTemplate) + "()");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            For Each z As " + DataNamespaceName + "." + Common.GetVbPropertyName(objName) + ".Serializable" + Common.GetVbPropertyName(objName) + " In zs");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetVbPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)))");
                        serializationCode.AppendLine("            Next");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If zs.Count > 0 Then");
                        serializationCode.AppendLine("                Dim decryptor As New Encryption64()");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zs(0).SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Return tmp");
                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Converts an instance of an object to a string format");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"format\">Specifies if it should convert to XML or JSON</param>");
                        serializationCode.AppendLine("        ''' <returns>The object, converted to a string representation</returns>");
                        serializationCode.AppendLine("        Public Function ToString(format As SerializationFormats) As String");
                        serializationCode.AppendLine("            Dim encryptor As new Encryption64()");
                        serializationCode.AppendLine("            Dim zs As New List(Of " + DataNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + "." + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + ")()");
                        serializationCode.AppendLine("            For Each z As " + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + " In Me");
                        serializationCode.AppendLine("                Dim " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + " As new " + DataNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + "." + Common.GetSafeVbName("Serializable" + Common.GetVbPropertyName(objName)) + "()");
                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("                If z.IsNull(" + Common.GetSafeVbName(objName) + ".Fields." + field.SafeVbPropertyName + ") Then");
                                serializationCode.AppendLine("                    " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = DirectCast(Nothing, System.Nullable(Of " + field.VbDataType + "))");
                                serializationCode.AppendLine("                Else");
                                serializationCode.AppendLine("                    " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = z." + field.SafeVbPropertyName);
                                serializationCode.AppendLine("                End If");
                                //serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = If(z.IsNull(" + Common.GetSafeVbName(objName) + ".Fields." + field.SafeVbPropertyName + "), DirectCast(Nothing, System.Nullable(Of " + field.VbDataType + ")), z." + field.SafeVbPropertyName + ")");
                            }
                            else if (field.CsDataType == "string")
                            {
                                serializationCode.AppendLine("                If z.IsNull(" + Common.GetSafeVbName(objName) + ".Fields." + field.SafeVbPropertyName + ") Then");
                                serializationCode.AppendLine("                    " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = Nothing");
                                serializationCode.AppendLine("                Else");
                                serializationCode.AppendLine("                    " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = z." + field.SafeVbPropertyName);
                                serializationCode.AppendLine("                End If");
                                //serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = If(z.IsNull(" + Common.GetSafeVbName(objName) + ".Fields." + field.SafeVbPropertyName + "), Nothing, z." + field.SafeVbPropertyName + ")");
                            }
                            else
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + "." + field.SafeVbPropertyName + " = z." + field.SafeVbPropertyName);
                            }
                        }
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetVbPropertyName(objName)) + ".SerializationIsUpdate = z.LayerGenIsUpdate()");
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationConnectionString = encryptor.Encrypt(z.LayerGenConnectionString(), " + DataNamespaceName + ".Universal.LayerGenEncryptionKey)");
                        serializationCode.AppendLine("                zs.Add(" + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ")");
                        serializationCode.AppendLine("            Next");
                        serializationCode.AppendLine("            ");
                        serializationCode.AppendLine("            If format = SerializationFormats.Json Then");
                        serializationCode.AppendLine("                Return Newtonsoft.Json.JsonConvert.SerializeObject(zs)");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If format = SerializationFormats.Xml Then");
                        serializationCode.AppendLine("                Dim xType As New System.Xml.Serialization.XmlSerializer(zs.GetType())");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                Using sw As New System.IO.StringWriter()");
                        serializationCode.AppendLine("                    xType.Serialize(sw, zs)");
                        serializationCode.AppendLine("                    Return sw.ToString()");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            If format = SerializationFormats.BsonBase64 Then");
                        serializationCode.AppendLine("                Using ms As New System.IO.MemoryStream");
                        serializationCode.AppendLine("                    Using writer As New Newtonsoft.Json.Bson.BsonWriter(ms)");
                        serializationCode.AppendLine("                        Dim serializer As New Newtonsoft.Json.JsonSerializer()");
                        serializationCode.AppendLine("                        serializer.Serialize(writer, zs)");
                        serializationCode.AppendLine("                    End Using");
                        serializationCode.AppendLine("                    Return Convert.ToBase64String(ms.ToArray())");
                        serializationCode.AppendLine("                End Using");
                        serializationCode.AppendLine("            End If");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            Return \"\"");
                        serializationCode.AppendLine("        End Function");
                    }

                    businessLayerTemplate.Replace("{33}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetVbPropertyName(objName) + " from a JSON string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"json\">The JSON string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetVbPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromJson(json As String) As " + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Return JsonTo" + Common.GetVbPropertyName(objName) + "(json)");
                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetVbPropertyName(objName) + " from an XML string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"xml\">The XML string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetVbPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromXml(xml As String) As " + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Return XmlTo" + Common.GetVbPropertyName(objName) + "(xml)");
                        serializationCode.AppendLine("        End Function");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        ''' <summary>");
                        serializationCode.AppendLine("        ''' Creates an instance of " + Common.GetVbPropertyName(objName) + " from a Base64 Encoded BSON string");
                        serializationCode.AppendLine("        ''' </summary>");
                        serializationCode.AppendLine("        ''' <param name=\"bson\">The Base64 Encoded BSON string</param>");
                        serializationCode.AppendLine("        ''' <returns>A " + Common.GetVbPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        Public Shared Function FromBson(bson As String) As " + Common.GetVbPropertyName(objName));
                        serializationCode.AppendLine("            Return BsonTo" + Common.GetVbPropertyName(objName) + "(bson)");
                        serializationCode.AppendLine("        End Function");
                    }

                    businessLayerTemplate.Replace("{34}", serializationCode.ToString());

                    Common.DoComments(ref businessLayerTemplate, "'", IncludeComments);
                    sw.Write(businessLayerTemplate.ToString());
                }
            }
        }

        private void CreateVbUniversalFile()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var universal1Template = new StringBuilder();
            var universal2Template = new StringBuilder();

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal1MsAccessVb.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal1Template.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal2MsAccessVb.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal2Template.Append(reader.ReadToEnd());
                    }
                }
            }

            universal2Template.Replace("{0}", DataNamespaceName);

            using (StreamWriter sw = File.CreateText(OutputDirectory + "\\Universal.vb"))
            {
                sw.WriteLine("Option Strict On");
                sw.WriteLine("Option Explicit On");
                sw.WriteLine();
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine("Imports System.Collections.Generic");
                    sw.WriteLine("Imports System.Data");
                    sw.WriteLine("Imports System.Data.SqlClient");
                    sw.WriteLine("Imports System.Dynamic");
                    sw.WriteLine("Imports System.Linq");
                    sw.WriteLine("Imports System.Reflection");
                    sw.WriteLine();
                }
                sw.WriteLine("Namespace " + DataNamespaceName);
                sw.WriteLine("    Friend NotInheritable Class Universal");
                sw.WriteLine("        Private Sub New()");
                sw.WriteLine("        End Sub");
                sw.WriteLine("        ''' <summary>");
                sw.WriteLine("        ''' The encryption key that LayerGen uses internally to encrypt data.");
                sw.WriteLine("        ''' </summary>");
                sw.WriteLine("        Public Shared LayerGenEncryptionKey As String = \"L@y3rG3n\"");
                sw.WriteLine();
                sw.WriteLine("        ''' <summary>");
                sw.WriteLine("        ''' Gets the connection string to connect to the database");
                sw.WriteLine("        ''' </summary>");
                sw.WriteLine("        ''' <returns>A string containing the connection string</returns>");
                sw.WriteLine("        Friend Shared Function GetConnectionString() As String");
                sw.WriteLine("            ' If this is an ASP.NET application, you can use a line like the following to pull");
                sw.WriteLine("            ' the connection string from the Web.Config:");
                sw.WriteLine("            ' Return System.Configuration.ConfigurationManager.ConnectionStrings(\"MyConnectionString\").ConnectionString");
                sw.WriteLine();
                sw.WriteLine("            Return \"" + ConnectionString.Replace("\"", "\"\"") + "\"");
                sw.WriteLine("        End Function");
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine();
                    sw.WriteLine(universal1Template.ToString());
                    sw.WriteLine();
                }
                sw.WriteLine("    End Class");

                sw.WriteLine("End Namespace");
                sw.WriteLine();
                sw.WriteLine("Namespace " + BusinessNamespaceName);
                if (AllowSerialization)
                {
                    sw.WriteLine();
                    sw.WriteLine("    ''' <summary>");
                    sw.WriteLine("    ''' Enumeration of various serialization formats");
                    sw.WriteLine("    ''' </summary>");
                    sw.WriteLine("    Public Enum SerializationFormats");
                    sw.WriteLine("        ''' <summary>");
                    sw.WriteLine("        ''' JSON format");
                    sw.WriteLine("        ''' </summary>");
                    sw.WriteLine("        Json = 1");
                    sw.WriteLine("        ''' <summary>");
                    sw.WriteLine("        ''' XML format");
                    sw.WriteLine("        ''' </summary>");
                    sw.WriteLine("        Xml = 2");
                    sw.WriteLine("        ''' <summary>");
                    sw.WriteLine("        ''' Base 64 encoded BSON format");
                    sw.WriteLine("        ''' </summary>");
                    sw.WriteLine("        BsonBase64 = 3");
                    sw.WriteLine("    End Enum");
                }
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine();
                    sw.WriteLine(universal2Template.ToString());
                    sw.WriteLine();
                }
                sw.WriteLine("    ''' <summary>");
                sw.WriteLine("    ''' The exception that is thrown when data in memory is out of sync with data in the database.");
                sw.WriteLine("    ''' </summary>");
                sw.WriteLine("    Public Class OutOfSyncException");
                sw.WriteLine("        Inherits System.Exception");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New()");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String)");
                sw.WriteLine("            MyBase.New(message)");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String, inner As System.Exception)");
                sw.WriteLine("            MyBase.New(message, inner)");
                sw.WriteLine("        End Sub");
                sw.WriteLine("    End Class");
                sw.WriteLine();
                sw.WriteLine("    ''' <summary>");
                sw.WriteLine("    ''' The exception that is thrown when data in memory is in a read only state.");
                sw.WriteLine("    ''' </summary>");
                sw.WriteLine("    Public Class ReadOnlyException");
                sw.WriteLine("        Inherits System.Exception");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New()");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String)");
                sw.WriteLine("            MyBase.New(message)");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String, inner As System.Exception)");
                sw.WriteLine("            MyBase.New(message, inner)");
                sw.WriteLine("        End Sub");
                sw.WriteLine("    End Class");
                sw.WriteLine();
                sw.WriteLine("    ''' <summary>");
                sw.WriteLine("    ''' The exception that is thrown when trying to read from a row that doesn't exist.");
                sw.WriteLine("    ''' </summary>");
                sw.WriteLine("    Public Class RowNotFoundException");
                sw.WriteLine("        Inherits System.Exception");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New()");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String)");
                sw.WriteLine("            MyBase.New(message)");
                sw.WriteLine("        End Sub");
                sw.WriteLine();
                sw.WriteLine("        Public Sub New(message As String, inner As System.Exception)");
                sw.WriteLine("            MyBase.New(message, inner)");
                sw.WriteLine("        End Sub");
                sw.WriteLine("    End Class");
                sw.WriteLine("    Public Class LayerGenConnectionString");
                sw.WriteLine("        Dim _connString As String = \"\"");
                sw.WriteLine("        Public Property ConnectionString() As String");
                sw.WriteLine("            Get");
                sw.WriteLine("                Return _connString");
                sw.WriteLine("            End Get");
                sw.WriteLine("            Set(ByVal value As String)");
                sw.WriteLine("                _connString = value");
                sw.WriteLine("            End Set");
                sw.WriteLine("        End Property");
                sw.WriteLine("    End Class");
                sw.WriteLine();
                sw.WriteLine("    Public Class Encryption64");
                sw.WriteLine("        Private _key As Byte() = {}");
                sw.WriteLine("        Private ReadOnly _iv As Byte() = {65, 108, 97, 110, 32, 66, 46, 9}");
                sw.WriteLine();
                sw.WriteLine("        Public Function Encrypt(stringToEncrypt As String, encryptionKey As String) As String");
                sw.WriteLine("            Try");
                sw.WriteLine("                _key = System.Text.Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 8))");
                sw.WriteLine("                Using des As New System.Security.Cryptography.DESCryptoServiceProvider()");
                sw.WriteLine("                    Dim inputByteArray As Byte() = System.Text.Encoding.UTF8.GetBytes(stringToEncrypt)");
                sw.WriteLine("                    Using ms As New System.IO.MemoryStream()");
                sw.WriteLine("                        Using cs As New System.Security.Cryptography.CryptoStream(ms, des.CreateEncryptor(_key, _iv), System.Security.Cryptography.CryptoStreamMode.Write)");
                sw.WriteLine("                            cs.Write(inputByteArray, 0, inputByteArray.Length)");
                sw.WriteLine("                            cs.FlushFinalBlock()");
                sw.WriteLine("                            Return Convert.ToBase64String(ms.ToArray())");
                sw.WriteLine("                        End Using");
                sw.WriteLine("                    End Using");
                sw.WriteLine("                End Using");
                sw.WriteLine("            Catch");
                sw.WriteLine("                Return \"\"");
                sw.WriteLine("            End Try");
                sw.WriteLine("        End Function");
                sw.WriteLine();
                sw.WriteLine("        Public Function Decrypt(stringToDecrypt As String, encryptionKey As String) As String");
                sw.WriteLine("            Try");
                sw.WriteLine("                _key = System.Text.Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 8))");
                sw.WriteLine("                Using des As New System.Security.Cryptography.DESCryptoServiceProvider()");
                sw.WriteLine("                    Dim inputByteArray As Byte() = Convert.FromBase64String(stringToDecrypt)");
                sw.WriteLine("                    Using ms As New System.IO.MemoryStream()");
                sw.WriteLine("                        Using cs As New System.Security.Cryptography.CryptoStream(ms, des.CreateDecryptor(_key, _iv), System.Security.Cryptography.CryptoStreamMode.Write)");
                sw.WriteLine("                            cs.Write(inputByteArray, 0, inputByteArray.Length)");
                sw.WriteLine("                            cs.FlushFinalBlock()");
                sw.WriteLine("                            Dim encoding As System.Text.Encoding = System.Text.Encoding.UTF8");
                sw.WriteLine("                            Return encoding.GetString(ms.ToArray())");
                sw.WriteLine("                        End Using");
                sw.WriteLine("                    End Using");
                sw.WriteLine("                End Using");
                sw.WriteLine("            Catch");
                sw.WriteLine("                Return \"\"");
                sw.WriteLine("            End Try");
                sw.WriteLine("        End Function");
                sw.WriteLine("    End Class");
                sw.WriteLine("End Namespace");
            }
        }

        private void CreateCsDataLayers()
        {
            var storedProcedures = new StringBuilder();

            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length*2);
                _progressNdx++;
                var assembly = Assembly.GetExecutingAssembly();
                StringBuilder dataLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.DataLayer.MsAccessCSharp.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            dataLayerTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                string objName = objectName.Trim();
                bool isView = IsView(objName);

                List<Field> fields = MapFields(objName);

                if (!HasPrimaryKey(fields) && !isView)
                {
                    continue;
                }

                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Data.cs"))
                {
                    //storedProcedures.Append(CreateStoredProcedures(Common.GetSafeCsName(Common.GetCsPropertyName(objName)), fields, isView));

                    dataLayerTemplate.Replace("{1}", Common.GetSafeCsName(Common.GetCsPropertyName(objName)));

                    var fieldsPart = new StringBuilder();
                    foreach (var field in fields)
                    {
                        fieldsPart.AppendLine("        private " + field.CsDataType + " " + field.SafeCsFieldName + ";");
                    }
                    dataLayerTemplate.Replace("{2}", fieldsPart.ToString());

                    dataLayerTemplate.Replace("{3}", objName);

                    var fieldListPart = new StringBuilder();
                    foreach (var field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        fieldListPart.Append("[" + field.FieldName + "],");
                    }
                    dataLayerTemplate.Replace("{4}", fieldListPart.ToString().TrimEnd(','));
                    dataLayerTemplate.Replace("{45}", fieldListPart.ToString().TrimEnd(','));

                    var fieldListAllPart = new StringBuilder();
                    foreach (var field in fields.OrderBy(z => z.FieldName))
                    {
                        fieldListAllPart.Append("[" + field.FieldName + "],");
                    }
                    dataLayerTemplate.Replace("{44}", fieldListAllPart.ToString().TrimEnd(','));

                    var valPart = new StringBuilder();
                    int i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        valPart.Append("@val" + i + ",");
                        i++;
                    }
                    dataLayerTemplate.Replace("{5}", valPart.ToString().TrimEnd(','));
                    if (!isView)
                    {
                        dataLayerTemplate.Replace("{6}", fields.First(z => z.IsPrimaryKey).FieldName);
                    }

                    var propertiesPart = new StringBuilder();
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        if (!string.IsNullOrEmpty(field.Description))
                        {
                            propertiesPart.Append("        /// <summary>" + Environment.NewLine);
                            propertiesPart.Append("        /// " + field.Description + Environment.NewLine);
                            propertiesPart.Append("        /// </summary>" + Environment.NewLine);
                        }
                        propertiesPart.Append("        public virtual " + field.CsDataType + " " + field.SafeCsPropertyName + Environment.NewLine);
                        propertiesPart.Append("        {" + Environment.NewLine);
                        propertiesPart.Append("            get { return " + field.SafeCsFieldName + "; }" + Environment.NewLine);

                        if (field.IsIdentity && (!isView))
                        {
                            propertiesPart.Append("            protected set { " + field.SafeCsFieldName + " = value; _layerGenIsDirty = true; }" + Environment.NewLine);
                        } else if ((!field.IsComputedField) && (!isView))
                        {
                            if (field.CsDataType == "DateTime")
                                propertiesPart.Append("            set { " + field.SafeCsFieldName + " = value; _layerGenIsDirty = true; if(value == DateTime.MinValue) SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "); else UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "); }" + Environment.NewLine);
                            else if (field.CsDataType == "string")
                                propertiesPart.Append("            set { " + field.SafeCsFieldName + " = value; _layerGenIsDirty = true; if(value == null) SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "); else UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "); }" + Environment.NewLine);
                            else propertiesPart.Append("            set { " + field.SafeCsFieldName + " = value; _layerGenIsDirty = true; UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "); }" + Environment.NewLine);
                        }

                        if (field.IsComputedField || isView)
                        {
                            propertiesPart.Append("            protected set { " + field.SafeCsFieldName + " = value; _layerGenIsDirty = true; }" + Environment.NewLine);
                        }
                        propertiesPart.Append("        }" + Environment.NewLine + Environment.NewLine);
                    }

                    dataLayerTemplate.Replace("{7}", propertiesPart.ToString());

                    if (!isView)
                    {
                        string pkCsName = fields.First(z => z.IsPrimaryKey).SafeCsFieldName;
                        dataLayerTemplate.Replace("{8}", pkCsName);
                        if (fields.First(z => z.IsPrimaryKey).TextBased)
                        {
                            dataLayerTemplate.Replace("{88}", "'\" + " + pkCsName + " + \"'\";");
                        }
                        else
                        {
                            dataLayerTemplate.Replace("{88}", "\" + " + pkCsName + ";");
                        }
                    }

                    var nullDictionaryPart = new StringBuilder("            _nullDictionary = new Dictionary<" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields, bool>();" + Environment.NewLine);

                    //nullDictionaryPart.Append("            {" + Environment.NewLine);
                    foreach (var field in fields)
                        nullDictionaryPart.AppendLine("            _nullDictionary.Add(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ", true);");
                        //nullDictionaryPart.Append("                {" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ", true}," + Environment.NewLine);
                    //dataLayerTemplate.Replace("{9}", nullDictionaryPart.ToString().TrimEnd(Environment.NewLine.ToCharArray()).TrimEnd(',') + Environment.NewLine + "            };");
                    dataLayerTemplate.Replace("{9}", nullDictionaryPart.ToString());

                    string internalNamePart = "            _internalNameDictionary = new Dictionary<" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields, string>();" + Environment.NewLine;
                    //internalNamePart = internalNamePart + "            {" + Environment.NewLine;
                    foreach (var field in fields)
                        internalNamePart = internalNamePart + "            _internalNameDictionary.Add(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ", \"" + field.FieldName + "\");" + Environment.NewLine;
                    //internalNamePart = fields.Aggregate(internalNamePart, (current, field) => current + "                {" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ", \"" + field.FieldName + "\"}," + Environment.NewLine);
                    //internalNamePart = internalNamePart.TrimEnd(Environment.NewLine.ToCharArray()).TrimEnd(',') + "";
                    //internalNamePart = internalNamePart + Environment.NewLine + "            };";
                    dataLayerTemplate.Replace("{10}", internalNamePart);

                    var fillPart = new StringBuilder();
                    foreach (Field field in fields)
                    {
                        fillPart.Append("            if (HasField(_internalNameDictionary[" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "], dr))" + Environment.NewLine);
                        fillPart.Append("            {" + Environment.NewLine);
                        fillPart.Append("                if (dr[_internalNameDictionary[" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "]] == DBNull.Value)" + Environment.NewLine);
                        fillPart.Append("                {" + Environment.NewLine);
                        fillPart.Append("                    SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                        fillPart.Append("                }" + Environment.NewLine);
                        fillPart.Append("                else" + Environment.NewLine);
                        fillPart.Append("                {" + Environment.NewLine);
                        fillPart.Append("                    " + field.SafeCsPropertyName + " = (" + field.CsDataType + ") dr[_internalNameDictionary[" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "]];" + Environment.NewLine);
                        if (field.TextBased && AutoRightTrimStrings)
                        {
                            fillPart.AppendLine("                    " + field.SafeCsPropertyName + " = " + field.SafeCsPropertyName + ".TrimEnd();");
                        }
                        fillPart.Append("                    UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                        fillPart.Append("                }" + Environment.NewLine);
                        fillPart.Append("            }" + Environment.NewLine);
                        fillPart.Append("            else" + Environment.NewLine);
                        fillPart.Append("            {" + Environment.NewLine);
                        fillPart.Append("                _isReadOnly = true;" + Environment.NewLine);
                        fillPart.Append("                SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                        fillPart.Append("            }" + Environment.NewLine + Environment.NewLine);
                    }

                    dataLayerTemplate.Replace("{11}", fillPart.ToString());

                    var getPart = new StringBuilder();
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        if (field.IsPrimaryKey)
                        {
                            getPart.Append("                            " + field.SafeCsPropertyName + " = (" + field.CsDataType + ") reader[\"" + field.FieldName + "\"];" + Environment.NewLine);
                            getPart.Append("                            UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                        }
                        else
                        {
                            getPart.Append("                            if ((!HasField(\"" + field.FieldName + "\", reader)) || reader.IsDBNull(reader.GetOrdinal(\"" + field.FieldName + "\")))" + Environment.NewLine);
                            getPart.Append("                            {" + Environment.NewLine);
                            getPart.Append("                                SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                            getPart.Append("                            }" + Environment.NewLine);
                            getPart.Append("                            else" + Environment.NewLine);
                            getPart.Append("                            {" + Environment.NewLine);
                            getPart.Append("                                " + field.SafeCsPropertyName + " = (" + field.CsDataType + ") reader[\"" + field.FieldName + "\"];" + Environment.NewLine);
                            if (field.TextBased && AutoRightTrimStrings)
                            {
                                getPart.AppendLine("                                " + field.SafeCsPropertyName + " = " + field.SafeCsPropertyName + ".TrimEnd();");
                            }
                            getPart.Append("                                UnsetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                            getPart.Append("                            }" + Environment.NewLine);
                        }
                    }

                    dataLayerTemplate.Replace("{12}", getPart.ToString());

                    var resetToDefaultPart = new StringBuilder();
                    foreach (var z in fields)
                    {
                        if (!z.IsIdentity)
                            resetToDefaultPart.Append("            _nullDictionary[" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + z.SafeCsPropertyName + "] = true;" + Environment.NewLine);
                    }
                    dataLayerTemplate.Replace("{13}", resetToDefaultPart.ToString());

                    var savePart = new StringBuilder();

                    i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        savePart.AppendLine("                        parameter = new OleDbParameter(\"@val" + i + "\", OleDbType." + field.SqlDbType + ");");
                        savePart.AppendLine("                        if (IsNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Fields." + field.SafeCsPropertyName + "))");
                        if (field.CanBeNull)
                            savePart.AppendLine("                            parameter.Value = DBNull.Value;");
                        else
                            savePart.AppendLine("                            parameter.Value = " + GetDefaultCsValue(field) + ";");
                        savePart.AppendLine("                        else");
                        savePart.AppendLine("                            parameter.Value = " + field.SafeCsFieldName + ";");
                        savePart.AppendLine("                        command.Parameters.Add(parameter);");
                        i++;
                    }

                    dataLayerTemplate.Replace("{14}", savePart.ToString());

                    var str1 = new StringBuilder("                            ");

                    if (!isView)
                    {
                        Field f = fields.First(z => z.IsPrimaryKey);
                        if (f.IsIdentity)
                            str1.Append(f.SafeCsFieldName + " = (" + f.CsDataType + ") obj;" + Environment.NewLine);
                    }

                    dataLayerTemplate.Replace("{15}", str1.ToString());

                    str1.Clear();
                    str1.Append("                    const string cmdString = \"UPDATE [\" + LayerGenTableName + \"] SET ");
                    i = 1;
                    foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
                    {
                        str1.Append("[" + field.FieldName + "]=@val" + i + ",");
                        i++;
                    }
                    dataLayerTemplate.Replace("{16}", str1.ToString().TrimEnd(',') + " WHERE \" + LayerGenPrimaryKey + \"=@val" + i + "\";" + Environment.NewLine);

                    dataLayerTemplate.Replace("{17}", "                            command.Parameters.AddWithValue(\"@val" + i + "\", _oldPrimaryKeyValue);" + Environment.NewLine);
                    if (!isView)
                    {
                        dataLayerTemplate.Replace("{18}","        private " + fields.First(z => z.IsPrimaryKey).CsDataType + " _oldPrimaryKeyValue;" + Environment.NewLine);
                        dataLayerTemplate.Replace("{19}", fields.First(z => z.IsPrimaryKey).CsDataType);
                        
                        if (fields.First(z => z.IsPrimaryKey).TextBased || fields.First(z => z.IsPrimaryKey).CsDataType.ToLower() == "guid")
                        {
                            dataLayerTemplate.Replace("{20}", "            string sql = \"SELECT \" + strFields + \" FROM [\" + LayerGenTableName + \"] WHERE \" + LayerGenPrimaryKey + \"=@val1\";" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "                    command.Parameters.AddWithValue(\"@val1\", id);");
                        }
                        else
                        {
                            dataLayerTemplate.Replace("{20}", "            string sql = \"SELECT \" + strFields + \" FROM [\" + LayerGenTableName + \"] WHERE \" + LayerGenPrimaryKey + \"=\" + id;" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "");
                        }
                    }

                    var fkDataGetBy = new StringBuilder();
                    var fkDataGetByConnString = new StringBuilder();

                    if (!isView)
                    {
                        var fkProperties = new StringBuilder();
                        var fkFields = new StringBuilder();
                        string sqlDataType = "";

                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkFields.Append("        private " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + " _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";" + Environment.NewLine);

                            fkProperties.Append("        public " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + " F" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " {" + Environment.NewLine);
                            fkProperties.Append("            get {" + Environment.NewLine);
                            fkProperties.Append("                if (_my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " == null) {" + Environment.NewLine);
                            fkProperties.Append("                    _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " = new " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "(" + Common.GetCsFieldName(key.ForeignColumnName) + ");" + Environment.NewLine);
                            fkProperties.Append("                }" + Environment.NewLine);
                            fkProperties.Append("                return _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";" + Environment.NewLine);
                            fkProperties.Append("            }" + Environment.NewLine);
                            fkProperties.Append("        }" + Environment.NewLine + Environment.NewLine);

                            fkDataGetBy.Append("        internal static DataTable GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetBy.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetBy.Append("        {" + Environment.NewLine);
                            fkDataGetBy.Append("            using (OleDbConnection connection = new OleDbConnection())" + Environment.NewLine);
                            fkDataGetBy.Append("            {" + Environment.NewLine);
                            fkDataGetBy.Append("                connection.ConnectionString = Universal.GetConnectionString();" + Environment.NewLine);
                            fkDataGetBy.Append("                using (OleDbCommand command = new OleDbCommand())" + Environment.NewLine);
                            fkDataGetBy.Append("                {" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandText = \"SELECT * FROM [" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetBy.AppendLine("                    OleDbParameter parameter =  new OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ");");
                            fkDataGetBy.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetBy.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetBy.Append("                    command.Parameters.Add(new OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                    connection.Open();" + Environment.NewLine);
                            fkDataGetBy.Append("                    using (OleDbDataAdapter adapter = new OleDbDataAdapter())" + Environment.NewLine);
                            fkDataGetBy.Append("                    {" + Environment.NewLine);
                            fkDataGetBy.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetBy.Append("                        {" + Environment.NewLine);
                            fkDataGetBy.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetBy.Append("                            adapter.Fill(ds);" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetBy.Append("                            {" + Environment.NewLine);
                            fkDataGetBy.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetBy.Append("                            }" + Environment.NewLine);
                            fkDataGetBy.Append("                        }" + Environment.NewLine);
                            fkDataGetBy.Append("                    }" + Environment.NewLine);
                            fkDataGetBy.Append("                }" + Environment.NewLine);
                            fkDataGetBy.Append("            }" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("            return null;" + Environment.NewLine);
                            fkDataGetBy.Append("        }" + Environment.NewLine);

                            fkDataGetByConnString.Append("        internal static DataTable GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "(" + BusinessNamespaceName + ".LayerGenConnectionString connectionString, ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnString.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByConnString.Append("        {" + Environment.NewLine);
                            fkDataGetByConnString.Append("            using (OleDbConnection connection = new OleDbConnection())" + Environment.NewLine);
                            fkDataGetByConnString.Append("            {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                connection.ConnectionString = Universal.GetConnectionString();" + Environment.NewLine);
                            fkDataGetByConnString.Append("                using (OleDbCommand command = new OleDbCommand())" + Environment.NewLine);
                            fkDataGetByConnString.Append("                {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandText = \"SELECT * FROM [" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByConnString.AppendLine("                    OleDbParameter parameter =  new OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ");");
                            fkDataGetByConnString.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByConnString.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByConnString.Append("                    command.Parameters.Add(new OleDbParameter(\"@val1\", OleDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("                    connection.Open();" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    using (OleDbDataAdapter adapter = new OleDbDataAdapter())" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetByConnString.Append("                        {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            adapter.Fill(ds);" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            }" + Environment.NewLine);
                            fkDataGetByConnString.Append("                        }" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    }" + Environment.NewLine);
                            fkDataGetByConnString.Append("                }" + Environment.NewLine);
                            fkDataGetByConnString.Append("            }" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("            return null;" + Environment.NewLine);
                            fkDataGetByConnString.Append("        }" + Environment.NewLine);
                        }

                        dataLayerTemplate.Replace("{22}", fkFields.ToString());
                        dataLayerTemplate.Replace("{21}", fkProperties.ToString());
                    }

                    dataLayerTemplate.Replace("{23}", fkDataGetBy + Environment.NewLine + fkDataGetByConnString);
                    if (isView)
                    {
                        dataLayerTemplate.Replace("{18}", "");
                        dataLayerTemplate.Replace("{21}", "");
                        dataLayerTemplate.Replace("{22}", "");
                        RemoveTemplateComments(ref dataLayerTemplate);
                    }
                    else
                    {
                        dataLayerTemplate.Replace("{/*}", "");
                        dataLayerTemplate.Replace("{*/}", "");
                    }

                    var equalPart = new StringBuilder();
                    
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        equalPart.Append("            byte[] cls" + field.SafeCsFieldName.Remove(0, 1) + " = ObjectToByteArray(cls." + field.SafeCsPropertyName + ");" + Environment.NewLine);
                    }
                    equalPart.Append(Environment.NewLine);

                    var tmpEqual = new StringBuilder();
                    if (!isView)
                    {
                        Field primaryKeyField = fields.First(z => z.IsPrimaryKey);
                        dataLayerTemplate.Replace("{25}", primaryKeyField.SafeCsPropertyName);
                    }

                    equalPart.Append("            byte[] clsArray = new byte[");
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        tmpEqual.Append("cls" + field.SafeCsFieldName.Remove(0, 1) + ".Length + ");
                        if (tmpEqual.Length >= 110)
                        {
                            equalPart.Append(tmpEqual + Environment.NewLine + "                       ");
                            tmpEqual.Clear();
                        }
                    }
                    equalPart.Append(tmpEqual);
                    equalPart.ReplaceAllText(equalPart.ToString().TrimEnd(' ', '\r', '\n', '+'));
                    equalPart.Append("];" + Environment.NewLine);

                    tmpEqual.Clear();
                    tmpEqual.Append("0");

                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey))
                    {
                        equalPart.Append("            Array.Copy(cls" + field.SafeCsFieldName.Remove(0, 1));
                        equalPart.Append(", 0, clsArray, " + tmpEqual + ", cls" + field.SafeCsFieldName.Remove(0, 1) + ".Length);" + Environment.NewLine);
                        tmpEqual.Append(" + cls" + field.SafeCsFieldName.Remove(0, 1) + ".Length");
                        int teLength = tmpEqual.ToString().Split(Environment.NewLine.ToCharArray()).Length;
                        if (teLength > 0)
                        {
                            string te = tmpEqual.ToString().Split(Environment.NewLine.ToCharArray())[teLength - 1];
                            if (te.Length >= 85)
                            {
                                tmpEqual.Append(Environment.NewLine + "                        ");
                            }
                        }
                    }

                    equalPart.Append(Environment.NewLine);
                    equalPart.Append("            return clsArray;" + Environment.NewLine);

                    dataLayerTemplate.Replace("{24}", equalPart.ToString());

                    dataLayerTemplate.Replace("{26}", DataNamespaceName);
                    dataLayerTemplate.Replace("{27}", BusinessNamespaceName);

                    var serializationCode = new StringBuilder();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        [Serializable]");
                        serializationCode.AppendLine("        public class " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)));
                        serializationCode.AppendLine("        {");

                        foreach (var field in fields)
                        {
                            if (field.IsValueType)
                                serializationCode.AppendLine("            private " + field.CsDataType + "? " + field.SafeCsFieldName + ";");
                            else
                                serializationCode.AppendLine("            private " + field.CsDataType + " " + field.SafeCsFieldName + ";");
                        }
                        serializationCode.AppendLine("            private bool _serializationIsUpdate;");
                        serializationCode.AppendLine("            private string _serializationConnectionString;");
                        serializationCode.AppendLine();

                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                                serializationCode.AppendLine("            public " + field.CsDataType + "? " + field.SafeCsPropertyName);
                            else
                                serializationCode.AppendLine("            public " + field.CsDataType + " " + field.SafeCsPropertyName);
                            serializationCode.AppendLine("            {");
                            serializationCode.AppendLine("                get { return " + field.SafeCsFieldName + "; }");
                            serializationCode.AppendLine("                set { " + field.SafeCsFieldName + " = value; }");
                            serializationCode.AppendLine("            }");
                        }
                        serializationCode.AppendLine("            /// <summary>");
                        serializationCode.AppendLine("            /// Set this to true if <see cref=\"Save()\"></see> should do an update.");
                        serializationCode.AppendLine("            /// Otherwise, set to false to force <see cref=\"Save()\"></see> to do an insert.");
                        serializationCode.AppendLine("            /// </summary>");
                        serializationCode.AppendLine("            public bool SerializationIsUpdate");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                get { return _serializationIsUpdate; }");
                        serializationCode.AppendLine("                set { _serializationIsUpdate = value; }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            /// <summary>");
                        serializationCode.AppendLine("            /// The connection string used to connect to the database.");
                        serializationCode.AppendLine("            /// </summary>");
                        serializationCode.AppendLine("            public string SerializationConnectionString");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                get { return _serializationConnectionString; }");
                        serializationCode.AppendLine("                set { _serializationConnectionString = value; }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine("        }");
                    }
                    
                    dataLayerTemplate.Replace("{32}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Converts an instance of an object to a string format");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"format\">Specifies if it should convert to XML, BSON, or JSON</param>");
                        serializationCode.AppendLine("        /// <returns>The object, converted to a string representation</returns>");
                        serializationCode.AppendLine("        public string ToString(" + BusinessNamespaceName + ".SerializationFormats format)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + " = new " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "();");
                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = IsNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                ? (" + field.CsDataType + "?) null : " + field.SafeCsFieldName + ";");
                            } else if (field.CsDataType == "string")
                            {
                                serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = IsNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                ? null : " + field.SafeCsFieldName + ";");
                            }
                            else
                            {
                                serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = " + field.SafeCsFieldName + ";");
                            }
                        }
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationIsUpdate = _layerGenIsUpdate;");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (format == " + BusinessNamespaceName + ".SerializationFormats.Json)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                return Newtonsoft.Json.JsonConvert.SerializeObject(" + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ");");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (format == " + BusinessNamespaceName + ".SerializationFormats.Xml)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                System.Xml.Serialization.XmlSerializer xType = new System.Xml.Serialization.XmlSerializer(" + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".GetType());");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                using (StringWriter sw = new StringWriter())");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    xType.Serialize(sw, " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ");");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                    return sw.ToString();");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (format == " + BusinessNamespaceName + ".SerializationFormats.BsonBase64)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    using (Newtonsoft.Json.Bson.BsonWriter writer = new Newtonsoft.Json.Bson.BsonWriter(ms))");
                        serializationCode.AppendLine("                    {");
                        serializationCode.AppendLine("                        Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();");
                        serializationCode.AppendLine("                        serializer.Serialize(writer, " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ");");
                        serializationCode.AppendLine("                    }");
                        serializationCode.AppendLine("                    return Convert.ToBase64String(ms.ToArray());");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            return \"\";");
                        serializationCode.AppendLine("        }");
                    }

                    dataLayerTemplate.Replace("{33}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        protected static " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " BsonTo" + Common.GetCsPropertyName(objName) + "(string bson)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " z;");
                        serializationCode.AppendLine("            byte[] data = Convert.FromBase64String(bson);");
                        serializationCode.AppendLine("            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                using (Newtonsoft.Json.Bson.BsonReader reader = new Newtonsoft.Json.Bson.BsonReader(ms))");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();");
                        serializationCode.AppendLine("                    z = serializer.Deserialize<" + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + ">(reader);");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " tmp = new " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + "();");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + ".HasValue)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ".Value;");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            }
                            else
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + " == null)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ";");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + ".Encryption64 decryptor = new " + BusinessNamespaceName + ".Encryption64();");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate;");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = true;");
                        serializationCode.AppendLine("            return tmp;");

                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        protected static " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " XmlTo" + Common.GetCsPropertyName(objName) + "(string xml)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            System.Xml.Serialization.XmlSerializer xType = new System.Xml.Serialization.XmlSerializer(typeof(" + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "));");
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " z;");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            using (StringReader sr = new StringReader(xml))");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                z = (" + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + ") xType.Deserialize(sr);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " tmp = new " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + "();");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + ".HasValue)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ".Value;");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            }
                            else
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + " == null)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ";");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + ".Encryption64 decryptor = new " + BusinessNamespaceName + ".Encryption64();");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate;");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = true;");
                        serializationCode.AppendLine("            return tmp;");

                        serializationCode.AppendLine("        }");

                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        protected static " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " JsonTo" + Common.GetCsPropertyName(objName) + "(string json)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            " + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " z = Newtonsoft.Json.JsonConvert.DeserializeObject<Serializable" + Common.GetCsPropertyName(objName) + ">(json);");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + " tmp = new " + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + "();");
                        serializationCode.AppendLine();
                        foreach (Field field in fields)
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + ".HasValue)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ".Value;");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            } 
                            else
                            {
                                serializationCode.AppendLine("            if (z." + field.SafeCsPropertyName + " == null)");
                                serializationCode.AppendLine("            {");
                                serializationCode.AppendLine("                tmp.SetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            } else {");
                                serializationCode.AppendLine("                tmp." + field.SafeCsFieldName + " = z." + field.SafeCsPropertyName + ";");
                                serializationCode.AppendLine("                tmp.UnsetNull(" + BusinessNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ");");
                                serializationCode.AppendLine("            }");
                            }
                        }
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            " + BusinessNamespaceName + ".Encryption64 decryptor = new " + BusinessNamespaceName + ".Encryption64();");
                        serializationCode.AppendLine("            tmp._connectionString = decryptor.Decrypt(z.SerializationConnectionString, Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            tmp._layerGenIsUpdate = z.SerializationIsUpdate;");
                        serializationCode.AppendLine("            tmp._layerGenIsDirty = true;");
                        serializationCode.AppendLine("            return tmp;");
                        
                        serializationCode.AppendLine("        }");
                    }

                    dataLayerTemplate.Replace("{34}", serializationCode.ToString());

                    Common.DoComments(ref dataLayerTemplate, "//", IncludeComments);

                    sw.Write(dataLayerTemplate);
                }
            }
            using (StreamWriter sw = File.CreateText(OutputDirectory + "\\StoredProcedureScripts.SQL"))
            {
                sw.Write(storedProcedures.ToString());
            }
        }

        private void CreateCsBusinessLayers()
        {
            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var businessLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.BusinessLayer.MsAccessCSharp.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            businessLayerTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                string objName = objectName.Trim();
                List<Field> fields = MapFields(objName);
                bool isView = IsView(objName);

                if (!HasPrimaryKey(fields) && !isView)
                {
                    continue;
                }

                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Business.cs"))
                {
                    var enumsPart = new StringBuilder();
                    foreach (Field field in fields)
                    {
                        if (!string.IsNullOrEmpty(field.Description))
                        {
                            enumsPart.Append("            /// <summary>" + Environment.NewLine);
                            enumsPart.Append("            /// " + field.Description + Environment.NewLine);
                            enumsPart.Append("            /// </summary>" + Environment.NewLine);
                        }
                        enumsPart.Append("            " + field.SafeCsPropertyName + "," + Environment.NewLine);
                    }
                    businessLayerTemplate.Replace("{3}", enumsPart.ToString().TrimEnd(Environment.NewLine.ToCharArray()).TrimEnd(','));

                    businessLayerTemplate.Replace("{0}", Common.GetSafeCsName(Common.GetCsPropertyName(objName)));
                    businessLayerTemplate.Replace("{35}", Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)));
                    businessLayerTemplate.Replace("{99}", objName);
                    if (!isView)
                    {
                        businessLayerTemplate.Replace("{1}", fields.First(z => z.IsPrimaryKey).CsDataType);
                    }

                    if (isView)
                    {
                        RemoveTemplateComments(ref businessLayerTemplate);
                    }
                    else
                    {
                        businessLayerTemplate.Replace("{/*}", "");
                        businessLayerTemplate.Replace("{*/}", "");
                    }

                    var fkBusinessGetBy = new StringBuilder();
                    if (!isView)
                    {
                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkBusinessGetBy.Append("        public void GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    fkBusinessGetBy.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkBusinessGetBy.Append("        {" + Environment.NewLine);
                            fkBusinessGetBy.AppendLine("            LayerGenConnectionString connectString = new LayerGenConnectionString();");
                            fkBusinessGetBy.AppendLine("            connectString.ConnectionString = _connectionString;");
                            fkBusinessGetBy.Append("            DataTable dt = " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "(connectString, fkId);" + Environment.NewLine);
                            fkBusinessGetBy.Append("            if (dt != null)" + Environment.NewLine);
                            fkBusinessGetBy.Append("            {" + Environment.NewLine);
                            fkBusinessGetBy.Append("                Load(dt.Rows);" + Environment.NewLine);
                            fkBusinessGetBy.Append("            }" + Environment.NewLine);
                            fkBusinessGetBy.Append("        }" + Environment.NewLine);
                        }
                    }
                    businessLayerTemplate.Replace("{2}", fkBusinessGetBy.ToString());

                    businessLayerTemplate.Replace("{26}", DataNamespaceName);
                    businessLayerTemplate.Replace("{27}", BusinessNamespaceName);

                    var serializationCode = new StringBuilder();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " from a base64 encoded BSON string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"bson\">The base64 encoded BSON string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " FromBson(string bson)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            List<" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + "> zc;");
                        serializationCode.AppendLine("            byte[] data = Convert.FromBase64String(bson);");
                        serializationCode.AppendLine("            " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " tmp = new " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + "();");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            using (System.IO.MemoryStream ms = new System.IO.MemoryStream(data))");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                using (Newtonsoft.Json.Bson.BsonReader reader = new Newtonsoft.Json.Bson.BsonReader(ms))");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    reader.ReadRootValueAsArray = true;");
                        serializationCode.AppendLine("                    Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();");
                        serializationCode.AppendLine("                    zc = serializer.Deserialize<List<" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + ">>(reader);");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            foreach (" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + " z in zc)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetCsPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)));");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (zc.Count > 0)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                Encryption64 decryptor = new Encryption64();");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zc[0].SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            return tmp;");
                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)) + " from an XML string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"xml\">The XML string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)) + " FromXml(string xml)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            System.Xml.Serialization.XmlSerializer xType = new System.Xml.Serialization.XmlSerializer(typeof(List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Serializable" + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ">));");
                        serializationCode.AppendLine("            List<" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + "> zc;");
                        serializationCode.AppendLine("            " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " tmp = new " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + "();");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            using (System.IO.StringReader sr = new System.IO.StringReader(xml))");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                zc = (List<" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + ">)xType.Deserialize(sr);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            foreach (" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + " z in zc)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetCsPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)));");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (zc.Count > 0)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                Encryption64 decryptor = new Encryption64();");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zc[0].SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            return tmp;");
                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)) + " from a JSON string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"json\">The JSON string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetSafeCsName(Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate)) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " FromJson(string json)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            List<" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + "> zs = Newtonsoft.Json.JsonConvert.DeserializeObject<List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".Serializable" + Common.GetCsPropertyName(objName) + ">>(json);");
                        serializationCode.AppendLine("            " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + " tmp = new " + Common.GetPluralizedName(Common.GetCsPropertyName(objName), PluralizationTemplate) + "();");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            foreach (" + DataNamespaceName + "." + Common.GetCsPropertyName(objName) + ".Serializable" + Common.GetCsPropertyName(objName) + " z in zs)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                tmp.Add(" + Common.GetCsPropertyName(objName) + ".FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(z)));");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (zs.Count > 0)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                Encryption64 decryptor = new Encryption64();");
                        serializationCode.AppendLine("                tmp._connectionString = decryptor.Decrypt(zs[0].SerializationConnectionString, " + DataNamespaceName + ".Universal.LayerGenEncryptionKey);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            return tmp;");
                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Converts an instance of an object to a string format");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"format\">Specifies if it should convert to XML, BSON or JSON</param>");
                        serializationCode.AppendLine("        /// <returns>The object, converted to a string representation</returns>");
                        serializationCode.AppendLine("        public string ToString(SerializationFormats format)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "> zs = new List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + ">();");
                        serializationCode.AppendLine("            foreach (" + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + " z in this)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + " = new " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "();");
                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z.IsNull(" + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                ? (" + field.CsDataType + "?) null : z." + field.SafeCsPropertyName + ";");
                            }
                            else if (field.CsDataType == "string")
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z.IsNull(" + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                ? null : z." + field.SafeCsPropertyName + ";");
                            }
                            else
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z." + field.SafeCsPropertyName + ";");
                            }
                        }
                        serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationIsUpdate = z.LayerGenIsUpdate();");
                        serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationConnectionString = z.LayerGenConnectionString();");
                        serializationCode.AppendLine("                zs.Add(" + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ");");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine("            ");
                        serializationCode.AppendLine("            if (format == SerializationFormats.Json)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                return Newtonsoft.Json.JsonConvert.SerializeObject(zs);");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (format == SerializationFormats.Xml)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                System.Xml.Serialization.XmlSerializer xType = new System.Xml.Serialization.XmlSerializer(zs.GetType());");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("                using (System.IO.StringWriter sw = new System.IO.StringWriter())");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    xType.Serialize(sw, zs);");
                        serializationCode.AppendLine("                    return sw.ToString();");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            if (format == SerializationFormats.BsonBase64)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                using (System.IO.MemoryStream ms = new System.IO.MemoryStream())");
                        serializationCode.AppendLine("                {");
                        serializationCode.AppendLine("                    using (Newtonsoft.Json.Bson.BsonWriter writer = new Newtonsoft.Json.Bson.BsonWriter(ms))");
                        serializationCode.AppendLine("                    {");
                        serializationCode.AppendLine("                        Newtonsoft.Json.JsonSerializer serializer = new Newtonsoft.Json.JsonSerializer();");
                        serializationCode.AppendLine("                        serializer.Serialize(writer, zs);");
                        serializationCode.AppendLine("                    }");
                        serializationCode.AppendLine("                    return Convert.ToBase64String(ms.ToArray());");
                        serializationCode.AppendLine("                }");
                        serializationCode.AppendLine("            }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            return \"\";");
                        serializationCode.AppendLine("        }");
                    }

                    businessLayerTemplate.Replace("{33}", serializationCode.ToString());

                    serializationCode.Clear();

                    if (AllowSerialization)
                    {
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetCsPropertyName(objName) + " from a JSON string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"json\">The JSON string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetCsPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetCsPropertyName(objName) + " FromJson(string json)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            return JsonTo" + Common.GetCsPropertyName(objName) + "(json);");
                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetCsPropertyName(objName) + " from an XML string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"xml\">The XML string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetCsPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetCsPropertyName(objName) + " FromXml(string xml)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            return XmlTo" + Common.GetCsPropertyName(objName) + "(xml);");
                        serializationCode.AppendLine("        }");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("        /// <summary>");
                        serializationCode.AppendLine("        /// Creates an instance of " + Common.GetCsPropertyName(objName) + " from a base64 encoded BSON string");
                        serializationCode.AppendLine("        /// </summary>");
                        serializationCode.AppendLine("        /// <param name=\"bson\">The base64 encoded BSON string</param>");
                        serializationCode.AppendLine("        /// <returns>A " + Common.GetCsPropertyName(objName) + " object instance</returns>");
                        serializationCode.AppendLine("        public static " + Common.GetCsPropertyName(objName) + " FromBson(string bson)");
                        serializationCode.AppendLine("        {");
                        serializationCode.AppendLine("            return BsonTo" + Common.GetCsPropertyName(objName) + "(bson);");
                        serializationCode.AppendLine("        }");
                    }

                    businessLayerTemplate.Replace("{34}", serializationCode.ToString());

                    Common.DoComments(ref businessLayerTemplate, "//", IncludeComments);
                    sw.Write(businessLayerTemplate.ToString());
                }
            }
        }

        private void CreateCsUniversalFile()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var universal1Template = new StringBuilder();
            var universal2Template = new StringBuilder();

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal1MsAccessCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal1Template.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal2MsAccessCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal2Template.Append(reader.ReadToEnd());
                    }
                }
            }

            universal2Template.Replace("{0}", DataNamespaceName);

            using (StreamWriter sw = File.CreateText(OutputDirectory + "\\Universal.cs"))
            {
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine("using System;");
                    sw.WriteLine("using System.Collections.Generic;");
                    sw.WriteLine("using System.Data;");
                    sw.WriteLine("using System.Data.OleDb;");
                    sw.WriteLine("using System.Dynamic;");
                    sw.WriteLine("using System.Linq;");
                    sw.WriteLine("using System.Reflection;");
                    sw.WriteLine();
                }
                else
                {
                    sw.WriteLine("using System;");
                }
                sw.WriteLine("namespace " + DataNamespaceName);
                sw.WriteLine("{");
                sw.WriteLine("    internal static class Universal");
                sw.WriteLine("    {");
                sw.WriteLine("        /// <summary>");
                sw.WriteLine("        /// The encryption key that LayerGen uses internally to encrypt data.");
                sw.WriteLine("        /// </summary>");
                sw.WriteLine("        public static string LayerGenEncryptionKey = \"L@y3rG3n\";");
                sw.WriteLine();
                sw.WriteLine("        /// <summary>");
                sw.WriteLine("        /// Gets the connection string to connect to the database");
                sw.WriteLine("        /// </summary>");
                sw.WriteLine("        /// <returns>A string containing the connection string</returns>");
                sw.WriteLine("        internal static string GetConnectionString()");
                sw.WriteLine("        {");
                sw.WriteLine("            // If this is an ASP.NET application, you can use a line like the following to pull");
                sw.WriteLine("            // the connection string from the Web.Config:");
                sw.WriteLine("            // return System.Configuration.ConfigurationManager.ConnectionStrings[\"MyConnectionString\"].ConnectionString;");
                sw.WriteLine("");
                sw.WriteLine("            return \"" + ConnectionString.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\";");
                sw.WriteLine("        }");
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine();
                    sw.WriteLine(universal1Template.ToString());
                    sw.WriteLine();
                }
                sw.WriteLine("    }");
                sw.WriteLine("}");
                sw.WriteLine();
                sw.WriteLine("namespace " + BusinessNamespaceName);
                sw.WriteLine("{");
                if (AllowSerialization)
                {
                    sw.WriteLine();
                    sw.WriteLine("    /// <summary>");
                    sw.WriteLine("    /// Enumeration of various serialization formats");
                    sw.WriteLine("    /// </summary>");
                    sw.WriteLine("    public enum SerializationFormats");
                    sw.WriteLine("    {");
                    sw.WriteLine("        /// <summary>");
                    sw.WriteLine("        /// JSON format");
                    sw.WriteLine("        /// </summary>");
                    sw.WriteLine("        Json = 1,");
                    sw.WriteLine("        /// <summary>");
                    sw.WriteLine("        /// XML format");
                    sw.WriteLine("        /// </summary>");
                    sw.WriteLine("        Xml = 2,");
                    sw.WriteLine("        /// <summary>");
                    sw.WriteLine("        /// Base 64 encoded BSON format");
                    sw.WriteLine("        /// </summary>");
                    sw.WriteLine("        BsonBase64 = 3");
                    sw.WriteLine("    }");
                }
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine();
                    sw.WriteLine(universal2Template.ToString());
                    sw.WriteLine();
                }
                sw.WriteLine("    /// <summary>");
                sw.WriteLine("    /// The exception that is thrown when data in memory is out of sync with data in the database.");
                sw.WriteLine("    /// </summary>");
                sw.WriteLine("    public class OutOfSyncException : System.Exception");
                sw.WriteLine("    {");
                sw.WriteLine("        public OutOfSyncException()");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public OutOfSyncException(string message)");
                sw.WriteLine("            : base(message)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public OutOfSyncException(string message, System.Exception inner)");
                sw.WriteLine("            : base(message, inner)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine("    }");
                sw.WriteLine();
                sw.WriteLine("    /// <summary>");
                sw.WriteLine("    /// The exception that is thrown when data in memory is in a read only state.");
                sw.WriteLine("    /// </summary>");
                sw.WriteLine("    public class ReadOnlyException : System.Exception");
                sw.WriteLine("    {");
                sw.WriteLine("        public ReadOnlyException()");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public ReadOnlyException(string message)");
                sw.WriteLine("            : base(message)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public ReadOnlyException(string message, System.Exception inner)");
                sw.WriteLine("            : base(message, inner)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine("    }");
                sw.WriteLine();
                sw.WriteLine("    /// <summary>");
                sw.WriteLine("    /// The exception that is thrown when trying to read from a row that doesn't exist.");
                sw.WriteLine("    /// </summary>");
                sw.WriteLine("    public class RowNotFoundException : System.Exception");
                sw.WriteLine("    {");
                sw.WriteLine("        public RowNotFoundException()");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public RowNotFoundException(string message)");
                sw.WriteLine("            : base(message)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public RowNotFoundException(string message, System.Exception inner)");
                sw.WriteLine("            : base(message, inner)");
                sw.WriteLine("        {");
                sw.WriteLine("        }");
                sw.WriteLine("    }");
                sw.WriteLine();
                sw.WriteLine("    public class LayerGenConnectionString");
                sw.WriteLine("    {");
                sw.WriteLine("        private string _connectionString;");
                sw.WriteLine();
                sw.WriteLine("        public string ConnectionString");
                sw.WriteLine("        {");
                sw.WriteLine("            get");
                sw.WriteLine("            {");
                sw.WriteLine("                return _connectionString;");
                sw.WriteLine("            }");
                sw.WriteLine("            set");
                sw.WriteLine("            {");
                sw.WriteLine("                _connectionString = value;");
                sw.WriteLine("            }");
                sw.WriteLine("        }");
                sw.WriteLine("    }");
                sw.WriteLine();
                sw.WriteLine("    public class Encryption64");
                sw.WriteLine("    {");
                sw.WriteLine("        private byte[] _key = {};");
                sw.WriteLine("        private readonly byte[] _iv = {65, 108, 97, 110, 32, 66, 46, 9};");
                sw.WriteLine();
                sw.WriteLine("        public string Encrypt(string stringToEncrypt, string encryptionKey)");
                sw.WriteLine("        {");
                sw.WriteLine("            try");
                sw.WriteLine("            {");
                sw.WriteLine("                _key = System.Text.Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 8));");
                sw.WriteLine("                using (System.Security.Cryptography.DESCryptoServiceProvider des = new System.Security.Cryptography.DESCryptoServiceProvider())");
                sw.WriteLine("                {");
                sw.WriteLine("                    byte[] inputByteArray = System.Text.Encoding.UTF8.GetBytes(stringToEncrypt);");
                sw.WriteLine("                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())");
                sw.WriteLine("                    {");
                sw.WriteLine("                        using (System.Security.Cryptography.CryptoStream cs = new System.Security.Cryptography.CryptoStream(ms, des.CreateEncryptor(_key, _iv), System.Security.Cryptography.CryptoStreamMode.Write))");
                sw.WriteLine("                        {");
                sw.WriteLine("                            cs.Write(inputByteArray, 0, inputByteArray.Length);");
                sw.WriteLine("                            cs.FlushFinalBlock();");
                sw.WriteLine("                            return Convert.ToBase64String(ms.ToArray());");
                sw.WriteLine("                        }");
                sw.WriteLine("                    }");
                sw.WriteLine("                }");
                sw.WriteLine("            } catch");
                sw.WriteLine("            {");
                sw.WriteLine("                return \"\";");
                sw.WriteLine("            }");
                sw.WriteLine("        }");
                sw.WriteLine();
                sw.WriteLine("        public string Decrypt(string stringToDecrypt, string encryptionKey)");
                sw.WriteLine("        {");
                sw.WriteLine("            try");
                sw.WriteLine("            {");
                sw.WriteLine("                _key = System.Text.Encoding.UTF8.GetBytes(encryptionKey.Substring(0, 8));");
                sw.WriteLine("                using (System.Security.Cryptography.DESCryptoServiceProvider des = new System.Security.Cryptography.DESCryptoServiceProvider())");
                sw.WriteLine("                {");
                sw.WriteLine("                    byte[] inputByteArray = Convert.FromBase64String(stringToDecrypt);");
                sw.WriteLine("                    using (System.IO.MemoryStream ms = new System.IO.MemoryStream())");
                sw.WriteLine("                    {");
                sw.WriteLine("                        using (System.Security.Cryptography.CryptoStream cs = new System.Security.Cryptography.CryptoStream(ms, des.CreateDecryptor(_key, _iv), System.Security.Cryptography.CryptoStreamMode.Write))");
                sw.WriteLine("                        {");
                sw.WriteLine("                            cs.Write(inputByteArray, 0, inputByteArray.Length);");
                sw.WriteLine("                            cs.FlushFinalBlock();");
                sw.WriteLine("                            System.Text.Encoding encoding = System.Text.Encoding.UTF8;");
                sw.WriteLine("                            return encoding.GetString(ms.ToArray());");
                sw.WriteLine("                        }");
                sw.WriteLine("                    }");
                sw.WriteLine("                }");
                sw.WriteLine("            } catch");
                sw.WriteLine("            {");
                sw.WriteLine("                return \"\";");
                sw.WriteLine("            }");
                sw.WriteLine("        }"); 
                sw.WriteLine("    }");
                sw.WriteLine("}");
            }
        }

        private bool HasPrimaryKey(List<Field> fields)
        {
            return fields.Any(field => field.IsPrimaryKey);
        }

        private bool IsView(string tableName)
        {
            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();

                using (DataTable tables = connection.GetSchema("Tables"))
                {
                    if (tables.Rows.Cast<DataRow>().Where(row => ((string) row["TABLE_TYPE"]).ToLower() == "view").
                        Any(row => ((string) row["TABLE_NAME"]).ToLower().Equals(tableName.ToLower())))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private List<Field> MapFields(string tableName)
        {
            var fields = new List<Field>();

            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();

                using (DataTable columns = connection.GetSchema("Columns"))
                {
                    foreach (DataRow row in columns.Rows)
                    {
                        if (((string)row["TABLE_NAME"]).ToLower() != tableName.ToLower())
                            continue;

                        var field = new Field();
                        field.FieldName = (string) row["COLUMN_NAME"];
                        field.IsPrimaryKey = IsPrimaryKey(tableName, field.FieldName);
                        field.IsIdentity = field.IsPrimaryKey && (((int)row["DATA_TYPE"] == 3) && (((long)row["COLUMN_FLAGS"] == 90)) || (((string)row["COLUMN_DEFAULT"]).ToLower() == "genguid()"));
                        field.CsDataType = GetCsDataType((int) row["DATA_TYPE"]);
                        field.VbDataType = GetVbDataType((int)row["DATA_TYPE"]);
                        field.TextBased = IsTextBased(GetCsDataType(((int)row["DATA_TYPE"])));
                        field.SafeCsFieldName = Common.GetSafeCsName(Common.GetCsFieldName(field.FieldName));
                        field.SafeCsPropertyName = Common.GetSafeCsName(Common.GetCsPropertyName(field.FieldName));
                        field.SafeVbFieldName = Common.GetSafeVbName(Common.GetVbFieldName(field.FieldName));
                        field.SafeVbPropertyName = Common.GetSafeVbName(Common.GetVbPropertyName(field.FieldName));
                        field.SqlDbType = GetOleDbTypeFromOleType(((int)row["DATA_TYPE"]));
                        field.IsValueType = Common.IsValueType(field.CsDataType);
                        field.Description = (row["DESCRIPTION"]) is DBNull ? "" : (string) row["DESCRIPTION"];
                        field.IsComputedField = IsComputed(tableName, field.FieldName);
                        field.CanBeNull = (bool)row["IS_NULLABLE"];
                        fields.Add(field);
                    }
                }
            }

            return fields;
        }

        private bool IsComputed(string tableName, string fieldName)
        {
            using (var connection = new OleDbConnection(ConnectionString))
            {
                using (var command = new OleDbCommand("SELECT TOP 1 * FROM " + tableName, connection))
                {
                    connection.Open();
                    using (OleDbDataReader reader = command.ExecuteReader())
                    {
                        if (reader != null)
                        {
                            DataTable dt = reader.GetSchemaTable();
                            if (dt != null && dt.Rows.Cast<DataRow>().Where(row => String.Equals(((string) row["ColumnName"]), fieldName,StringComparison.CurrentCultureIgnoreCase))
                                                                     .Any(row => (bool) row["IsReadOnly"]))
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsTextBased(string csType)
        {
            return csType.ToLower() == "string";
        }

        private void RemoveTemplateComments(ref StringBuilder templateString)
        {
            int ndx1 = templateString.IndexOf("{/*}");
            int ndx2 = templateString.IndexOf("{*/}");

            while (ndx1 >= 0 && ndx2 >= 0)
            {
                string q = "";
                string p = "";
                try
                {
                    q = templateString.ToString().Substring(ndx1 - 250, 500);
                    p = templateString.ToString().Substring(ndx1 - 25, 50);
                }
                catch { }
                templateString.Remove(ndx1, ndx2 - ndx1 + 4);

                ndx1 = templateString.IndexOf("{/*}");
                ndx2 = templateString.IndexOf("{*/}");
            }
        }

        private List<ForeignKey> GetForeignKeys(string tableName)
        {
            var foreignKeys = new List<ForeignKey>();

            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();

                using (DataTable columns = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Foreign_Keys, null))
                {
                    if (columns != null)
                    {
                        foreignKeys.AddRange(from DataRow row in columns.Rows
                            where String.Equals(((string) row["FK_TABLE_NAME"]), tableName, StringComparison.CurrentCultureIgnoreCase)
                            select new ForeignKey
                            {
                                ForeignTableName = (string) row["FK_TABLE_NAME"],
                                ForeignColumnName = (string) row["FK_COLUMN_NAME"],
                                PrimaryTableName = (string) row["PK_TABLE_NAME"]
                            });
                    }
                }
            }

            return foreignKeys;
        }

        private string GetOleDbTypeFromOleType(int dataType)
        {
            OleDbType type = (OleDbType) dataType;

            return type.ToString();
            //switch (type)
            //{
            //    case OleDbType.BigInt:
            //        return "BigInt";
            //    case OleDbType.Binary:
            //        return "Binary";
            //    case OleDbType.Boolean:
            //        return "Boolean";
            //    case OleDbType.BSTR:
            //        return "BSTR";
            //    case OleDbType.Char:
            //        return "Char";
            //    case OleDbType.Currency:
            //        return "Currency";
            //    case OleDbType.Date:
            //        return "Date";
            //    case OleDbType.DBDate:
            //        return "DBDate";
            //    case OleDbType.DBTime:
            //        return "DBTime";
            //    case OleDbType.DBTimeStamp:
            //        return "DBTimeStamp";
            //    case OleDbType.Decimal:
            //        return "Decimal";
            //    case OleDbType.Double:
            //        return "Double";
            //    case OleDbType.Empty:
            //        return "Empty";
            //    case OleDbType.Error:
            //        return "Error";
            //    case OleDbType.Filetime:
            //        return "Filetime";
            //    case OleDbType.Guid:
            //        return "Guid";
            //    case OleDbType.IDispatch:
            //        return "IDispatch";
            //    case OleDbType.Integer:
            //        return "Integer";
            //    case OleDbType.IUnknown:
            //        return "IUnknown";
            //    case OleDbType.LongVarBinary:
            //        return "LongVarBinary";
            //    case OleDbType.LongVarChar:
            //        return "LongVarChar";
            //    case OleDbType.LongVarWChar:
            //        return "LongVarWChar";
            //    case OleDbType.Numeric:
            //        return "Numeric";
            //    case OleDbType.PropVariant:
            //        return "PropVariant";
            //    case OleDbType.Single:
            //        return "Single";
            //    case OleDbType.SmallInt:
            //        return "SmallInt";
            //    case OleDbType.TinyInt:
            //        return "TinyInt";
            //    case OleDbType.UnsignedBigInt:
            //        return "UnsignedBigInt";
            //    case OleDbType.UnsignedInt:
            //        return "UnsignedInt";
            //    case OleDbType.UnsignedSmallInt:
            //        return "UnsignedSmallInt";
            //    case OleDbType.UnsignedTinyInt:
            //        return "UnsignedTinyInt";
            //    case OleDbType.VarBinary:
            //        return "VarBinary";
            //    case OleDbType.VarChar:
            //        return "VarChar";
            //    case OleDbType.Variant:
            //        return "Variant";
            //    case OleDbType.VarNumeric:
            //        return "VarNumeric";
            //    case OleDbType.VarWChar:
            //        return "VarWChar";
            //    case OleDbType.WChar:
            //        return "WChar";
            //}

            //return "UNKNOWN";
        }

        private string GetVbDataType(int dataType)
        {
            switch (dataType)
            {
                case 2:
                    return "Short";
                case 3:
                    return "Integer";
                case 4:
                    return "Single";
                case 5:
                    return "Double";
                case 6:
                case 14:
                case 131:
                case 139:
                    return "Decimal";
                case 7:
                    return "DateTime";
                case 8:
                    return "String";
                case 9:
                case 12:
                case 13:
                case 138:
                    return "Object";
                case 10:
                    return "Exception";
                case 11:
                    return "Boolean";
                case 16:
                    return "SByte";
                case 17:
                    return "Byte";
                case 18:
                    return "UShort";
                case 19:
                    return "UInteger";
                case 20:
                    return "Long";
                case 21:
                    return "ULong";
                case 64:
                case 133:
                case 135:
                    return "DateTime";
                case 72:
                    return "Guid";
                case 128:
                case 204:
                case 205:
                    return "Byte()";
                case 129:
                case 130:
                case 200:
                case 201:
                case 202:
                case 203:
                    return "String";
                case 134:
                    return "TimeSpan";
            }

            return "UNKNOWN";
        }

        private string GetCsDataType(int dataType)
        {
            switch (dataType)
            {
                case 2:
                    return "short";
                case 3:
                    return "int";
                case 4:
                    return "float";
                case 5:
                    return "double";
                case 6:
                case 14:
                case 131:
                case 139:
                    return "decimal";
                case 7:
                    return "DateTime";
                case 8:
                    return "string";
                case 9:
                case 12:
                case 13:
                case 138:
                    return "object";
                case 10:
                    return "Exception";
                case 11:
                    return "bool";
                case 16:
                    return "sbyte";
                case 17:
                    return "byte";
                case 18:
                    return "ushort";
                case 19:
                    return "uint";
                case 20:
                    return "long";
                case 21:
                    return "ulong";
                case 64:
                case 133:
                case 135:
                    return "DateTime";
                case 72:
                    return "Guid";
                case 128:
                case 204:
                case 205:
                    return "byte[]";
                case 129:
                case 130:
                case 200:
                case 201:
                case 202:
                case 203:
                    return "string";
                case 134:
                    return "TimeSpan";
            }

            return "UNKNOWN";
        }

        private bool IsPrimaryKey(string tableName, string fieldName)
        {
            using (var connection = new OleDbConnection(ConnectionString))
            {
                connection.Open();

                using (DataTable indexes = connection.GetSchema("Indexes"))
                {
                    if (indexes.Rows.Cast<DataRow>()
                               .Where(row =>
                                    ((string) row["TABLE_NAME"]).ToLower() == tableName.ToLower() &&
                                    ((string) row["COLUMN_NAME"]).ToLower() == fieldName.ToLower())
                            .Any(row => (bool) row["PRIMARY_KEY"]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string GetDefaultCsValue(Field field)
        {
            string csType = field.CsDataType;

            if (csType == "Guid")
                return "Guid.Empty";

            if (csType == "bool")
                return "false";

            if (csType == "DateTime")
                return "DateTime.Now";

            if (csType == "short")
                return "0";

            if (csType == "int")
                return "0";

            if (csType == "long")
                return "0";

            if (csType == "char")
                return "'\\0'";

            if (csType == "string")
                return "\"\"";

            if (csType == "byte[]")
                return "new byte[] {0}";

            if (csType == "float")
                return "0f";

            if (csType == "decimal")
                return "0m";

            if (csType == "double")
                return "0";

            if (csType == "byte")
                return "0";

            return "";
        }

        private string GetDefaultVbValue(Field field)
        {
            string csType = field.CsDataType;

            if (csType == "Guid")
                return "Guid.Empty";

            if (csType == "bool")
                return "False";

            if (csType == "DateTime")
                return "DateTime.Now";

            if (csType == "short")
                return "0";

            if (csType == "int")
                return "0";

            if (csType == "long")
                return "0";

            if (csType == "char")
                return "'\\0'";

            if (csType == "string")
                return "\"\"";

            if (csType == "byte[]")
                return "new Byte() {0}";

            if (csType == "float")
                return "0";

            if (csType == "decimal")
                return "0";

            if (csType == "double")
                return "0";

            if (csType == "byte")
                return "0";

            return "";
        }

        private class Field
        {
            public string FieldName { get; set; }
            public string CsDataType { get; set; }
            public string VbDataType { get; set; }
            public bool IsPrimaryKey { get; set; }
            public bool IsIdentity { get; set; }
            public string SafeCsPropertyName { get; set; }
            public string SafeVbPropertyName { get; set; }
            public string SafeCsFieldName { get; set; }
            public string SafeVbFieldName { get; set; }
            public string SqlDbType { get; set; }
            public string IntrinsicSqlDataType { get; set; }
            public bool TextBased { get; set; }
            public string Description { get; set; }
            public bool IsComputedField { get; set; }
            public bool CanBeNull { get; set; }
            public bool IsValueType { get; set; }
        }

        private class ForeignKey
        {
            public string ForeignTableName { get; set; }
            public string PrimaryTableName { get; set; }
            public string ForeignColumnName { get; set; }
        }
    }
}
