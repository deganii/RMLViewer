using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Windows.Media;

namespace RMLViewer3D
{
    class UnitConverter : IValueConverter, INotifyPropertyChanged
    {
        private string _displayUnits = "steps";
        public string DisplayUnits
        {
            get { return _displayUnits; }
            set { _displayUnits = value;
            RaisePropertyChanged("DisplayUnits");
            }
        }

        public double ConvertD(object value)
        {
            return (double)Convert(value);
        }

        public object Convert(object value, Type targetType = null, object parameter= null, CultureInfo culture= null)
        {
            if(value is int)
            {
                var steps = (int)value;
                return ConvertFromStepsToDisplay(steps);
            } if( value is double)
            {
                var steps = (int)(double)value;
                return ConvertFromStepsToDisplay(steps);
            }
            var enumerable = value as IEnumerable<int>;
            if (enumerable != null)
            {
                return (enumerable).Select(ConvertFromStepsToDisplay);
            }
            throw new FormatException(string.Format("Don't know how to convert {0} to {1}.", value, _displayUnits));
        }

        private double ConvertFromStepsToDisplay(int steps)
        {
            // convert from steps to desired display value
            switch (_displayUnits)
            {
                case "mm":
                    return steps / (40.0);
                case "steps":
                    return steps;
                case "inches":
                    return Math.Round(steps / (25.4 * 40.0), 4);
            }
            throw new FormatException(string.Format("Don't know how to convert {0} steps to {1}.", steps, _displayUnits));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if(value == null)
            {
                return 0;
            }

            // convert from the display value back to steps
            var displayValue = value is double ? (double)value : double.Parse((string) value);
            switch (_displayUnits)
            {
                case "mm":
                    return (int) Math.Round(displayValue*40.0);
                case "steps":
                    return (int) Math.Round(displayValue);
                case "inches":
                    return (int) Math.Round(displayValue * 25.4 * 40.0);
            }
            throw new FormatException(string.Format("Don't know how to convert {0} {1} to steps.", displayValue, _displayUnits));
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


    public class RMLExecutionStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if((bool)value)
            {
                return new SolidColorBrush(Colors.Green);
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
