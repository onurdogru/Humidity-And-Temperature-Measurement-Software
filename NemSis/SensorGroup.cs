using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NemSis
{
    public class SensorGroup
    {
        public double TempValue { get; set; }
        public double HumValue { get; set; }
        public string TempText { get; set; }
        public string HumText { get; set; }
        public List<Sensor> SensorList { get; set; }
        public LabelControl TempLabelControl { get; set; }
        public LabelControl HumLabelControl { get; set; }

        public SensorGroup(LabelControl lblControlTemp, LabelControl lblControlHum)
        {
            TempValue = -9999;
            HumValue = -9999;
            TempText = "ERROR";
            HumText = "ERROR";
            SensorList = new List<Sensor>();
            TempLabelControl = lblControlTemp;
            HumLabelControl = lblControlHum;
        }

        public void Reset()
        {
            this.TempValue = -9999;
            this.TempText = "ERROR";
            this.HumValue = -9999;
            this.HumText = "ERROR";
            this.TempLabelControl.Text = this.TempText;
            this.TempLabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            this.HumLabelControl.Text = this.HumText;
            this.HumLabelControl.ForeColor = Color.FromArgb(240, 30, 30);

            foreach (Sensor sensor in this.SensorList)
            {
                sensor.Reset();
            }
        }

        public void Update()
        {
            int divTemp = 0;
            int divHum = 0;
            double sumTemp = 0;
            double sumHum = 0;
            foreach (Sensor sensor in SensorList)
            {
                if (sensor.Type == SensorType.Temperature)
                {
                    if (!sensor.Text.Equals("ERROR"))
                    {
                        divTemp++;
                        sumTemp += sensor.Value;
                    }
                }
                else if (sensor.Type == SensorType.Humudity)
                {
                    if (!sensor.Text.Equals("ERROR"))
                    {
                        divHum++;
                        sumHum += sensor.Value;
                    }
                }
            }

            if (divTemp > 0)
            {
                sumTemp = sumTemp / divTemp;
                this.TempValue = sumTemp;
                this.TempText = this.TempValue.ToString("0.00") + " °C"; ;
                this.TempLabelControl.ForeColor = Color.FromArgb(30, 30, 30);
            }
            else
            {
                this.TempValue = -9999;
                this.TempText = "ERROR";
                this.TempLabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            }

            if (divHum > 0)
            {
                sumHum = sumHum / divHum;
                this.HumValue = sumHum;
                this.HumText = this.HumValue.ToString("0.00") + " %"; ;
                this.HumLabelControl.ForeColor = Color.FromArgb(30, 30, 30);
            }
            else
            {
                this.HumValue = -9999;
                this.HumText = "ERROR";
                this.HumLabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            }

            if (this.TempValue < Sensor.tempMin || this.TempValue > Sensor.tempMax)
            {
                this.TempLabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            }

            if (this.HumValue < Sensor.humMin || this.HumValue > Sensor.humMax)
            {
                this.HumLabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            }

            this.TempLabelControl.Text = this.TempText;
            this.HumLabelControl.Text = this.HumText;
        }
    }

}
