using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoPushAttendanceService
{
    public static class Helper
    {
        private static string ConfigFileName = "\\ConfigFile.txt";

        public static void WriteLog(string Message)
        {
            string path = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            string filepath = AppDomain.CurrentDomain.BaseDirectory + "\\Logs\\ServiceLog_" + DateTime.Now.Date.ToShortDateString().Replace('/', '_') + ".txt";
            if (!File.Exists(filepath))
            {
                // Create a file to write to.   
                using (StreamWriter sw = File.CreateText(filepath))
                {
                    sw.WriteLine(DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt") + ": " + Message);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(filepath))
                {
                    sw.WriteLine(DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt") + ": " + Message);
                }
            }
        }

        public static string[] GetConfigData()
        {
            try
            {
                string filePath = AppDomain.CurrentDomain.BaseDirectory + ConfigFileName;
                if (File.Exists(filePath))
                {
                    string[] lines = System.IO.File.ReadAllLines(filePath);
                    return lines;
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex.Message);
            }
            return null;
        }

        public static void ExecuteQuery(DataTable dt,string con)
        {
            try
            {
                string sql = @"sp_Upsert_DeviceLog";
                Dictionary<string, object> parameters =  new Dictionary<string, object>{{"@tblLogs",dt}};

                using (SqlConnection connection = new SqlConnection(con))
                {
                    connection.Open();
                    SqlCommand cmd = new SqlCommand(sql, connection);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = sql;
                    if (cmd.Connection.State != ConnectionState.Open)
                        cmd.Connection.Open();
                    if (parameters != null)
                    {
                        foreach (KeyValuePair<string, object> param in parameters)
                        {
                            DbParameter dbParameter = cmd.CreateParameter();
                            dbParameter.ParameterName = param.Key;
                            dbParameter.Value = param.Value;
                            cmd.Parameters.Add(dbParameter);
                        }
                    }
                    cmd.CommandTimeout = 200000;
                    var reader = cmd.ExecuteNonQuery();

                    //DataTable dtResponse = new DataTable();
                    //dtResponse.Load(reader);

                    if (cmd.Connection.State != ConnectionState.Closed)
                        cmd.Connection.Close();

                    //return dtResponse;
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex);
            }
        }

    }
}
