using Google.Protobuf.WellKnownTypes;
using iTextSharp.text.pdf.parser;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ScriptsApplication.Models;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;

namespace ScriptsApplication.Controllers
{
    public class HomeController : Controller
    {
        
        StringBuilder ForeignkeyForChild = new StringBuilder();
        List<string> checkingValues = new List<string>();
        private readonly ILogger<HomeController> _logger;
        StringBuilder columnsForScrript = new StringBuilder();
        string connectionString = "data source=SDEMUCA07054.de001.itgr.net;initial catalog=MediciUAT;user id=sqlMedici;password=E8416ABF830C0B38670F8A84AEECA093;multipleactiveresultsets=True;";
        //string connectionString = "data source=182.72.175.14;initial catalog=Medici;user id=Medici;password=medici;multipleactiveresultsets=True;";

        List<string> childColumns = new List<string>();
        Dictionary<string, string> myIdDescription = new Dictionary<string,string>();

        StringBuilder sqlScript = new StringBuilder();

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult GeneratePdf() 
        {
            return View();
        }

        private bool CheckTableExists(string tableName)
        {
            //string connectionString = "Data Source=COGNINE-L156\\SQLEXPRESS;Integrated Security=True;Database=StudentInternal";
            //string connectionString = "Data Source=10.103.201.36\\SQLExpress2016;User ID=sa;Password=SQL*2016;Encrypt=False;Database=Medici";

            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                try
                {
                    connection.Open();
                    // Check if the table exists in the database
                    using (SqlCommand command = new SqlCommand($"IF OBJECT_ID('{tableName}', 'U') IS NOT NULL SELECT 1 ELSE SELECT 0", connection))
                    {
                        int result = Convert.ToInt32(command.ExecuteScalar());
                        return result == 1;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return false;
                }
            }
        }

        private IEnumerable<string> GetTableData(string tableName,string ProjecttypeIDList)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                var Values = new List<string>();
                try
                {
                    List<int> keys= ProjecttypeIDList.Split(',').Select(int.Parse).ToList();
                    ///List<int> keys = new List<int> { 155, 157, 158, 159, 160, 161, 162, 164, 165, 166, 167, 168, 169, 170, 171, 172, 173, 174, 175, 177 };

                    StringBuilder columnName = new StringBuilder();
                    StringBuilder columnNames= new StringBuilder();
                    connection.Open();

                    string columnNamesQuery = $@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' and ORDINAL_POSITION>1";
                    using (SqlCommand command = new SqlCommand(columnNamesQuery, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                columnNames.Append(reader.GetString(0));
                            }
                        }
                    }

                    string columnQuery = $@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' and ORDINAL_POSITION=1";
                    using (SqlCommand command = new SqlCommand(columnQuery, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                columnName.Append(reader.GetString(0));
                                ForeignkeyForChild.Append(reader.GetString(0));
                            }
                        }
                    }
                    foreach(int k in keys)
                    {
                        string query = $@"SELECT * FROM {tableName} where {columnName}={k}";
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                var rowData = new List<string>();
                                while (reader.Read())
                                {
                                    int numberOfColumns = reader.FieldCount;

                                    // Constructing the string dynamically based on the number of columns
                                    StringBuilder concatenatedString = new StringBuilder();
                                    for (int i = 1; i < numberOfColumns; i++)
                                    {
                                        object value = reader.GetValue(i);
                                        object type = value.GetType().Name;
                                        if (type == typeof(int).Name || type == typeof(uint).Name || type == typeof(short).Name || type == typeof(ushort).Name || type == typeof(long).Name || type == typeof(ulong).Name)
                                        {
                                            concatenatedString.Append(value);
                                        }
                                        else if(value is Boolean)
                                        {
                                            if (value is true)
                                            {
                                                concatenatedString.Append(1);
                                            }
                                            else
                                            {
                                                concatenatedString.Append(0);
                                            }
                                        }
                                        else if (value is DateTime)
                                        {
                                            //DateTime dateValue = (DateTime)value;
                                            //string valued = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                            concatenatedString.Append($@"Getdate()");
                                        }
                                        else if (type == typeof(DBNull).Name)
                                        {
                                            concatenatedString.Append($@"null");
                                        }
                                        else
                                        {
                                            concatenatedString.Append($@"'{value}'");
                                        }
                                        if (i < numberOfColumns - 1)
                                        {
                                            concatenatedString.Append(", ");
                                        }
                                    }

                                    rowData.Add($"{concatenatedString}");
                                }

                                Values.Add(rowData[0]);
                            }
                        }
                        //string query1 = $@"SELECT code FROM {tableName} WHERE {columnName}= {k}";
                        //using (SqlCommand command = new SqlCommand(query1, connection))
                        //{
                        //    using (SqlDataReader reader = command.ExecuteReader())
                        //    {
                        //        while (reader.Read())
                        //        {
                        //            myIdDescription.Add(k.ToString(), reader.GetString(0));
                        //        }
                        //    }
                        //}

                    }
                    return Values;
                }
                catch (Exception ex)
                {
                    // Handle exceptions according to your application's needs
                    // Log the exception, display an error message, etc.
                    Console.WriteLine(ex.Message);
                    return Values;
                }
            }
        }

        private IEnumerable<string> GetChildTableData(string childTableName,string ChildScript)
       {
            var ChildValues = new List<string>();

            try
            {
                var rowData = new List<string>();
                StringBuilder childForeignKeyColumn = new StringBuilder();
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    connection.Open();
                    //foreach(int k in keys)
                    //{
                        string query = $@"{ChildScript}";
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                
                                while (reader.Read())
                                {
                                    int numberOfColumns = reader.FieldCount;

                                    // Constructing the string dynamically based on the number of columns
                                    StringBuilder concatenatedString = new StringBuilder();
                                    for (int i = 1; i < numberOfColumns; i++)
                                    {
                                        object value = reader.GetValue(i);
                                        object type = value.GetType().Name;
                                        if (type == typeof(int).Name || type == typeof(uint).Name ||type == typeof(short).Name || type == typeof(ushort).Name ||type == typeof(long).Name || type == typeof(ulong).Name || type==typeof(Byte).Name)
                                        {
                                            concatenatedString.Append(value);
                                        }
                                        else if (value is Boolean)
                                        {
                                            if (value is true)
                                            {
                                                concatenatedString.Append(1);
                                            }
                                            else
                                            {
                                                concatenatedString.Append(0);
                                            }
                                        }
                                        else if (type == typeof(DBNull).Name)
                                        {
                                            concatenatedString.Append($@"null");
                                        }
                                        else if(value is DateTime)
                                        {
                                           //DateTime dateValue=(DateTime)value;
                                           //string valued= dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                           concatenatedString.Append($@"Getdate()");
                                        }
                                        else
                                        {
                                            concatenatedString.Append($@"'{value}'");
                                        }
                                        if (i < numberOfColumns - 1)
                                        {
                                            concatenatedString.Append(",");
                                        }
                                    }

                                    rowData.Add($"{concatenatedString}");
                                }
                            }
                        }
                    //}
                    string columnQuery = $@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{childTableName}' and ORDINAL_POSITION>1";
                    using (SqlCommand command = new SqlCommand(columnQuery, connection))
                    {
                        using (SqlDataReader reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                childColumns.Add(reader.GetString(0));
                            }
                        }
                    }
                }
                ChildValues = rowData;
                return ChildValues;

            }
            catch (Exception ex)
            {
                // Handle exceptions according to your application's needs
                // Log the exception, display an error message, etc.
                Console.WriteLine(ex.Message);
                return ChildValues=new List<string>();
            }

        }


        [HttpPost]
        public ActionResult GeneratePdf(string tableName, string ProjecttypeIDList,string ColumnName, string childTableName, string ChildScript)
        {
            bool isTableExists = CheckTableExists(tableName);
            bool isChildTableExists = CheckTableExists(childTableName);
            if (isTableExists || isChildTableExists)
            {
                var Values = new List<string>();
                var ChildValues = new List<string>();
                StringBuilder columnNames = new StringBuilder();
                //Values = (List<string>)GetTableData(tableName, ProjecttypeIDList);
                //ChildValues = (List<string>)GetChildTableData(childTableName, ChildScript);
                if (isTableExists && isChildTableExists is false)
                {
                    Values = (List<string>)GetTableData(tableName, ProjecttypeIDList);

                    using (SqlConnection connection = new SqlConnection(connectionString))
                    {
                        connection.Open();
                        string columnNamesQuery = $@"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{tableName}' and ORDINAL_POSITION>1";
                        using (SqlCommand command = new SqlCommand(columnNamesQuery, connection))
                        {
                            using (SqlDataReader reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    int numberOfColumns = reader.FieldCount;
                                    for (int i = 0; i < numberOfColumns; i++)
                                    {
                                        object value = reader.GetValue(i);

                                        columnNames.Append(value);
                                        if (i < numberOfColumns)
                                        {
                                            columnNames.Append(",");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    columnNames.Length -= 1;
                    List<string> stringList = new List<string>(columnNames.ToString().Split(','));
                    int indexvalue = stringList.FindIndex(a => a.Contains(ColumnName));
                    //int checkingValue = stringList.FindIndex(a => a.Contains(CheckingColumn));
                    foreach (string column in stringList)
                    {
                        columnsForScrript.Append($@"[{column}]");
                        columnsForScrript.Append(", ");
                    }
                    columnsForScrript.Length -= 2;
                    int masterValue = 0;
                    foreach (string line in Values)
                    {
                        masterValue++;
                        List<string> valueList = new List<string>(Regex.Matches(line, @"('[^']*'|[^,]+)")
                             .Cast<Match>()
                             .Select(m => m.Value)
                             .ToArray());
                        string ColumnValue = valueList[indexvalue];
                        //checkingValues.Add(valueList[checkingValue]);
                        sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {tableName} WHERE {ColumnName}={ColumnValue})\r\n BEGIN \r\n");
                        sqlScript.AppendLine($"INSERT INTO {tableName}({columnsForScrript}) VALUES ({line});\r\nPrint {masterValue}\r\n END\r\n");
                    }
                }
                if (isTableExists is false && isChildTableExists)
                {
                    ChildValues = (List<string>)GetChildTableData(childTableName, ChildScript);
                    if (childTableName.ToLower() == "ptstandardmanagement")
                    {
                        sqlScript.AppendLine("--------Data of PTStandardManagement \r\n");
                        sqlScript.AppendLine("Declare @ID INT;\r\nDeclare @SID INT;\r\n");

                        int Printvalue = 0;
                        foreach (string line in ChildValues)
                        {
                            Printvalue++;
                            int childProjectTypevalue = childColumns.FindIndex(a => a.Contains("ProjectTypeID"));
                            int childstandardindexvalue = childColumns.FindIndex(a => a.Contains("StandardID"));
                            List<string> valueList = new List<string>(line.ToString().Split(','));
                            string ColumnValue = valueList[childProjectTypevalue];
                            string standardID = valueList[childstandardindexvalue];
                            valueList[childProjectTypevalue] = "@ID";
                            valueList[childstandardindexvalue] = "@SID";
                            string resultString = string.Join(",", valueList);
                            //string Description = myIdDescription[ColumnValue];
                            StringBuilder CodeValue = new StringBuilder();
                            StringBuilder standardDescription = new StringBuilder();
                            string childColumn = string.Join(",", childColumns);
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Code from mstprojecttypes where projecttypeid= {ColumnValue} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            CodeValue.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select description from mststandards where standardid= {standardID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            standardDescription.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            sqlScript.AppendLine($"select  @Id=ProjectTypeID from mstprojecttypes where [code]='{CodeValue}' ;");
                            sqlScript.AppendLine($"select  @SID=StandardID from mststandards where [Description]='{standardDescription}' ;\r\n");
                            sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {childTableName} WHERE ProjectTypeid=@ID and StandardID=@SID )\r\n BEGIN \r\n");
                            sqlScript.AppendLine($"INSERT INTO {childTableName}({childColumn}) VALUES ({resultString});\r\nprint {Printvalue}\r\nEND\r\n");
                        }
                    }
                    if (childTableName.ToLower() == "ptdocumentmanagement")
                    {
                        sqlScript.AppendLine("--------Data of ptdocumentmanagement \r\n");
                        sqlScript.AppendLine("Declare @ID INT;\r\nDeclare @SID INT;\r\nDeclare @DID INT\r\n");
                        int printValue = 0;
                        foreach (string line in ChildValues)
                        {
                            printValue++;
                            int childProjectTypeIndex = childColumns.FindIndex(a => a.Contains("ProjectTypeID"));
                            int childstandardindex = childColumns.FindIndex(a => a.Contains("StandardID"));
                            int childDocumentIDIndex = childColumns.FindIndex(a => a.Contains("DocumentID"));
                            List<string> valueList = new List<string>(line.ToString().Split(','));
                            string ColumnValue = valueList[childProjectTypeIndex];
                            string standardID = valueList[childstandardindex];
                            string DocumentID = valueList[childDocumentIDIndex];
                            valueList[childProjectTypeIndex] = "@ID";
                            valueList[childstandardindex] = "@SID";
                            valueList[childDocumentIDIndex] = "@DID";
                            string resultString = string.Join(",", valueList);
                            //string Description = myIdDescription[ColumnValue];
                            StringBuilder CodeValue = new StringBuilder();
                            StringBuilder standardDescription = new StringBuilder();
                            StringBuilder DocumentValues = new StringBuilder();
                            string childColumn = string.Join(",", childColumns);
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Code from mstprojecttypes where projecttypeid= {ColumnValue} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            CodeValue.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select description from mststandards where standardid= {standardID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            standardDescription.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select DocumentType,[UniqueID],[Applicablefor] from mstdocuments where Documentid= {DocumentID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            int numberOfColumns = reader.FieldCount;
                                            for (int i = 0; i < numberOfColumns; i++)
                                            {
                                                object value = reader.GetValue(i);
                                                object type = value.GetType().Name;
                                                if (type == typeof(int).Name || type == typeof(uint).Name || type == typeof(short).Name || type == typeof(ushort).Name || type == typeof(long).Name || type == typeof(ulong).Name || type == typeof(Byte).Name)
                                                {
                                                    DocumentValues.Append(value);
                                                }
                                                else if (value is Boolean)
                                                {
                                                    if (value is true)
                                                    {
                                                        DocumentValues.Append(1);
                                                    }
                                                    else
                                                    {
                                                        DocumentValues.Append(0);
                                                    }
                                                }
                                                else if (type == typeof(DBNull).Name)
                                                {
                                                    DocumentValues.Append($@"null");
                                                }
                                                else if (value is DateTime)
                                                {
                                                    DateTime dateValue = (DateTime)value;
                                                    string valued = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                                    DocumentValues.Append($@"Getdate()");
                                                }
                                                else
                                                {
                                                    DocumentValues.Append($@"'{value}'");
                                                }
                                                if (i < numberOfColumns - 1)
                                                {
                                                    DocumentValues.Append(",");
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                            List<string> DocumentvalueList = new List<string>(DocumentValues.ToString().Split(','));
                            sqlScript.AppendLine($"select  @Id=ProjectTypeID from mstprojecttypes where [code]='{CodeValue}' ;");
                            sqlScript.AppendLine($"select  @SID=StandardID from mststandardS where [Description]='{standardDescription}' ;");
                            sqlScript.AppendLine($"select  @DID=DocumentID from mstdocuments where DocumentType={DocumentvalueList[0]} and [UniqueID]={DocumentvalueList[1]} and [Applicablefor]={DocumentvalueList[2]} ;\r\n");
                            sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {childTableName} WHERE ProjectTypeid=@ID and StandardID=@SID and DocumentID=@DID)\r\n BEGIN \r\n");
                            sqlScript.AppendLine($"INSERT INTO {childTableName}({childColumn}) VALUES ({resultString});\r\nPrint {printValue}\r\nEND\r\n");
                        }
                    }
                    if (childTableName.ToLower() == "pttrcriteriamanagement")
                    {
                        sqlScript.AppendLine("--------Data of PTTRCriteriaManagement \r\n");
                        sqlScript.AppendLine("Declare @ID INT;\r\nDeclare @SID INT;\r\nDeclare @TRID INT\r\n");


                        foreach (string line in ChildValues)
                        {

                            int childProjectTypeIndex = childColumns.FindIndex(a => a.Contains("ProjectTypeID"));
                            int childstandardindex = childColumns.FindIndex(a => a.Contains("StandardID"));
                            int childTRIDIndex = childColumns.FindIndex(a => a.Contains("TRCriteriaID"));
                            List<string> valueList = new List<string>(line.ToString().Split(','));
                            string ColumnValue = valueList[childProjectTypeIndex];
                            string standardID = valueList[childstandardindex];
                            string DocumentID = valueList[childTRIDIndex];
                            valueList[childProjectTypeIndex] = "@ID";
                            valueList[childstandardindex] = "@SID";
                            valueList[childTRIDIndex] = "@TRID";
                            string resultString = string.Join(",", valueList);
                            //string Description = myIdDescription[ColumnValue];
                            StringBuilder CodeValue = new StringBuilder();
                            StringBuilder standardDescription = new StringBuilder();
                            StringBuilder TRValues = new StringBuilder();
                            string childColumn = string.Join(",", childColumns);

                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Code from mstprojecttypes where projecttypeid= {ColumnValue} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            CodeValue.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select description from mststandards where standardid= {standardID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            standardDescription.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Criteria,[UniqueID] from mstTRCriteria where TRcriteriaID= {DocumentID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            int numberOfColumns = reader.FieldCount;
                                            for (int i = 0; i < numberOfColumns; i++)
                                            {
                                                object value = reader.GetValue(i);
                                                object type = value.GetType().Name;
                                                if (type == typeof(int).Name || type == typeof(uint).Name || type == typeof(short).Name || type == typeof(ushort).Name || type == typeof(long).Name || type == typeof(ulong).Name || type == typeof(Byte).Name)
                                                {
                                                    TRValues.Append(value);
                                                }
                                                else if (value is DateTime)
                                                {
                                                    DateTime dateValue = (DateTime)value;
                                                    string valued = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                                    TRValues.Append($@"'{valued}'");
                                                }
                                                else if (type == typeof(DBNull).Name)
                                                {
                                                    TRValues.Append($@"null");
                                                }
                                                else
                                                {
                                                    TRValues.Append($@"'{value}'");
                                                }
                                                if (i < numberOfColumns - 1)
                                                {
                                                    TRValues.Append(",");
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                            List<string> TRvalueList = new List<string>(TRValues.ToString().Split(','));
                            sqlScript.AppendLine($"select  @Id=ProjectTypeID from mstprojecttypes where [code]='{CodeValue}' ;");
                            sqlScript.AppendLine($"select  @SID=StandardID from mststandardS where [Description]='{standardDescription}' ;");
                            sqlScript.AppendLine($"select  @DID=DocumentID from mstTRCriteria where Criteria={TRvalueList[0]} and [UniqueID]={TRvalueList[1]} ;\r\n");
                            sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {childTableName} WHERE ProjectTypeid=@ID and StandardID=@SID and TRCriteriaID=@TRID)\r\n BEGIN \r\n");
                            sqlScript.AppendLine($"INSERT INTO {childTableName}({childColumn}) VALUES ({resultString});\r\nEND\r\n");

                        }
                    }
                    if (childTableName.ToLower() == "ptcrcriteriamanagement")
                    {
                        sqlScript.AppendLine("--------Data of PTCRCriteriaManagement \r\n");
                        sqlScript.AppendLine("Declare @ID INT;\r\nDeclare @SID INT;\r\nDeclare @CRID INT\r\n");
                        int incrementValue = 0;
                        foreach (string line in ChildValues)
                        {
                            incrementValue++;
                            int childProjectTypeIndex = childColumns.FindIndex(a => a.Contains("ProjectTypeID"));
                            int childstandardindex = childColumns.FindIndex(a => a.Contains("StandardID"));
                            int childTRIDIndex = childColumns.FindIndex(a => a.Contains("CRCriteriaID"));
                            List<string> valueList = new List<string>(line.ToString().Split(','));
                            string ColumnValue = valueList[childProjectTypeIndex];
                            string standardID = valueList[childstandardindex];
                            string DocumentID = valueList[childTRIDIndex];
                            valueList[childProjectTypeIndex] = "@ID";
                            valueList[childstandardindex] = "@SID";
                            valueList[childTRIDIndex] = "@CRID";
                            string resultString = string.Join(",", valueList);
                            //string Description = myIdDescription[ColumnValue];
                            StringBuilder CodeValue = new StringBuilder();
                            StringBuilder standardDescription = new StringBuilder();
                            StringBuilder TRValues = new StringBuilder();
                            string childColumn = string.Join(",", childColumns);
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Code from mstprojecttypes where projecttypeid= {ColumnValue} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            CodeValue.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select description from mststandards where standardid= {standardID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            standardDescription.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Criteria from mstCRCriteria where CRcriteriaID= {DocumentID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            int numberOfColumns = reader.FieldCount;
                                            for (int i = 0; i < numberOfColumns; i++)
                                            {
                                                object value = reader.GetValue(i);
                                                object type = value.GetType().Name;
                                                if (type == typeof(int).Name || type == typeof(uint).Name || type == typeof(short).Name || type == typeof(ushort).Name || type == typeof(long).Name || type == typeof(ulong).Name)
                                                {
                                                    TRValues.Append(value);
                                                }
                                                else if (value is DateTime)
                                                {
                                                    DateTime dateValue = (DateTime)value;
                                                    string valued = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                                    TRValues.Append($@"'{valued}'");
                                                }
                                                else if (type == typeof(DBNull).Name)
                                                {
                                                    TRValues.Append($@"null");
                                                }
                                                else
                                                {
                                                    TRValues.Append($@"'{value}'");
                                                }
                                                if (i < numberOfColumns - 1)
                                                {
                                                    TRValues.Append(",");
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                            sqlScript.AppendLine($"select  @Id=ProjectTypeID from mstprojecttypes where [code]='{CodeValue}' ;");
                            sqlScript.AppendLine($"select  @SID=StandardID from mststandardS where [Description]='{standardDescription}' ;");
                            sqlScript.AppendLine($"select  @CRID=TRcriteriaID from mstTRCriteria where Criteria={TRValues} and ApplicableCB='UKMDR' ;\r\n");
                            sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {childTableName} WHERE ProjectTypeid=@ID and StandardID=@SID and TRCriteriaID=@CRID)\r\n BEGIN \r\n ");
                            sqlScript.AppendLine($"INSERT INTO {childTableName}({childColumn}) VALUES ({resultString});\r\nprint {incrementValue} \r\nEND\r\n");
                        }
                    }
                    if (childTableName.ToLower() == "ptcdcriteriamanagement")
                    {
                        sqlScript.AppendLine("--------Data of PTCDCriteriaManagement \r\n");
                        sqlScript.AppendLine("Declare @ID INT;\r\nDeclare @SID INT;\r\nDeclare @CDID INT\r\n");
                        int incrementValue = 0;
                        foreach (string line in ChildValues)
                        {
                            incrementValue++;
                            int childProjectTypeIndex = childColumns.FindIndex(a => a.Contains("ProjectTypeID"));
                            int childstandardindex = childColumns.FindIndex(a => a.Contains("StandardID"));
                            int childTRIDIndex = childColumns.FindIndex(a => a.Contains("CDCriteriaID"));
                            List<string> valueList = new List<string>(line.ToString().Split(','));
                            string ColumnValue = valueList[childProjectTypeIndex];
                            string standardID = valueList[childstandardindex];
                            string DocumentID = valueList[childTRIDIndex];
                            valueList[childProjectTypeIndex] = "@ID";
                            valueList[childstandardindex] = "@SID";
                            valueList[childTRIDIndex] = "@CDID";
                            string resultString = string.Join(",", valueList);
                            //string Description = myIdDescription[ColumnValue];
                            StringBuilder CodeValue = new StringBuilder();
                            StringBuilder standardDescription = new StringBuilder();
                            StringBuilder TRValues = new StringBuilder();
                            string childColumn = string.Join(",", childColumns);
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select Code from mstprojecttypes where projecttypeid= {ColumnValue} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            CodeValue.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }

                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select description from mststandards where standardid= {standardID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            standardDescription.Append(reader.GetString(0));
                                        }
                                    }
                                }
                            }
                            using (SqlConnection connection = new SqlConnection(connectionString))
                            {
                                connection.Open();
                                string Query = $"Select [UNIQUEID] from mstCDCriteria where CDcriteriaID= {DocumentID} ";
                                using (SqlCommand command = new SqlCommand(Query, connection))
                                {
                                    using (SqlDataReader reader = command.ExecuteReader())
                                    {
                                        while (reader.Read())
                                        {
                                            int numberOfColumns = reader.FieldCount;
                                            for (int i = 0; i < numberOfColumns; i++)
                                            {
                                                object value = reader.GetValue(i);
                                                object type = value.GetType().Name;
                                                if (type == typeof(int).Name || type == typeof(uint).Name || type == typeof(short).Name || type == typeof(ushort).Name || type == typeof(long).Name || type == typeof(ulong).Name)
                                                {
                                                    TRValues.Append(value);
                                                }
                                                else if (value is DateTime)
                                                {
                                                    DateTime dateValue = (DateTime)value;
                                                    string valued = dateValue.ToString("yyyy-MM-dd HH:mm:ss");
                                                    TRValues.Append($@"'{valued}'");
                                                }
                                                else if (type == typeof(DBNull).Name)
                                                {
                                                    TRValues.Append($@"null");
                                                }
                                                else
                                                {
                                                    TRValues.Append($@"'{value}'");
                                                }
                                                if (i < numberOfColumns - 1)
                                                {
                                                    TRValues.Append(",");
                                                }
                                            }

                                        }
                                    }
                                }
                            }
                            sqlScript.AppendLine($"select  @Id=ProjectTypeID from mstprojecttypes where [code]='{CodeValue}' ;");
                            sqlScript.AppendLine($"select  @SID=StandardID from mststandardS where [Description]='{standardDescription}' ;");
                            sqlScript.AppendLine($"select  @CDID=CDcriteriaID from mstCDCriteria where [UNIQUEID]={TRValues} and ApplicableCB='UKMDR' ;\r\n");
                            sqlScript.Append($"IF NOT EXISTS ( SELECT 1 FROM {childTableName} WHERE ProjectTypeid=@ID and StandardID=@SID and CDCriteriaID=@CDID)\r\n BEGIN \r\n");
                            sqlScript.AppendLine($"INSERT INTO {childTableName}({childColumn}) VALUES ({resultString});\r\nprint {incrementValue}\r\nEND\r\n");
                        }
                    }
                }

                byte[] byteArray = Encoding.UTF8.GetBytes(sqlScript.ToString());
                return File(byteArray, "application/sql", "script.sql");

            }
            
            else
            {
                ViewBag.ValidationMessage = "Table does not exist!";
            }
            
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}