using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media.Media3D;

namespace RMLViewer3D
{
    public static class Point3DExtensions
    {
        public static void AddTwice(this Point3DCollection collection, Point3D point)
        {
            collection.Add(point);
            collection.Add(point);
        }
    }

    public static class ObservableCollectionExtensions
    {
        public static Point3DCollection Clone(this Point3DCollection collection)
        {
            var clone = new Point3DCollection();
            foreach(var pt in collection)
            {
                clone.Add(new Point3D(pt.X, pt.Y, pt.Z));
            }
            return clone;
        }


        public static void Translate(this Point3DCollection collection, 
            int index, Point3D offset)
        {
            collection[index] = new Point3D(
                collection[index].X + offset.X, 
                collection[index].Y + offset.Y, 
                collection[index].Z + offset.Z);
        }

        public static ObservableCollection<T> Clone<T>(this ObservableCollection<T> collection) where T : ICloneable
        {
            var clone = new ObservableCollection<T>();
            foreach(var item in collection)
            {
                clone.Add((T)item.Clone());
            }
            return clone;
        }
    }


}
