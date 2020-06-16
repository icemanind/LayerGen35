using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Reflection;
using System.Text;
using System.IO;
using System.Windows.Forms;

namespace LayerGen35.DatabasePlugins
{
    public class SqlServer : IDatabasePlugin
    {
        private delegate void SetTextCallback(int percentage);

        private int _progressNdx;

        public Languages Language { get; set; }
        public string DatabaseName { get; set; }

        public DatabaseTypes DatabaseType
        {
            get { return DatabaseTypes.SqlServer; }
        }

        public string OutputDirectory { get; set; }
        public bool HasDynamicDataRetrieval { get; set; }
        public bool AutoRightTrimStrings { get; set; }
        public bool AllowSerialization { get; set; }
        public bool CreateAsyncMethods { get; set; }
        public bool CreateWebApiClasses { get; set; }
        public bool AspNetCore2 { get; set; }
        public string DatabaseServer { get; set; }
        public int DatabasePort { get; set; }
        public string Objects { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public bool TrustedConnection { get; set; }
        public bool HasCustomConnectionString { get; set; }
        public string CustomConnectionString { get; set; }
        public string DefaultSchema { get; set; }
        public bool IncludeComments { get; set; }
        public ProgressBar ProgressBar { get; set; }
        public string DataNamespaceName { get; set; }
        public string BusinessNamespaceName { get; set; }
        public string PluralizationTemplate { get; set; }

        private string ConnectionString
        {
            get
            {
                if (HasCustomConnectionString)
                    return CustomConnectionString;

                var builder = new SqlConnectionStringBuilder();
                builder["Data Source"] = DatabaseServer + "," + DatabasePort;
                builder["Integrated Security"] = TrustedConnection;
                builder["Initial Catalog"] = DatabaseName;
                if (!TrustedConnection)
                {
                    builder["User ID"] = UserName;
                    builder["Password"] = Password;
                }

                return builder.ConnectionString;
            }
        }

        public void CreateLayers()
        {
            _progressNdx = 0;
            UpdateProgress(0, 1);

            if (Language == Languages.CSharp)
            {
                CreateCsDataLayers();
                CreateCsBusinessLayers();
                CreateCsUniversalFile();
                if (CreateWebApiClasses)
                {
                    CreateCsWebApiClasses();
                }
            }
            if (Language == Languages.VbNet)
            {
                CreateVbDataLayers();
                CreateVbBusinessLayers();
                CreateVbUniversalFile();
            }
        }

        private void CreateCsWebApiClasses()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var pageLinkBuilderTemplate = new StringBuilder();
            var tokenParserTemplate = new StringBuilder();
            var webApiErrorModelTemplate = new StringBuilder();
            var webApiPatchTemplate = new StringBuilder();
            var modelsTemplate = new StringBuilder();
            var createModelsTemplate = new StringBuilder();
            var controllersTemplate = new StringBuilder();
            var hasCreatedField = false;

            if (!Directory.Exists(Path.Combine(OutputDirectory, "WebApi", "Infrastructure")))
            {
                Directory.CreateDirectory(Path.Combine(OutputDirectory, "WebApi", "Infrastructure"));
            }

            if (!Directory.Exists(Path.Combine(OutputDirectory, "WebApi", "Models")))
            {
                Directory.CreateDirectory(Path.Combine(OutputDirectory, "WebApi", "Models"));
            }

            if (!Directory.Exists(Path.Combine(OutputDirectory, "WebApi", "Controllers")))
            {
                Directory.CreateDirectory(Path.Combine(OutputDirectory, "WebApi", "Controllers"));
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.TokenParserCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        tokenParserTemplate.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.WebApiPatchCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        webApiPatchTemplate.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = AspNetCore2 ? assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.PageLinkBuilderCore2Cs.txt") : assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.PageLinkBuilderCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        pageLinkBuilderTemplate.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.WebApiErrorModelCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        webApiErrorModelTemplate.Append(reader.ReadToEnd());
                    }
                }
            }

            using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Infrastructure", "PageLinkBuilder.cs")))
            {
                pageLinkBuilderTemplate.Replace("{DataNamespaceName}", DataNamespaceName);

                sw.Write(pageLinkBuilderTemplate);
            }

            using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Infrastructure", "TokenParser.cs")))
            {
                tokenParserTemplate.Replace("{DataNamespaceName}", DataNamespaceName);

                sw.Write(tokenParserTemplate);
            }

            using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Models", "WebApiErrorModel.cs")))
            {
                webApiErrorModelTemplate.Replace("{DataNamespaceName}", DataNamespaceName);

                sw.Write(webApiErrorModelTemplate);
            }

            using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Models", "WebApiPatch.cs")))
            {
                webApiPatchTemplate.Replace("{DataNamespaceName}", DataNamespaceName);

                sw.Write(webApiPatchTemplate);
            }

            foreach (string objectName in Objects.Split(';'))
            {
                string objName = objectName.Trim();
                List<Field> fields = MapFields(objName);
                bool isView = IsView(objName);

                if (isView)
                    continue;

                if (!HasPrimaryKey(fields) && !isView)
                {
                    continue;
                }

                List<ForeignKey> foreignKeys = GetForeignKeys(objName);

                hasCreatedField = fields.Any(z => z.FieldName.ToLower() == "created");

                modelsTemplate = new StringBuilder();
                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.ModelCs.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            modelsTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                createModelsTemplate = new StringBuilder();
                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.CreateModelCs.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            createModelsTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                controllersTemplate = new StringBuilder();
                using (Stream stream = AspNetCore2 ? assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.ControllerCore2Cs.txt") : assembly.GetManifestResourceStream("LayerGen35.Templates.WebApi.ControllerCs.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            controllersTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Controllers", objName.ToProperFileName() + "Controller.cs")))
                {
                    controllersTemplate.Replace("{DataNamespaceName}", DataNamespaceName);
                    controllersTemplate.Replace("{BusinessNamespaceName}", BusinessNamespaceName);
                    controllersTemplate.Replace("{SafeTableName}", Common.GetSafeCsName(objName));
                    controllersTemplate.Replace("{PkDataType}", fields.First(z => z.IsPrimaryKey).CsDataType);
                    controllersTemplate.Replace("{PkName}", fields.First(z => z.IsPrimaryKey).SafeCsPropertyName);

                    string getRowFields = "";
                    foreach (Field field in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        getRowFields = getRowFields + string.Format("            retval.{0} = data.{0};{1}", field.SafeCsPropertyName, Environment.NewLine);
                    }
                    controllersTemplate.Replace("{GetRowFields}", getRowFields);

                    controllersTemplate.Replace("{ActualDeleteTrueFalse}",
                        fields.Any(z => z.FieldName.ToLower() == "isdeleted") ? "false" : "true");

                    string deleteIdCheck = "";
                    switch (fields.First(z => z.IsPrimaryKey).CsDataType.ToLower())
                    {
                        case "int":
                        case "long":
                        case "byte":
                        case "sbyte":
                        case "uint":
                        case "short":
                        case "ushort":
                        case "ulong":
                        case "float":
                        case "double":
                        case "decimal":
                            deleteIdCheck = deleteIdCheck + string.Format("            if (id <= 0){0}", Environment.NewLine);
                            deleteIdCheck = deleteIdCheck + string.Format("            {{{0}", Environment.NewLine);
                            deleteIdCheck = deleteIdCheck + string.Format("                return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel {{ Status = \"400\", Message = \"Invalid ID!\" }});{0}", Environment.NewLine);
                            deleteIdCheck = deleteIdCheck + string.Format("            }}{0}", Environment.NewLine);
                            break;
                    }
                    controllersTemplate.Replace("{DeleteIdCheck}", deleteIdCheck);
                    controllersTemplate.Replace("{DeleteIdField}",
                        fields.Any(z => z.FieldName.ToLower() == "isdeleted") ? "data.IsDeleted = true;" : "");

                    string putIdCheck = "";
                    switch (fields.First(z => z.IsPrimaryKey).CsDataType.ToLower())
                    {
                        case "int":
                        case "long":
                        case "byte":
                        case "sbyte":
                        case "uint":
                        case "short":
                        case "ushort":
                        case "ulong":
                        case "float":
                        case "double":
                        case "decimal":
                            putIdCheck = putIdCheck + string.Format("            if (id <= 0){0}", Environment.NewLine);
                            putIdCheck = putIdCheck + string.Format("            {{{0}", Environment.NewLine);
                            putIdCheck = putIdCheck + string.Format("                return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel {{ Status = \"400\", Message = \"Invalid ID!\" }});{0}", Environment.NewLine);
                            putIdCheck = putIdCheck + string.Format("            }}{0}", Environment.NewLine);
                            break;
                    }
                    controllersTemplate.Replace("{PutIdCheck}", putIdCheck);



                    string putParamsCheck = "";
                    foreach (Field f in fields.OrderBy(z => z.FieldName))
                    {
                        if (f.IsPrimaryKey)
                        {
                            continue;
                        }
                        if (f.IsComputedField)
                        {
                            continue;
                        }

                        if (hasCreatedField && f.FieldName.ToLower() == "created")
                        {
                            continue;
                        }

                        if (f.CsDataType == "string" && !f.CanBeNull)
                        {
                            putParamsCheck = putParamsCheck + string.Format("            if (model.{0} == null){1}", f.SafeCsPropertyName, Environment.NewLine);
                            putParamsCheck = putParamsCheck + string.Format("            {{{0}", Environment.NewLine);
                            putParamsCheck = putParamsCheck + string.Format("                return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel {{ Status = \"400\", Message = \"{0} is required!\" }});{1}", f.SafeCsPropertyName, Environment.NewLine);
                            putParamsCheck = putParamsCheck + string.Format("            }}{0}", Environment.NewLine);
                        }
                    }
                    controllersTemplate.Replace("{PutParamsCheck}", putParamsCheck);

                    string putRowFields1 = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        if (f.IsPrimaryKey)
                        {
                            continue;
                        }
                        if (hasCreatedField && f.FieldName.ToLower() == "created")
                        {
                            continue;
                        }
                        putRowFields1 = putRowFields1 + string.Format("                data.{0} = model.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                    }
                    controllersTemplate.Replace("{PutRowFields1}", putRowFields1);

                    string putRowFields2 = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        putRowFields2 = putRowFields2 + string.Format("                retval.{0} = data.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                    }
                    controllersTemplate.Replace("{PutRowFields2}", putRowFields2);

                    string postParamsCheck = "";
                    foreach (Field f in fields.OrderBy(z => z.FieldName))
                    {
                        if (f.IsPrimaryKey)
                        {
                            if (f.IsIdentity)
                            {
                                continue;
                            }
                        }

                        if (f.IsComputedField)
                        {
                            continue;
                        }

                        if (hasCreatedField && f.FieldName.ToLower() == "created")
                        {
                            continue;
                        }

                        if (f.CsDataType == "int" || f.CsDataType == "long")
                        {
                            if (!f.CanBeNull && !f.IsIdentity)
                            {
                                postParamsCheck = postParamsCheck + string.Format("            if (model.{0} == 0){1}", f.SafeCsPropertyName, Environment.NewLine);
                                postParamsCheck = postParamsCheck + string.Format("            {{{0}", Environment.NewLine);
                                postParamsCheck = postParamsCheck + string.Format("                return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel {{ Status = \"400\", Message = \"{0} is required!\" }});{1}", f.SafeCsPropertyName, Environment.NewLine);
                                postParamsCheck = postParamsCheck + string.Format("            }}{0}", Environment.NewLine);
                            }
                        }
                        if (f.CsDataType == "string" && !f.CanBeNull)
                        {
                            postParamsCheck = postParamsCheck + string.Format("            if (model.{0} == null){1}", f.SafeCsPropertyName, Environment.NewLine);
                            postParamsCheck = postParamsCheck + string.Format("            {{{0}", Environment.NewLine);
                            postParamsCheck = postParamsCheck + string.Format("                return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel {{ Status = \"400\", Message = \"{0} is required!\" }});{1}", f.SafeCsPropertyName, Environment.NewLine);
                            postParamsCheck = postParamsCheck + string.Format("            }}{0}", Environment.NewLine);
                        }
                    }
                    controllersTemplate.Replace("{PostParamsCheck}", postParamsCheck);

                    string postRowFields1 = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        if (f.IsPrimaryKey && f.IsIdentity)
                        {
                            continue;
                        }

                        ForeignKey fk = foreignKeys.FirstOrDefault(z => z.ForeignColumnName == f.FieldName);
                        if (fk != null)
                        {
                            postRowFields1 = postRowFields1 + "                if (model." + f.SafeCsPropertyName + " == 0)" + Environment.NewLine;
                            postRowFields1 = postRowFields1 + "                {" + Environment.NewLine;
                            postRowFields1 = postRowFields1 +
                                             string.Format("                    data.SetNull({0}.{1}.Fields.{2});{3}",
                                                 BusinessNamespaceName, Common.GetSafeCsName(objName),
                                                 f.SafeCsPropertyName, Environment.NewLine);
                            postRowFields1 = postRowFields1 + "                }" + Environment.NewLine;
                            postRowFields1 = postRowFields1 + "                else" + Environment.NewLine;
                            postRowFields1 = postRowFields1 + "                {" + Environment.NewLine;
                            postRowFields1 = postRowFields1 + string.Format("                    data.{0} = model.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                            postRowFields1 = postRowFields1 + "                }" + Environment.NewLine;
                        }
                        else
                        {
                            if (hasCreatedField && f.FieldName.ToLower() == "created")
                            {
                                postRowFields1 = postRowFields1 + string.Format("                data.{0} = DateTime.UtcNow;{1}", f.SafeCsPropertyName, Environment.NewLine);
                            }
                            else
                            {
                                postRowFields1 = postRowFields1 + string.Format("                data.{0} = model.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                            }
                        }
                    }
                    controllersTemplate.Replace("{PostRowFields1}", postRowFields1);

                    string postRowFields2 = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        postRowFields2 = postRowFields2 + string.Format("                retval.{0} = data.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                    }
                    controllersTemplate.Replace("{PostRowFields2}", postRowFields2);

                    string getFields1 = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        getFields1 = getFields1 + string.Format("                m.{0} = d.{0};{1}", f.SafeCsPropertyName, Environment.NewLine);
                    }
                    controllersTemplate.Replace("{GetFields1}", getFields1);

                    string validFields = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        validFields = validFields + string.Format("                case \"{0}\":{1}", f.FieldName.ToLower(), Environment.NewLine);
                        validFields = validFields + string.Format("                case \"-{0}\":{1}", f.FieldName.ToLower(), Environment.NewLine);
                        validFields = validFields + string.Format("                case \"+{0}\":{1}", f.FieldName.ToLower(), Environment.NewLine);
                    }
                    controllersTemplate.Replace("{ValidFields}", validFields);

                    string queryString = "";
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        if (f.CsDataType.ToLower() != "string")
                        {
                            continue;
                        }
                        queryString = queryString + string.Format(" [{0}] LIKE '%\" + escapedQuery + \"%' OR", f.FieldName);
                    }

                    if (queryString.Length >= 3)
                    {
                        queryString = queryString.Remove(queryString.Length - 3);
                    }
                    
                    controllersTemplate.Replace("{QueryString}", queryString);

                    string patchFields = "";
                    
                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.FieldName))
                    {
                        if (f.IsPrimaryKey)
                        {
                            continue;
                        }
                        
                        patchFields = patchFields + "                    if (path.ToLower() == \"" + f.FieldName.ToLower() + "\")" + Environment.NewLine;
                        patchFields = patchFields + "                    {" + Environment.NewLine;
                        patchFields = patchFields + "                        if (patch.Value == null)" + Environment.NewLine;
                        patchFields = patchFields + "                        {" + Environment.NewLine;
                        if (f.CanBeNull)
                        {
                            patchFields = patchFields + "                            data.SetNull(" + BusinessNamespaceName + "." + Common.GetSafeCsName(objName) + ".Fields." + f.SafeCsPropertyName + ");" + Environment.NewLine;
                        }
                        else
                        {
                            patchFields = patchFields + "                            return CreateResponse(HttpStatusCode.BadRequest, new Models.WebApiErrorModel { Status = \"400\", Message = \"Field cannot be null: '\" + path + \"'\" });" + Environment.NewLine;
                        }
                        patchFields = patchFields + "                        }" + Environment.NewLine;
                        patchFields = patchFields + "                        else" + Environment.NewLine;
                        patchFields = patchFields + "                        {" + Environment.NewLine;
                        if (f.CsDataType.ToLower() == "int" || f.CsDataType.ToLower() == "short")
                        {
                            patchFields = patchFields + "                            try" + Environment.NewLine;
                            patchFields = patchFields + "                            {" + Environment.NewLine;
                            patchFields = patchFields + "                                data." + f.SafeCsPropertyName + " = (" + f.CsDataType + ")patch.Value;" + Environment.NewLine;
                            patchFields = patchFields + "                            }" + Environment.NewLine;
                            patchFields = patchFields + "                            catch (InvalidCastException)" + Environment.NewLine;
                            patchFields = patchFields + "                            {" + Environment.NewLine;
                            patchFields = patchFields + "                                data." + f.SafeCsPropertyName + " = (" + f.CsDataType + ")((long)patch.Value);" + Environment.NewLine;
                            patchFields = patchFields + "                            }" + Environment.NewLine;
                        }
                        else if (f.CsDataType.ToLower() == "float")
                        {
                            patchFields = patchFields + "                            try" + Environment.NewLine;
                            patchFields = patchFields + "                            {" + Environment.NewLine;
                            patchFields = patchFields + "                                data." + f.SafeCsPropertyName + " = (" + f.CsDataType + ")patch.Value;" + Environment.NewLine;
                            patchFields = patchFields + "                            }" + Environment.NewLine;
                            patchFields = patchFields + "                            catch (InvalidCastException)" + Environment.NewLine;
                            patchFields = patchFields + "                            {" + Environment.NewLine;
                            patchFields = patchFields + "                                data." + f.SafeCsPropertyName + " = (" + f.CsDataType + ")((double)patch.Value);" + Environment.NewLine;
                            patchFields = patchFields + "                            }" + Environment.NewLine;
                        }
                        else
                        {
                            patchFields = patchFields + "                            data." + f.SafeCsPropertyName + " = (" + f.CsDataType + ")patch.Value;" + Environment.NewLine;
                        }
                        
                        patchFields = patchFields + "                        }" + Environment.NewLine;
                        patchFields = patchFields + "                    }" + Environment.NewLine;
                    }
                    controllersTemplate.Replace("{PatchFields}", patchFields);
                    sw.Write(controllersTemplate);
                }


                using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Models", objName.ToProperFileName() + "Model.cs")))
                {
                    modelsTemplate.Replace("{DataNamespaceName}", DataNamespaceName);
                    modelsTemplate.Replace("{SafeTableName}", Common.GetSafeCsName(objName));

                    string backingFields = "";
                    string properties = "";

                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.SafeCsFieldName))
                    {
                        backingFields = backingFields + string.Format("        [JsonIgnore]{0}", Environment.NewLine);
                        backingFields = backingFields + string.Format("        private {0} {1};{2}", f.CsDataType, f.SafeCsFieldName, Environment.NewLine);

                        properties = properties + string.Format("        public {0} {1}{2}", f.CsDataType, f.SafeCsPropertyName, Environment.NewLine);
                        properties = properties + string.Format("        {{{0}", Environment.NewLine);
                        properties = properties + string.Format("            get {{ return {0}; }}{1}", f.SafeCsFieldName, Environment.NewLine);
                        properties = properties + string.Format("            set {{ {0} = value; }}{1}", f.SafeCsFieldName, Environment.NewLine);
                        properties = properties + string.Format("        }}{0}{0}", Environment.NewLine);
                    }

                    modelsTemplate.Replace("{BackingFields}", backingFields);
                    modelsTemplate.Replace("{Properties}", properties);

                    sw.Write(modelsTemplate);
                }

                using (StreamWriter sw = File.CreateText(Path.Combine(OutputDirectory, "WebApi", "Models", objName.ToProperFileName() + "CreateModel.cs")))
                {
                    createModelsTemplate.Replace("{DataNamespaceName}", DataNamespaceName);
                    createModelsTemplate.Replace("{SafeTableName}", Common.GetSafeCsName(objName));

                    string backingFields = "";
                    string properties = "";

                    foreach (Field f in fields.OrderByDescending(z => z.IsPrimaryKey).ThenBy(z => z.SafeCsFieldName))
                    {
                        if (f.IsIdentity)
                        {
                            continue;
                        }

                        if (hasCreatedField && f.FieldName.ToLower() == "created")
                        {
                            continue;
                        }

                        backingFields = backingFields + string.Format("        [JsonIgnore]{0}", Environment.NewLine);
                        backingFields = backingFields + string.Format("        private {0} {1};{2}", f.CsDataType, f.SafeCsFieldName, Environment.NewLine);

                        properties = properties + string.Format("        public {0} {1}{2}", f.CsDataType, f.SafeCsPropertyName, Environment.NewLine);
                        properties = properties + string.Format("        {{{0}", Environment.NewLine);
                        properties = properties + string.Format("            get {{ return {0}; }}{1}", f.SafeCsFieldName, Environment.NewLine);
                        properties = properties + string.Format("            set {{ {0} = value; }}{1}", f.SafeCsFieldName, Environment.NewLine);
                        properties = properties + string.Format("        }}{0}{0}", Environment.NewLine);
                    }

                    createModelsTemplate.Replace("{BackingFields}", backingFields);
                    createModelsTemplate.Replace("{Properties}", properties);

                    sw.Write(createModelsTemplate);
                }
            }
        }

        private void CreateCsUniversalFile()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var universal1Template = new StringBuilder();
            var universal2Template = new StringBuilder();

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal1SqlServerCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal1Template.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal2SqlServerCs.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal2Template.Append(reader.ReadToEnd());
                    }
                }
            }

            if (!CreateAsyncMethods)
            {
                int ndx = universal1Template.IndexOf("{async}");

                while (ndx >= 0)
                {
                    int ndx2 = universal1Template.IndexOf("{/async}", ndx);

                    universal1Template = universal1Template.Remove(ndx, ndx2 - ndx + 8);

                    ndx = universal1Template.IndexOf("{async}");
                }
            }
            universal1Template = universal1Template.Replace("{async}", "");
            universal1Template = universal1Template.Replace("{/async}", "");

            if (!CreateAsyncMethods)
            {
                int ndx = universal2Template.IndexOf("{async}");

                while (ndx >= 0)
                {
                    int ndx2 = universal2Template.IndexOf("{/async}", ndx);

                    universal2Template = universal2Template.Remove(ndx, ndx2 - ndx + 8);

                    ndx = universal2Template.IndexOf("{async}");
                }
            }
            universal2Template = universal2Template.Replace("{async}", "");
            universal2Template = universal2Template.Replace("{/async}", "");

            universal2Template.Replace("{0}", DataNamespaceName);

            using (StreamWriter sw = File.CreateText(OutputDirectory + "\\Universal.cs"))
            {
                if (HasDynamicDataRetrieval)
                {
                    sw.WriteLine("using System;");
                    sw.WriteLine("using System.Collections.Generic;");
                    sw.WriteLine("using System.Data;");
                    sw.WriteLine("using System.Data.SqlClient;");
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
                sw.WriteLine();
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

        private void CreateVbUniversalFile()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var universal1Template = new StringBuilder();
            var universal2Template = new StringBuilder();

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal1SqlServerVb.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal1Template.Append(reader.ReadToEnd());
                    }
                }
            }

            using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.Universal2SqlServerVb.txt"))
            {
                if (stream != null)
                {
                    using (var reader = new StreamReader(stream))
                    {
                        universal2Template.Append(reader.ReadToEnd());
                    }
                }
            }

            if (!CreateAsyncMethods)
            {
                int ndx = universal1Template.IndexOf("{async}");

                while (ndx >= 0)
                {
                    int ndx2 = universal1Template.IndexOf("{/async}", ndx);

                    universal1Template = universal1Template.Remove(ndx, ndx2 - ndx + 8);

                    ndx = universal1Template.IndexOf("{async}");
                }
            }
            universal1Template = universal1Template.Replace("{async}", "");
            universal1Template = universal1Template.Replace("{/async}", "");

            if (!CreateAsyncMethods)
            {
                int ndx = universal2Template.IndexOf("{async}");

                while (ndx >= 0)
                {
                    int ndx2 = universal2Template.IndexOf("{/async}", ndx);

                    universal2Template = universal2Template.Remove(ndx, ndx2 - ndx + 8);

                    ndx = universal2Template.IndexOf("{async}");
                }
            }
            universal2Template = universal2Template.Replace("{async}", "");
            universal2Template = universal2Template.Replace("{/async}", "");

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
                sw.WriteLine();
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

        private void CreateCsBusinessLayers()
        {
            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var businessLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.BusinessLayer.SqlServerCSharp.txt"))
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

                if (!CreateAsyncMethods)
                {
                    int ndx = businessLayerTemplate.IndexOf("{async}");

                    while (ndx >= 0)
                    {
                        int ndx2 = businessLayerTemplate.IndexOf("{/async}", ndx);

                        businessLayerTemplate = businessLayerTemplate.Remove(ndx, ndx2 - ndx + 8);

                        ndx = businessLayerTemplate.IndexOf("{async}");
                    }
                }
                businessLayerTemplate = businessLayerTemplate.Replace("{async}", "");
                businessLayerTemplate = businessLayerTemplate.Replace("{/async}", "");

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
                    var fkBusinessGetByAsync = new StringBuilder();

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

                            fkBusinessGetByAsync.Append("        public async System.Threading.Tasks.Task GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    fkBusinessGetByAsync.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkBusinessGetByAsync.Append("        {" + Environment.NewLine);
                            fkBusinessGetByAsync.AppendLine("            LayerGenConnectionString connectString = new LayerGenConnectionString();");
                            fkBusinessGetByAsync.AppendLine("            connectString.ConnectionString = _connectionString;");
                            fkBusinessGetByAsync.Append("            DataTable dt = await " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(connectString, fkId);" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            if (dt != null)" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            {" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("                Load(dt.Rows);" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            }" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("        }" + Environment.NewLine);


                            fkBusinessGetByAsync.Append("        public async System.Threading.Tasks.Task GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    fkBusinessGetByAsync.Append(f.CsDataType + " fkId, System.Threading.CancellationToken cancellationToken)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkBusinessGetByAsync.Append("        {" + Environment.NewLine);
                            fkBusinessGetByAsync.AppendLine("            LayerGenConnectionString connectString = new LayerGenConnectionString();");
                            fkBusinessGetByAsync.AppendLine("            connectString.ConnectionString = _connectionString;");
                            fkBusinessGetByAsync.Append("            DataTable dt = await " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + ".GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(connectString, fkId, cancellationToken);" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            if (dt != null)" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            {" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("                Load(dt.Rows);" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("            }" + Environment.NewLine);
                            fkBusinessGetByAsync.Append("        }" + Environment.NewLine);
                        }
                    }

                    if (!CreateAsyncMethods)
                    {
                        businessLayerTemplate.Replace("{2}", fkBusinessGetBy.ToString());
                    }
                    else
                    {
                        businessLayerTemplate.Replace("{2}", fkBusinessGetBy.ToString() + Environment.NewLine + fkBusinessGetByAsync.ToString());
                    }

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
                        serializationCode.AppendLine("            Encryption64 encryptor = new Encryption64();");
                        serializationCode.AppendLine("            List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "> zs = new List<" + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + ">();");
                        serializationCode.AppendLine();
                        serializationCode.AppendLine("            foreach (" + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + " z in this)");
                        serializationCode.AppendLine("            {");
                        serializationCode.AppendLine("                " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + " " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + " = new " + DataNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(objName)) + "." + Common.GetSafeCsName("Serializable" + Common.GetCsPropertyName(objName)) + "();");
                        foreach (var field in fields.OrderByDescending(z => z.IsPrimaryKey))
                        {
                            if (field.IsValueType)
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z.IsNull(" + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                    ? (" + field.CsDataType + "?) null : z." + field.SafeCsPropertyName + ";");
                            }
                            else if (field.CsDataType == "string")
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z.IsNull(" + Common.GetCsPropertyName(objName) + ".Fields." + field.SafeCsPropertyName + ")");
                                serializationCode.AppendLine("                    ? null : z." + field.SafeCsPropertyName + ";");
                            }
                            else
                            {
                                serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + "." + field.SafeCsPropertyName + " = z." + field.SafeCsPropertyName + ";");
                            }
                        }
                        serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationIsUpdate = z.LayerGenIsUpdate();");
                        serializationCode.AppendLine("                " + Common.GetSafeCsName("serializable" + Common.GetCsPropertyName(objName)) + ".SerializationConnectionString = encryptor.Encrypt(z.LayerGenConnectionString(), " + DataNamespaceName + ".Universal.LayerGenEncryptionKey);");
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

        private void CreateVbBusinessLayers()
        {
            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var businessLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.BusinessLayer.SqlServerVbNet.txt"))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            businessLayerTemplate.Append(reader.ReadToEnd());
                        }
                    }
                }

                if (!CreateAsyncMethods)
                {
                    int ndx = businessLayerTemplate.IndexOf("{async}");

                    while (ndx >= 0)
                    {
                        int ndx2 = businessLayerTemplate.IndexOf("{/async}", ndx);

                        businessLayerTemplate = businessLayerTemplate.Remove(ndx, ndx2 - ndx + 8);

                        ndx = businessLayerTemplate.IndexOf("{async}");
                    }
                }
                businessLayerTemplate = businessLayerTemplate.Replace("{async}", "");
                businessLayerTemplate = businessLayerTemplate.Replace("{/async}", "");

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
                            fkBusinessGetBy.Append("            Dim connString As New LayerGenConnectionString" + Environment.NewLine);
                            fkBusinessGetBy.Append("            connString.ConnectionString = _connectionString" + Environment.NewLine);
                            fkBusinessGetBy.Append("            Dim dt As DataTable = " + DataNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(objName)) + ".GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "(connString, fkId)" + Environment.NewLine);
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
                        serializationCode.AppendLine("            " + Common.GetSafeVbName("serializable" + Common.GetVbPropertyName(objName)) + ".SerializationIsUpdate = z.LayerGenIsUpdate()");
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

        private void CreateCsDataLayers()
        {
            var storedProcedures = new StringBuilder();

            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;
                var assembly = Assembly.GetExecutingAssembly();
                StringBuilder dataLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.DataLayer.SqlServerCSharp.txt"))
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
                if (!CreateAsyncMethods)
                {
                    int ndx = dataLayerTemplate.IndexOf("{async}");

                    while (ndx >= 0)
                    {
                        int ndx2 = dataLayerTemplate.IndexOf("{/async}", ndx);

                        dataLayerTemplate = dataLayerTemplate.Remove(ndx, ndx2 - ndx + 8);

                        ndx = dataLayerTemplate.IndexOf("{async}");
                    }
                }
                dataLayerTemplate = dataLayerTemplate.Replace("{async}", "");
                dataLayerTemplate = dataLayerTemplate.Replace("{/async}", "");
                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Data.cs"))
                {
                    storedProcedures.Append(CreateStoredProcedures(Common.GetSafeCsName(Common.GetCsPropertyName(objName)), fields, isView));

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
                        }
                        else if ((!field.IsComputedField) && (!isView))
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
                        savePart.AppendLine("                        parameter = new SqlParameter(\"@val" + i + "\", SqlDbType." + field.SqlDbType + ");");
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
                            str1.Append(f.SafeCsFieldName + " = (" + f.CsDataType + ") (decimal) obj;" + Environment.NewLine);
                    }

                    dataLayerTemplate.Replace("{15}", str1.ToString());

                    str1.Clear();
                    str1.Append("                    const string cmdString = \"UPDATE [" + DefaultSchema + "].[\" + LayerGenTableName + \"] SET ");
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
                        dataLayerTemplate.Replace("{18}", "        private " + fields.First(z => z.IsPrimaryKey).CsDataType + " _oldPrimaryKeyValue;" + Environment.NewLine);
                        dataLayerTemplate.Replace("{19}", fields.First(z => z.IsPrimaryKey).CsDataType);

                        if (fields.First(z => z.IsPrimaryKey).TextBased || fields.First(z => z.IsPrimaryKey).CsDataType.ToLower() == "guid")
                        {
                            dataLayerTemplate.Replace("{20}", "            string sql = \"SELECT \" + strFields + \" FROM [" + DefaultSchema + "].[\" + LayerGenTableName + \"] WHERE \" + LayerGenPrimaryKey + \"=@val1\";" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "                    command.Parameters.AddWithValue(\"@val1\", id);");
                        }
                        else
                        {
                            dataLayerTemplate.Replace("{20}", "            string sql = \"SELECT \" + strFields + \" FROM [" + DefaultSchema + "].[\" + LayerGenTableName + \"] WHERE \" + LayerGenPrimaryKey + \"=\" + id;" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "");
                        }
                    }

                    var fkDataGetBy = new StringBuilder();
                    var fkDataGetByAsync = new StringBuilder();
                    var fkDataGetByConnString = new StringBuilder();
                    var fkDataGetByConnStringAsync = new StringBuilder();

                    if (!isView)
                    {
                        //var fkProperties = new StringBuilder();
                        var fkMethods = new StringBuilder();
                        var fkFields = new StringBuilder();
                        string sqlDataType = "";

                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkFields.Append("        private " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + " _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";" + Environment.NewLine);


                            fkMethods.AppendLine("        public " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + " F" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "()");
                            fkMethods.AppendLine("        {");
                            fkMethods.AppendLine("            if (_my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " == null)");
                            fkMethods.AppendLine("            {");
                            fkMethods.AppendLine("                _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " = new " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "();");
                            fkMethods.AppendLine("                _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ".LoadRow(" + Common.GetCsFieldName(key.ForeignColumnName) + ");");
                            fkMethods.AppendLine("            }");
                            fkMethods.AppendLine();
                            fkMethods.AppendLine("            return _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";");
                            fkMethods.AppendLine("        }");

                            if (CreateAsyncMethods)
                            {
                                fkMethods.AppendLine();
                                fkMethods.AppendLine("        public async System.Threading.Tasks.Task<" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "> F" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async()");
                                fkMethods.AppendLine("        {");
                                fkMethods.AppendLine("            if (_my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " == null)");
                                fkMethods.AppendLine("            {");
                                fkMethods.AppendLine("                _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " = new " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "();");
                                fkMethods.AppendLine("                await _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ".LoadRowAsync(" + Common.GetCsFieldName(key.ForeignColumnName) + ");");
                                fkMethods.AppendLine("            }");
                                fkMethods.AppendLine();
                                fkMethods.AppendLine("            return _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";");
                                fkMethods.AppendLine("        }");

                                fkMethods.AppendLine();
                                fkMethods.AppendLine("        public async System.Threading.Tasks.Task<" + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "> F" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(System.Threading.CancellationToken cancellationToken)");
                                fkMethods.AppendLine("        {");
                                fkMethods.AppendLine("            if (_my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " == null)");
                                fkMethods.AppendLine("            {");
                                fkMethods.AppendLine("                _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " = new " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "();");
                                fkMethods.AppendLine("                await _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ".LoadRowAsync(" + Common.GetCsFieldName(key.ForeignColumnName) + ", cancellationToken);");
                                fkMethods.AppendLine("            }");
                                fkMethods.AppendLine();
                                fkMethods.AppendLine("            return _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";");
                                fkMethods.AppendLine("        }");
                            }


                            //fkProperties.Append("        public " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + " F" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " {" + Environment.NewLine);
                            //fkProperties.Append("            get {" + Environment.NewLine);
                            //fkProperties.Append("                if (_my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " == null) {" + Environment.NewLine);
                            //fkProperties.Append("                    _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + " = new " + BusinessNamespaceName + "." + Common.GetSafeCsName(Common.GetCsPropertyName(key.PrimaryTableName)) + "(" + Common.GetCsFieldName(key.ForeignColumnName) + ");" + Environment.NewLine);
                            //fkProperties.Append("                }" + Environment.NewLine);
                            //fkProperties.Append("                return _my" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + ";" + Environment.NewLine);
                            //fkProperties.Append("            }" + Environment.NewLine);
                            //fkProperties.Append("        }" + Environment.NewLine + Environment.NewLine);

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

                            fkDataGetByAsync.Append("        internal static async System.Threading.Tasks.Task<DataTable> GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByAsync.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }

                            fkDataGetBy.Append("        {" + Environment.NewLine);
                            fkDataGetBy.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetBy.Append("            {" + Environment.NewLine);
                            fkDataGetBy.Append("                connection.ConnectionString = Universal.GetConnectionString();" + Environment.NewLine);
                            fkDataGetBy.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetBy.Append("                {" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetBy.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetBy.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetBy.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetBy.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                    connection.Open();" + Environment.NewLine);
                            fkDataGetBy.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
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

                            fkDataGetByAsync.Append("        {" + Environment.NewLine);
                            fkDataGetByAsync.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetByAsync.Append("            {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                connection.ConnectionString = Universal.GetConnectionString();" + Environment.NewLine);
                            fkDataGetByAsync.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByAsync.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetByAsync.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByAsync.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByAsync.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                    await connection.OpenAsync();" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            await System.Threading.Tasks.Task.Run(() => adapter.Fill(ds));" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                }" + Environment.NewLine);
                            fkDataGetByAsync.Append("            }" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("            return null;" + Environment.NewLine);
                            fkDataGetByAsync.Append("        }" + Environment.NewLine + Environment.NewLine);


                            fkDataGetByAsync.Append("        internal static async System.Threading.Tasks.Task<DataTable> GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByAsync.Append(f.CsDataType + " fkId, System.Threading.CancellationToken cancellationToken)" + Environment.NewLine);
                                    break;
                                }
                            }


                            fkDataGetByAsync.Append("        {" + Environment.NewLine);
                            fkDataGetByAsync.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetByAsync.Append("            {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                connection.ConnectionString = Universal.GetConnectionString();" + Environment.NewLine);
                            fkDataGetByAsync.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByAsync.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetByAsync.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByAsync.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByAsync.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                    await connection.OpenAsync(cancellationToken);" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            await System.Threading.Tasks.Task.Run(() => adapter.Fill(ds), cancellationToken);" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            {" + Environment.NewLine);
                            fkDataGetByAsync.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    }" + Environment.NewLine);
                            fkDataGetByAsync.Append("                }" + Environment.NewLine);
                            fkDataGetByAsync.Append("            }" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("            return null;" + Environment.NewLine);
                            fkDataGetByAsync.Append("        }" + Environment.NewLine);









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

                            fkDataGetByConnStringAsync.Append("        internal static async System.Threading.Tasks.Task<DataTable> GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(" + BusinessNamespaceName + ".LayerGenConnectionString connectionString, ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnStringAsync.Append(f.CsDataType + " fkId)" + Environment.NewLine);
                                    break;
                                }
                            }

                            fkDataGetByConnString.Append("        {" + Environment.NewLine);
                            fkDataGetByConnString.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetByConnString.Append("            {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                connection.ConnectionString = connectionString.ConnectionString;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetByConnString.Append("                {" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByConnString.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetByConnString.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByConnString.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByConnString.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("                    connection.Open();" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
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

                            fkDataGetByConnStringAsync.Append("        {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                connection.ConnectionString = connectionString.ConnectionString;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByConnStringAsync.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetByConnStringAsync.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByConnStringAsync.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByConnStringAsync.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    await connection.OpenAsync();" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            await System.Threading.Tasks.Task.Run(() => adapter.Fill(ds));" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            return null;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("        }" + Environment.NewLine + Environment.NewLine);

                            fkDataGetByConnStringAsync.Append("        internal static async System.Threading.Tasks.Task<DataTable> GetBy" + Common.GetSafeCsName(Common.GetCsPropertyName(key.ForeignColumnName)) + "Async(" + BusinessNamespaceName + ".LayerGenConnectionString connectionString, ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnStringAsync.Append(f.CsDataType + " fkId, System.Threading.CancellationToken cancellationToken)" + Environment.NewLine);
                                    break;
                                }
                            }

                            fkDataGetByConnStringAsync.Append("        {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            using (SqlConnection connection = new SqlConnection())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                connection.ConnectionString = connectionString.ConnectionString;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                using (SqlCommand command = new SqlCommand())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.Connection = connection;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandType = CommandType.Text;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\";" + Environment.NewLine);
                            fkDataGetByConnStringAsync.AppendLine("                    SqlParameter parameter =  new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ");");
                            fkDataGetByConnStringAsync.AppendLine("                    parameter.Value = fkId;");
                            fkDataGetByConnStringAsync.AppendLine("                    command.Parameters.Add(parameter);");
                            //fkDataGetByConnStringAsync.Append("                    command.Parameters.Add(new SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") {Value = fkId});" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    await connection.OpenAsync(cancellationToken);" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    using (SqlDataAdapter adapter = new SqlDataAdapter())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        using (DataSet ds = new DataSet())" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            adapter.SelectCommand = command;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            await System.Threading.Tasks.Task.Run(() => adapter.Fill(ds), cancellationToken);" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            if (ds.Tables.Count > 0)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            {" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                                return ds.Tables[0];" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            }" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            return null;" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("        }" + Environment.NewLine);
                        }

                        dataLayerTemplate.Replace("{22}", fkFields.ToString());
                        //dataLayerTemplate.Replace("{21}", fkProperties.ToString());
                        dataLayerTemplate.Replace("{21}", fkMethods.ToString());
                    }

                    if (!CreateAsyncMethods)
                    {
                        dataLayerTemplate.Replace("{23}", fkDataGetBy + Environment.NewLine + fkDataGetByConnString);
                    }
                    else
                    {
                        dataLayerTemplate.Replace("{23}", fkDataGetBy + Environment.NewLine + fkDataGetByConnString + Environment.NewLine + fkDataGetByAsync + Environment.NewLine + fkDataGetByConnStringAsync);
                    }
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
                    dataLayerTemplate.Replace("{30}", DefaultSchema);

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
                            }
                            else if (field.CsDataType == "string")
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

        private void CreateVbDataLayers()
        {
            var storedProcedures = new StringBuilder();

            foreach (string objectName in Objects.Split(';'))
            {
                UpdateProgress(_progressNdx, Objects.Split(';').Length * 2);
                _progressNdx++;

                var assembly = Assembly.GetExecutingAssembly();
                var dataLayerTemplate = new StringBuilder();

                using (Stream stream = assembly.GetManifestResourceStream("LayerGen35.Templates.DataLayer.SqlServerVbNet.txt"))
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

                if (!CreateAsyncMethods)
                {
                    int ndx = dataLayerTemplate.IndexOf("{async}");

                    while (ndx >= 0)
                    {
                        int ndx2 = dataLayerTemplate.IndexOf("{/async}", ndx);

                        dataLayerTemplate = dataLayerTemplate.Remove(ndx, ndx2 - ndx + 8);

                        ndx = dataLayerTemplate.IndexOf("{async}");
                    }
                }
                dataLayerTemplate = dataLayerTemplate.Replace("{async}", "");
                dataLayerTemplate = dataLayerTemplate.Replace("{/async}", "");

                using (StreamWriter sw = File.CreateText(OutputDirectory + "\\" + objName.ToProperFileName() + "Data.vb"))
                {
                    storedProcedures.Append(CreateStoredProcedures(Common.GetSafeCsName(Common.GetCsPropertyName(objName)), fields, isView));

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
                        savePart.Append("                                Dim param As New SqlParameter(\"@val" + i + "\", SqlDbType." + field.SqlDbType + ")" + Environment.NewLine);
                        if (field.CanBeNull)
                            savePart.Append("                                param.Value = DBNull.Value" + Environment.NewLine);
                        else savePart.Append("                                param.Value = " + GetDefaultVbValue(field) + Environment.NewLine);
                        savePart.Append("                                command.Parameters.Add(param)" + Environment.NewLine);
                        savePart.Append("                            Else" + Environment.NewLine);
                        savePart.Append("                                Dim param As New SqlParameter(\"@val" + i + "\", SqlDbType." + field.SqlDbType + ")" + Environment.NewLine);
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
                        if (f.IsIdentity)
                            str1.Append(f.SafeVbFieldName + " = " + Common.GetVbConversionFunction(f.VbDataType) + "(obj)" + Environment.NewLine);
                    }

                    dataLayerTemplate = dataLayerTemplate.Replace("{15}", str1.ToString());

                    str1.Clear();
                    str1.Append("                    Const cmdString As String = \"UPDATE [" + DefaultSchema + "].[\" & LayerGenTableName & \"] SET ");
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
                            dataLayerTemplate = dataLayerTemplate.Replace("{20}", "            Dim sql As String = \"SELECT \" & strFields & \" FROM [" + DefaultSchema + "].[\" & LayerGenTableName & \"] WHERE \" & LayerGenPrimaryKey & \"=@val1\"" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "                    command.Parameters.AddWithValue(\"@val1\", id)");
                        }
                        else
                        {
                            dataLayerTemplate = dataLayerTemplate.Replace("{20}", "            Dim sql As String = \"SELECT \" & strFields & \" FROM [" + DefaultSchema + "].[\" & LayerGenTableName & \"] WHERE \" & LayerGenPrimaryKey & \"=\" & id" + Environment.NewLine);
                            dataLayerTemplate.Replace("{28}", "");
                        }
                    }

                    var fkDataGetBy = new StringBuilder();
                    var fkDataGetByAsync = new StringBuilder();
                    var fkDataGetByConnString = new StringBuilder();
                    var fkDataGetByConnStringAsync = new StringBuilder();

                    if (!isView)
                    {
                        //var fkProperties = new StringBuilder();
                        var fkMethods = new StringBuilder();
                        var fkFields = new StringBuilder();
                        string sqlDataType = "";

                        List<ForeignKey> keys = GetForeignKeys(objName);

                        foreach (ForeignKey key in keys)
                        {
                            fkFields.Append("        Private _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " As " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + Environment.NewLine);

                            fkMethods.Append("        Public Function F" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "() As " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + Environment.NewLine);
                            fkMethods.Append("                If _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " Is Nothing Then" + Environment.NewLine);
                            fkMethods.Append("                    _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " = New " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + "()" + Environment.NewLine);
                            fkMethods.Append("                    _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + ".LoadRow(" + Common.GetVbFieldName(key.ForeignColumnName) + ")" + Environment.NewLine);
                            fkMethods.Append("                End If" + Environment.NewLine);
                            fkMethods.Append("                Return _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + Environment.NewLine);
                            fkMethods.Append("        End Function" + Environment.NewLine + Environment.NewLine);

                            if (CreateAsyncMethods)
                            {
                                fkMethods.AppendLine();
                                fkMethods.Append("        Public Async Function F" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async() As System.Threading.Tasks.Task(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + ")" + Environment.NewLine);
                                fkMethods.Append("                If _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " Is Nothing Then" + Environment.NewLine);
                                fkMethods.Append("                    _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " = New " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + "()" + Environment.NewLine);
                                fkMethods.Append("                    Await _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + ".LoadRowAsync(" + Common.GetVbFieldName(key.ForeignColumnName) + ")" + Environment.NewLine);
                                fkMethods.Append("                End If" + Environment.NewLine);
                                fkMethods.Append("                Return _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + Environment.NewLine);
                                fkMethods.Append("        End Function" + Environment.NewLine + Environment.NewLine);

                                fkMethods.Append("        Public Async Function F" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async(cancellationToken As System.Threading.CancellationToken) As System.Threading.Tasks.Task(Of " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + ")" + Environment.NewLine);
                                fkMethods.Append("                If _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " Is Nothing Then" + Environment.NewLine);
                                fkMethods.Append("                    _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + " = New " + BusinessNamespaceName + "." + Common.GetSafeVbName(Common.GetVbPropertyName(key.PrimaryTableName)) + "()" + Environment.NewLine);
                                fkMethods.Append("                    Await _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + ".LoadRowAsync(" + Common.GetVbFieldName(key.ForeignColumnName) + ", cancellationToken)" + Environment.NewLine);
                                fkMethods.Append("                End If" + Environment.NewLine);
                                fkMethods.Append("                Return _my" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + Environment.NewLine);
                                fkMethods.Append("        End Function" + Environment.NewLine + Environment.NewLine);
                            }

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
                            fkDataGetBy.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetBy.Append("                connection.ConnectionString = Universal.GetConnectionString()" + Environment.NewLine);
                            fkDataGetBy.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetBy.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetBy.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetBy.AppendLine("                    parameter.Value = fkId");
                            fkDataGetBy.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetBy.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetBy.Append("                        .Value = fkId _" + Environment.NewLine); 
                            //fkDataGetBy.Append("                    })" + Environment.NewLine);
                            fkDataGetBy.Append(Environment.NewLine);
                            fkDataGetBy.Append("                    connection.Open()" + Environment.NewLine);
                            fkDataGetBy.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
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





                            fkDataGetByAsync.Append("        Friend Shared Async Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async(fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByAsync.Append(f.VbDataType + ") As System.Threading.Tasks.Task(Of DataTable)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByAsync.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                connection.ConnectionString = Universal.GetConnectionString()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetByAsync.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetByAsync.AppendLine("                    parameter.Value = fkId");
                            fkDataGetByAsync.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetByAsync.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetByAsync.Append("                        .Value = fkId _" + Environment.NewLine); 
                            //fkDataGetByAsync.Append("                    })" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                    Await connection.OpenAsync()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            Await System.Threading.Tasks.Task.Run(Function() adapter.Fill(ds))" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetByAsync.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            End If" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("                End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("            End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetByAsync.Append("        End Function" + Environment.NewLine);







                            fkDataGetByAsync.Append("        Friend Shared Async Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async(fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByAsync.Append(f.VbDataType + ", cancellationToken As System.Threading.CancellationToken) As System.Threading.Tasks.Task(Of DataTable)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByAsync.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                connection.ConnectionString = Universal.GetConnectionString()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetByAsync.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetByAsync.AppendLine("                    parameter.Value = fkId");
                            fkDataGetByAsync.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetByAsync.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetByAsync.Append("                        .Value = fkId _" + Environment.NewLine); 
                            //fkDataGetByAsync.Append("                    })" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                    Await connection.OpenAsync(cancellationToken)" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            Await System.Threading.Tasks.Task.Run(Function() adapter.Fill(ds), cancellationToken)" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetByAsync.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetByAsync.Append("                            End If" + Environment.NewLine);
                            fkDataGetByAsync.Append("                        End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("                    End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("                End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append("            End Using" + Environment.NewLine);
                            fkDataGetByAsync.Append(Environment.NewLine);
                            fkDataGetByAsync.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetByAsync.Append("        End Function" + Environment.NewLine);









                            fkDataGetByConnString.Append("        Friend Shared Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "(connString As " + BusinessNamespaceName + ".LayerGenConnectionString, fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnString.Append(f.VbDataType + ") As DataTable" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByConnString.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetByConnString.Append("                connection.ConnectionString = connString.ConnectionString" + Environment.NewLine);
                            fkDataGetByConnString.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetByConnString.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetByConnString.AppendLine("                    parameter.Value = fkId");
                            fkDataGetByConnString.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetByConnString.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetByConnString.Append("                        .Value = fkId _" + Environment.NewLine);
                            //fkDataGetByConnString.Append("                    })" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("                    connection.Open()" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
                            fkDataGetByConnString.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            adapter.Fill(ds)" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetByConnString.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetByConnString.Append("                            End If" + Environment.NewLine);
                            fkDataGetByConnString.Append("                        End Using" + Environment.NewLine);
                            fkDataGetByConnString.Append("                    End Using" + Environment.NewLine);
                            fkDataGetByConnString.Append("                End Using" + Environment.NewLine);
                            fkDataGetByConnString.Append("            End Using" + Environment.NewLine);
                            fkDataGetByConnString.Append(Environment.NewLine);
                            fkDataGetByConnString.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetByConnString.Append("        End Function" + Environment.NewLine);





                            fkDataGetByConnStringAsync.Append("        Friend Shared Async Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async(connString As " + BusinessNamespaceName + ".LayerGenConnectionString, fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnStringAsync.Append(f.VbDataType + ") As System.Threading.Tasks.Task(Of DataTable)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByConnStringAsync.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                connection.ConnectionString = connString.ConnectionString" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetByConnStringAsync.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetByConnStringAsync.AppendLine("                    parameter.Value = fkId");
                            fkDataGetByConnStringAsync.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetByConnStringAsync.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetByConnStringAsync.Append("                        .Value = fkId _" + Environment.NewLine);
                            //fkDataGetByConnStringAsync.Append("                    })" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    Await connection.OpenAsync()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            Await System.Threading.Tasks.Task.Run(Function() adapter.Fill(ds))" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            End If" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("        End Function" + Environment.NewLine);




                            fkDataGetByConnStringAsync.AppendLine();
                            fkDataGetByConnStringAsync.Append("        Friend Shared Async Function GetBy" + Common.GetSafeVbName(Common.GetVbPropertyName(key.ForeignColumnName)) + "Async(connString As " + BusinessNamespaceName + ".LayerGenConnectionString, fkId As ");
                            foreach (Field f in fields)
                            {
                                if (String.Equals(f.FieldName, key.ForeignColumnName, StringComparison.CurrentCultureIgnoreCase))
                                {
                                    sqlDataType = f.SqlDbType;
                                    fkDataGetByConnStringAsync.Append(f.VbDataType + ", cancellationToken As System.Threading.CancellationToken) As System.Threading.Tasks.Task(Of DataTable)" + Environment.NewLine);
                                    break;
                                }
                            }
                            fkDataGetByConnStringAsync.Append("            Using connection As New SqlConnection()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                connection.ConnectionString = connString.ConnectionString" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                Using command As New SqlCommand()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.Connection = connection" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandType = CommandType.Text" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    command.CommandText = \"SELECT * FROM [" + DefaultSchema + "].[" + key.ForeignTableName + "] WHERE [" + key.ForeignColumnName + "]=@val1\"" + Environment.NewLine);
                            fkDataGetByConnStringAsync.AppendLine("                    Dim parameter As New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ")");
                            fkDataGetByConnStringAsync.AppendLine("                    parameter.Value = fkId");
                            fkDataGetByConnStringAsync.AppendLine("                    command.Parameters.Add(parameter)");
                            //fkDataGetByConnStringAsync.Append("                    command.Parameters.Add(New SqlParameter(\"@val1\", SqlDbType." + sqlDataType + ") With { _" + Environment.NewLine);
                            //fkDataGetByConnStringAsync.Append("                        .Value = fkId _" + Environment.NewLine);
                            //fkDataGetByConnStringAsync.Append("                    })" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    Await connection.OpenAsync(cancellationToken)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    Using adapter As New SqlDataAdapter()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        Using ds As New DataSet()" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            adapter.SelectCommand = command" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            Await System.Threading.Tasks.Task.Run(Function() adapter.Fill(ds), cancellationToken)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            If ds.Tables.Count > 0 Then" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                                Return ds.Tables(0)" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                            End If" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                        End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                    End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("                End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            End Using" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append(Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("            Return Nothing" + Environment.NewLine);
                            fkDataGetByConnStringAsync.Append("        End Function" + Environment.NewLine);
                        }

                        dataLayerTemplate = dataLayerTemplate.Replace("{22}", fkFields.ToString());
                        //dataLayerTemplate = dataLayerTemplate.Replace("{21}", fkProperties.ToString());
                        dataLayerTemplate = dataLayerTemplate.Replace("{21}", fkMethods.ToString());
                    }

                    if (CreateAsyncMethods)
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{23}", fkDataGetBy + Environment.NewLine + fkDataGetByAsync + Environment.NewLine + fkDataGetByConnString + Environment.NewLine + fkDataGetByConnStringAsync + Environment.NewLine);
                    }
                    else
                    {
                        dataLayerTemplate = dataLayerTemplate.Replace("{23}", fkDataGetBy + Environment.NewLine + fkDataGetByConnString + Environment.NewLine);
                    }


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
                    dataLayerTemplate.Replace("{30}", DefaultSchema);

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
            using (StreamWriter sw = File.CreateText(OutputDirectory + "\\StoredProcedureScripts.SQL"))
            {
                sw.Write(storedProcedures.ToString());
            }
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

        private void RemoveTemplateComments(ref StringBuilder templateString)
        {
            int ndx1 = templateString.IndexOf("{/*}");
            int ndx2 = templateString.IndexOf("{*/}");

            while (ndx1 >= 0 && ndx2 >= 0)
            {
                templateString.Remove(ndx1, ndx2 - ndx1 + 4);

                ndx1 = templateString.IndexOf("{/*}");
                ndx2 = templateString.IndexOf("{*/}");
            }
        }

        private bool IsPrimaryKey(string tableName, string fieldName)
        {
            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "sp_pkeys";
                    command.Parameters.AddWithValue("@table_name", tableName);
                    command.Parameters.AddWithValue("@table_owner", DefaultSchema);

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0)
                            {
                                if (ds.Tables[0].Rows.Cast<DataRow>().Any(row => ((string)row["COLUMN_NAME"]).ToLower() == fieldName.ToLower()))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }

        private List<Field> MapFields(string tableName)
        {
            var fields = new List<Field>();

            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "sp_columns";
                    command.Parameters.AddWithValue("@table_name", tableName);
                    command.Parameters.AddWithValue("@table_owner", DefaultSchema);

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0)
                            {
                                foreach (DataRow row in ds.Tables[0].Rows)
                                {
                                    var field = new Field();

                                    field.FieldName = (string)row["COLUMN_NAME"];
                                    field.IsPrimaryKey = IsPrimaryKey(tableName, field.FieldName);
                                    field.IsIdentity = ((string)row["TYPE_NAME"]).ToLower().Trim().EndsWith("identity");
                                    field.CsDataType = GetCsDataType(((string)row["TYPE_NAME"]).Trim());
                                    field.VbDataType = GetVbDataType(((string)row["TYPE_NAME"]).Trim());
                                    field.SafeCsFieldName = Common.GetSafeCsName(Common.GetCsFieldName(field.FieldName));
                                    field.SafeCsPropertyName = Common.GetSafeCsName(Common.GetCsPropertyName(field.FieldName, Common.GetSafeCsName(Common.GetCsPropertyName(tableName))));
                                    field.SafeVbFieldName = Common.GetSafeVbName(Common.GetVbFieldName(field.FieldName));
                                    field.SafeVbPropertyName = Common.GetSafeVbName(Common.GetVbPropertyName(field.FieldName, Common.GetSafeVbName(Common.GetVbPropertyName(tableName))));
                                    field.SqlDbType = GetSqlDbTypeFromSqlType(((string)row["TYPE_NAME"]).Trim());
                                    field.IntrinsicSqlDataType = ((string)row["TYPE_NAME"]).ToUpper().Trim();
                                    field.IntrinsicSqlDataType = field.IntrinsicSqlDataType.Replace(" IDENTITY", "").Trim();
                                    field.IsValueType = Common.IsValueType(field.CsDataType);
                                    field.DefaultValue = GetDefaultValue(((string)row["TYPE_NAME"]).Trim());

                                    try
                                    {
                                        field.SqlPrecision = (int)row["PRECISION"];
                                    }
                                    catch
                                    {
                                        field.SqlPrecision = 0;
                                    }
                                    try
                                    {
                                        field.SqlScale = (short)row["SCALE"];
                                    }
                                    catch
                                    {
                                        field.SqlScale = 0;
                                    }
                                    field.TextBased = IsTextBased(GetCsDataType(((string)row["TYPE_NAME"]).Trim()));
                                    field.Description = GetDescription(tableName, field.FieldName);
                                    field.IsComputedField = IsFieldComputed(tableName, field.FieldName);
                                    field.CanBeNull = ((short)row["NULLABLE"]) == 1;
                                    fields.Add(field);
                                }
                            }
                        }
                    }
                }
            }

            return fields;
        }

        private bool IsFieldComputed(string tableName, string fieldName)
        {
            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    string sql = "SELECT sysobjects.name AS TableName, syscolumns.name AS ColumnName FROM syscolumns INNER JOIN sysobjects ON syscolumns.id = sysobjects.id";
                    sql = sql + " AND sysobjects.xtype = 'U' WHERE syscolumns.iscomputed = 1 AND sysobjects.name = '" + tableName + "'";
                    sql = sql + " AND syscolumns.name = '" + fieldName + "'";

                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                            {
                                if (ds.Tables[0].Rows[0]["ColumnName"] == null || ds.Tables[0].Rows[0]["ColumnName"] == DBNull.Value)
                                    return false;

                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private bool IsView(string objectName)
        {
            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    string sql = "SELECT type_desc FROM sys.objects WHERE name= N'" + objectName + "'";

                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0)
                            {
                                if (ds.Tables[0].Rows[0]["type_desc"] == null || ds.Tables[0].Rows[0]["type_desc"] == DBNull.Value)
                                    return false;
                                var view = (string)ds.Tables[0].Rows[0]["type_desc"];

                                return !string.IsNullOrEmpty(view) && view.ToLower() == "view";
                            }
                        }
                    }
                }
            }

            return false;
        }

        private string GetDescription(string tableName, string fieldName)
        {
            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    string sql = "SELECT  st.name AS [Table] , sc.name AS [Column] ,sep.value AS [Description] FROM sys.tables st";
                    sql = sql + " INNER JOIN sys.columns sc ON st.object_id = sc.object_id LEFT JOIN sys.extended_properties sep ON st.object_id = sep.major_id AND sc.column_id = sep.minor_id AND sep.name = 'MS_Description'";
                    sql = sql + " WHERE st.name = '" + tableName + "' AND sc.name = '" + fieldName + "'";

                    command.Connection = connection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = sql;

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                            {
                                if (ds.Tables[0].Rows[0]["Description"] == null || ds.Tables[0].Rows[0]["Description"] == DBNull.Value)
                                    return "";
                                var description = (string)ds.Tables[0].Rows[0]["Description"];

                                return string.IsNullOrEmpty(description) ? "" : description;
                            }
                        }
                    }
                }
            }

            return "";
        }

        private List<ForeignKey> GetForeignKeys(string tableName)
        {
            var keys = new List<ForeignKey>();

            using (var connection = new SqlConnection())
            {
                connection.ConnectionString = ConnectionString;
                using (var command = new SqlCommand())
                {
                    command.Connection = connection;
                    command.CommandType = CommandType.StoredProcedure;
                    command.CommandText = "sp_fkeys";

                    command.Parameters.AddWithValue("@fktable_name", tableName);

                    using (var adapter = new SqlDataAdapter())
                    {
                        adapter.SelectCommand = command;
                        using (var ds = new DataSet())
                        {
                            adapter.Fill(ds);
                            if (ds.Tables.Count > 0 && ds.Tables[0].Rows.Count > 0)
                            {
                                keys.AddRange(from DataRow row in ds.Tables[0].Rows
                                              select new ForeignKey
                                              {
                                                  ForeignTableName = (string)row["FKTABLE_NAME"],
                                                  ForeignColumnName = (string)row["FKCOLUMN_NAME"],
                                                  PrimaryTableName = (string)row["PKTABLE_NAME"]
                                              });
                            }
                        }
                    }
                }
            }

            return keys;
        }

        private bool IsTextBased(string csType)
        {
            return csType.ToLower() == "string";
        }

        private string GetSqlDbTypeFromSqlType(string sqlType)
        {
            sqlType = sqlType.EndsWith("identity") ? sqlType.Substring(0, sqlType.Length - 8) : sqlType;
            sqlType = sqlType.Trim().ToLower();

            switch (sqlType)
            {
                case "bigint":
                    return "BigInt";
                case "binary":
                    return "Binary";
                case "bit":
                    return "Bit";
                case "char":
                    return "Char";
                case "date":
                    return "Date";
                case "datetime":
                    return "DateTime";
                case "datetime2":
                    return "DateTime2";
                case "datetimeoffset":
                    return "DateTimeOffset";
                case "decimal":
                    return "Decimal";
                case "float":
                    return "Float";
                case "image":
                    return "Image";
                case "int":
                    return "Int";
                case "money":
                    return "Money";
                case "nchar":
                    return "NChar";
                case "ntext":
                    return "NText";
                case "numeric":
                    return "Decimal";
                case "nvarchar":
                    return "NVarChar";
                case "real":
                    return "Real";
                case "smalldatetime":
                    return "SmallDateTime";
                case "smallint":
                    return "SmallInt";
                case "smallmoney":
                    return "SmallMoney";
                case "text":
                    return "Text";
                case "time":
                    return "Time";
                case "timestamp":
                    return "Timestamp";
                case "tinyint":
                    return "TinyInt";
                case "uniqueidentifier":
                    return "UniqueIdentifier";
                case "varbinary":
                    return "VarBinary";
                case "varchar":
                    return "VarChar";
                case "variant":
                    return "Variant";
                case "xml":
                    return "Xml";
            }

            return sqlType;
        }

        private string GetVbDataType(string sqlTypeName)
        {
            sqlTypeName = sqlTypeName.EndsWith("identity") ? sqlTypeName.Substring(0, sqlTypeName.Length - 8) : sqlTypeName;
            sqlTypeName = sqlTypeName.Trim().ToLower();

            switch (sqlTypeName)
            {
                case "bigint":
                    return "Long";
                case "binary":
                case "image":
                case "rowversion":
                case "timestamp":
                case "varbinary":
                    return "Byte()";
                case "bit":
                    return "Boolean";
                case "char":
                case "nchar":
                case "ntext":
                case "nvarchar":
                case "text":
                case "varchar":
                    return "String";
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                    return "DateTime";
                case "datetimeoffset":
                    return "DateTimeOffset";
                case "decimal":
                case "money":
                case "numeric":
                case "smallmoney":
                    return "Decimal";
                case "float":
                    return "Double";
                case "real":
                    return "Single";
                case "int":
                    return "Integer";
                case "smallint":
                    return "Short";
                case "sql_variant":
                    return "Object";
                case "time":
                    return "TimeSpan";
                case "tinyint":
                    return "Byte";
                case "uniqueidentifier":
                    return "Guid";
                case "xml":
                    return "string";
            }

            return "object";
        }

        private string GetDefaultValue(string sqlTypeName)
        {
            sqlTypeName = sqlTypeName.EndsWith("identity") ? sqlTypeName.Substring(0, sqlTypeName.Length - 8) : sqlTypeName;
            sqlTypeName = sqlTypeName.Trim().ToLower();

            switch (sqlTypeName)
            {
                case "bigint":
                    return "0";
                case "binary":
                case "image":
                case "rowversion":
                case "timestamp":
                case "varbinary":
                    return "0";
                case "bit":
                    return "false";
                case "char":
                case "nchar":
                case "ntext":
                case "nvarchar":
                case "text":
                case "varchar":
                    return "\"\"";
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                    return "DateTime.Now";
                case "datetimeoffset":
                    return "DateTimeOffset.Now";
                case "decimal":
                case "money":
                case "numeric":
                case "smallmoney":
                    return "0";
                case "float":
                    return "0";
                case "real":
                    return "0";
                case "int":
                    return "0";
                case "smallint":
                    return "0";
                case "sql_variant":
                    return "null";
                case "time":
                    return "TimeSpan.Now";
                case "tinyint":
                    return "0";
                case "uniqueidentifier":
                    return "new Guid()";
                case "xml":
                    return "null";
            }

            return "object";
        }

        private string GetCsDataType(string sqlTypeName)
        {
            sqlTypeName = sqlTypeName.EndsWith("identity") ? sqlTypeName.Substring(0, sqlTypeName.Length - 8) : sqlTypeName;
            sqlTypeName = sqlTypeName.Trim().ToLower();

            switch (sqlTypeName)
            {
                case "bigint":
                    return "long";
                case "binary":
                case "image":
                case "rowversion":
                case "timestamp":
                case "varbinary":
                    return "byte[]";
                case "bit":
                    return "bool";
                case "char":
                case "nchar":
                case "ntext":
                case "nvarchar":
                case "text":
                case "varchar":
                    return "string";
                case "date":
                case "datetime":
                case "datetime2":
                case "smalldatetime":
                    return "DateTime";
                case "datetimeoffset":
                    return "DateTimeOffset";
                case "decimal":
                case "money":
                case "numeric":
                case "smallmoney":
                    return "decimal";
                case "float":
                    return "double";
                case "real":
                    return "float";
                case "int":
                    return "int";
                case "smallint":
                    return "short";
                case "sql_variant":
                    return "object";
                case "time":
                    return "TimeSpan";
                case "tinyint":
                    return "byte";
                case "uniqueidentifier":
                    return "Guid";
                case "xml":
                    return "string";
            }

            return "object";
        }

        private bool HasPrimaryKey(List<Field> fields)
        {
            return fields.Any(field => field.IsPrimaryKey);
        }

        private string CreateSelectStoredProcedure(string tableName, List<Field> fields)
        {
            var selectProcedure = new StringBuilder();

            selectProcedure.AppendLine("IF EXISTS ( SELECT  *");
            selectProcedure.AppendLine("            FROM    dbo.sysobjects");
            selectProcedure.AppendLine("            WHERE   id = OBJECT_ID(N'[" + DefaultSchema + "].[sp" + tableName + "_Select]')");
            selectProcedure.AppendLine("                    AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )");
            selectProcedure.AppendLine("    DROP PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Select];");
            selectProcedure.AppendLine("GO");
            selectProcedure.AppendLine();
            selectProcedure.AppendLine("SET ANSI_NULLS OFF;");
            selectProcedure.AppendLine("GO");
            selectProcedure.AppendLine("SET QUOTED_IDENTIFIER OFF;");
            selectProcedure.AppendLine("GO");
            selectProcedure.AppendLine("CREATE    PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Select]");
            selectProcedure.AppendLine("    (");
            selectProcedure.Append("     @id ");
            selectProcedure.Append(fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper());
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("DECIMAL") || fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NUMERIC"))
            {
                selectProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ", " + fields.First(z => z.IsPrimaryKey).SqlScale + ")");
            }
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("VARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NVARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("CHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NCHAR"))
            {
                selectProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ")");
            }
            selectProcedure.AppendLine(" ,");
            selectProcedure.AppendLine("     @fields NVARCHAR(MAX)");
            selectProcedure.AppendLine("    )");
            selectProcedure.AppendLine("AS");
            selectProcedure.AppendLine("    DECLARE @sqlCommand NVARCHAR(MAX);");
            selectProcedure.AppendLine();
            selectProcedure.Append("    SET @sqlCommand = 'SELECT ' + @fields + ' FROM [" + DefaultSchema + "].[" + tableName + "] WHERE " + fields.First(z => z.IsPrimaryKey).FieldName + " = '");
            if (fields.First(z => z.IsPrimaryKey).TextBased)
            {
                selectProcedure.AppendLine(" + CHAR(39) + @id + CHAR(39);");
            }
            else
            {
                selectProcedure.AppendLine();
                selectProcedure.AppendLine("        + CAST(@id AS NVARCHAR(MAX));");
            }
            selectProcedure.AppendLine("    EXEC (@sqlCommand);");
            selectProcedure.AppendLine();
            selectProcedure.AppendLine("    SET QUOTED_IDENTIFIER ON;");
            selectProcedure.AppendLine("GO");
            selectProcedure.AppendLine();

            return selectProcedure.ToString();
        }

        private string CreateUpdateStoredProcedure(string tableName, List<Field> fields)
        {
            var updateProcedure = new StringBuilder();

            updateProcedure.AppendLine("IF EXISTS ( SELECT  *");
            updateProcedure.AppendLine("            FROM    dbo.sysobjects");
            updateProcedure.AppendLine("            WHERE   id = OBJECT_ID(N'[" + DefaultSchema + "].[sp" + tableName + "_Update]')");
            updateProcedure.AppendLine("                    AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )");
            updateProcedure.AppendLine("    DROP PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Update];");
            updateProcedure.AppendLine("GO");
            updateProcedure.AppendLine();
            updateProcedure.AppendLine("SET ANSI_NULLS OFF;");
            updateProcedure.AppendLine("GO");
            updateProcedure.AppendLine("SET QUOTED_IDENTIFIER OFF;");
            updateProcedure.AppendLine("GO");
            updateProcedure.AppendLine("CREATE    PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Update]");
            updateProcedure.AppendLine("    (");
            int count = 1;
            foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
            {
                updateProcedure.Append("     @val" + count + " " + field.IntrinsicSqlDataType.ToUpper());
                if (field.IntrinsicSqlDataType.ToUpper().Equals("DECIMAL") || fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NUMERIC"))
                {
                    updateProcedure.Append("(" + field.SqlPrecision + ", " + field.SqlScale + ")");
                }
                if (field.IntrinsicSqlDataType.ToUpper().Equals("VARCHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("NVARCHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("CHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("NCHAR"))
                {
                    updateProcedure.Append("(" + field.SqlPrecision + ")");
                }
                updateProcedure.AppendLine(" ,");
                count++;
            }
            updateProcedure.Append("     @val" + count + " " + fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper());
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("DECIMAL") || fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NUMERIC"))
            {
                updateProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ", " + fields.First(z => z.IsPrimaryKey).SqlScale + ")");
            }
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("VARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NVARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("CHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NCHAR"))
            {
                updateProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ")");
            }
            updateProcedure.AppendLine();
            updateProcedure.AppendLine("    )");
            updateProcedure.AppendLine("AS");
            updateProcedure.AppendLine("    UPDATE  [" + DefaultSchema + "].[" + tableName + "]");
            updateProcedure.Append("    SET");

            count = 1;

            Field first = fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName).First();
            Field last = fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName).Last();

            foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
            {
                if (first.FieldName == field.FieldName)
                    updateProcedure.AppendLine("     [" + field.FieldName + "]=@val" + count + (field.FieldName == last.FieldName ? "" : ","));
                else updateProcedure.AppendLine("            [" + field.FieldName + "]=@val" + count + (field.FieldName == last.FieldName ? "" : ","));
                count++;
            }
            updateProcedure.Remove(updateProcedure.Length - 1, 1);
            updateProcedure.AppendLine("    WHERE   [" + fields.First(z => z.IsPrimaryKey).FieldName + "]=@val" + count + ";");
            updateProcedure.AppendLine("    SET QUOTED_IDENTIFIER ON;");
            updateProcedure.AppendLine("GO");
            updateProcedure.AppendLine();

            return updateProcedure.ToString();
        }

        private string CreateInsertStoredProcedure(string tableName, List<Field> fields)
        {
            var insertProcedure = new StringBuilder();

            insertProcedure.AppendLine("IF EXISTS ( SELECT  *");
            insertProcedure.AppendLine("            FROM    dbo.sysobjects");
            insertProcedure.AppendLine("            WHERE   id = OBJECT_ID(N'[" + DefaultSchema + "].[sp" + tableName + "_Insert]')");
            insertProcedure.AppendLine("                    AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )");
            insertProcedure.AppendLine("    DROP PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Insert];");
            insertProcedure.AppendLine("GO");
            insertProcedure.AppendLine();
            insertProcedure.AppendLine("SET ANSI_NULLS OFF;");
            insertProcedure.AppendLine("GO");
            insertProcedure.AppendLine("SET QUOTED_IDENTIFIER OFF;");
            insertProcedure.AppendLine("GO");
            insertProcedure.AppendLine("CREATE    PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Insert]");
            insertProcedure.AppendLine("    (");
            int count = 1;
            Field first = fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName).First();
            Field last = fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName).Last();

            foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
            {
                insertProcedure.Append("     @val" + count + " " + field.IntrinsicSqlDataType.ToUpper());
                if (field.IntrinsicSqlDataType.ToUpper().Equals("DECIMAL") || fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NUMERIC"))
                {
                    insertProcedure.Append("(" + field.SqlPrecision + ", " + field.SqlScale + ")");
                }
                if (field.IntrinsicSqlDataType.ToUpper().Equals("VARCHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("NVARCHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("CHAR") ||
                    field.IntrinsicSqlDataType.ToUpper().Equals("NCHAR"))
                {
                    insertProcedure.Append("(" + field.SqlPrecision + ")");
                }
                if (field.FieldName == last.FieldName)
                {
                    insertProcedure.AppendLine();
                }
                else
                {
                    insertProcedure.AppendLine(" ,");
                }

                count++;
            }

            insertProcedure.AppendLine("    )");
            insertProcedure.AppendLine("AS");
            insertProcedure.AppendLine("    INSERT  INTO  " + DefaultSchema + ".[" + tableName + "]");
            insertProcedure.Append("            (");

            foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
            {
                if (field.FieldName == first.FieldName)
                {
                    insertProcedure.Append(" [" + field.FieldName + "]");
                }
                else
                {
                    insertProcedure.Append("              [" + field.FieldName + "]");
                }
                if (field.FieldName == last.FieldName)
                {
                    insertProcedure.AppendLine();
                }
                else
                {
                    insertProcedure.AppendLine(" ,");
                }
            }
            insertProcedure.AppendLine("            )");
            insertProcedure.Append("    VALUES  (");
            count = 1;
            foreach (Field field in fields.Where(z => (!z.IsIdentity) && (!z.IsComputedField)).OrderBy(z => z.FieldName))
            {
                if (field.FieldName == first.FieldName)
                {
                    insertProcedure.Append(" @val" + count);
                }
                else
                {
                    insertProcedure.Append("               @val" + count);
                }
                if (field.FieldName == last.FieldName)
                {
                    insertProcedure.AppendLine();
                }
                else
                {
                    insertProcedure.AppendLine(" ,");
                }
                count++;
            }
            insertProcedure.AppendLine("            );");
            insertProcedure.AppendLine("    SELECT  SCOPE_IDENTITY();");
            insertProcedure.AppendLine();

            insertProcedure.AppendLine("    SET QUOTED_IDENTIFIER ON;");
            insertProcedure.AppendLine("GO");
            insertProcedure.AppendLine();

            return insertProcedure.ToString();
        }

        private string CreateGetAllStoredProcedure(string tableName)
        {
            var getAllProcedure = new StringBuilder();

            getAllProcedure.AppendLine("IF EXISTS ( SELECT  *");
            getAllProcedure.AppendLine("            FROM    dbo.sysobjects");
            getAllProcedure.AppendLine("            WHERE   id = OBJECT_ID(N'[" + DefaultSchema + "].[sp" + tableName + "_GetAll]')");
            getAllProcedure.AppendLine("                    AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )");
            getAllProcedure.AppendLine("    DROP PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_GetAll];");
            getAllProcedure.AppendLine("GO");
            getAllProcedure.AppendLine();
            getAllProcedure.AppendLine("SET ANSI_NULLS OFF;");
            getAllProcedure.AppendLine("GO");
            getAllProcedure.AppendLine("SET QUOTED_IDENTIFIER OFF;");
            getAllProcedure.AppendLine("GO");
            getAllProcedure.AppendLine("CREATE    PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_GetAll]");
            getAllProcedure.AppendLine("AS");
            getAllProcedure.AppendLine("    SELECT *");
            getAllProcedure.AppendLine("    FROM [" + DefaultSchema + "].[" + tableName + "];");
            getAllProcedure.AppendLine();
            getAllProcedure.AppendLine("    SET QUOTED_IDENTIFIER ON;");
            getAllProcedure.AppendLine("GO");
            getAllProcedure.AppendLine();

            return getAllProcedure.ToString();
        }

        private string CreateDeleteStoredProcedure(string tableName, List<Field> fields)
        {
            var deleteProcedure = new StringBuilder();

            deleteProcedure.AppendLine("IF EXISTS ( SELECT  *");
            deleteProcedure.AppendLine("            FROM    dbo.sysobjects");
            deleteProcedure.AppendLine("            WHERE   id = OBJECT_ID(N'[" + DefaultSchema + "].[sp" + tableName + "_Delete]')");
            deleteProcedure.AppendLine("                    AND OBJECTPROPERTY(id, N'IsProcedure') = 1 )");
            deleteProcedure.AppendLine("    DROP PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Delete];");
            deleteProcedure.AppendLine("GO");
            deleteProcedure.AppendLine();
            deleteProcedure.AppendLine("SET ANSI_NULLS OFF;");
            deleteProcedure.AppendLine("GO");
            deleteProcedure.AppendLine("SET QUOTED_IDENTIFIER OFF;");
            deleteProcedure.AppendLine("GO");
            deleteProcedure.Append("CREATE    PROCEDURE [" + DefaultSchema + "].[sp" + tableName + "_Delete] ( @val1 ");
            deleteProcedure.Append(fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper());
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("DECIMAL") || fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NUMERIC"))
            {
                deleteProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ", " + fields.First(z => z.IsPrimaryKey).SqlScale + ")");
            }
            if (fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("VARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NVARCHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("CHAR") ||
                fields.First(z => z.IsPrimaryKey).IntrinsicSqlDataType.ToUpper().Equals("NCHAR"))
            {
                deleteProcedure.Append("(" + fields.First(z => z.IsPrimaryKey).SqlPrecision + ")");
            }

            deleteProcedure.AppendLine(" )");
            deleteProcedure.AppendLine("AS");
            deleteProcedure.AppendLine("    DELETE FROM [" + DefaultSchema + "].[" + tableName + "] WHERE [" + fields.First(z => z.IsPrimaryKey).FieldName + "]=@val1;");
            deleteProcedure.AppendLine();
            deleteProcedure.AppendLine("    SET QUOTED_IDENTIFIER ON;");
            deleteProcedure.AppendLine("GO");
            deleteProcedure.AppendLine();

            return deleteProcedure.ToString();
        }

        private string CreateStoredProcedures(string tableName, List<Field> fields, bool isView)
        {
            var procedures = new StringBuilder();

            if (!isView)
            {
                procedures.AppendLine(CreateSelectStoredProcedure(tableName, fields));
                procedures.AppendLine(CreateInsertStoredProcedure(tableName, fields));
                procedures.AppendLine(CreateUpdateStoredProcedure(tableName, fields));
                procedures.AppendLine(CreateDeleteStoredProcedure(tableName, fields));
            }
            procedures.AppendLine(CreateGetAllStoredProcedure(tableName));


            return procedures.ToString();
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
            public short SqlScale { get; set; }
            public int SqlPrecision { get; set; }
            public bool TextBased { get; set; }
            public string Description { get; set; }
            public bool IsComputedField { get; set; }
            public bool CanBeNull { get; set; }
            public bool IsValueType { get; set; }
            public string DefaultValue { get; set; }
        }

        private class ForeignKey
        {
            public string ForeignTableName { get; set; }
            public string PrimaryTableName { get; set; }
            public string ForeignColumnName { get; set; }
        }
    }
}
