using DevExpress.XtraEditors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NemSis
{
    public class Sensor
    {
        public static float tempMin = 23;
        public static float tempMax = 27;
        public static float humMin = 40;
        public static float humMax = 60;

        public double Value { get; set; }
        public short ModuleNo { get; set; }
        public short ModuleError { get; set; }
        public string Text { get; set; }
        public SensorType Type { get; set; }
        public LabelControl LabelControl { get; set; }

        public Sensor()
        {
            Value = -9999;
            Text = "ERROR";
        }
        public Sensor(SensorType type, LabelControl lblControl)
        {
            Value = -9999;
            Text = "ERROR";
            Type = type;
            LabelControl = lblControl;
        }

        public void Reset()
        {
            this.Value = -9999;
            this.Text = "ERROR";
            this.LabelControl.Text = this.Text;
            this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
        }

        public void Update(short value, short moduleNo, short moduleError)
        {
            this.ModuleNo = moduleNo;
            this.ModuleError = moduleError;
            int min = 0;
            int max = 4000;

            if (this.ModuleNo > 0 && this.ModuleError == 0)
            {
                if (value >= min && value <= max)
                {
                    if (this.Type == SensorType.Temperature)
                    {
                        this.Value = Convert.ToDouble(value) * 0.025 - 20;
                        this.Text = this.Value.ToString("0.00") + " °C";

                        if (this.Value < -20 || this.Value > 80)
                        {
                            this.Value = -9999;
                            this.Text = "ERROR";
                            this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
                        }
                        else if (this.Value < tempMin || this.Value > tempMax)
                        {
                            this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
                        }
                        else
                        {
                            this.LabelControl.ForeColor = Color.FromArgb(30, 30, 30);
                        }
                    }
                    else if (this.Type == SensorType.Humudity)
                    {
                        this.Value = Convert.ToDouble(value) * 0.025;
                        this.Text = this.Value.ToString("0.00") + " %"; ;

                        if (this.Value < 0 || this.Value > 100)
                        {
                            this.Value = -9999;
                            this.Text = "ERROR";
                            this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
                        }
                        else if (this.Value < humMin || this.Value > humMax)
                        {
                            this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
                        }
                        else
                        {
                            this.LabelControl.ForeColor = Color.FromArgb(30, 30, 30);
                        }
                    }
                }
                else
                {
                    this.Value = -9999;
                    this.Text = "ERROR";
                    this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
                }
            }
            else
            {
                this.Value = -9999;
                this.Text = "ERROR";
                this.LabelControl.ForeColor = Color.FromArgb(240, 30, 30);
            }
            this.LabelControl.Text = this.Text;
        }
    }

    public enum SensorType
    {
        Temperature,
        Humudity
    };

    public enum LedStatus
    {
        None,
        Green,
        Red
    };
}
