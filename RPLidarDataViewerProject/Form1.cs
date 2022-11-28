﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RPLidarDataViewerProject.Form1;
using static System.Windows.Forms.AxHost;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace RPLidarDataViewerProject
{
    public partial class Form1 : Form
    {
        public struct RPLidarData
        {
            public bool StartFlagBit;
            public bool InversedStartFlagBit;
            public uint Quality;
            public bool CheckBit;   //Constantly set to 1.
            public float Angle;
            public float Distance;
        }

        Bitmap lidarViewBitmap = new Bitmap(500, 500);
        RPLidarData[] rPLidarData = new RPLidarData[2000];
        List<RPLidarData> rpLidarDatas = new List<RPLidarData>();   //NOTE: Not using now.

        //Seriport okuması yapılır iken eklenenler.
        //RPLidarData[] serialPortRPLidarData = new RPLidarData[30];
        int serialPortRPLidarDataIndex = 0;
        Byte[] spLidarScanRawDataBuf = new Byte[5];
        Byte[] rpLidarScanIDResponse = new Byte[] { 0xA5, 0x5A, 0x05, 0x00, 0x00, 0x40, 0x81 };
        Byte[] rpLidarScanIDResponseData = new Byte[7];

        Stopwatch watch = new Stopwatch();//NOTE: Test için eklendi.

        //Test_Start_For_Zone
        LidarZone zone;
        //Test_End_For_Zone

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Seri Port İsimleri combobox icerisine eklendi.
            string[] serialPortListArray = SerialPort.GetPortNames();
            foreach (string serialPort in serialPortListArray)
            {
                comboBoxSeriPortList.Items.Add(serialPort);
            }

            int serialPortCounts = comboBoxSeriPortList.Items.Count;
            if (serialPortCounts > 0) comboBoxSeriPortList.SelectedIndex = 0;
        }

        private void buttonOpenFile_Click(object sender, EventArgs e)
        {
            //Slected the file address
            string fileAddress;
            openFileDialog1.ShowDialog(this);
            fileAddress = @"" + openFileDialog1.FileName;   //Okunacak dosya path'i adrese ceviriliyor.
            if (fileAddress == "") return;   //Bos dosya adresi gelir ise fonksiyondan cıkacak.

            //Read file from file address.
            string fileText;
            StreamReader sr = new StreamReader(fileAddress);
            fileText = sr.ReadToEnd();
            sr.Close();
            textBoxOpenFile.Text = fileText;    //Okunan dosya icerigi gosteriliyor.

            //File to Raw Lidar Data Convert
            List<byte[]> lidarRawDatas = new List<byte[]>();    //5 byte'lık lidar raw data dizisi.
            lidarRawDatas = readRecieveLidarDataFromFile(fileText); //Okunan text 

            //Lidar Raw Data To Lidar Data Convert
            Array.Clear(rPLidarData, 0, rPLidarData.Length);//NOTE: veriler üst üste yazılmasın diye dizi temizleniyor.
            for (int index = 0; index < lidarRawDatas.Count; index++)
            {
                rPLidarData[index] = createRPlidarDataFromRawData(lidarRawDatas[index]);
            }

            //Asağısı düzeltilecek.(15112022)
            //Kullanılacak list box icerigi her dosya acmada temizleniyor.
            listBoxRPLidarData.Items.Clear();
            listBoxRPLidarStartBits.Items.Clear();
            listBoxRPLidarDataQuality.Items.Clear();

            //Lidar data viewer.
            float distanceThreshod = (float)Convert.ToUInt32(textBoxViewLidarDistance.Text);
            float qualityThreshold = (float)Convert.ToUInt32(textBoxDataQualityThreshold.Text);

            //Lidar data viewer Mid ve Low degerleri.
            float LowDistanceThreshod = (float)Convert.ToUInt32(textBoxLidarLowDistance.Text);
            float MidDistanceThreshod = (float)Convert.ToUInt32(textBoxLidarMidDistance.Text);

            //Lidar Zone 'Width' , 'Height' ve offset  degerleri.
            float lidarZoneHeight = (float)Convert.ToUInt32(numericUpDownLidarZoneHeight.Value);
            float lidarZoneHeightOffset = (float)Convert.ToInt32(numericUpDownLidarZoneHeightOffset.Value);
            float lidarZoneWidth = (float)Convert.ToUInt32(numericUpDownLidarZoneWidth.Value);
            float lidarZoneWidthOffset = (float)Convert.ToInt32(numericUpDownLidarZoneWidthOffset.Value);

            float zoneCenterX = lidarZoneWidth / 2;
            float zoneCenterY = lidarZoneHeight / 2;

            int lidarZoneMin_X = (int)(lidarZoneWidthOffset - (zoneCenterX));
            int lidarZoneMax_X = (int)(lidarZoneWidthOffset + (zoneCenterX));
            int lidarZoneMin_Y = (int)(lidarZoneHeightOffset * (-1) - (zoneCenterY));
            int lidarZoneMax_Y = (int)(lidarZoneHeightOffset * (-1) + (zoneCenterY));

            //Lidar zone çizdiriliyor.
            lidarViewBitmap = createLidarDetectZoneViewer(lidarViewBitmap, lidarZoneWidth, lidarZoneWidthOffset, lidarZoneHeight, lidarZoneHeightOffset);

            //RPLidar datası istenen bir text formatına çevrildi.
            for (int i = 0; i < rPLidarData.Length; i++)
            {
                //string tempText =$"{(i + 1).ToString()} )\t";
                //tempText +=$"Start Bit:\t{(tempStartBit.ToString())}";
                //tempText +=$"\tQuality:\t{(tempQuality.ToString("00"))}";
                //tempText +=$"\tAngle:\t{(tempAngle.ToString("0.0"))}";
                //tempText +=$"\tDistance:\t{(tempDistance.ToString("0.0"))}mm";

                //listBoxRPLidarData.Items.Add(tempText);
                //lidarDataCount++;

                //Start Bitleri filtrelendi.
                //if (Convert.ToBoolean(tempStartBit & 0x01))
                //{
                //    listBoxRPLidarStartBits.Items.Add(tempText);
                //    lidarStartBitCount++;
                //}

                //Data Quality filtrelendi.
                //lidarDataQualityThreshold = Convert.ToUInt32(textBoxDataQualityThreshold.Text);
                //if (tempQuality >= lidarDataQualityThreshold)
                //{
                //    listBoxRPLidarDataQuality.Items.Add(tempText);
                //    lidarDataQualityCount++;
                //}

                //Lidar datası zone alanı için kontrol ediliyor.
                //bool detectionZoneFlag = false;
                //detectionZoneFlag = checkTheLidarDetectionZone((float)tempAngle, (float)tempDistance, lidarZoneMin_X, lidarZoneMax_X, lidarZoneMin_Y, lidarZoneMax_Y);

                /*//lidar datası grafiğe çizdiriliyor.
                if ( (tempDistance <= distanceThreshod) && (tempQuality >= qualityThreshold) )
                {
                    //Draving lidar data 'x' and 'y' coordinate.
                    if (tempDistance <= LowDistanceThreshod && detectionZoneFlag == true)
                    {
                        bmp = createLidarDataGraph(new Pen(Brushes.Red, 3), (float)tempAngle, (float)tempDistance, bmp, 150, 6000);
                    }
                    else if(tempDistance > LowDistanceThreshod && tempDistance <= MidDistanceThreshod && detectionZoneFlag == true)
                    {
                        bmp = createLidarDataGraph(new Pen(Brushes.Yellow, 3), (float)tempAngle, (float)tempDistance, bmp, 150, 6000);
                    }
                    else
                    {
                        bmp = createLidarDataGraph(new Pen(Brushes.Green, 3), (float)tempAngle, (float)tempDistance, bmp, 150, 6000);
                    }

                    //pictureBoxRPLidarDataViewer.BackColor = Color.Black;//NOTE: picture box default olarak background ayarı yapıdı.
                    pictureBoxRPLidarDataViewer.Image = bmp;
                }*/
            }

            //Invalidate();//NOTE: Kaldırılsada veri güncelleniyor neden.

            //list box icin elde edilen count degerleri label uzerine yazdiriliyor.
            /*labelLidarData.Text = $"Lidar Data: {lidarDataCount}";
            labelLİdarDataStartBits.Text = $"Lidar Data Start Bits: {lidarStartBitCount}";
            labelLidarDataQuality.Text = $"Selected Data Quality List: {lidarDataQualityCount}";*/
        }

        private void serialPort1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (!checkResponseData(rpLidarScanIDResponse, rpLidarScanIDResponseData))
            {
                serialPort1.Read(rpLidarScanIDResponseData, 0, 7);
                serialPort1.DiscardInBuffer();//NOTE:!!!! Kaldırılması gerekebilir.
            }
            else if (checkResponseData(rpLidarScanIDResponse, rpLidarScanIDResponseData) && serialPortRPLidarDataIndex < rPLidarData.Length)
            {
                //Üzerinde çalışılıyor.
                if (serialPort1.BytesToRead < 5) return;

                int serialPortBytesToRead = serialPort1.BytesToRead;
                int bufferSize = (serialPortBytesToRead - (serialPortBytesToRead % 5));
                int reminderLidarDataBufferSize = (rPLidarData.Length - serialPortRPLidarDataIndex) * 5;

                if (bufferSize > reminderLidarDataBufferSize) bufferSize = reminderLidarDataBufferSize;
                byte[] buffer = new byte[bufferSize];

                serialPort1.Read(buffer, 0, buffer.Length);

                for (int bufferOffset = 0; bufferOffset < buffer.Length; bufferOffset += 5)
                {
                    Buffer.BlockCopy(buffer, bufferOffset, spLidarScanRawDataBuf, 0, 5);

                    rPLidarData[serialPortRPLidarDataIndex] = createRPlidarDataFromRawData(spLidarScanRawDataBuf);
                    //textBoxSeriPortDataReceive.Invoke(new showTextSerialPortData(writeToTextBox), rPLidarData[serialPortRPLidarDataIndex]);

                    if (rPLidarData[serialPortRPLidarDataIndex].StartFlagBit == true)
                    {
                        pictureBoxRPLidarDataViewer.Invalidate();
                    }
                    else
                    {
                        serialPortRPLidarDataIndex++;
                    }
                }
            }
        }

        private void serialPort1_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            MessageBox.Show($"Serial Port Error.\n{e.EventType}", "Error", MessageBoxButtons.OK);
        }

        private void buttonSeriPortCon_MouseClick(object sender, MouseEventArgs e)
        {
            if (serialPort1.IsOpen)
            {
                //Open File button enable hale getiriliyor.
                buttonOpenFile.Enabled = true;

                //Combo box enable hale getiriliyor.
                comboBoxSeriPortList.Enabled = true;

                try
                {
                    serialPort1.Close();
                    buttonSeriPortCon.Text = "Serial Port Connect";

                    labelSerialPortInfo.Text = "Serial Port Info:";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Serial Port Not Closed.\n{ex.Message}", "Error", MessageBoxButtons.OK);
                }
            }
            else
            {
                //Open File button enable hale getiriliyor.
                buttonOpenFile.Enabled = false;

                //Combo box disable hale getiriliyor.
                comboBoxSeriPortList.Enabled = false;

                //Serial Port Parameters setting.
                serialPort1.PortName = comboBoxSeriPortList.Text;

                //Open serial port
                try
                {
                    serialPort1.Open();
                    buttonSeriPortCon.Text = "Serial Port Discon.";

                    string[] serialPortİnfo = new string[5];
                    serialPortİnfo[0] = serialPort1.PortName;
                    serialPortİnfo[1] = serialPort1.BaudRate.ToString();
                    serialPortİnfo[2] = serialPort1.Parity.ToString();
                    serialPortİnfo[3] = serialPort1.StopBits.ToString();
                    serialPortİnfo[4] = serialPort1.DataBits.ToString();

                    labelSerialPortInfo.Text = "Serial Port Info:";

                    foreach (var info in serialPortİnfo)
                    {
                        labelSerialPortInfo.Text += "  " + info;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Serial Port Not Opened.\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void buttonRPLidarScanID_Click(object sender, EventArgs e)
        {
            Array.Clear(rpLidarScanIDResponseData, 0, rpLidarScanIDResponseData.Length);
            if (serialPort1.IsOpen) serialPort1.DiscardInBuffer();

            if (serialPort1.IsOpen) serialPort1.Write(new byte[] { 0xA5, 0x20 }, 0, 2);
        }

        private void buttonRPLidarStopID_Click(object sender, EventArgs e)
        {
            if (serialPort1.IsOpen) serialPort1.Write(new byte[] { 0xA5, 0x25 }, 0, 2);

            Array.Clear(rpLidarScanIDResponseData, 0, rpLidarScanIDResponseData.Length);
            if (serialPort1.IsOpen) serialPort1.DiscardInBuffer();
        }

        private void pictureBoxRPLidarDataViewer_Paint(object sender, PaintEventArgs e)
        {
            //NOTE: Süre ölçümü için eklendi.
            //watch.Stop();
            //Console.WriteLine("Bağlantı  süresi: {0}", watch.Elapsed.TotalSeconds + " saniye");

            Graphics pictureBoxGraphics = e.Graphics;

            //Test_Start_For_Zone
            Size zoneSize = new Size((int)Convert.ToUInt32(numericUpDownLidarZoneWidth.Value), (int)Convert.ToUInt32(numericUpDownLidarZoneHeight.Value));
            Size zoneSizeOffset = new Size((int)Convert.ToInt32(numericUpDownLidarZoneWidthOffset.Value), (int)Convert.ToInt32(numericUpDownLidarZoneHeightOffset.Value));
            zone = new LidarZone(LidarZone.ZoneDistanceTypes.Low, zoneSize, zoneSizeOffset);
            //Test_End_For_Zone

            //NOTE:'e.Graphics.DrawImage'ile çizdirilecek hale çerildi.
            if (rPLidarData != null)
            {
                pictureBoxGraphics = drawLidarData(rPLidarData, rPLidarData.Length, pictureBoxGraphics, pictureBoxRPLidarDataViewer, zone);
            }

            serialPortRPLidarDataIndex = 0;

            //Test_Start_For_Zone
            //pictureBoxGraphics = zone.drawLidarZone(pictureBoxGraphics);
            pictureBoxGraphics = zone.drawLidarZone(pictureBoxGraphics, calculateScaleRation(0.0f, 12000.0f, 0.0f, (float)pictureBoxRPLidarDataViewer.Width));
            //Test_End_For_Zone

            //NOTE: Süre ölçümü için eklendi.
            //watch.Restart();
        }



        //Dosyadan okunan lidar datası raw lidar datasına çeviriliyor.
        public List<byte[]> readRecieveLidarDataFromFile(string fileText)
        {
            Byte[] bytes = new Byte[4];//Genişletilebilir olmalı.
            string[] textArray;

            textArray = fileText.Split('\n');  //Satır satır split edildi.

            //Okunan hex dosyasının hex kısmı alınıyor.
            for (uint i = 0; i < textArray.Length; i++)
            {
                string arrayHeader = " - ";
                int startIndexNumOfString = textArray[i].IndexOf(arrayHeader);

                if (startIndexNumOfString != -1)
                {
                    textArray[i] = textArray[i].Remove(textArray[i].Length - 1, 1);   //Satır sonu \r karakterleri kaldırıldı.
                    textArray[i] = textArray[i].Substring((startIndexNumOfString + arrayHeader.Length));  //Satır içerisinde hex datasını bulunduğu bölüm alındı.

                    string[] tempArray = textArray[i].Split(' ');  //Starır içerisinde yer alan boşluk karakterleri kaldırıldı.
                    textArray[i] = textArray[i].Remove(0, textArray[i].Length); //textArray araması yeniden yapılabilmesi için her indeksi silinmiş oldu.

                    foreach (string tempArrayString in tempArray)
                    {
                        textArray[i] += tempArrayString;   //Birteştirilmiş hex kodu her text array indeksi için atanıyor.
                    }
                }
            }

            int hexDataIndex = 2;
            List<byte[]> tempLidarRawDatas = new List<byte[]>();

            //RPlidar datası veri yapısı içerisine eklendi.
            for (int index = 0; index < (textArray[hexDataIndex].Length / 10); index++)
            {
                int startIndex, indexLength;
                startIndex = (index * 10);
                indexLength = 2;
                byte[] LidarDataFrame = new byte[5];

                string tempQuality = "0x" + textArray[hexDataIndex].Substring(startIndex, indexLength);

                //Angle degerinin bulunduğu 2 baytlık hex kodu capraz alındı.
                string tempAngle_L = "0x" + textArray[hexDataIndex].Substring(startIndex + 4, indexLength);
                string tempAngle_H = "0x" + textArray[hexDataIndex].Substring(startIndex + 2, indexLength);

                //Distance degerinin bulunduğu 2 baytlık hex kodu capraz alındı.
                string tempDistance_L = "0x" + textArray[hexDataIndex].Substring(startIndex + 8, indexLength);
                string tempDistance_H = "0x" + textArray[hexDataIndex].Substring(startIndex + 6, indexLength);

                LidarDataFrame[0] = Convert.ToByte(tempQuality, 16);
                LidarDataFrame[1] = Convert.ToByte(tempAngle_H, 16);
                LidarDataFrame[2] = Convert.ToByte(tempAngle_L, 16);
                LidarDataFrame[3] = Convert.ToByte(tempDistance_H, 16);
                LidarDataFrame[4] = Convert.ToByte(tempDistance_L, 16);

                tempLidarRawDatas.Add(LidarDataFrame);
            }

            return tempLidarRawDatas;
        }

        //Lidar datası Raw datadan anlamlı hale getiriliyor.
        public RPLidarData createRPlidarDataFromRawData(Byte[] bytesOfRawLidarDataFrame)
        {
            RPLidarData tempRPLidarData = new RPLidarData();

            bool tempStartFlagBit = false;
            bool tempInvertedStartFlagBit = false;
            byte tempQuality = 0;
            bool tempCheckBit = false;
            double tempAngle = 0;
            double tempDistance = 0;

            //Temp Star Flag Bit, Inverted Start Flag Bit and Quality Value convert.
            tempStartFlagBit = Convert.ToBoolean(((byte)bytesOfRawLidarDataFrame[0]) & ((byte)0x01));
            tempInvertedStartFlagBit = Convert.ToBoolean(((byte)bytesOfRawLidarDataFrame[0] >> 1) & ((byte)0x01));
            tempQuality = (byte)(bytesOfRawLidarDataFrame[0] >> 2);

            //Temp Check Bit and Angle Value convert.
            tempCheckBit = Convert.ToBoolean(((byte)bytesOfRawLidarDataFrame[1]) & ((byte)0x01));
            tempAngle = (((ushort)(bytesOfRawLidarDataFrame[2] << 8)) + ((ushort)bytesOfRawLidarDataFrame[1])) >> 1;
            tempAngle = tempAngle / 64.0f;

            //Temp Distance convert.
            tempDistance = ((ushort)(bytesOfRawLidarDataFrame[4] << 8)) + ((ushort)bytesOfRawLidarDataFrame[3]);
            tempDistance = tempDistance / 4.0f;

            //Temp RPLidar Data type assignment.
            tempRPLidarData.StartFlagBit = tempStartFlagBit;
            tempRPLidarData.InversedStartFlagBit = tempInvertedStartFlagBit;
            tempRPLidarData.Quality = (uint)tempQuality;
            tempRPLidarData.CheckBit = tempCheckBit;
            tempRPLidarData.Angle = (float)tempAngle;
            tempRPLidarData.Distance = (float)tempDistance;

            return tempRPLidarData;
        }

        //SCAN ID responsu kontrol ediliyor.
        public bool checkResponseData(Byte[] firstBytes, Byte[] secondBytes)
        {
            return firstBytes.SequenceEqual(secondBytes);
        }

        //Lidar Data Kontrol ediliyor.Yazılacak!!!
        public bool checkLidarData()
        {
            bool checkFlag = false;



            return checkFlag;
        }

        //Kutupsal gösterimden koordinat dikdörtgensel gösterime çevirme fonksiyon tanımı.
        public (int x, int y) convertPolarToCartesian(float angle, float distance, int coordinateOriginX, int coordinateOriginY)
        {
            int x, y;
            double radian = Math.PI * angle / 180.0;

            if (angle >= 0 && angle <= 180)
            {
                x = coordinateOriginX + (int)(distance * Math.Sin(radian));
                y = coordinateOriginY - (int)(distance * Math.Cos(radian));
            }
            else
            {
                x = coordinateOriginX - (int)(distance * -Math.Sin(radian));
                y = coordinateOriginY - (int)(distance * Math.Cos(radian));
            }

            return (x, y);
        }
        public (int x, int y) convertPolarToCartesian(float angle, float distance)
        {
            //Convert lidar angle to polar angle
            float degree360 = 360;
            float degree270 = 270;
            float degree180 = 180;
            float degree90 = 90;
            float newAngle = 0;

            if(angle > 0 && angle <= 90)
            {
                newAngle = (degree90 - angle);  //1. Bölge
            }
            else if(angle > 90 && angle <= 180)
            {
                newAngle = (degree180 - angle) + degree270; //4. Bölge
            }
            else if(angle > 180 && angle <= 270)
            {
                newAngle = (degree270 - angle) + degree180; //3. Bölge
            }
            else if(angle > 270 && angle <= 360)
            {
                newAngle = (degree360 - angle) + degree90; //2. Bölge
            }

            int x, y;
            double radian = Math.PI * newAngle / 180.0;

            x = (int)(distance * Math.Cos(radian));
            y = (int)(distance * Math.Sin(radian));

            //NOTE:27112022 kullanılmayacak.
            //if (newAngle >= 0 && newAngle <= 180)
            //{
            //    x = (int)(distance * Math.Sin(radian));
            //    y = (-1) * (int)(distance * Math.Cos(radian));
            //}
            //else
            //{
            //    x = (-1) * (int)(distance * -Math.Sin(radian));
            //    y = (-1) * (int)(distance * Math.Cos(radian));
            //}

            return (x, y);
        }

        //uzunluk ölceklendirme fonksiyonu.
        public float scale(float value, float minValue, float maxValue, float scaledMin, float scaledMax)
        {
            float scale = (float)(scaledMax - scaledMin) / (float)(maxValue - minValue);
            float offset = minValue * scale - scaledMin;

            float scaledValue = (value * scale) - offset;
            return (scaledValue);
        }
        public int scale(int value, int minValue, int maxValue, int minScaledValued, int maxScaledValue)
        {
            int scaledValue;
            float scaleRation;

            scaleRation = (float)(maxScaledValue - minScaledValued) / (float)(maxValue - minValue);
            scaledValue = (int)(scaleRation * value);

            return scaledValue;
        }
        public float calculateScaleRation(float minValue, float maxValue, float minScaledValued, float maxScaledValue)
        {
            float scaleRation;

            scaleRation = (float)(maxScaledValue - minScaledValued) / (float)(maxValue - minValue);

            return scaleRation;
        }

        //Lidar Viewer Background çizdiriliyor.(createLidarViewerPanel -> drawLidarViwerBackground)
        public Bitmap createLidarViewerPanel(Bitmap bmp)
        {
            int centerX = bmp.Size.Width / 2;
            int centerY = bmp.Size.Height / 2;

            Bitmap viewerBitMap = new Bitmap(bmp.Size.Width, bmp.Size.Height);
            Pen p = new Pen(Brushes.DarkGreen, 1);
            Graphics viewerGraphic = Graphics.FromImage(viewerBitMap);

            //draw circle
            viewerGraphic.DrawEllipse(p, 0 + 1, 0 + 1, bmp.Size.Width - 3, bmp.Size.Width - 3);  //bigger circle
            viewerGraphic.DrawEllipse(p, 80, 80, bmp.Size.Width - 160, bmp.Size.Height - 160);    //smaller circle

            //draw perpendicular line
            viewerGraphic.DrawLine(p, new Point(centerX, 0), new Point(centerX, bmp.Size.Height)); // UP-DOWN
            viewerGraphic.DrawLine(p, new Point(0, centerY), new Point(bmp.Size.Width, centerY)); //LEFT-RIGHT
            int crossLO = 74;//Capraz cizilen cizgiler bitmap e göre dinamik değildir.Gerektigidurumda dinamik yapılacaktır.
            viewerGraphic.DrawLine(p, new Point(centerX, centerY), new Point(bmp.Size.Width - crossLO, 0 + crossLO)); //Right Up Cross Line
            viewerGraphic.DrawLine(p, new Point(centerX, centerY), new Point(bmp.Size.Width - crossLO, bmp.Size.Height - crossLO)); //Right Down Cross Line
            viewerGraphic.DrawLine(p, new Point(centerX, centerY), new Point(0 + crossLO, bmp.Size.Height - crossLO)); //Left Down Cross Line
            viewerGraphic.DrawLine(p, new Point(centerX, centerY), new Point(0 + crossLO, 0 + crossLO)); //Left UP Cross Line

            return (viewerBitMap);
        }
        public Graphics drawLidarViwerBackground(Graphics graphicsObject, PictureBox pictureBoxObject)
        {
            int pictureBoxSizeWidth = pictureBoxObject.Size.Width;
            int pictureBoxSizeHeight = pictureBoxObject.Size.Height;

            int centerX = pictureBoxSizeWidth / 2;
            int centerY = pictureBoxSizeHeight / 2;

            Pen p = new Pen(Brushes.DarkGreen, 1);

            graphicsObject.TranslateTransform(centerX, centerY);

            //Draw Ellipse
            p.DashPattern = new float[3] { 1, 5, 5 };
            //p.DashStyle = System.Drawing.Drawing2D.DashStyle.DashDot;

            uint ellipseCount = 6;
            uint ellipseMaxSize = (uint)((pictureBoxSizeWidth < pictureBoxSizeHeight) ? pictureBoxSizeWidth : pictureBoxSizeHeight);

            for (uint i = 1; i < ellipseCount + 1; i++)
            {
                uint ellipseSize = (ellipseMaxSize / ellipseCount) * i;
                int ellipseCenter = (int)(ellipseSize / 2) * (-1);

                graphicsObject.DrawEllipse(p, ellipseCenter, ellipseCenter, ellipseSize, ellipseSize);
            }

            //Draw Perpendicular Line
            //p.DashPattern = new float[3] { 1, 3, 3 };
            p.DashStyle = System.Drawing.Drawing2D.DashStyle.Dot;

            int rotationAngle = 30;
            Font drawStringFont = new Font(new FontFamily("Arial"), 10);

            for (int i = 0; i < (360 / rotationAngle); i += 1)
            {
                graphicsObject.DrawLine(p, new Point(0, 0), new Point(0, -centerY));
                graphicsObject.DrawString($"{rotationAngle * i}", drawStringFont, Brushes.Green, new Point(0, -centerY));
                graphicsObject.RotateTransform(rotationAngle);
            }

            return (graphicsObject);
        }


        //Lİdar datası görselleştiriliyor.(createLidarDataGraph -> drawLidarData)
        Bitmap createLidarDataGraph(Pen pen, float angleDataOfLidar, float distaceInAngleDataOflidar_mm, Bitmap bitmap, uint minLidarDistance_mm, uint maxLidarDistance_mm)
        {
            int x, y;
            int cx, cy;

            cx = bitmap.Size.Width / 2;
            cy = bitmap.Size.Height / 2;

            float scaledLidarDistance_mm = scale(distaceInAngleDataOflidar_mm, minLidarDistance_mm, maxLidarDistance_mm, 0, cx);

            var coordinate = convertPolarToCartesian(angleDataOfLidar, scaledLidarDistance_mm, cx, cy);
            x = coordinate.x;
            y = coordinate.y;

            Graphics lidarDataGraph = Graphics.FromImage(bitmap);
            lidarDataGraph.DrawLine(pen, new Point(x - 2, y), new Point(x + 2, y));
            return (bitmap);
        }
        Graphics drawLidarDataSample(Graphics graphicsObject, PictureBox pictureBoxObject, Pen pen, float angleDataOfLidar, float distaceInAngleDataOflidar_mm, uint minLidarDistance_mm, uint maxLidarDistance_mm)
        {
            int pictureBoxSizeWidth = pictureBoxObject.Size.Width;
            int pictureBoxSizeHeight = pictureBoxObject.Size.Height;

            int centerX = pictureBoxSizeWidth / 2;
            int centerY = pictureBoxSizeHeight / 2;

            int lidarDataSample_X, lidarDataSample_Y;

            float scaledLidarDistance_mm = scale(distaceInAngleDataOflidar_mm, minLidarDistance_mm, maxLidarDistance_mm, 0, centerX);

            var coordinate = convertPolarToCartesian(angleDataOfLidar, scaledLidarDistance_mm);
            lidarDataSample_X = coordinate.x;
            lidarDataSample_Y = coordinate.y;

            graphicsObject.DrawLine(pen, new Point(lidarDataSample_X - 2, lidarDataSample_Y), new Point(lidarDataSample_X + 2, lidarDataSample_Y));
            return (graphicsObject);
        }
        Graphics drawLidarDataSample(Graphics graphicsObject, PictureBox pictureBoxObject, Pen pen, float angleDataOfLidar, float distaceInAngleDataOflidar_mm, uint minLidarDistance_mm, uint maxLidarDistance_mm, LidarZone lidarZone)
        {
            int pictureBoxSizeWidth = pictureBoxObject.Size.Width;
            int pictureBoxSizeHeight = pictureBoxObject.Size.Height;

            int centerX = pictureBoxSizeWidth / 2;
            int centerY = pictureBoxSizeHeight / 2;

            //Lidar sample in zone check.
            var coordinate = convertPolarToCartesian(angleDataOfLidar, distaceInAngleDataOflidar_mm);
            if (lidarZone.checkInTheZone((float)coordinate.x, (float)(coordinate.y)) == true)
            {
                pen.Brush = Brushes.Yellow;
            }
            else
            {
                pen.Brush = Brushes.Purple;
            }

            //lidar sample scale.
            int scaledLidarDataSample_X, scaledLidarDataSample_Y;
            //NOTE:(27.11.2022) scale işleminde x = 0 ve y = 0 değerleri için -11 gibi bir değer üretiyordu hatalı olduğu için kapatıldı.
            //float scaledLidarDistance_mm = scale(distaceInAngleDataOflidar_mm, minLidarDistance_mm, maxLidarDistance_mm, 0, centerX);
            float scaledLidarDistance_mm = distaceInAngleDataOflidar_mm * calculateScaleRation(minLidarDistance_mm, maxLidarDistance_mm, 0, centerX);
            var scaledCoordinate = convertPolarToCartesian(angleDataOfLidar, scaledLidarDistance_mm);
            scaledLidarDataSample_X = scaledCoordinate.x;
            scaledLidarDataSample_Y = (-1) * scaledCoordinate.y;//NOTE: Converting to graphic y coordinate.(27.11.2022

            //graphicsObject.DrawLine(pen, new Point(scaledLidarDataSample_X - 2, scaledLidarDataSample_Y), new Point(scaledLidarDataSample_X + 2, scaledLidarDataSample_Y));
            graphicsObject.FillEllipse(pen.Brush,scaledLidarDataSample_X - 1, scaledLidarDataSample_Y - 1, 3, 3);

            return (graphicsObject);
        }


        //Lidar datası bitmap ile birleştiriliyor.(drawLidarScreen -> drawLidarData)
        public Bitmap drawLidarScreen(RPLidarData[] data, int dataCount, Bitmap bmp)
        {
            //Lidar viewer backgroung create
            bmp = createLidarViewerPanel(bmp);

            //Lidar data draw
            for (int i = 0; i < dataCount; i++)
            {
                bmp = createLidarDataGraph(new Pen(Brushes.Purple, 3), data[i].Angle, data[i].Distance, bmp, 150, 6000);
            }

            return bmp;
        }
        public Graphics drawLidarData(RPLidarData[] lidarData, int dataCount, Graphics graphicsObject, PictureBox pictureBoxObject)
        {
            //Lidar viewer backgroung create
            graphicsObject = drawLidarViwerBackground(graphicsObject, pictureBoxObject);

            //Lidar data draw
            for (int i = 0; i < lidarData.Length; i++)
            {
                graphicsObject = drawLidarDataSample(graphicsObject, pictureBoxObject, new Pen(Brushes.Purple, 3), lidarData[i].Angle, lidarData[i].Distance, 150, 6000);
            }

            return (graphicsObject);
        }
        public Graphics drawLidarData(RPLidarData[] lidarData, int dataCount, Graphics graphicsObject, PictureBox pictureBoxObject, LidarZone lidarzone)
        {
            //Lidar viewer backgroung create
            graphicsObject = drawLidarViwerBackground(graphicsObject, pictureBoxObject);

            //Lidar data draw
            Pen lidarSampleDrawPen = new Pen(Brushes.Purple, 3);
            for (int i = 0; i < lidarData.Length; i++)
            {
                graphicsObject = drawLidarDataSample(graphicsObject, pictureBoxObject, lidarSampleDrawPen, lidarData[i].Angle, lidarData[i].Distance, 150, 6000, lidarzone);
            }

            return (graphicsObject);
        }


        //Lidar Zone viewer.
        public Bitmap createLidarDetectZoneViewer(Bitmap lidarPanelBitmap, float zoneWidth, float zoneWidthOffset, float zoneHeight, float zoneHeightOffset)
        {
            Pen p = new Pen(Brushes.Blue, 2);
            Graphics zoneViewerGraphic = Graphics.FromImage(lidarPanelBitmap);

            int bmpCenterX = lidarPanelBitmap.Size.Width / 2;
            int bmpCenterY = lidarPanelBitmap.Size.Height / 2;

            float newZoneWidthOffset;
            float newZoneHeightOffset;

            //Width değerleri için ölçekleme yapılıyor.
            zoneWidth = scale(zoneWidth, (float)0, (float)12000, (float)0, (float)lidarPanelBitmap.Size.Width);
            newZoneWidthOffset = scale(Math.Abs(zoneWidthOffset), (float)0, (float)6000, (float)0, (float)lidarPanelBitmap.Size.Width / 2);
            newZoneWidthOffset = (zoneWidthOffset < 0) ? (newZoneWidthOffset * (-1)) : newZoneWidthOffset;

            //Height değerleri için ölçekleme yapılıyor.
            zoneHeight = scale(zoneHeight, (float)0, (float)12000, (float)0, (float)lidarPanelBitmap.Size.Height);
            newZoneHeightOffset = scale(Math.Abs(zoneHeightOffset), (float)0, (float)6000, (float)0, (float)lidarPanelBitmap.Size.Height / 2);
            newZoneHeightOffset = (zoneHeightOffset < 0) ? (newZoneHeightOffset * (-1)) : newZoneHeightOffset;

            //Ölceklenmiş değerler için, 'X(Width)' için sağ yani '+' , 'Y(Height)' için aşağı yani '-' işlemleri uygulanarak zone enter hesaplanıyor.
            float ZoneCenterX = (zoneWidth / 2) - (newZoneWidthOffset);
            float ZoneCenterY = (zoneHeight / 2) + (newZoneHeightOffset);

            zoneViewerGraphic.DrawRectangle(p, (bmpCenterX - ZoneCenterX), (bmpCenterY - ZoneCenterY), zoneWidth, zoneHeight);
            zoneViewerGraphic.FillEllipse(Brushes.Blue, ((bmpCenterX - ZoneCenterX) + zoneWidth / 2) - 3, ((bmpCenterY - ZoneCenterY) + zoneHeight / 2) - 3, 6, 6);
            return (lidarPanelBitmap);
        }
        //Lidar verisi zone alanındamı kontrolü.
        public bool checkTheLidarDetectionZone(float lidarAngle, float lidarDistance, float lidarZoneMinX, float lidarZoneMaxX, float lidarZoneMinY, float lidarZoneMaxY)
        {
            bool detectionZone = false;

            var lidarSampleCoordinate = convertPolarToCartesian(lidarAngle, lidarDistance);

            if ((lidarSampleCoordinate.x >= lidarZoneMinX && lidarSampleCoordinate.x <= lidarZoneMaxX) && (lidarSampleCoordinate.y >= lidarZoneMinY && lidarSampleCoordinate.y <= lidarZoneMaxY))
            {
                detectionZone = true;
            }
            else
            {
                detectionZone = false;
            }

            return (detectionZone);
        }

        //Tread cakısması hatasının giderilmesi icin kullanılmıstır.
        public delegate void showTextSerialPortData(RPLidarData data);
        public void writeToTextBox(RPLidarData data)
        {
            //NOTE: veri yapısı reel değerlerine çevirildiği için buradaki veri çevrimine ihtiyaç kalmamıştır.
            uint tempQuality = (uint)data.Quality;
            float tempAngle = (float)((double)data.Angle / 64.0);
            float tempDistance = (float)((double)data.Distance / 4.0);

            textBoxSeriPortDataReceive.Text += "Quality:" + tempQuality + "\t";
            textBoxSeriPortDataReceive.Text += "Angle:" + tempAngle + "\t";
            textBoxSeriPortDataReceive.Text += "Distance:" + tempDistance + "\t";
            textBoxSeriPortDataReceive.Text += "\r\n";
        }


        //Zone Class Definition
        public class LidarZone
        {
            public enum ZoneDistanceTypes
            {
                Low,
                Mid,
            }

            private ZoneDistanceTypes distanceType;
            private Size size;
            private Size offsetSize;
            private Point positionOfStarting;
            private Point positionOfStartingForGraphic;
            private Point positionOfCornerMin;
            private Point positionOfCornerMax;
            private bool detectOfSample;
            private uint detectedSampleCount;

            public ZoneDistanceTypes DistanceType
            {
                get{return this.distanceType;}
                set{this.distanceType = value;}
            }
            public Size Size
            {
                get{return this.size;}
                set
                {
                    if((value.Width >= 0) && (value.Height >= 0))
                    {
                        this.size.Width = value.Width;
                        this.size.Height = value.Height;
                    }
                    else
                    {
                        this.size.Width = 0;
                        this.size.Height = 0;
                    }
                }
            }
            public Size OffsetSize
            {
                get{return this.offsetSize;}
                set
                {
                    this.offsetSize.Width = value.Width;
                    this.offsetSize.Height = value.Height;
                }
            }
            public Point PositionOfStarting
            {
                get{return this.positionOfStarting;}
            }
            public Point PositionOfStartingForGraphic
            {
                get{return this.positionOfStartingForGraphic; }
            }
            public Point PositionOfCornerMin
            {
                get{return this.positionOfCornerMin;}
            }
            public Point PositionOfCornerMax
            {
                get{return this.positionOfCornerMax;}
            }
            public bool DetectOfSample
            {
                get { return this.detectOfSample; }
                set { this.detectOfSample = value; }
            }
            public uint DetectedSampleCount
            {
                get { return this.detectedSampleCount; }
                set { this.detectedSampleCount = value; }
            }


            public LidarZone()
            {
                this.DistanceType = ZoneDistanceTypes.Mid;
                this.Size = new Size(0, 0);
                this.OffsetSize = new Size(0, 0);
                this.positionOfStarting = calculateZoneStartPosition(this.Size, this.OffsetSize);
                this.positionOfStartingForGraphic = calculateZoneStartPositionForGraphic(this.Size, this.OffsetSize);
                var corner = calculateTheMaxAndMinCornerCoordinatesOfTheZone(this.Size, this.positionOfStarting);//NOTE: Bu fonksiyon değer döndüren hale getirilecek döndürdüğü yapı tüm pozisyonları içeren bir yapı şekilnde olabilir.Bakılacak!! Şuan corner max min şeklinde iki ayrı Point nesnesi.
                this.positionOfCornerMin = corner.Min;
                this.positionOfCornerMax = corner.Max;
                this.DetectOfSample = false;
                this.DetectedSampleCount = 0;
            }
            public LidarZone(ZoneDistanceTypes zoneDistanceType, Size zoneSize, Size zoneOffsetSize)
            {
                this.DistanceType = zoneDistanceType;
                this.Size = new Size(zoneSize.Width, zoneSize.Height);
                this.OffsetSize = new Size(zoneOffsetSize.Width, zoneOffsetSize.Height);
                this.positionOfStarting = calculateZoneStartPosition(this.Size, this.OffsetSize);
                this.positionOfStartingForGraphic = calculateZoneStartPositionForGraphic(this.Size, this.OffsetSize);
                var corner = calculateTheMaxAndMinCornerCoordinatesOfTheZone(this.Size, this.positionOfStarting);//NOTE: Bu fonksiyon değer döndüren hale getirilecek döndürdüğü yapı tüm pozisyonları içeren bir yapı şekilnde olabilir.Bakılacak!! Şuan corner max min şeklinde iki ayrı Point nesnesi.
                this.positionOfCornerMin = corner.Min;
                this.positionOfCornerMax = corner.Max;
                this.DetectOfSample = false;
                this.DetectedSampleCount = 0;
            }

            Point calculateZoneStartPosition(Size zoneSize, Size zoneOffsetSize)
            {
                Point zoneStartPosition = new Point();

                zoneStartPosition.X = (-1) * (zoneSize.Width / 2);
                zoneStartPosition.Y = (zoneSize.Height / 2);

                zoneStartPosition.X = zoneStartPosition.X + zoneOffsetSize.Width;
                zoneStartPosition.Y = zoneStartPosition.Y + zoneOffsetSize.Height;

                return zoneStartPosition;
            }
            Point calculateZoneStartPositionForGraphic(Size zoneSize, Size zoneOffsetSize)
            {
                Point zoneStartPositionForGraphic = new Point();

                int rectangleCenterX = (zoneSize.Width / 2);
                int rectangleCenterY = (zoneSize.Height / 2);

                int lidarOriginOffsetOfRectangleCenterX = ((-1) * rectangleCenterX);
                int lidarOriginOffsetOfRectangleCenterY = ((-1) * rectangleCenterY);

                zoneStartPositionForGraphic.X = lidarOriginOffsetOfRectangleCenterX + zoneOffsetSize.Width;
                zoneStartPositionForGraphic.Y = lidarOriginOffsetOfRectangleCenterY + (zoneOffsetSize.Height * (-1));  //'Y (Height)' axis was reversed for drawing are with '* (-1)'.

                return zoneStartPositionForGraphic;
            }
            (Point Min, Point Max) calculateTheMaxAndMinCornerCoordinatesOfTheZone(Size zoneSize, Point zoneStartPosition)
            {
                Point cornerMin = new Point();
                Point cornerMax = new Point();

                cornerMin.X = zoneStartPosition.X;
                cornerMin.Y = zoneStartPosition.Y - zoneSize.Height;
                cornerMax.X = zoneStartPosition.X + zoneSize.Width;
                cornerMax.Y = zoneStartPosition.Y;

                return (cornerMin, cornerMax);
            }

            public Graphics drawLidarZone(Graphics graphicsObject)
            {
                Pen zoneDrawPen = new Pen(Brushes.White, 2);

                if (this.DistanceType == ZoneDistanceTypes.Low) zoneDrawPen.Brush = Brushes.Red;
                if (this.DistanceType == ZoneDistanceTypes.Mid) zoneDrawPen.Brush = Brushes.Yellow;

                Rectangle zoneRectangle = new Rectangle(this.PositionOfStartingForGraphic, this.Size);
                graphicsObject.DrawRectangle(zoneDrawPen, zoneRectangle);

                return graphicsObject;
            }
            public Graphics drawLidarZone(Graphics graphicsObject, float scaleRation)
            {
                Pen zoneDrawPen = new Pen(Brushes.White, 2);

                if (this.DistanceType == ZoneDistanceTypes.Low) zoneDrawPen.Brush = Brushes.Red;
                if (this.DistanceType == ZoneDistanceTypes.Mid) zoneDrawPen.Brush = Brushes.Yellow;

                Point scaledStartPosition = new Point();
                Size scaledSize = new Size();

                scaledStartPosition.X = (int)((float)this.PositionOfStartingForGraphic.X * scaleRation);
                scaledStartPosition.Y = (int)((float)this.PositionOfStartingForGraphic.Y * scaleRation);
                scaledSize.Width = (int)((float)this.Size.Width * scaleRation);
                scaledSize.Height = (int)((float)this.Size.Height * scaleRation);

                Rectangle zoneRectangle = new Rectangle(scaledStartPosition, scaledSize);
                graphicsObject.DrawRectangle(zoneDrawPen, zoneRectangle);

                return graphicsObject;
            }
            public bool checkInTheZone(float coordinateX, float coordinateY)
            {
                bool detectedOfSample = false;

                if((coordinateX >= this.positionOfCornerMin.X) && (coordinateY >= this.positionOfCornerMin.Y) && (coordinateX <= this.positionOfCornerMax.X) && (coordinateY <= this.positionOfCornerMax.Y))
                {
                    detectedOfSample = true;
                }
                else
                {
                    detectedOfSample = false;
                }

                return detectedOfSample;
            }
        }
    }
}
/*NOTE: 21/11/2022 yapılacak notu!
 * - Fonksiyonlar için kullanılan parametre tiplerini tekrar düzenle.
 * - RPLidar aray tipini Arraylist veya list yapısına çevir, bilinmeyen boyut için rahat işlem yapabilesin.
 * - 'createLidarViewerPanel();' fonksiyonu'drawLidarScreen();' içerisinde iken sadece lidar adtası çizilmiyor incele.
 * - Kapatılan list box yazdırmalarını ayrı ayrı buton altına alabilirsin.(önemsiz.)
 * 
 * - Görselleştirme alanı için zom in/zom out yapılabilmesi için çizme fonksiyonunen scale edilmesi için
 * geçilen max lidar mesafe değeri değiştirilerek yapılabilir. Beraberinde çizdirilen çizdirilen elipsler bu değerle ölçekli 1metre
 * gösterecek şekilde ölçeklendirilebilir.
 * 
 * - (27.11.2022 Yapıldı!!) Görselleştirmede kullanılan line çizme işlemini point olarak değiştir.
 * - (27.11.20.22 Düzeltildi!!)Uygulama ilk açıldığında bir nokta görselleştiriliyor nedenini bul çöz.
 */
/*NOTE: 27.11.2022 Yapılanlar.
 * - Lidar zone çizme işlemi ve zone algılama renklendirme işlemi düzeltildi ve çalışır durumda.
 * - lidar zone start pozisyonu ile graphics için start pozisyon hesaplaması ayrı tutuldu.
 * - Lidar 'convertPolarToRectangular()' fonksiyonu bilinen şekilde düzeltildi. Ve ismi 'convertPolarToCartesian()' olarak değiştirildi.
 * - Lidar datalarının scale edildiği fonksiyon (0,0) değerleri için merkezden uzak değer ürettiği için app ilk açıldığında merkezden uzak nokta gözüküyordu.Düzeltildi!
 * - 'scaledCoordinate.y' değeri graphic koordinatına uyması için (-1) değeri ile çarpıldı.(NOTE: bir fonksiyon yazılarak bu çevirim yapılabilir.)
 * - Lidar götselleştirme çizgiden daire çizme olarak değiştirildi.
 * - 'Scan' ve 'Stop' buton click eventları içerisinde yer alan 'serialInBufferDiscard() ' fonksiyonu seriportun açık olduğu koşuluna bağlandı.
 */