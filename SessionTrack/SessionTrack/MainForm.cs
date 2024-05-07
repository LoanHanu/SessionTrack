using OxyPlot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace SessionTrack
{
    public partial class MainForm : Form
    {
        struct CartesianPoint
        {
            public double x;
            public double y;
            public double latitude;
            public double longitude;
            public double speed;
        }

        private List<(DateTime time, double lat, double lon)> rawData;
        private (DateTime time, double lat, double lon) origin = (DateTime.Now, 0, 0);
        private List<CartesianPoint> cartesianPoints;
        private double maxSpeed = 0;

        public MainForm()
        {
            InitializeComponent();
        }

        private List<(DateTime time, double lat, double lon)> LoadData(string filePath)
        {
            DateTime oldTime = DateTime.Now;

            var dataList = new List<(DateTime, double, double)>();
            using (var reader = new StreamReader(filePath))
            {
                string line;
                reader.ReadLine(); // Optionally skip the header
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    var year = int.Parse(parts[0]);
                    var month = int.Parse(parts[1]);
                    var day = int.Parse(parts[2]);
                    var ms = int.Parse(parts[3].Substring(parts[3].Length - 2, 2));
                    var ss = int.Parse(parts[3].Substring(parts[3].Length - 4, 2));
                    var mm = int.Parse(parts[3].Substring(parts[3].Length - 6, 2));
                    var hh = int.Parse(parts[3].Remove(parts[3].Length - 6, 6));
                    var time = new DateTime(year, month, day, hh, mm, ss, ms);
                    var lat = double.Parse(parts[4], CultureInfo.InvariantCulture);
                    var lon = double.Parse(parts[5], CultureInfo.InvariantCulture);
                    this.origin.lat = double.Parse(parts[6], CultureInfo.InvariantCulture);
                    this.origin.lon = double.Parse(parts[7], CultureInfo.InvariantCulture);

                    if (time == oldTime) continue;

                    dataList.Add((time, lat, lon));
                    oldTime = time;
                }
            }
            return dataList;
        }

        private double GetDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth radius in km
            var dLat = (lat2 - lat1) * Math.PI / 180;
            var dLon = (lon2 - lon1) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c; // Distance in km
        }

        private List<CartesianPoint> ConvertToCartesianWithMetaData(List<(DateTime time, double lat, double lon)> data, (DateTime time, double lat, double lon) origin)
        {
            var results = new List<CartesianPoint>();
            (DateTime time, double lat, double lon) prev = data[0];

            foreach (var point in data)
            {
                var distance = GetDistance(prev.lat, prev.lon, point.lat, point.lon); // Your existing distance method
                var timeDiff = (point.time - prev.time).TotalHours;
                var speed = (timeDiff > 0) ? distance / timeDiff : 0;

                var flatPoint = ConvertToXY(point.lat, point.lon, origin.lat, origin.lon);
                CartesianPoint cartesianPoint;
                cartesianPoint.x = flatPoint.x; cartesianPoint.y = flatPoint.y;
                cartesianPoint.latitude = point.lat; cartesianPoint.longitude = point.lon;
                cartesianPoint.speed = speed;
                results.Add(cartesianPoint);

                prev = (point.time, point.lat, point.lon);
            }
            return results;
        }

        private (double x, double y) ConvertToXY(double lat, double lon, double originLat, double originLon)
        {
            var dLat = (lat - originLat) * Math.PI / 180;
            var dLon = (lon - originLon) * Math.PI / 180;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(originLat * Math.PI / 180) * Math.Cos(lat * Math.PI / 180) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var distance = 6371.0 * c;

            var x = distance * Math.Cos(dLon);
            var y = distance * Math.Sin(dLon);
            return (x, y);
        }

        private List<(double x, double y)> ConvertToCartesian(List<(DateTime time, double lat, double lon)> data, (DateTime time, double lat, double lon) origin)
        {
            var earthRadiusKm = 6371.0; // Radius of the Earth in kilometers
            var results = new List<(double x, double y)>();

            foreach (var point in data)
            {
                var dLat = (point.lat - origin.lat) * (Math.PI / 180);
                var dLon = (point.lon - origin.lon) * (Math.PI / 180);
                var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                        Math.Cos(origin.lat * Math.PI / 180) * Math.Cos(point.lat * Math.PI / 180) *
                        Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
                var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                var distance = earthRadiusKm * c; // Distance in kilometers

                var x = distance * Math.Cos(dLon);
                var y = distance * Math.Sin(dLon);

                results.Add((x, y));
            }
            return results;
        }

        private void PlotPoints(List<CartesianPoint> points)
        {
            var model = new OxyPlot.PlotModel { Title = "GPS Data in Cartesian Coordinates" };

            var scatterSeries = new OxyPlot.Series.ScatterSeries
            {
                MarkerType = OxyPlot.MarkerType.Circle,
            };

            var lineSeries = new OxyPlot.Series.LineSeries();

            foreach (var point in points)
            {
                scatterSeries.Points.Add(new OxyPlot.Series.ScatterPoint(point.x, point.y, tag: $"latitude: {point.latitude}\nlongitude: {point.longitude}\nspeed: {point.speed}"));
                lineSeries.Points.Add(new DataPoint(point.x, point.y)); // Add points to the line series

                if (this.maxSpeed < point.speed)
                    this.maxSpeed = point.speed;

            }

            scatterSeries.TrackerFormatString = scatterSeries.TrackerFormatString + "\n{Tag}";
            lineSeries.CanTrackerInterpolatePoints = false;

            model.Series.Add(lineSeries); // Add the line series to the plot model
            model.Series.Add(scatterSeries);
            this.plotView.Model = model;
        }

        private void buttonLoadCSV_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = Application.StartupPath;
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                this.rawData = this.LoadData(openFileDialog.FileName);
                this.cartesianPoints = ConvertToCartesianWithMetaData(this.rawData, this.origin);
                this.PlotPoints(this.cartesianPoints);
                this.textBoxMaxSpeed.Text = $"{this.maxSpeed}";
            }
        }
    }
}
