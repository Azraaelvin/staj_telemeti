using GMap.NET;
using System;
using System.Drawing.Text;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace odev
{
    public partial class Form1 : Form
    {
        private string[] telemetryPacket;
        private int rowindex = 0;
        private float anlikRoll = 0;
        private float anlikPitch = 0;
        private float anlikYaw = 0;
        private long beklenenPaketNo = -1;
        private int toplamBaþariliPaket = 0;
        private int toplamAtlananPaket = 0;
        private double[] zScorelar = new double[15];
        private Queue<double>[] sensorGecmisleri = new Queue<double>[8];
        private Queue<double> batteryGecmisi = new Queue<double>();

        public Form1()
        {
            InitializeComponent();
            for (int i = 0; i < 8; i++)
            {
                sensorGecmisleri[i] = new Queue<double>();
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer1.Interval = 1000;
            GMap.NET.GMaps.Instance.Mode = GMap.NET.AccessMode.ServerOnly;
            gMapControl1.MapProvider = GMap.NET.MapProviders.GoogleMapProvider.Instance;
            gMapControl1.MinZoom = 2;
            gMapControl1.MaxZoom = 20;
            gMapControl1.Zoom = 15;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string dosyaAdi = "C:\\Users\\azrag\\OneDrive\\Desktop\\odev\\telemetry_clean.csv";
            try
            {
                yenidenBaslat();

                button1.Enabled = false;
                button2.Enabled = true;

                telemetryPacket = File.ReadAllLines(dosyaAdi);
                timer1.Start();
            }
            catch
            {
                MessageBox.Show("Normal Dosya Okunurken Sorun Oluþtu!");
            }

        }

        private void button2_Click(object sender, EventArgs e)
        {
            string sifreliDosyaAdi = "C:\\Users\\azrag\\OneDrive\\Desktop\\odev\\telemetry_encrypted.csv";
            try
            {
                yenidenBaslat();

                button2.Enabled = false;
                button1.Enabled = true;
               
                string[] sifreliVeri = File.ReadAllLines(sifreliDosyaAdi);
                telemetryPacket = new string[sifreliVeri.Length];
                for (int i = 0; i < sifreliVeri.Length; i++)
                {
                    string temizSatir = sifreliVeri[i].Trim();
                    if (!string.IsNullOrEmpty(temizSatir))
                    {
                        telemetryPacket[i] = sifreyiCoz(temizSatir).Trim();
                    }
                    else telemetryPacket[i] = string.Empty;
                }

                timer1.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Þifreli Dosya Çözülemedi" + ex.Message);
            }
        }

        private void yenidenBaslat()
        {
            timer1.Stop();
            rowindex = 0;
            beklenenPaketNo = -1;
            toplamAtlananPaket = 0;
            telemetryPacket = null;

            anlikPitch = 0;
            anlikRoll = 0;
            anlikYaw = 0;

            listBox1.Items.Clear();
            listBox2.Items.Clear();

            batteryGecmisi.Clear();
            for (int i = 0; i < 8; i++) 
            {
                sensorGecmisleri[i].Clear();
                string grafikIsmi = "sensor" + i + "_ch";
                var dinamikgrafik = this.Controls.Find(grafikIsmi,true).FirstOrDefault() as System.Windows.Forms.DataVisualization.Charting.Chart;

                if (dinamikgrafik != null) 
                {
                    string sensorIsmi = "sensor" + i;

                    var hedefsensor=dinamikgrafik.Series.FindByName(sensorIsmi);

                    if (hedefsensor != null) 
                    {
                        dinamikgrafik.Series[sensorIsmi].Points.Clear();
                    }
                }
            }

            if (batarya_ch != null) 
            {
                batarya_ch.Series["battery"].Points.Clear();
            }


        }

        private string sifreyiCoz(string temizSatir)
        {
            string keyývyolu = "C:\\Users\\azrag\\OneDrive\\Desktop\\odev\\keyýv.csv";
            string keyýv = File.ReadAllText(keyývyolu).Trim();
            string[] parcala = keyýv.Split(',');
            string keyHex = parcala[0].Trim();
            string ivHex = parcala[1].Trim();

            byte[] sifreliveriBytes = Convert.FromBase64String(temizSatir);
            byte[] keyBytes = Convert.FromHexString(keyHex);
            byte[] ivBytes = Convert.FromHexString(ivHex);
            /*byte[] keyBytes = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
            byte[] IvBytes = new byte[] { 0x0F, 0x0E, 0x0D, 0x0C, 0x0B, 0x0A, 0x09, 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01, 0x00 };*/

            using Aes aes = Aes.Create();
            aes.KeySize = 128;
            aes.Key = keyBytes;
            aes.IV = ivBytes;
            aes.Mode = System.Security.Cryptography.CipherMode.CBC;
            aes.Padding = System.Security.Cryptography.PaddingMode.PKCS7;


            using ICryptoTransform sifrecozucu = aes.CreateDecryptor(keyBytes, ivBytes);
            using MemoryStream ms = new MemoryStream(sifreliveriBytes);
            using CryptoStream cs = new CryptoStream(ms, sifrecozucu, CryptoStreamMode.Read);
            using StreamReader sr = new StreamReader(cs, System.Text.Encoding.UTF8);

            return sr.ReadToEnd();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (telemetryPacket == null || telemetryPacket.Length == null)
            {
                label2.Text = "Hayýr";
                return;
            }
            if (rowindex >= telemetryPacket.Length)
            {
                rowindex = 0;
            }
            string Row = telemetryPacket[rowindex];

            if (string.IsNullOrEmpty(Row))
            {
                listBox1.Items.Insert(0,$"[{DateTime.Now:HH:mm:ss} Satýr {rowindex} : BOÞ VERÝ - Satýr içeriði Boþ geldi.]");
                toplamAtlananPaket++;
                gostergeleriguncelle();
                return;
            }

            rowindex++;
            string[] data = Row.Split(',');

            if (data.Length < 15 || data.Length > 16)
            {
                listBox1.Items.Insert(0,$"[{DateTime.Now:HH:mm:ss}] Satýr {rowindex} : Beklenen 15 veri yerine {data.Length} veri geldi! ");
            }

            if (long.TryParse(data[0], out long gelenpaketno))
            {
                if (beklenenPaketNo == -1) beklenenPaketNo = gelenpaketno;

                if (gelenpaketno > beklenenPaketNo)
                {
                    for (long i = beklenenPaketNo; i < gelenpaketno; i += 50)
                    {
                        listBox1.Items.Insert(0,$"[{DateTime.Now:private long beklenenPaketNo = -1;HH:mm:ss}] Paket {i} : ATLANDI ");
                    }
                    toplamAtlananPaket++;
                    gostergeleriguncelle();
                }
                beklenenPaketNo = gelenpaketno + 50;

                toplamBaþariliPaket++;
                gostergeleriguncelle();
            }


            try
            {
                var kultur = System.Globalization.CultureInfo.InvariantCulture;
                long paketNo = long.Parse(data[0], kultur);
                float roll = float.Parse(data[1], kultur);
                float pitch = float.Parse(data[2], kultur);
                float yaw = float.Parse(data[3], kultur);
                float sensor0 = float.Parse(data[4], kultur);
                float sensor1 = float.Parse(data[5], kultur);
                float sensor2 = float.Parse(data[6], kultur);
                float sensor3 = float.Parse(data[7], kultur);
                float sensor4 = float.Parse(data[8], kultur);
                float sensor5 = float.Parse(data[9], kultur);
                float sensor6 = float.Parse(data[10], kultur);
                float sensor7 = float.Parse(data[11], kultur);
                float battery = float.Parse(data[12], kultur);
                double gps_lat = float.Parse(data[13], kultur);
                double gps_lon = float.Parse(data[14], kultur);

                float[] sensorValues = { sensor0, sensor1, sensor2, sensor3, sensor4, sensor5, sensor6, sensor7 };
                ZScoreHesapla(sensorValues, roll, pitch, yaw, battery, gps_lat, gps_lon, paketNo);
                gpshesapla(gps_lat, gps_lon);

                sensor0_ch.Series["sensor0"].Points.AddXY(paketNo, sensor0);
                sensor1_ch.Series["sensor1"].Points.AddXY(paketNo, sensor1);
                sensor2_ch.Series["sensor2"].Points.AddXY(paketNo, sensor2);
                sensor3_ch.Series["sensor3"].Points.AddXY(paketNo, sensor3);
                sensor4_ch.Series["sensor4"].Points.AddXY(paketNo, sensor4);
                sensor5_ch.Series["sensor5"].Points.AddXY(paketNo, sensor5);
                sensor6_ch.Series["sensor6"].Points.AddXY(paketNo, sensor6);
                sensor7_ch.Series["sensor7"].Points.AddXY(paketNo, sensor7);
                batarya_ch.Series["battery"].Points.AddXY(paketNo, battery);

                for (int i = 0; i < 8; i++)
                { 
                    string chartName = "sensor" + i + "_ch";
                    var dynamicChart = this.Controls.Find(chartName, true).FirstOrDefault() as System.Windows.Forms.DataVisualization.Charting.Chart;

                    if (dynamicChart != null)
                    {
                        string serieName = "sensor" + i;

                        dynamicChart.Series[serieName].Points.AddXY(paketNo, sensorValues[i]);

                        if (dynamicChart.Series[serieName].Points.Count > 20)
                        {
                            dynamicChart.Series[serieName].Points.RemoveAt(0);
                        }
                    }
                }
                anlikPitch = pitch;
                anlikRoll = roll;
                anlikYaw = yaw;

                panel1.Invalidate();
            }

            catch(Exception ex)
            {
                MessageBox.Show("Verilerde sýkýntý var dikkat et"+ex.Message);
            }

        }

        private void gpshesapla(double gps_lat, double gps_lon)
        {
            PointLatLng konum = new PointLatLng(gps_lat, gps_lon);
            gMapControl1.Position = konum;
        }

        private void ZScoreHesapla(float[] sensorValues, float roll, float pitch, float yaw, float battery, double gps_lat, double gps_lon, long paketNo)
        {
            for (int i = 0; i < 8; i++)
            {
                sensorGecmisleri[i].Enqueue(sensorValues[i]);
                if (sensorGecmisleri[i].Count > 5) sensorGecmisleri[i].Dequeue();

                double zScore = 0;
                if (sensorGecmisleri[i].Count >= 2)
                {
                    double ortalama = sensorGecmisleri[i].Average();
                    double farkal = sensorGecmisleri[i].Sum(val => Math.Pow(val - ortalama, 2));
                    double standartsapma = Math.Sqrt(farkal / sensorGecmisleri[i].Count);

                    if (standartsapma > 0)
                    {
                        zScore = (sensorValues[i] - ortalama) / standartsapma;
                    }
                }
                zScorelar[i] = zScore;
            }

            batteryGecmisi.Enqueue(battery);
            if (batteryGecmisi.Count > 5)
            {
                batteryGecmisi.Dequeue();
            }

            double batteryZScore = 0;
            if (batteryGecmisi.Count >= 2)
            {
                double bMean = batteryGecmisi.Average();
                double bSumOfSquares = batteryGecmisi.Sum(val => Math.Pow(val - bMean, 2));
                double bStandardDeviation = Math.Sqrt(bSumOfSquares / batteryGecmisi.Count);

                if (bStandardDeviation > 0)
                {
                    batteryZScore = (battery - bMean) / bStandardDeviation;
                }
            }
            zScorelar[8] = batteryZScore;
            listBox2.Items.Insert(0,$"--- Voltaj/Sensör Analizi [{paketNo}] ---");
            for (int i = 0; i < zScorelar.Length; i++)
            {
                if (i < 8)
                    if (zScorelar[i] >3 || zScorelar[i] < -3)
                    listBox2.Items.Insert(0,$"Dizi[{i}] (Sensor{i} Z-Score): {zScorelar[i]:F4} ANORMAL DAVRANIÞ");
                    else listBox2.Items.Insert(0,$"Dizi[{i}] (Sensor{i} Z-Score): {zScorelar[i]:F4}");

                else if (i == 8)
                        if(zScorelar[i] > 3 || zScorelar[i] < -3) listBox2.Items.Add($"Dizi[{i}] (BATARYA Z-Score): {zScorelar[i]:F4} ANORMAL DAVRANIÞ");
                        else listBox2.Items.Insert(0,$"Dizi[{i}] (BATARYA Z-Score): {zScorelar[i]:F4}");
            }
        }

        private void gostergeleriguncelle()
        {
            int toplamPaket = toplamBaþariliPaket + toplamAtlananPaket;

            if (toplamPaket == 0) return;

            double kayipOrani = ((double)toplamAtlananPaket / toplamPaket) * 100;
            double saðliklioran = ((double)toplamBaþariliPaket / toplamPaket) * 100;

            kayiporanilb.Text = $"%{kayipOrani:F2}";
            verisaglýgýlb.Text = $"%{saðliklioran:F2}";

        }


        public struct Nokta3D
        {
            public double X, Y, Z;
            public Nokta3D(double x, double y, double z) { X = x; Y = y; Z = z; }
        }


        private PointF UcBoyutluDondur(Nokta3D p, double roll, double pitch, double yaw)
        {
            double r = roll * Math.PI / 180.0;
            double pt = pitch * Math.PI / 180.0;
            double y = yaw * Math.PI / 180.0;

            // Yaw yani z eksenide dönme 
            double x1 = p.X * Math.Cos(y) - p.Y * Math.Sin(y);
            double y1 = p.X * Math.Sin(y) + p.Y * Math.Cos(y);
            double z1 = p.Z;

            // Pitch yani y ekseninde dönme
            double x2 = x1 * Math.Cos(pt) + z1 * Math.Sin(pt);
            double y2 = y1;
            double z2 = -x1 * Math.Sin(pt) + z1 * Math.Cos(pt);

            // Roll yani x ekseninde dönme 
            double x3 = x2;
            double y3 = y2 * Math.Cos(r) - z2 * Math.Sin(r);

            return new PointF((float)x3, (float)y3);
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float cx = panel1.Width / 2f;
            float cy = panel1.Height / 2f;

            using (Brush sky = new SolidBrush(Color.DeepSkyBlue))
            using (Brush ground = new SolidBrush(Color.SaddleBrown))
            {
                g.FillRectangle(sky, 0, 0, panel1.Width, cy);
                g.FillRectangle(ground, 0, cy, panel1.Height, panel1.Height - cy);
            }

            g.TranslateTransform(cx, cy);

            Nokta3D[] koseler = new Nokta3D[4]
            {
            new Nokta3D(0, 0, 0),
            new Nokta3D(120, 0, 0),
            new Nokta3D(0, 120, 0),
            new Nokta3D(0, 0, 120),
            };

            PointF[] ekranNoktalari = new PointF[4];

            for (int i = 0; i < 4; i++)
            {
                ekranNoktalari[i] = UcBoyutluDondur(koseler[i], anlikRoll, anlikPitch, anlikYaw);
            }

            using (Pen yesilKalem = new Pen(Color.White, 4))
            {
                g.DrawLine(yesilKalem, ekranNoktalari[0], ekranNoktalari[1]);
            }

            using (Pen kýrmýzýKalem = new Pen(Color.Red, 4))
            {
                g.DrawLine(kýrmýzýKalem, ekranNoktalari[0], ekranNoktalari[2]);
            }

            using (Pen siyahKalem = new Pen(Color.Black, 4))
            {
                g.DrawLine(siyahKalem, ekranNoktalari[0], ekranNoktalari[3]);
            }
        }
    }
}
