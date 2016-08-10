using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;


namespace RMLViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private OpenFileDialog openFileDialog;
        private Regex PU = new Regex(@"PU(\d+),(\d+);");
        private Regex PD = new Regex(@"PD(\d+),(\d+);");
        private Regex Z = new Regex(@"Z(\d+),(\d+),.*;");

        public MainWindow()
        {
            InitializeComponent();
        }



        private void LoadClick(object sender, RoutedEventArgs e)
        {
            // get the file
            openFileDialog = new OpenFileDialog
                                 {FileName = "Document", DefaultExt = ".rml",
                                  Filter = "RML documents (.rml)|*.rml|All files (*.*)|*.*"
                                 };
            // Default file name
            // Default file extension
            // Filter files by extension

            // Show open file dialog box
            var result = openFileDialog.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                var filename = openFileDialog.FileName;

                // open the file
                using (var s = new StreamReader(filename))
                {
                    string line;
                    var lastCoord = new Point(-1, -1);
                    var blueLinesGroup = new GeometryGroup();
                    var redLinesGroup = new GeometryGroup();

                    double maxX = 0, maxY = 0;
                    while ((line = s.ReadLine()) != null)
                    {
                        var pu = PU.IsMatch(line);
                        var pd = PD.IsMatch(line);
                        var z = Z.IsMatch(line);

                        var match = pu ? PU.Match(line)
                                  : pd ? PD.Match(line)
                                  :  z ? Z.Match(line) : null;
                        if (match != null)
                        {
                            var coord = new Point(int.Parse(match.Groups[1].Value),
                                                  int.Parse(match.Groups[2].Value));

                            maxX = Math.Max(maxX, coord.X);
                            maxY = Math.Max(maxY, coord.Y);

                            var group = pu ? redLinesGroup : blueLinesGroup;
                            if (lastCoord.X > 0.0)
                            {
                                // draw a line from the last coord to this one
                                var lineG = new LineGeometry(lastCoord, coord);
                                group.Children.Add(lineG);
                            }
                            lastCoord = coord;
                        }
                    }
                    var blueLines = new GeometryDrawing {Geometry = blueLinesGroup, Pen = new Pen(Brushes.Blue, 2)};
                    var redLines = new GeometryDrawing {Geometry = redLinesGroup, Pen = new Pen(Brushes.Red, 2)};

                    var allLines = new DrawingGroup();
                    allLines.Children.Add(blueLines);
                    allLines.Children.Add(redLines);

                    var geometryImage = new DrawingImage(allLines);
                    geometryImage.Freeze();
                    
                    image1.Source = geometryImage;
                    image1.Stretch = Stretch.UniformToFill;

                    // why dividing by 40 you ask? 
                    // http://vonkonow.com/wordpress/2012/08/bringing-a-12-year-old-roland-mdx-20-up-to-date/
                    labelMeasurements.Content = string.Format("X: {0:00}mm, Y: {1:0.00}mm", maxX/40.0, maxY/40.0);
                }
            }
        }
    }
}
