using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
using HelixToolkit.Wpf;
using Microsoft.Win32;

namespace RMLViewer3D
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Based on article at:
    /// http://www.codeproject.com/Articles/23332/WPF-3D-Primer
    /// </summary>
    public partial class MainWindow : INotifyPropertyChanged
    {
        private const double STEPS_PER_MM = 40.0;

        private SimplePrintModule printModule = new SimplePrintModule();
        private SerialModule serialModule = new SerialModule();
        private GeometryModel3D mGeometry;
        private bool mDown;
        private Point mLastPos;
        private Tuple<double, double> pUpDown;

        private struct PenPlotterConfig
        {
            public double PenUpZ;
            public double PenDownZ;
        }

        public class RMLInstruction : ICloneable, INotifyPropertyChanged
        {
            public string Line { get; set; }
            public int StartIndex { get; set; }
            public int EndIndex { get; set; }
            public double ExecutionTime { get; set; }
            private bool _executed;
            public bool Executed
            {
                get { return _executed; }
                set { _executed = value;
                    RaisePropertyChanged("Executed");}
            }

            public object Clone()
            {
                return new RMLInstruction {
                    StartIndex = StartIndex,
                    EndIndex = EndIndex,
                    Line = Line,
                    ExecutionTime = ExecutionTime
                };
            }

            protected void RaisePropertyChanged(string property)
            {
                var handler = this.PropertyChanged;
                if (handler != null)
                {
                    handler(this, new PropertyChangedEventArgs(property));
                }
            }
            public event PropertyChangedEventHandler PropertyChanged;


        }

        private int _toolOffsetX;
        public int ToolOffsetX
        {
            get { return _toolOffsetX; }
            set { _toolOffsetX = value;
                RaisePropertyChanged("ToolOffsetX");
            }
        }

        private int _toolOffsetY;
        public int ToolOffsetY
        {
            get { return _toolOffsetY; }
            set { _toolOffsetY = value;
            RaisePropertyChanged("ToolOffsetY");
            }
        }


        private int _partOffsetX;
        public int PartOffsetX
        {
            get { return _partOffsetX; }
            set
            {
                _partOffsetX = value;
                RaisePropertyChanged("PartOffsetX");
            }
        }

        private int _partOffsetY;
        public int PartOffsetY
        {
            get { return _partOffsetY; }
            set
            {
                _partOffsetY = value;
                RaisePropertyChanged("PartOffsetY");
            }
        }

        private int _manualMoveStepIdx = 3;
        public int ManualMoveStepIndex
        {
            get { return _manualMoveStepIdx; }
            set
            {
                _manualMoveStepIdx = value;
                RaisePropertyChanged("ManualMoveStepIndex");
            }
        }


        private int _selectedStepIncrement = 40;
        public int SelectedStepIncrement
        {
            get { return _selectedStepIncrement; }
            set
            {
                _selectedStepIncrement = value;
                RaisePropertyChanged("SelectedStepIncrement");
            }
        }

        
        private string _selectedDisplayUnit = "steps";
        public string SelectedDisplayUnit
        {
            get { return _selectedDisplayUnit; }
            set
            {
                _selectedDisplayUnit = value;
                UpdateUnitDisplay();
                RaisePropertyChanged("SelectedDisplayUnit");
            }
        }


        private List<int> _stepIncrements = new List<int> {1, 4, 10, 40, 100, 200, 400};
        public List<int> StepIncrements
        {
            get { return _stepIncrements; }
            set { _stepIncrements = value; }
        }

        private List<string> _displayUnits = new List<string> {"steps", "mm","inches"};
        public List<string> DisplayUnits
        {
            get { return _displayUnits; }
            set { _displayUnits = value; }
        }

        private string _partInfo;
        public string PartInfo
        {
            get { return _partInfo; }
            set
            {
                _partInfo = value;
                RaisePropertyChanged("PartInfo");
            }
        }


        private PenPlotterConfig _penConfig;

        private OpenFileDialog openFileDialog;

        private class RMLCommands
        {
            private static Regex PU = new Regex(@"(PU)([-+]?\d+)(,)([-+]?\d+)(;)");
            private static Regex PD = new Regex(@"(PD)([-+]?\d+),([-+]?\d+)(;)");
            private static Regex Z = new Regex(@"(Z)([-+]?\d+)(,)([-+]?\d+)(,)([-+]?\d+\.*\d*)(;)");
            // defines the "PenUp" and "PenDown" Z positions
            private static Regex PZ = new Regex(@"(!PZ)([-+]?\d+)(,)([-+]?\d+)(;)");
            private static Regex VZ = new Regex(@"(!VZ)(\d+.*\d*)(;)");
            private static Regex V = new Regex(@"(V)(\d+.*\d*)(;)");

            public static List<Regex> Regexes = new List<Regex>{
                PU,PD,Z,PZ
            };
            public static List<Regex> SpeedRegexes = new List<Regex>{
                V, VZ
            };

            /*private static Dictionary<string, Regex> _regexDictionary = new Dictionary<string, Regex>{
            {"PU", PU},{"PD", PD}, {"Z", Z}, {"PZ", PZ}
            };

            public static IDictionary<string, Regex> Regexes()
            {
                return _regexDictionary ?? (
                    _regexDictionary =  typeof(RMLCommands).GetFields(
                    BindingFlags.Static | BindingFlags.NonPublic).ToDictionary(
                        f => f.Name, f => (Regex)f.GetValue(null)));
            }*/
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            //BuildSolid();
        }


        private string GetSupportedTypes()
        {
            var formats = new[] {"rml", "mill", "prn", "gcode", "nc"};
            var individualFormats = string.Join("|", formats.Select(
                format => string.Format("{0} documents (.{1})|*.{1}", 
                    format.ToUpper(), format)));
            var commonFormat = string.Format("Common Milling Formats|{0}",
                string.Join(";",formats.Select(f => string.Format("*.{0}", f))));
            return string.Join("|", individualFormats, commonFormat);
        }

        private void LoadClick(object sender, RoutedEventArgs e)
        {
            // get the file
            var filter = GetSupportedTypes();
            openFileDialog = new OpenFileDialog
            {
                FileName = "Document",
                DefaultExt = ".rml",
                Filter = filter
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
                    var points = new Point3DCollection {new Point3D()};
                    var instructions = new ObservableCollection<RMLInstruction>();
                    double currentSpeed = 0;
                    while ((line = s.ReadLine()) != null)
                    {
                        var lineC = line;
                        var matchRegexes = RMLCommands.Regexes;
                        var hasMatch = matchRegexes.FirstOrDefault(
                            r => r.IsMatch(lineC));
                        var speedRegexes = RMLCommands.SpeedRegexes;
                        var hasSpeedMatch = speedRegexes.FirstOrDefault(
                            r => r.IsMatch(lineC));
                        if (hasMatch != null)
                        {
                            var match = hasMatch.Match(lineC);
                            var i1 = double.Parse(match.Groups[2].Value);
                            var i2 = double.Parse(match.Groups[4].Value);
                            switch (match.Groups[1].Value)
                            {
                                case "PU":
                                    points.AddTwice(new Point3D(i1, i2, _penConfig.PenUpZ));
                                    break;
                                case "PD":
                                    points.AddTwice(new Point3D(i1, i2, _penConfig.PenDownZ));
                                    break;
                                case "PZ":
                                    _penConfig.PenUpZ = i1;
                                    _penConfig.PenDownZ = i2;
                                    break;
                                case "Z":
                                    var i3 = double.Parse(match.Groups[6].Value);
                                    points.AddTwice(new Point3D(i1, i2, i3));
                                    break;
                            }
                        }
                        else if (hasSpeedMatch != null)
                        {
                            var match = hasSpeedMatch.Match(lineC);
                            currentSpeed = double.Parse(match.Groups[2].Value);
                            
                        }
                        instructions.Add(new RMLInstruction {
                              Line = lineC,
                              StartIndex = points.Count - 3,
                              EndIndex = points.Count - 2,
                              // get a rough time estimate from the last point
                              ExecutionTime = points.Count - 3 >= 0 ? 
                               (points[points.Count - 3].DistanceTo(
                                points[points.Count - 2]) / STEPS_PER_MM) / currentSpeed : 0
                        });

                    }
                    // remove the last point (duplicate)
                    points.RemoveAt(points.Count - 1);
                    this.Points = points;
                    this.RMLInstructions = instructions;
                    PartOffsetX = PartOffsetY = 0;
                    _rawPoints = points.Clone();
                    _rawInstructions = RMLInstructions.Clone();

                    // max dimension square:
                    // X: 203.2mm
                    // Y: 152.4mm
                    // Z: 60.5mm
                    var minZ = points.Min(p => p.Z);

                    var mdx20Base = new BoxVisual3D {
                        Material = new DiffuseMaterial(new SolidColorBrush(Colors.Brown)),
                        Length = MMToSteps(203.2),
                        Width  = MMToSteps(152.4),
                        Height = MMToSteps(5)
                    };
                    mdx20Base.Center = new Point3D(mdx20Base.Length/2.0, mdx20Base.Width/2.0, minZ*1.5);
                    viewport.Children.Add(mdx20Base);
                    ResetView();
                    viewport.Camera.LookAt(mdx20Base.Center, 0);
                    UpdatePartInfo();

                }
            }
        }

        private bool IsInt(double val)
        {
            const double eps = 0.000001;
            var intVal = (int) val;
            return Math.Abs(intVal - val) < eps;
        }

        private string FormatCoordinate(double val)
        {
            return IsInt(val) ? ((int) Math.Round(val)).ToString(
                CultureInfo.InvariantCulture) : val.ToString("#.##");
        }


        private void TranslatePart()
        {
            if(Points == null || RMLInstructions == null || 
                _rawInstructions == null || _rawPoints == null)
            {
                return;
            }
            Points = null;
            RMLInstructions = null;

            var translatedRml = _rawInstructions.Clone();
            var translatedPts = _rawPoints.Clone();
           
            foreach (var rml in translatedRml)
            {
                var matchRegexes = RMLCommands.Regexes;
                var hasMatch = matchRegexes.FirstOrDefault(
                    r => r.IsMatch(rml.Line));
                if(hasMatch != null)
                {
                    // it's an rml we need to rewrite
                    var match = hasMatch.Match(rml.Line);
                    var i1 = double.Parse(match.Groups[2].Value) + PartOffsetX;
                    var i2 = double.Parse(match.Groups[4].Value) + PartOffsetY;
                    var offset = new Point3D(PartOffsetX, PartOffsetY, 0);
                    switch (match.Groups[1].Value)
                    {
                        case "PU":
                        case "PD":
                            rml.Line = string.Format("{0}{1}{2}{3}{4}",
                                match.Groups[1].Value, FormatCoordinate(i1),
                                match.Groups[3].Value, FormatCoordinate(i2), 
                                match.Groups[5].Value);
                            translatedPts.Translate(rml.StartIndex, offset);
                            translatedPts.Translate(rml.EndIndex, offset);
                            break;
                        case "PZ":
                            // nothing needed
                            break;
                        case "Z":
                            var i3 = match.Groups[6].Value;
                            rml.Line = string.Format("{0}{1}{2}{3}{4}{5}{6}",
                                match.Groups[1].Value, FormatCoordinate(i1),
                                match.Groups[3].Value, FormatCoordinate(i2),
                                match.Groups[5].Value, i3, match.Groups[7].Value);
                            translatedPts.Translate(rml.StartIndex, offset);
                            translatedPts.Translate(rml.EndIndex, offset);
                            break;
                    }
                }

                
            }

            Points = translatedPts;
            RMLInstructions = translatedRml;

            // update bounding box
            UpdatePartInfo();
        }

        private void UpdatePartInfo()
        {
            if(Points == null || RMLInstructions == null)
            {
                PartInfo = string.Empty;
                return;
            }
            var uCon = ((UnitConverter) FindResource("UnitConverter"));
            var min = new Point3D(uCon.ConvertD(Points.Min(p => p.X)),
                                  uCon.ConvertD(Points.Min(p => p.Y)),
                                  uCon.ConvertD(Points.Min(p => p.Z)));
            var max = new Point3D(uCon.ConvertD(Points.Max(p => p.X)),
                                  uCon.ConvertD(Points.Max(p => p.Y)),
                                  uCon.ConvertD(Points.Max(p => p.Z)));
            var dim = max - min;
            var infoMin = string.Format("Min (X: {0}, Y: {1}, Z: {2})",
                                        min.X, min.Y, min.Z);
            var infoMax = string.Format("Max (X: {0}, Y: {1}, Z: {2})",
                             max.X, max.Y, max.Z);
            var infoDim = string.Format("Dimension (L: {0}, W: {1}, H: {2})",
                             dim.X, dim.Y, dim.Z);

            var totalBuildTime = string.Format("Time: {0:h\\:mm\\:ss}", 
                new TimeSpan(0,0,(int)RMLInstructions.Sum(r => r.ExecutionTime)));

            PartInfo = string.Join(" | ", infoMin, infoMax, infoDim, totalBuildTime);
        }

        private void ResetViewClick(object sender, RoutedEventArgs e)
        {
            ResetView();
        }

        private void ResetView()
        {
            // fix the camera position
            viewport.Camera.Position = new Point3D(-850, -8000, 7600);
            viewport.Camera.LookDirection = new Vector3D(4500, 10000, -7000);
            
        }

        private double StepsToMM(int steps)
        {
            // why dividing by 40 you ask? 
            // http://vonkonow.com/wordpress/2012/08/bringing-a-12-year-old-roland-mdx-20-up-to-date/
            return steps / STEPS_PER_MM;
        }

        private int MMToSteps(double mm)
        {
            // why dividing by 40 you ask? 
            // http://vonkonow.com/wordpress/2012/08/bringing-a-12-year-old-roland-mdx-20-up-to-date/
            return (int)(mm * STEPS_PER_MM);
        }



        private void DrawLines(List<Point3D> points)
        {
            // Define 3D mesh object
            MeshGeometry3D mesh = new MeshGeometry3D();

            /* var line = new ScreenSpaceLines3D {
                Color = Color.
                Points = new Point3DCollection { 
                    points[1], points[2]
                }
            };
            */

            var lines = new LinesVisual3D();
            //mesh.TriangleIndices.Add();
            mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(4);

            // Geometry creation
            mGeometry = new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.YellowGreen));
            mGeometry.Transform = new Transform3DGroup();
            //group.Children.Add(mGeometry);
        }
        
        private void Grid_MouseWheel(object sender, MouseWheelEventArgs e)
        {
/*            var mousePos = Mouse.GetPosition(viewport);
            var dxdy = new Point3D(mousePos.X - viewport.ActualWidth / 2, mousePos.Y - viewport.ActualHeight / 2, 0);
            if (e.Delta < 0)
            {
                dxdy.X *= -1;
                dxdy.Y *= -1;
            }*/
            //camera.Position = new Point3D(camera.Position.X - dxdy.X / 1000, camera.Position.Y - dxdy.X / 1000, camera.Position.Z - e.Delta / 250D);
        }
        /*
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //camera.Position = new Point3D(camera.Position.X, camera.Position.Y, 5);
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
        */

        private Point3DCollection _rawPoints;
        private Point3DCollection _points;
        public Point3DCollection Points
        {
            get
            {
                return this._points;
            }

            set
            {
                this._points = value;
                this.RaisePropertyChanged("Points");
            }
        }

        private Point3DCollection _highlightedPoints;
        public Point3DCollection HighlightedPoints
        {
            get
            {
                return this._highlightedPoints;
            }

            set
            {
                this._highlightedPoints = value;
                this.RaisePropertyChanged("HighlightedPoints");
            }
        }

        private ObservableCollection<RMLInstruction> _rawInstructions;
        private ObservableCollection<RMLInstruction> _instructions;
        public ObservableCollection<RMLInstruction> RMLInstructions
        {
            get
            {
                return this._instructions;
            }

            set
            {
                this._instructions = value;
                this.RaisePropertyChanged("RMLInstructions");
            }
        }

        private RMLInstruction _selectedInstruction;
        public RMLInstruction SelectedInstruction
        {
            get
            {
                return this._selectedInstruction;
            }

            set
            {
                this._selectedInstruction = value;
                this.RaisePropertyChanged("SelectedInstruction");
                UpdateHighlightedLineSegment();
            }
        }

        private void UpdateHighlightedLineSegment()
        {
            if (_selectedInstruction == null || _selectedInstruction.StartIndex < 0
                || _selectedInstruction.EndIndex < 0)
            {
                Arrow.Visible = false;
                return;
            }

            // clear out the highlighted point
            var newHighlight = new Point3DCollection();
            var startPt = _points[_selectedInstruction.StartIndex];
            newHighlight.Add(startPt);
            newHighlight.Add(_points[_selectedInstruction.EndIndex]);
            HighlightedPoints = newHighlight;
            Arrow.Transform = new TranslateTransform3D(
                startPt.X, startPt.Y, startPt.Z + Arrow.Height);
            Arrow.Visible = true;
        }

        protected void RaisePropertyChanged(string property)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(property));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool _runningJob = false;

        private void RunClick(object sender, RoutedEventArgs e)
        {
            // disable all manual tools
            UpdateRunningState(true);
            ThreadPool.QueueUserWorkItem(wcb => RunCurrentJob());
        }


        private void StopClick(object sender, RoutedEventArgs e)
        {
            UpdateRunningState(false);
            StopOrPause();
        }

        private void UpdateRunningState(bool running)
        {
            _runningJob = running;
            groupBox1.IsEnabled = !running;
            ToolbarOffsets.IsEnabled = !running;
            MenuFile.IsEnabled = !running;
            buttonRun.IsEnabled = !running;
            buttonStop.IsEnabled = running;
            //listBox1.IsHitTestVisible = !running;

        }


        private void StopOrPause()
        {            
            /*serialModule.Connect();
            serialModule.Write("Z50,50,0;");
            serialModule.Disconnect();*/
            printModule.PrintLines("PA;PA;!VZ10;!PZ0,100;PU0,0;PD0,0;!MC0;");
        }

        private void RunCurrentJob()
        {
            //var dispatcher = Application.Current.MainWindow.Dispatcher;

            // send instructions 30 seconds at a time
            const int timeChunk = 30;

 
            var currentTime = 0.0;
            var currentRMLBlock = new StringBuilder();
            var executedInstructionIdx = 0;
            for (var i = 0; i < RMLInstructions.Count; i++ )
            {
                var currentIdx = i;
                currentTime += RMLInstructions[i].ExecutionTime;
                currentRMLBlock.Append(RMLInstructions[i].Line);
                if (currentTime > timeChunk || currentRMLBlock.Length > 1000 || 
                    currentIdx == RMLInstructions.Count - 1)
                {
                    // print this block and sleep the thread
                    Dispatcher.BeginInvoke(new Action<string, int, int>(
                        (c, j, k) => {
                            printModule.PrintLines(c);
                            // update the list
                            for (var l = j; l < k; l++) {
                                RMLInstructions[k].Executed = true;
                            }
                            executedInstructionIdx = currentIdx;
                            listBoxRml.ScrollIntoView(RMLInstructions[k]);
                        }), currentRMLBlock.ToString(), executedInstructionIdx, currentIdx);
                    if ((int)(currentTime * 1000.0) > 1000)
                    {
                        Thread.Sleep((int) (currentTime*1000.0) - 500);
                    } else
                    {

                    }
                    currentRMLBlock.Clear();
                    currentTime = 0.0;
                }
            }
            Dispatcher.BeginInvoke(new Action(() => UpdateRunningState(false)));
        }


        void WindowClosing(object sender, CancelEventArgs e)
        {
            if (_runningJob)
            {
                MessageBoxResult result = MessageBox.Show(
                    "Active job is running! Please stop job first.",
                    "RML Visualizer", MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Cancel = true;
            }
        }

        private bool spindleRotation;
        private bool raiseToolBeforeMoving;


        private void MoveXY(int deltaX = 0, int deltaY = 0)
        {
            // convert to steps
            ToolOffsetX += deltaX;
            ToolOffsetY += deltaY;
            printModule.PrintLines(string.Format("^DF;!MC0;!PZ0,0;V15.0;Z{0},{1},0;", ToolOffsetX, ToolOffsetY));

        }

        /*private void MoveXY()
        {
            if(!spindleRotation && !raiseToolBeforeMoving)
            {
                printModule.PrintLines("^DF;!MC0;!PZ0,0;V15.0;Z400,400,0;");
            }
            if(spindleRotation && raiseToolBeforeMoving)
            {
                printModule.PrintLines(
                    ";^DF;!MC1;V15.0;^PR;Z0,0,2420;^PA;!PZ0,0;V15.0;Z800,800,2420;Z800,800,40;V7.5;Z800,800,0;!MC0;");
            }
            if(!spindleRotation && raiseToolBeforeMoving)
            {
                printModule.PrintLines(
                    "^DF;!MC0;V15.0;^PR;Z0,0,2420;^PA;!PZ0,0;V15.0;Z2000,2000,2420;Z2000,2000,40;V7.5;Z2000,2000,0;");
            }
        }*/

        private int GetStepIncrement()
        {
            return _selectedStepIncrement;
        }

        private void MoveXYPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(_runningJob)
            {
                return;
            }
            var steps = GetStepIncrement();
            switch(e.Key)
            {
                case Key.Up:
                    MoveXY(0, steps);
                    e.Handled = true;
                    break;
                case Key.Down:
                    MoveXY(0, -steps);
                    e.Handled = true;
                    break;
                case Key.Left:
                    MoveXY(-steps, 0);
                    e.Handled = true;
                    break;
                case Key.Right:
                    MoveXY(steps, 0);
                    e.Handled = true;
                    break;
                case Key.OemMinus:
                    AlterStepIndex(-1);
                    e.Handled = true;
                    break;
                case Key.OemPlus:
                    AlterStepIndex(+1);
                    e.Handled = true;
                    break;
            }
        }

        private void AlterStepIndex(int delta)
        {
            var stepIncrements = comboStepIncrements.Items.Count;
            var newStepIncrement = delta + ManualMoveStepIndex;
            if(newStepIncrement >= 0 && newStepIncrement < stepIncrements)
            {
                ManualMoveStepIndex = newStepIncrement;
            }
        }

        private void MoveXYClick(object sender, RoutedEventArgs e)
        {
            var steps = GetStepIncrement();
            switch(((Button)sender).Name)
            {
                case "XP":
                    MoveXY(steps, 0);
                    break;
                case "XN":
                    MoveXY(-steps, 0);
                    break;
                case "YP":
                    MoveXY(0, steps);
                    break;
                case "YN":
                    MoveXY(0, -steps);
                    break;
            }
        }



        private void UpdateUnitDisplay()
        {
            ((UnitConverter) FindResource("UnitConverter")).DisplayUnits = SelectedDisplayUnit;
            BindingOperations.GetBindingExpressionBase(textPartOffsetX, TextBox.TextProperty).UpdateTarget();
            BindingOperations.GetBindingExpressionBase(textPartOffsetY, TextBox.TextProperty).UpdateTarget();
            BindingOperations.GetBindingExpressionBase(textToolX, TextBox.TextProperty).UpdateTarget();
            BindingOperations.GetBindingExpressionBase(textToolY, TextBox.TextProperty).UpdateTarget();

            // save the step increment and manual step index
            var comboIdx = ManualMoveStepIndex;
            BindingOperations.GetBindingExpressionBase(comboStepIncrements, ComboBox.ItemsSourceProperty).UpdateTarget();
            // give the combobox some time to process the new display
            Dispatcher.BeginInvoke((Action) (() =>
            {
                ManualMoveStepIndex = comboIdx;
                BindingOperations.GetBindingExpressionBase(comboStepIncrements, 
                    ComboBox.SelectedValueProperty).UpdateTarget();
            }));

            UpdatePartInfo();
        }

        private void UpdateUnitTargets(DependencyObject target)
        {
            
        }

        private void TextBoxValueCommit(object sender, KeyEventArgs e)
        {
            var textbox = (TextBox)sender;
            if(e.Key == Key.Enter)
            {
                var bindingExpressionBase = BindingOperations.
                    GetBindingExpressionBase(textbox, TextBox.TextProperty);
                if (bindingExpressionBase != null)
                    bindingExpressionBase.UpdateSource();

                // commit the value
                switch(textbox.Name)
                {
                    case "textPartOffsetX": 
                    case "textPartOffsetY":
                        TranslatePart();
                        break;
                    case "textToolX":
                    case "textToolY":
                        MoveXY();
                        break;
                }
            }
        }

        private void ButtonGoClick(object sender, RoutedEventArgs e)
        {
            var b1 = BindingOperations.GetBindingExpressionBase(textToolX, TextBox.TextProperty);
            if (b1 != null)
            {
                b1.UpdateSource();
            }
            var b2 = BindingOperations.GetBindingExpressionBase(textToolY, TextBox.TextProperty);
            if (b2 != null)
            {
                b2.UpdateSource();
            }
            MoveXY();

        }

        private void SetPartZeroClick(object sender, RoutedEventArgs e)
        {
            PartOffsetX = ToolOffsetX;
            PartOffsetY = ToolOffsetY;
            TranslatePart();
        }



    }
}
