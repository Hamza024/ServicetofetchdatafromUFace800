using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using zkemkeeper;

namespace AutoPushAttendanceService
{
    public partial class Scheduler : ServiceBase
    {
        private bool bIsConnected = false;
        private int iMachineNumber = 1;
        private int idwErrorCode = 0;

        private CZKEMClass axCZKEM1;

        private Timer timer = null;
        private string[] ConfigData = null;
        public Scheduler()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                timer = new Timer();
                ConfigData = Helper.GetConfigData();
                if (ConfigData != null && ConfigData.Length == 4)
                {
                    if (!string.IsNullOrEmpty(ConfigData[0]) && !string.IsNullOrEmpty(ConfigData[1]) && !string.IsNullOrEmpty(ConfigData[2]) && !string.IsNullOrEmpty(ConfigData[3]))
                    {
                        this.timer.Interval = 4 * 60 * 60 * 1000; // ervery 4 hours
                        this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Tick);
                        timer.Enabled = true;

                        Helper.WriteLog("Attendance window service started.");
                        FetchAttFromDevice();
                    }
                    else
                    {
                        Helper.WriteLog("[Error] Configuration data is not configure. (write connection string on first line, device IP on second, device port on third and device id on forth line.)");
                    }
                }
                else
                {
                    Helper.WriteLog("[Error] Configuration file not found or empty. (create a file with name \"ConfigFile.txt\" and write connection string on first line, device IP on second, device port on third and device id on forth line.)");
                }
            }
            catch (Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex);
            }
            
        }

        private void timer_Tick(object sender, ElapsedEventArgs e)
        {
            try
            {
                FetchAttFromDevice();
            }
            catch(Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex);
            }
        }

        public void FetchAttFromDevice()
        {
            try
            {
                axCZKEM1 = new CZKEMClass();
                DateTime startDate = DateTime.Now.AddDays(-2);
                DateTime endDate = DateTime.Now;

                string sdwEnrollNumber = "";
                int idwVerifyMode = 0;
                int idwInOutMode = 0;
                int idwYear = 0;
                int idwMonth = 0;
                int idwDay = 0;
                int idwHour = 0;
                int idwMinute = 0;
                int idwSecond = 0;
                int idwWorkcode = 0;

                int iGLCount = 0;
                int iIndex = 0;

                //Device user Info
                string sdwEmployeeName = "";
                string sdwPassword = "";
                int sdwPrivilege = 0;
                bool sdwEnabled = false;


                List<DeviceResponseVM> attList = new List<DeviceResponseVM>();

                #region Check Connection

                // if device not connected then connect it
                if (bIsConnected == false)
                {
                    if (ConfigData[1] == "" || ConfigData[2] == "0")
                    {
                        Helper.WriteLog("[Error] IP or Port must be vaild.");
                        return;
                    }

                    idwErrorCode = 0;

                    bIsConnected = axCZKEM1.Connect_Net(ConfigData[1], Convert.ToInt32(ConfigData[2]));
                    if (bIsConnected == true)
                    {
                        iMachineNumber = 1;
                        axCZKEM1.RegEvent(iMachineNumber, 65535);
                    }
                    else
                    {
                        axCZKEM1.GetLastError(ref idwErrorCode);
                        Helper.WriteLog("[Error] Unable to connect the device. Error Code: " + idwErrorCode);
                        return;
                    }
                }
                Helper.WriteLog("Device is connected.");
                #endregion

                #region Read logs and Save

                //disable the device
                axCZKEM1.EnableDevice(iMachineNumber, false);
                //read all the attendance records to the memory
                if (axCZKEM1.ReadGeneralLogData(iMachineNumber))
                {
                    while (axCZKEM1.SSR_GetGeneralLogData(iMachineNumber, out sdwEnrollNumber, out idwVerifyMode,
                               out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour, out idwMinute, out idwSecond, ref idwWorkcode))//get records from the memory
                    {
                        if (new DateTime(idwYear, idwMonth, idwDay,idwHour,idwMinute,idwSecond) >= startDate &&
                            new DateTime(idwYear, idwMonth, idwDay, idwHour, idwMinute, idwSecond) <= endDate
                            )
                        {
                            #region Read logs

                            axCZKEM1.SSR_GetUserInfo(iMachineNumber, sdwEnrollNumber, out sdwEmployeeName, out sdwPassword, out sdwPrivilege, out sdwEnabled);

                            iGLCount++;
                            iIndex++;
                            DeviceResponseVM item = new DeviceResponseVM();
                            item.idwIndex = iIndex;
                            item.sdwEnrollNumber = Convert.ToInt32(sdwEnrollNumber);
                            item.sdwEmployeeName = sdwEmployeeName;
                            item.idwVerifyMode = idwVerifyMode;
                            item.idwInOutMode = idwInOutMode;
                            item.idwDate = new DateTime(idwYear, idwMonth, idwDay, idwHour, idwMinute, idwSecond);
                            item.idwWorkcode = idwWorkcode;
                            attList.Add(item);

                            #endregion
                        }
                    }

                    DataTable dt = new DataTable();
                    dt.Columns.Add("EmpId", typeof(Int32));
                    dt.Columns.Add("EmpName", typeof(string));
                    dt.Columns.Add("DeviceId", typeof(Int32));
                    dt.Columns.Add("LogDate", typeof(DateTime));
                    dt.Columns.Add("Status", typeof(Int32));
                    dt.Columns.Add("CreatedBy", typeof(Int32));

                    foreach (DeviceResponseVM item in attList)
                    {
                        #region Save Logs

                        DataRow workRow = dt.NewRow();
                        workRow["EmpId"] = item.sdwEnrollNumber;
                        workRow["EmpName"] = item.sdwEmployeeName;
                        workRow["DeviceId"] = 1;
                        workRow["LogDate"] = item.idwDate;
                        workRow["Status"] = item.idwInOutMode;
                        workRow["CreatedBy"] = 1;

                        dt.Rows.Add(workRow);

                        #endregion
                    }

                    if (dt.Rows.Count > 0)
                    {
                        Helper.ExecuteQuery(dt,ConfigData[0]);
                    }
                }
                else
                {
                    axCZKEM1.GetLastError(ref idwErrorCode);
                    if (idwErrorCode != 0)
                    {
                        Helper.WriteLog("[Error] Reading data from terminal failed. Error Code: " + idwErrorCode);
                        return;
                    }
                    else
                    {
                        Helper.WriteLog("[Error] No data from terminal returns!");
                        return;
                    }
                }

                Helper.WriteLog( attList.Count + " records fetched successfully from " + startDate.ToString() + " to " + endDate.ToString());

                #endregion
            }
            catch (Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex);
            }
            finally
            {
                axCZKEM1.EnableDevice(iMachineNumber, true);
                axCZKEM1.Disconnect();
                bIsConnected = false;
            }
            return;
        }

        protected override void OnStop()
        {
            try
            {
                timer.Enabled = false;
                axCZKEM1 = null;

                Helper.WriteLog("Attendance window service stopped.");
            }
            catch(Exception ex)
            {
                Helper.WriteLog("[Exception] " + ex);
            }
        }

        internal void TestStartupAndStop(string[] args)
        {
            this.OnStart(args);
            Console.ReadLine();
            this.OnStop();
        }
    }
}
