using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace RMLViewer3D
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Based on article at:
    /// http://www.codeproject.com/Articles/23332/WPF-3D-Primer
    /// </summary>
    public partial class MainWindow : Window
    {
        private GeometryModel3D mGeometry;
        private bool mDown;
        private Point mLastPos;

        private OpenFileDialog openFileDialog;
        private Regex PU = new Regex(@"PU(\d+),(\d+);");
        private Regex PD = new Regex(@"PD(\d+),(\d+);");
        private Regex Z = new Regex(@"Z(\d+),(\d+),.*;");

        public MainWindow()
        {
            InitializeComponent();

            BuildSolid();
        }


        private void LoadClick(object sender, RoutedEventArgs e)
        {
            // get the file
            openFileDialog = new OpenFileDialog
            {
                FileName = "Document",
                DefaultExt = ".rml",
                Filter = "RML documents (.rml)|*.rml|MILL documents (.mill)|*.mill|All files (*.*)|*.*"
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
                                  : z ? Z.Match(line) : null;
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
                    var blueLines = new GeometryDrawing { Geometry = blueLinesGroup, Pen = new Pen(Brushes.Blue, 2) };
                    var redLines = new GeometryDrawing { Geometry = redLinesGroup, Pen = new Pen(Brushes.Red, 2) };

                    var allLines = new DrawingGroup();
                    allLines.Children.Add(blueLines);
                    allLines.Children.Add(redLines);

                    var geometryImage = new DrawingImage(allLines);
                    geometryImage.Freeze();

                    image1.Source = geometryImage;
                    image1.Stretch = Stretch.UniformToFill;

                    // why dividing by 40 you ask? 
                    // http://vonkonow.com/wordpress/2012/08/bringing-a-12-year-old-roland-mdx-20-up-to-date/
                    labelMeasurements.Content = string.Format("X: {0:00}mm, Y: {1:0.00}mm", maxX / 40.0, maxY / 40.0);
                }
            }
        }

        private void BuildSolid()
        {
            // Define 3D mesh object
            MeshGeometry3D mesh = new MeshGeometry3D();

            mesh.
            mesh.Positions.Add(new Point3D(-0.5, -0.5, 1));
            //mesh.Normals.Add(new Vector3D(0, 0, 1));
            mesh.Positions.Add(new Point3D(0.5, -0.5, 1));
            //mesh.Normals.Add(new Vector3D(0, 0, 1));
            mesh.Positions.Add(new Point3D(0.5, 0.5, 1));
            //mesh.Normals.Add(new Vector3D(0, 0, 1));
            mesh.Positions.Add(new Point3D(-0.5, 0.5, 1));
            //mesh.Normals.Add(new Vector3D(0, 0, 1));

            mesh.Positions.Add(new Point3D(-1, -1, -1));
            //mesh.Normals.Add(new Vector3D(0, 0, -1));
            mesh.Positions.Add(new Point3D(1, -1, -1));
            //mesh.Normals.Add(new Vector3D(0, 0, -1));
            mesh.Positions.Add(new Point3D(1, 1, -1));
            //mesh.Normals.Add(new Vector3D(0, 0, -1));
            mesh.Positions.Add(new Point3D(-1, 1, -1));
            //mesh.Normals.Add(new Vector3D(0, 0, -1));

            // Front face
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(0);

            // Back face
            mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(6);

            // Right face
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(2);

            // Top face
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(7);

            // Bottom face
            mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(5);

            // Right face
            mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(4);

            // Geometry creation
            mGeometry = new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.YellowGreen));
            mGeometry.Transform = new Transform3DGroup();
            group.Children.Add(mGeometry);
        }

        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var mousePos = Mouse.GetPosition(viewport);
            var dxdy = new Point3D(mousePos.X - viewport.ActualWidth / 2, mousePos.Y - viewport.ActualHeight / 2, 0);
            if (e.Delta < 0)
            {
                dxdy.X *= -1;
                dxdy.Y *= -1;
            }
            camera.Position = new Point3D(camera.Position.X - dxdy.X / 1000, camera.Position.Y - dxdy.X / 1000, camera.Position.Z - e.Delta / 250D);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            camera.Position = new Point3D(camera.Position.X, camera.Position.Y, 5);
            mGeometry.Transform = new Transform3DGroup();
        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {
            if (mDown)
            {
                Point pos = Mouse.GetPosition(viewport);
                Point actualPos = new Point(pos.X - viewport.ActualWidth / 2, viewport.ActualHeight / 2 - pos.Y);
                double dx = actualPos.X - mLastPos.X, dy = actualPos.Y - mLastPos.Y;

                double mouseAngle = 0;
                if (dx != 0 && dy != 0)
                {
                    mouseAngle = Math.Asin(Math.Abs(dy) / Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2)));
                    if (dx < 0 && dy > 0) mouseAngle += Math.PI / 2;
                    else if (dx < 0 && dy < 0) mouseAngle += Math.PI;
                    else if (dx > 0 && dy < 0) mouseAngle += Math.PI * 1.5;
                }
                else if (dx == 0 && dy != 0) mouseAngle = Math.Sign(dy) > 0 ? Math.PI / 2 : Math.PI * 1.5;
                else if (dx != 0 && dy == 0) mouseAngle = Math.Sign(dx) > 0 ? 0 : Math.PI;

                double axisAngle = mouseAngle + Math.PI / 2;

                Vector3D axis = new Vector3D(Math.Cos(axisAngle) * 4, Math.Sin(axisAngle) * 4, 0);

                double rotation = 0.01 * Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));

                Transform3DGroup group = mGeometry.Transform as Transform3DGroup;
                QuaternionRotation3D r = new QuaternionRotation3D(new Quaternion(axis, rotation * 180 / Math.PI));
                group.Children.Add(new RotateTransform3D(r));

                mLastPos = actualPos;
            }
        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed) return;
            mDown = true;
            Point pos = Mouse.GetPosition(viewport);
            mLastPos = new Point(pos.X - viewport.ActualWidth / 2, viewport.ActualHeight / 2 - pos.Y);
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {
            mDown = false;
        }


    }
}
