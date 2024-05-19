using System;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using DevExpress.XtraBars.Docking2010;
using System.Text.RegularExpressions;
using IndustrialNetworks.ModbusCore.ASCII;
using IndustrialNetworks.ModbusCore.DataTypes;
using System.Threading;
using System.Data.SqlClient;
using System.Configuration;
using DevExpress.XtraCharts;
using System.Globalization;
using DevExpress.Spreadsheet;
using System.Collections.Generic;

namespace NemSis
{
    public partial class FormMain : DevExpress.XtraEditors.XtraForm
    {
        private bool lastBuzzerStatus = false;
        private LedStatus lastLedStatus = LedStatus.Green;

        private string sqlConnectionString;
        private SqlConnection sqlConnection;

        private Sensor sensorSG1Temp1;
        private Sensor sensorSG1Temp2;
        //    private Sensor sensorSG1Temp3;
        private Sensor sensorSG2Temp1;
        private Sensor sensorSG2Temp2;
        //   private Sensor sensorSG2Temp3;
        private Sensor sensorSG1Hum1;
        private Sensor sensorSG1Hum2;
        //   private Sensor sensorSG1Hum3;
        private Sensor sensorSG2Hum1;
        private Sensor sensorSG2Hum2;
        //  private Sensor sensorSG2Hum3;
        private SensorGroup SensorGroup1;
        private SensorGroup SensorGroup2;

        private string date = string.Empty;
        private string time = string.Empty;

        Thread threadPLCManager = null;

        private bool modbusConnectionStatus = false;
        private string modbusComPort = string.Empty;
        private int modbusBaudrate = 0;
        private int modbusDataBits = 0;
        private System.IO.Ports.StopBits modbusStopBits = System.IO.Ports.StopBits.None;
        private System.IO.Ports.Parity modbusParity = System.IO.Ports.Parity.None;

        private byte modbusSlaveAddress = 0;
        private ModbusASCIIMaster objModbusASCIIMaster = null;

        public FormMain()
        {
            InitializeComponent();
        }

        private void FormMain_Load(object sender, EventArgs e)
        {
            System.Globalization.CultureInfo customCulture = (System.Globalization.CultureInfo)System.Threading.Thread.CurrentThread.CurrentCulture.Clone();
            customCulture.NumberFormat.NumberDecimalSeparator = ".";
            System.Threading.Thread.CurrentThread.CurrentCulture = customCulture;

            // TODO: This line of code loads data into the 'nemsisDataSet.Nemsis' table. You can move, or remove it, as needed.
            this.nemsisTableAdapter.Fill(this.nemsisDataSet.Nemsis);
            sqlConnectionString = ConfigurationManager.ConnectionStrings["NemSis.Properties.Settings.NemsisConnectionString"].ConnectionString;

            navFrMain.TransitionAnimationProperties.FrameCount = 200;
            lblScreenName.Text = "Temperature and Humidity Monitor";
            navFrMain.SelectedPage = navPgHome;
            InitModbusConnectionSettings();

            sensorSG1Temp1 = new Sensor(SensorType.Temperature, lblSG1Temp1);
            sensorSG1Hum1 = new Sensor(SensorType.Humudity, lblSG1Hum1);
            sensorSG1Temp2 = new Sensor(SensorType.Temperature, lblSG1Temp2);
            sensorSG1Hum2 = new Sensor(SensorType.Humudity, lblSG1Hum2);
            //  sensorSG1Temp3 = new Sensor(SensorType.Temperature, lblSG1Temp3);
            //sensorSG1Hum3 = new Sensor(SensorType.Humudity, lblSG1Hum3);

            sensorSG2Temp1 = new Sensor(SensorType.Temperature, lblSG2Temp1);
            sensorSG2Hum1 = new Sensor(SensorType.Humudity, lblSG2Hum1);
            sensorSG2Temp2 = new Sensor(SensorType.Temperature, lblSG2Temp2);
            sensorSG2Hum2 = new Sensor(SensorType.Humudity, lblSG2Hum2);
            //   sensorSG2Temp3 = new Sensor(SensorType.Temperature, lblSG2Temp3);
            //  sensorSG2Hum3 = new Sensor(SensorType.Humudity, lblSG2Hum3);

            SensorGroup1 = new SensorGroup(lblSG1AvgTemp, lblSG1AvgHum);
            SensorGroup1.SensorList.Add(sensorSG1Temp1);
            SensorGroup1.SensorList.Add(sensorSG1Hum1);
            SensorGroup1.SensorList.Add(sensorSG1Temp2);
            SensorGroup1.SensorList.Add(sensorSG1Hum2);
            //   SensorGroup1.SensorList.Add(sensorSG1Temp3);
            //   SensorGroup1.SensorList.Add(sensorSG1Hum3);

            SensorGroup2 = new SensorGroup(lblSG2AvgTemp, lblSG2AvgHum);
            SensorGroup2.SensorList.Add(sensorSG2Temp1);
            SensorGroup2.SensorList.Add(sensorSG2Hum1);
            SensorGroup2.SensorList.Add(sensorSG2Temp2);
            SensorGroup2.SensorList.Add(sensorSG2Hum2);
            // SensorGroup2.SensorList.Add(sensorSG2Temp3);
            //SensorGroup2.SensorList.Add(sensorSG2Hum3);

            timerDateTime.Start();
            //timerModbus.Start();

            ChartControlsInit();
            InitExcelExporter();

            // Start PLC Manager Thread if it is not already started.
            if (threadPLCManager == null)
            {
                threadPLCManager = new Thread(ThreadPLCManager);
                threadPLCManager.Start();
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Abort PLC Manager Thread if it is already started.
            if (threadPLCManager != null)
            {
                threadPLCManager.Abort();
                threadPLCManager = null;
            }
        }

        private void timerDateTime_Tick(object sender, EventArgs e)
        {
            string nowDate = DateTime.Now.ToString("dd.MM.yyyy");
            string nowDateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            if (!nowDate.Equals(date))
            {
                date = nowDate;
                lblDate.Text = date;
            }

            string nowTime = DateTime.Now.ToString("HH:mm");
            if (!nowTime.Equals(time))
            {
                time = nowTime;
                lblTime.Text = time;

                if (Convert.ToInt32(time.Substring(3, 2)) % 5 == 0)
                {
                    string query = "INSERT INTO Nemsis VALUES (@DateTime, @SG1_AvgTemp, @SG1_AvgHum, @SG1_Temp1, @SG1_Hum1, @SG1_Temp2, @SG1_Hum2, @SG1_Temp3, @SG1_Hum3, @SG2_AvgTemp, @SG2_AvgHum, @SG2_Temp1, @SG2_Hum1, @SG2_Temp2, @SG2_Hum2, @SG2_Temp3, @SG2_Hum3)";

                    using (sqlConnection = new SqlConnection(sqlConnectionString))
                    using (SqlCommand command = new SqlCommand(query, sqlConnection))
                    {
                        sqlConnection.Open();

                        command.Parameters.AddWithValue("@DateTime", nowDateTime);

                        command.Parameters.AddWithValue("@SG1_AvgTemp", (SensorGroup1.TempText.Equals("ERROR")) ? SensorGroup1.TempText : SensorGroup1.TempValue.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_AvgHum", (SensorGroup1.HumText.Equals("ERROR")) ? SensorGroup1.HumText : SensorGroup1.HumValue.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Temp1", (SensorGroup1.SensorList[0].Text.Equals("ERROR")) ? SensorGroup1.SensorList[0].Text : SensorGroup1.SensorList[0].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Hum1", (SensorGroup1.SensorList[1].Text.Equals("ERROR")) ? SensorGroup1.SensorList[1].Text : SensorGroup1.SensorList[1].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Temp2", (SensorGroup1.SensorList[2].Text.Equals("ERROR")) ? SensorGroup1.SensorList[2].Text : SensorGroup1.SensorList[2].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Hum2", (SensorGroup1.SensorList[3].Text.Equals("ERROR")) ? SensorGroup1.SensorList[3].Text : SensorGroup1.SensorList[3].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Temp3", (SensorGroup1.SensorList[4].Text.Equals("ERROR")) ? SensorGroup1.SensorList[4].Text : SensorGroup1.SensorList[4].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG1_Hum3", (SensorGroup1.SensorList[5].Text.Equals("ERROR")) ? SensorGroup1.SensorList[5].Text : SensorGroup1.SensorList[5].Value.ToString("0.00"));

                        command.Parameters.AddWithValue("@SG2_AvgTemp", (SensorGroup2.TempText.Equals("ERROR")) ? SensorGroup2.TempText : SensorGroup2.TempValue.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_AvgHum", (SensorGroup2.HumText.Equals("ERROR")) ? SensorGroup2.HumText : SensorGroup2.HumValue.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Temp1", (SensorGroup2.SensorList[0].Text.Equals("ERROR")) ? SensorGroup2.SensorList[0].Text : SensorGroup2.SensorList[0].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Hum1", (SensorGroup2.SensorList[1].Text.Equals("ERROR")) ? SensorGroup2.SensorList[1].Text : SensorGroup2.SensorList[1].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Temp2", (SensorGroup2.SensorList[2].Text.Equals("ERROR")) ? SensorGroup2.SensorList[2].Text : SensorGroup2.SensorList[2].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Hum2", (SensorGroup2.SensorList[3].Text.Equals("ERROR")) ? SensorGroup2.SensorList[3].Text : SensorGroup2.SensorList[3].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Temp3", (SensorGroup2.SensorList[4].Text.Equals("ERROR")) ? SensorGroup2.SensorList[4].Text : SensorGroup2.SensorList[4].Value.ToString("0.00"));
                        command.Parameters.AddWithValue("@SG2_Hum3", (SensorGroup2.SensorList[5].Text.Equals("ERROR")) ? SensorGroup2.SensorList[5].Text : SensorGroup2.SensorList[5].Value.ToString("0.00"));

                        command.ExecuteNonQuery();
                    }

                    this.nemsisTableAdapter.ClearBeforeFill = true;
                    this.nemsisTableAdapter.Fill(this.nemsisDataSet.Nemsis);

                    gridControl1.BeginUpdate();
                    try
                    {
                        gridControl1.DataSource = null;
                        gridControl1.DataSource = nemsisBindingSource;
                    }
                    finally
                    {
                        gridControl1.EndUpdate();
                    }

                    gridView1.BeginSort();
                    try
                    {
                        gridView1.ClearSorting();
                        gridView1.ClearSelection();
                        gridView1.Columns["DateTime"].SortOrder = DevExpress.Data.ColumnSortOrder.Descending;
                    }
                    finally
                    {
                        gridView1.EndSort();
                    }

                    gridView1.BeginSelection();
                    try
                    {
                        gridView1.ClearSelection();
                    }
                    finally
                    {
                        gridView1.EndSelection();
                    }

                    AddLastsToChartControls();
                    DateTime dateTime = DateTime.ParseExact(nowDateTime, "yyyy-MM-dd HH:mm", null);
                    ChartControlsShiftTime(dateTime);
                }
            }
        }

        private void btnExcelExport_Click(object sender, EventArgs e)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dir = path + "\\NemSis\\";

            System.IO.Directory.CreateDirectory(dir);

            DateTime dtStart = DateTime.ParseExact(deStartDate.Text + " " + cbStartTime.Text, "dd.MM.yyyy HH:mm", null);
            DateTime dtEnd = DateTime.ParseExact(deEndDate.Text + " " + cbEndTime.Text, "dd.MM.yyyy HH:mm", null);

            SQLToExcel(dir + "NemSis-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".xls", dtStart, dtEnd);
            System.Diagnostics.Process.Start(dir);
        }

        private void windowsUIButtonPanel1_ButtonClick(object sender, ButtonEventArgs e)
        {
            WindowsUIButton btn = e.Button as WindowsUIButton;
            if (btn.Caption != null)
            {
                if (btn.Caption.Equals("HOME"))
                {
                    lblScreenName.Text = "Temperature and Humidity Monitor";
                    navFrMain.SelectedPage = navPgHome;
                }
                else if (btn.Caption.Equals("DATA"))
                {
                    lblScreenName.Text = "Data Monitor";
                    navFrMain.SelectedPage = navPgData;
                }
                else if (btn.Caption.Equals("SETTINGS"))
                {
                    lblScreenName.Text = "Settings Monitor";
                    navFrMain.SelectedPage = navPgSettings;
                }
                else if (btn.Caption.Equals("ABOUT"))
                {
                    lblScreenName.Text = "About";
                    navFrMain.SelectedPage = navPgAbout;
                }
                else if (btn.Caption.Equals("EXIT"))
                {
                    this.Close();
                }
            }
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            DeactivateModbusConnection();
            InitModbusConnectionSettings();
        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            if (!modbusConnectionStatus)
            {
                modbusComPort = cbComPorts.SelectedItem.ToString();

                if (modbusComPort.Equals(string.Empty))
                {
                    DeactivateModbusConnection();
                    return;
                }

                modbusBaudrate = Convert.ToInt32(cbBaudrate.SelectedItem.ToString());

                modbusDataBits = Convert.ToInt32(cbDataBits.SelectedItem.ToString());

                if (cbStopBits.SelectedItem.ToString().Equals("None"))
                {
                    modbusStopBits = System.IO.Ports.StopBits.None;
                }
                else if (cbStopBits.SelectedItem.ToString().Equals("One"))
                {
                    modbusStopBits = System.IO.Ports.StopBits.One;
                }
                else
                {
                    DeactivateModbusConnection();
                    return;
                }

                if (cbParity.SelectedItem.ToString().Equals("None"))
                {
                    modbusParity = System.IO.Ports.Parity.None;
                }
                else if (cbParity.SelectedItem.ToString().Equals("Odd"))
                {
                    modbusParity = System.IO.Ports.Parity.Odd;
                }
                else if (cbParity.SelectedItem.ToString().Equals("Even"))
                {
                    modbusParity = System.IO.Ports.Parity.Even;
                }
                else
                {
                    DeactivateModbusConnection();
                    return;
                }

                ActivateModbusConnection();
            }
            else
            {
                DeactivateModbusConnection();
            }
        }

        private void pbAlpplasCati_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        private void InitModbusConnectionSettings()
        {
            cbSlaveAddress.Properties.Items.Clear();
            cbSlaveAddress.Text = "";
            for (int i = 0; i < 256; ++i)
            {
                cbSlaveAddress.Properties.Items.Add(i);
            }
            cbSlaveAddress.SelectedIndex = 1;
            modbusSlaveAddress = 1;

            cbBaudrate.Properties.Items.Clear();
            cbBaudrate.Text = "";
            cbBaudrate.Properties.Items.Add(4800);
            cbBaudrate.Properties.Items.Add(9600);
            cbBaudrate.Properties.Items.Add(19200);
            cbBaudrate.Properties.Items.Add(38400);
            cbBaudrate.Properties.Items.Add(57600);
            cbBaudrate.Properties.Items.Add(115200);
            cbBaudrate.SelectedIndex = 5;

            cbDataBits.Properties.Items.Clear();
            cbDataBits.Text = "";
            cbDataBits.Properties.Items.Add("7");
            cbDataBits.Properties.Items.Add("8");
            cbDataBits.SelectedIndex = 0;

            cbStopBits.Properties.Items.Clear();
            cbStopBits.Text = "";
            cbStopBits.Properties.Items.Add("None");
            cbStopBits.Properties.Items.Add("One");
            cbStopBits.SelectedIndex = 1;

            cbParity.Properties.Items.Clear();
            cbParity.Text = "";
            cbParity.Properties.Items.Add("None");
            cbParity.Properties.Items.Add("Odd");
            cbParity.Properties.Items.Add("Even");
            cbParity.SelectedIndex = 2;

            cbComPorts.Properties.Items.Clear();
            cbComPorts.Text = "";
            RefreshSerialPortList();
            if (cbComPorts.Properties.Items.Count > 0)
            {
                cbComPorts.SelectedIndex = 0;
            }
        }

        private void RefreshSerialPortList()
        {
            foreach (string portName in System.IO.Ports.SerialPort.GetPortNames())
            {
                cbComPorts.Properties.Items.Add("COM" + Regex.Replace(portName.Substring("COM".Length, portName.Length - "COM".Length), "[^.0-9]", "\0"));
            }
        }

        private void ThreadPLCManager()
        {
            for (; ; )
            {
                if (threadPLCManager != null)
                {
                    // Search all Com Ports to check is connection lost.
                    bool connectionStatus = false;
                    foreach (string portName in System.IO.Ports.SerialPort.GetPortNames())
                    {
                        string tempPort = "COM" + Regex.Replace(portName.Substring("COM".Length, portName.Length - "COM".Length), "[^.0-9]", "\0");
                        if (tempPort.Equals(modbusComPort))
                        {
                            // Connection is still exist. No need to do anything.
                            connectionStatus = true;
                            break;
                        }
                    }

                    // Com Port connection is lost.
                    if (connectionStatus == false)
                    {
                        // Deactivate conneciton.
                        DeactivateModbusConnection();
                    }

                    UpdateSensorValues();
                    SensorErrorDetection();

                    Thread.Sleep(500);
                }
                else
                {
                    return;
                }
            }
        }

        private void ActivateModbusConnection()
        {
            try
            {
                if (modbusConnectionStatus)
                {
                    return; // Already activated.
                }
                else
                {

                    if (objModbusASCIIMaster == null)
                    {
                        objModbusASCIIMaster = new ModbusASCIIMaster(modbusComPort, modbusBaudrate, modbusDataBits, modbusStopBits, modbusParity);
                        objModbusASCIIMaster.Connection();
                        ResetPLCError();
                    }
                    else
                    {
                        return;
                    }

                    modbusConnectionStatus = true;
                    btnConnect.Text = "DISCONNECT";
                    btnConnect.Appearance.BackColor = Color.FromArgb(240, 30, 30);
                    pbConnectionStatus.BackColor = Color.FromArgb(0, 150, 90);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeactivateModbusConnection()
        {
            try
            {
                if (modbusConnectionStatus)
                {

                    if (objModbusASCIIMaster != null)
                    {
                        modbusConnectionStatus = false;
                        objModbusASCIIMaster.Disconnection();
                        objModbusASCIIMaster = null;
                    }
                    else
                    {
                        return;
                    }

                    modbusConnectionStatus = false;
                    modbusComPort = string.Empty;
                    modbusBaudrate = 0;
                    modbusDataBits = 0;
                    modbusStopBits = System.IO.Ports.StopBits.None;
                    modbusParity = System.IO.Ports.Parity.None;

                    btnConnect.Text = "CONNECT";
                    btnConnect.Appearance.BackColor = Color.FromArgb(0, 150, 90);
                    pbConnectionStatus.BackColor = Color.FromArgb(240, 30, 30);

                    SensorGroup1.Reset();
                    SensorGroup2.Reset();
                }
                else
                {
                    return; // Already deactivated.
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSensorValues()
        {
            if (modbusConnectionStatus)
            {
                this.Invoke(new Action(delegate ()
                {
                    byte[] bytes = objModbusASCIIMaster.ReadHoldingRegisters(modbusSlaveAddress, 4096, 36);
                    short[] words = new short[bytes.Length / 2];
                    for (int i = 0; i < bytes.Length; i = i + 2)
                    {
                        words[i / 2] = Int.FromBytes(bytes[i + 1], bytes[i]);
                    }
                    if (words != null)
                    {
                        short module1Name = words[0];
                        short module1Error = words[2];
                        short module2Name = words[12];
                        short module2Error = words[14];
                        short module3Name = words[24];
                        short module3Error = words[26];
                        //???
                        sensorSG1Temp1.Update(words[4], module1Name, module1Error);
                        sensorSG1Hum1.Update(words[6], module1Name, module1Error);
                        sensorSG1Temp2.Update(words[8], module1Name, module1Error);
                        sensorSG1Hum2.Update(words[10], module1Name, module1Error);
                        // sensorSG1Temp3.Update(words[16], module2Name, module2Error);
                        //sensorSG1Hum3.Update(words[18], module2Name, module2Error);

                        sensorSG2Temp1.Update(words[20], module2Name, module2Error);
                        sensorSG2Hum1.Update(words[22], module2Name, module2Error);
                        sensorSG2Temp2.Update(words[28], module3Name, module3Error);
                        sensorSG2Hum2.Update(words[30], module3Name, module3Error);
                        // sensorSG2Temp3.Update(words[32], module3Name, module3Error);
                        //  sensorSG2Hum3.Update(words[34], module3Name, module3Error);

                        SensorGroup1.Update();
                        SensorGroup2.Update();

                    }
                }));
            }
            else
            {
                this.Invoke(new Action(delegate ()
                {
                    sensorSG1Temp1.Update(-9999, 0, 1);
                    sensorSG1Hum1.Update(-9999, 0, 1);
                    sensorSG1Temp2.Update(-9999, 0, 1);
                    sensorSG1Hum2.Update(-9999, 0, 1);
                    //  sensorSG1Temp3.Update(-9999, 0, 1);
                    //   sensorSG1Hum3.Update(-9999, 0, 1);

                    sensorSG2Temp1.Update(-9999, 0, 1);
                    sensorSG2Hum1.Update(-9999, 0, 1);
                    sensorSG2Temp2.Update(-9999, 0, 1);
                    sensorSG2Hum2.Update(-9999, 0, 1);
                    //   sensorSG2Temp3.Update(-9999, 0, 1);
                    //   sensorSG2Hum3.Update(-9999, 0, 1);

                    SensorGroup1.Update();
                    SensorGroup2.Update();
                }));
            }
        }

        private void SensorErrorDetection()
        {
            bool buzzerStatus = false;
            LedStatus ledStatus = LedStatus.Green;

            foreach (Sensor sensor in SensorGroup1.SensorList)
            {
                if (sensor.Text.Equals("ERROR"))
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                    break;
                }
                else
                {
                    if (sensor.Type == SensorType.Temperature)
                    {
                        if (sensor.Value < Sensor.tempMin || sensor.Value > Sensor.tempMax)   //???
                        {
                            buzzerStatus = true;
                            ledStatus = LedStatus.Red;
                            break;
                        }
                    }
                    else if (sensor.Type == SensorType.Humudity)
                    {
                        if (sensor.Value < Sensor.humMin || sensor.Value > Sensor.humMax)   //???
                        {
                            buzzerStatus = true;
                            ledStatus = LedStatus.Red;
                            break;
                        }
                    }
                }
            }

            foreach (Sensor sensor in SensorGroup2.SensorList)
            {
                if (sensor.Text.Equals("ERROR"))
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                    break;
                }
                else
                {
                    if (sensor.Type == SensorType.Temperature)
                    {
                        if (sensor.Value < Sensor.tempMin || sensor.Value > Sensor.tempMax)  //???
                        {
                            buzzerStatus = true;
                            ledStatus = LedStatus.Red;
                            break;
                        }
                    }
                    else if (sensor.Type == SensorType.Humudity)
                    {
                        if (sensor.Value < Sensor.humMin || sensor.Value > Sensor.humMax)   //???
                        {
                            buzzerStatus = true;
                            ledStatus = LedStatus.Red;
                            break;
                        }
                    }
                }
            }

            if (SensorGroup1.TempText.Equals("ERROR") || SensorGroup1.HumText.Equals("ERROR") || SensorGroup2.TempText.Equals("ERROR") || SensorGroup2.HumText.Equals("ERROR"))
            {
                buzzerStatus = true;
                ledStatus = LedStatus.Red;
            }
            else
            {
                if (SensorGroup1.TempValue < Sensor.tempMin || SensorGroup1.TempValue > Sensor.tempMax)  //???
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                }
                if (SensorGroup1.HumValue < Sensor.humMin || SensorGroup1.HumValue > Sensor.humMax)   //???
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                }
                if (SensorGroup2.TempValue < Sensor.tempMin || SensorGroup2.TempValue > Sensor.tempMax)  //???
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                }
                if (SensorGroup2.HumValue < Sensor.humMin || SensorGroup2.HumValue > Sensor.humMax)   //???
                {
                    buzzerStatus = true;
                    ledStatus = LedStatus.Red;
                }
            }

            if (modbusConnectionStatus)
            {
                if (lastBuzzerStatus != buzzerStatus)
                {
                    if (buzzerStatus)
                    {
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1280, true);
                    }
                    else
                    {
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1280, false);
                    }
                }
                lastBuzzerStatus = buzzerStatus;

                if (lastLedStatus != ledStatus)
                {
                    if (ledStatus == LedStatus.Green)
                    {
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1281, true);
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1282, false);
                    }
                    else if (ledStatus == LedStatus.Red)
                    {
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1281, false);
                        objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1282, true);
                    }
                }
                lastLedStatus = ledStatus;
            }
            else
            {
                return;
            }
        }

        private void ResetPLCError()
        {
            lastBuzzerStatus = false;
            lastLedStatus = LedStatus.None;

            if (modbusConnectionStatus)
            {
                objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1280, false);
                objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1281, false);
                objModbusASCIIMaster.WriteSingleCoil(modbusSlaveAddress, 1282, false);
            }
            else
            {
                return;
            }
        }

        private void InitExcelExporter()
        {
            cbStartTime.Properties.Items.Clear();
            cbStartTime.Text = "";
            cbEndTime.Properties.Items.Clear();
            cbEndTime.Text = "";
            for (int i = 0; i < 24; ++i)
            {
                for (int j = 0; j < 60; j += 5)
                {
                    string temp = i.ToString().PadLeft(2, '0') + ":" + j.ToString().PadLeft(2, '0');
                    cbStartTime.Properties.Items.Add(temp);
                    cbEndTime.Properties.Items.Add(temp);
                }
            }
            cbStartTime.SelectedIndex = 0;
            cbEndTime.SelectedIndex = 0;

            DateTime dateTime = GetDateTimeOfNextTimeMod5();
            int timeHour = Convert.ToInt32(dateTime.ToString("HH"));
            int timeMin = Convert.ToInt32(dateTime.ToString("mm"));

            int indexCnt = 0;
            for (int i = 0; i < 24; ++i)
            {
                for (int j = 0; j < 60; j += 5)
                {
                    if (i == timeHour && j == timeMin)
                    {
                        cbStartTime.SelectedIndex = indexCnt;
                        cbEndTime.SelectedIndex = indexCnt;
                        break;
                    }
                    indexCnt++;
                }
            }

            deStartDate.DateTime = dateTime.AddDays(-1);
            deEndDate.DateTime = dateTime;

        }

        private void ChartControlsInit()
        {
            string query = "SELECT * FROM (SELECT TOP(12) Id, DateTime, SG1_AvgTemp, SG1_AvgHum, SG2_AvgTemp, SG2_AvgHum FROM Nemsis ORDER BY Id DESC) T ORDER BY Id";

            DataTable dt = new DataTable();
            using (sqlConnection = new SqlConnection(sqlConnectionString))
            using (SqlCommand command = new SqlCommand(query, sqlConnection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                adapter.Fill(dt);
            }

            if (dt != null)
            {
                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        DateTime rowDateTime = DateTime.Parse(dr["DateTime"].ToString());
                        if (!dr["SG1_AvgTemp"].Equals("ERROR"))
                        {
                            float avgTemp_SG1 = Convert.ToSingle(dr["SG1_AvgTemp"], CultureInfo.CurrentCulture);
                            chartControlSG1AvgTemp.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgTemp_SG1));
                        }
                        if (!dr["SG1_AvgHum"].Equals("ERROR"))
                        {
                            float avgHum_SG1 = Convert.ToSingle(dr["SG1_AvgHum"], CultureInfo.CurrentCulture);
                            chartControlSG1AvgHum.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgHum_SG1));
                        }
                        if (!dr["SG2_AvgTemp"].Equals("ERROR"))
                        {
                            float avgTemp_SG2 = Convert.ToSingle(dr["SG2_AvgTemp"], CultureInfo.CurrentCulture);
                            chartControlSG2AvgTemp.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgTemp_SG2));
                        }
                        if (!dr["SG2_AvgHum"].Equals("ERROR"))
                        {
                            float avgHum_SG2 = Convert.ToSingle(dr["SG2_AvgHum"], CultureInfo.CurrentCulture);
                            chartControlSG2AvgHum.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgHum_SG2));
                        }
                    }
                }
            }

            ChartControlsShiftTime(GetDateTimeOfNextTimeMod5());
        }

        private DateTime GetDateTimeOfNextTimeMod5()
        {
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            int timeHour = Convert.ToInt32(DateTime.Now.ToString("HH"));
            int timeMin = Convert.ToInt32(DateTime.Now.ToString("mm"));
            if ((timeMin + 5 - (timeMin % 5)) >= 60)
            {
                timeHour++;
            }
            if (timeMin % 5 != 0)
            {
                timeMin = timeMin + 5 - (timeMin % 5);
            }
            if (timeMin == 60)
            {
                timeMin = 0;
            }
            DateTime dateTime = DateTime.Parse(date + " " + timeHour.ToString().PadLeft(2, '0') + ":" + timeMin.ToString().PadLeft(2, '0'));
            return dateTime;
        }

        private void AddLastsToChartControls()
        {
            string query = "SELECT TOP(1) Id, DateTime, SG1_AvgTemp, SG1_AvgHum, SG2_AvgTemp, SG2_AvgHum FROM Nemsis ORDER BY Id DESC";

            DataTable dt = new DataTable();
            using (sqlConnection = new SqlConnection(sqlConnectionString))
            using (SqlCommand command = new SqlCommand(query, sqlConnection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                adapter.Fill(dt);
            }

            if (dt != null)
            {
                if (dt.Rows.Count == 1)
                {
                    DataRow dr = dt.Rows[0];
                    DateTime rowDateTime = DateTime.Parse(dr["DateTime"].ToString());
                    if (!dr["SG1_AvgTemp"].Equals("ERROR"))
                    {
                        float avgTemp_SG1 = Convert.ToSingle(dr["SG1_AvgTemp"], CultureInfo.CurrentCulture);
                        chartControlSG1AvgTemp.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgTemp_SG1));
                    }
                    if (!dr["SG1_AvgHum"].Equals("ERROR"))
                    {
                        float avgHum_SG1 = Convert.ToSingle(dr["SG1_AvgHum"], CultureInfo.CurrentCulture);
                        chartControlSG1AvgHum.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgHum_SG1));
                    }
                    if (!dr["SG2_AvgTemp"].Equals("ERROR"))
                    {
                        float avgTemp_SG2 = Convert.ToSingle(dr["SG2_AvgTemp"], CultureInfo.CurrentCulture);
                        chartControlSG2AvgTemp.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgTemp_SG2));
                    }
                    if (!dr["SG2_AvgHum"].Equals("ERROR"))
                    {
                        float avgHum_SG2 = Convert.ToSingle(dr["SG2_AvgHum"], CultureInfo.CurrentCulture);
                        chartControlSG2AvgHum.Series[0].Points.Add(new SeriesPoint(rowDateTime, avgHum_SG2));
                    }
                }
            }
        }

        private void ChartControlsShiftTime(DateTime dateTime)
        {

            XYDiagram diagram = (XYDiagram)chartControlSG1AvgTemp.Diagram;
            diagram.AxisX.WholeRange.Auto = false;
            diagram.AxisX.WholeRange.AutoSideMargins = false;
            diagram.AxisX.WholeRange.MinValue = dateTime.AddMinutes(-60);
            diagram.AxisX.WholeRange.MaxValue = dateTime;
            diagram.AxisX.WholeRange.SideMarginsValue = 5;

            diagram = (XYDiagram)chartControlSG1AvgHum.Diagram;
            diagram.AxisX.WholeRange.Auto = false;
            diagram.AxisX.WholeRange.AutoSideMargins = false;
            diagram.AxisX.WholeRange.MinValue = dateTime.AddMinutes(-60);
            diagram.AxisX.WholeRange.MaxValue = dateTime;
            diagram.AxisX.WholeRange.SideMarginsValue = 5;

            diagram = (XYDiagram)chartControlSG2AvgTemp.Diagram;
            diagram.AxisX.WholeRange.Auto = false;
            diagram.AxisX.WholeRange.AutoSideMargins = false;
            diagram.AxisX.WholeRange.MinValue = dateTime.AddMinutes(-60);
            diagram.AxisX.WholeRange.MaxValue = dateTime;
            diagram.AxisX.WholeRange.SideMarginsValue = 5;

            diagram = (XYDiagram)chartControlSG2AvgHum.Diagram;
            diagram.AxisX.WholeRange.Auto = false;
            diagram.AxisX.WholeRange.AutoSideMargins = false;
            diagram.AxisX.WholeRange.MinValue = dateTime.AddMinutes(-60);
            diagram.AxisX.WholeRange.MaxValue = dateTime;
            diagram.AxisX.WholeRange.SideMarginsValue = 5;
        }

        public void SQLToExcel(string dirPath, DateTime dateTimeStart, DateTime dateTimeEnd)
        {
            string query = "SELECT DateTime, SG1_AvgTemp, SG1_AvgHum, SG1_Temp1, SG1_Hum1, SG1_Temp2, SG1_Hum2, SG1_Temp3, SG1_Hum3, SG2_AvgTemp, SG2_AvgHum, SG2_Temp1, SG2_Hum1, SG2_Temp2, SG2_Hum2, SG2_Temp3, SG2_Hum3 FROM Nemsis WHERE DateTime >= '" + dateTimeStart.ToString("yyyy-MM-dd HH:mm") + "' AND DateTime <= '" + dateTimeEnd.ToString("yyyy-MM-dd HH:mm") + "' ORDER BY DateTime DESC";

            DataTable dt = new DataTable();
            using (sqlConnection = new SqlConnection(sqlConnectionString))
            using (SqlCommand command = new SqlCommand(query, sqlConnection))
            using (SqlDataAdapter adapter = new SqlDataAdapter(command))
            {
                adapter.Fill(dt);
            }

            Workbook wb = new Workbook();
            wb.Worksheets[0].Import(dt, true, 0, 0);
            wb.SaveDocument(dirPath, DevExpress.Spreadsheet.DocumentFormat.Xls);
        }
    }
}